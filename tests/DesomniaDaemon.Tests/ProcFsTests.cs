using MadWizard.Desomnia.Processes.Manager.Native;
using Xunit;

namespace MadWizard.Desomnia.Daemon.Tests
{
    /// <summary>
    /// Reading /proc/[pid]/stat. The command field is arbitrary bytes the process chose for itself
    /// and may contain spaces and parentheses, so splitting the line on whitespace goes wrong for
    /// exactly the processes most worth watching — a browser content process, systemd's (sd-pam).
    /// Everything the manager needs sits after that field, which is why it has to be found first.
    /// </summary>
    public class ProcFsTests
    {
        // pid (comm) state ppid pgrp session tty_nr ... — deliberately all different, so a test
        // proves which field was read and not merely that four numbers were parsed
        private const string Fields = " S 1000 4711 1200 34816 4711 4194304 12345 0 0 0";

        private static ProcFs.Stat Parse(string line)
            => ProcFs.ParseStat(line) ?? throw new Xunit.Sdk.XunitException($"stat line was refused: {line}");

        [Fact]
        public void Stat_OfAnOrdinaryProcess_IsRead()
        {
            var stat = Parse("4711 (bash)" + Fields);

            Assert.Equal("bash", stat.Command);
            Assert.Equal('S', stat.State);
            Assert.Equal(1000, stat.ParentId);
            Assert.Equal(1200, stat.SessionId); // the session, not the process group at 4711
        }

        [Fact]
        public void Stat_OfACommandContainingSpaces_KeepsTheWholeName()
        {
            var stat = Parse("4711 (Web Content)" + Fields);

            Assert.Equal("Web Content", stat.Command);
            Assert.Equal(1000, stat.ParentId);
        }

        [Fact]
        public void Stat_OfACommandWrappedInParentheses_KeepsThem()
        {
            // systemd really does run a helper called "(sd-pam)"
            var stat = Parse("4711 ((sd-pam))" + Fields);

            Assert.Equal("(sd-pam)", stat.Command);
            Assert.Equal(1000, stat.ParentId);
        }

        [Fact]
        public void Stat_OfACommandContainingAClosingParenthesis_EndsAtTheLastOne()
        {
            var stat = Parse("4711 (weird) name)" + Fields);

            Assert.Equal("weird) name", stat.Command);
            Assert.Equal('S', stat.State); // the giveaway that the fields were split at the right place
            Assert.Equal(1000, stat.ParentId);
        }

        [Fact]
        public void Stat_OfAZombie_ReportsTheState()
        {
            var stat = Parse("4711 (gone) Z 1000 4711 1200 0 -1 4194560 0 0 0 0");

            Assert.Equal(ProcFs.Zombie, stat.State);
        }

        [Theory]
        [InlineData("")]
        [InlineData("4711 bash S 1000 4711 1200")]     // no parentheses at all
        [InlineData("4711 (bash) S 1000")]             // truncated before the session
        [InlineData("4711 (bash")]                     // never closed
        public void Stat_ThatCannotBeTrusted_IsRefused(string line)
        {
            Assert.Null(ProcFs.ParseStat(line));
        }

        [Fact]
        public void Name_WhereTheImageLengthensTheCommand_IsTakenFromTheImage()
        {
            // procfs stops at 15 characters; the image says what the rest of it was
            Assert.Equal("systemd-timesyncd", ProcFs.ResolveName("systemd-timesyn", "/usr/lib/systemd/systemd-timesyncd"));
            Assert.Equal("com.example.LongServiceName", ProcFs.ResolveName("com.example.Lon", "/opt/x/com.example.LongServiceName"));
        }

        [Fact]
        public void Name_OfAProcessThatRenamedItself_IsNotOverruledByTheImage()
        {
            // a prctl() rename is the process saying what it wants to be called; the image is not
            // a longer spelling of it, so it does not get to replace it
            Assert.Equal("worker", ProcFs.ResolveName("worker", "/usr/sbin/nginx"));
        }

        [Fact]
        public void Name_WithoutAReadableImage_IsTheCommand()
        {
            // kernel threads, and anything an unprivileged reader may not follow the link to
            Assert.Equal("kworker/0:1", ProcFs.ResolveName("kworker/0:1", null));
            Assert.Equal("bash", ProcFs.ResolveName("bash", ""));
        }
    }
}
