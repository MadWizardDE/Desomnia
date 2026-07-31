using MadWizard.Desomnia.Processes.Manager.Native;
using System.Runtime.InteropServices;
using Xunit;

namespace MadWizard.Desomnia.Daemon.Tests
{
    /// <summary>
    /// The layout of struct epoll_event, which is the one part of this ABI that is not the same
    /// everywhere: the kernel packs it on x86_64 and only there, so that the 64-bit struct matches
    /// the 32-bit one. Getting it wrong is invisible — epoll still reports events, their cookies
    /// just come back as rubbish — so the table is pinned here and the running layout is proved
    /// again at startup by firing the wake-up counter and checking its cookie survives.
    /// </summary>
    public class EpollTests
    {
        [Theory]
        [InlineData(Architecture.X64)]
        [InlineData(Architecture.X86)]
        public void Event_WhereTheKernelPacksIt_IsTwelveBytes(Architecture architecture)
        {
            Assert.Equal((12, 4), Epoll.EventLayout(architecture));
        }

        [Theory]
        [InlineData(Architecture.Arm64)]
        [InlineData(Architecture.Arm)]     // 32-bit, but AAPCS still aligns 64-bit types to 8
        [InlineData(Architecture.RiscV64)]
        [InlineData(Architecture.Ppc64le)]
        public void Event_WhereTheKernelDoesNot_IsSixteenBytesWithThePaddingInFront(Architecture architecture)
        {
            Assert.Equal((16, 8), Epoll.EventLayout(architecture));
        }

        [Fact]
        public void Event_OnArm_IsNotTheI386Layout()
        {
            // the counter-intuitive one: 32-bit ARM pads where 32-bit x86 does not, so "is it a
            // 32-bit architecture" is the wrong question to ask
            Assert.NotEqual(Epoll.EventLayout(Architecture.X86), Epoll.EventLayout(Architecture.Arm));
        }

        [Fact]
        public void Event_OfTheRunningArchitecture_IsSizedToMatch()
        {
            var (size, _) = Epoll.EventLayout(RuntimeInformation.ProcessArchitecture);

            Assert.Equal(size, Epoll.EventSize);
        }
    }
}
