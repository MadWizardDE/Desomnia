using MadWizard.Desomnia.Pipe.Messages;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MadWizard.Desomnia.Minion
{
    /// <summary>
    /// Holds a DISPLAY power request on the service's behalf — display requests are
    /// session-scoped, so the session-0 service cannot issue them itself, but this process
    /// runs inside the user session, where they are allowed.
    ///
    /// Driven by the (idempotent) <see cref="DisplayRequestMessage"/>: a hold replaces any
    /// previously held request, a release clears it. At most one request is held at a time.
    /// </summary>
    public class DisplayRequestService : IDisposable
    {
        private readonly object _lock = new object();

        private ILogger<DisplayRequestService> _logger;

        private IntPtr _handle = IntPtr.Zero;

        public DisplayRequestService(PipeMessageBroker broker, ILogger<DisplayRequestService> logger)
        {
            _logger = logger;

            broker.RegisterMessageHandler<DisplayRequestMessage>(HandleDisplayRequestMessage);
        }

        private void HandleDisplayRequestMessage(DisplayRequestMessage message)
        {
            lock (_lock)
            {
                ReleaseRequest();

                if (message.Active)
                {
                    try
                    {
                        AcquireRequest(message.Reason ?? "?");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to create display power request");
                    }
                }
            }
        }

        private void AcquireRequest(string reason)
        {
            var context = new POWER_REQUEST_CONTEXT
            {
                Version = POWER_REQUEST_CONTEXT_VERSION,
                Flags = POWER_REQUEST_CONTEXT_SIMPLE_STRING,
                SimpleReasonString = reason
            };

            IntPtr handle = PowerCreateRequest(ref context);

            if (handle == IntPtr.Zero || handle == INVALID_HANDLE_VALUE)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            if (!PowerSetRequest(handle, PowerRequestDisplayRequired))
            {
                int error = Marshal.GetLastWin32Error();

                CloseHandle(handle);

                throw new Win32Exception(error);
            }

            _handle = handle;

            _logger.LogDebug("Display power request created: {Reason}", reason);
        }

        private void ReleaseRequest()
        {
            if (_handle != IntPtr.Zero)
            {
                if (!PowerClearRequest(_handle, PowerRequestDisplayRequired))
                    _logger.LogWarning("Failed to clear display power request (error {Error})", Marshal.GetLastWin32Error());

                CloseHandle(_handle);

                _handle = IntPtr.Zero;

                _logger.LogDebug("Display power request released");
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                ReleaseRequest();
            }
        }

        #region API: Power-Requests
        private const int PowerRequestDisplayRequired = 0;

        private const uint POWER_REQUEST_CONTEXT_VERSION = 0;
        private const uint POWER_REQUEST_CONTEXT_SIMPLE_STRING = 0x1;

        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct POWER_REQUEST_CONTEXT
        {
            public uint Version;
            public uint Flags;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string SimpleReasonString;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr PowerCreateRequest(ref POWER_REQUEST_CONTEXT Context);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool PowerSetRequest(IntPtr PowerRequestHandle, int RequestType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool PowerClearRequest(IntPtr PowerRequestHandle, int RequestType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr Handle);
        #endregion
    }
}
