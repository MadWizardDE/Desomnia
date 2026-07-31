using MadWizard.Desomnia.Processes.Manager;
using Xunit;

namespace MadWizard.Desomnia.Processes.Tests
{
    /// <summary>
    /// When a process is asked to stop rather than simply stopped. The timeout is what buys it that
    /// chance: given one it is asked first, given none the action means terminate and says so —
    /// asking and killing in the same breath would only look graceful.
    ///
    /// Asking is the one step a platform overrides (SIGTERM on Unix, a close message on Windows),
    /// so it is the one step that can be observed from here; the waiting and the killing are the
    /// runtime's own and are left to it.
    /// </summary>
    public class ProcessStopTests
    {
        [Fact]
        public async Task WithoutATimeout_TheProcessIsNotAskedFirst()
        {
            var process = new FakeHandle { IsGone = true };

            await process.Stop();

            Assert.Equal(0, process.Requested);
        }

        [Fact]
        public async Task WithATimeout_TheProcessIsAsked()
        {
            var process = new FakeHandle { IsGone = true };

            await process.Stop(TimeSpan.FromSeconds(5));

            Assert.Equal(1, process.Requested);
        }

        [Fact]
        public async Task AProcessThatWillNotGo_IsKilled()
        {
            var process = new FakeHandle { IsGone = false };

            // The kill is not a seam any more, so it cannot be counted — but it can still be caught
            // in the act: this handle stands for a pid that cannot exist, so the moment Stop reaches
            // for the real process to kill it, that reach is what fails. Were the kill dropped, this
            // would return quietly instead.
            await Assert.ThrowsAsync<ArgumentException>(() => process.Stop());

            Assert.Equal(0, process.Requested); // still not asked – no timeout was given
        }

        /// <summary>
        /// A handle over a pid no operating system will hand out, so that any attempt to reach the
        /// process behind it fails at once instead of finding somebody else's.
        /// </summary>
        private sealed class FakeHandle() : ProcessHandle(new ProcessInformation(-1) { Name = "chrome", SessionId = 0 })
        {
            /// <summary>What the platform answers when asked to pass the request on.</summary>
            public bool CanBeAsked { get; init; } = true;

            /// <summary>Whether the process is already gone by the time it is looked at.</summary>
            public required bool IsGone { get; init; }

            public int Requested { get; private set; }

            public override bool HasStopped => IsGone;

            protected override bool RequestStop()
            {
                Requested++;

                return CanBeAsked;
            }
        }
    }
}
