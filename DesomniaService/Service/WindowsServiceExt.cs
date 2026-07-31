using System.Runtime.InteropServices;

namespace MadWizard.Desomnia.Service.Windows
{
    internal static class WindowsServiceExt
    {
        public static bool IsWindowsService(this System.Diagnostics.Process process)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return false;

            return process.SessionId == 0;
        }

        extension(Environment)
        {
            public static bool IsWindowsService => System.Diagnostics.Process.GetCurrentProcess().IsWindowsService();
        }
    }
}