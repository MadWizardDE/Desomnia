using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DesomniaSessionMinion.Services.NotificationArea
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Windows.Forms;

    /// <summary>
    /// Modifier flags for global hotkeys; values match the Win32 RegisterHotKey MOD_* constants
    /// (and are bit-identical to the former System.Windows.Input.ModifierKeys values).
    /// </summary>
    [Flags]
    public enum HotKeyModifiers
    {
        None = 0x0000,
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Windows = 0x0008
    }

    public class GlobalHotKey : IDisposable
    {
        /// <summary>
        /// Registers a global hotkey
        /// </summary>
        /// <param name="aKeyGestureString">e.g. Alt + Shift + Control + Win + S</param>
        /// <param name="aAction">Action to be called when hotkey is pressed</param>
        /// <returns>true, if registration succeeded, otherwise false</returns>
        public static bool RegisterHotKey(string aKeyGestureString, Action aAction)
        {
            ParseKeyGesture(aKeyGestureString, out HotKeyModifiers aModifier, out Keys aKey);
            return RegisterHotKey(aModifier, aKey, aAction);
        }

        public static bool RegisterHotKey(HotKeyModifiers aModifier, Keys aKey, Action aAction)
        {
            if (aModifier == HotKeyModifiers.None)
            {
                throw new ArgumentException("Modifier must not be HotKeyModifiers.None");
            }
            if (aAction is null)
            {
                throw new ArgumentNullException(nameof(aAction));
            }

            Keys aVirtualKeyCode = aKey & Keys.KeyCode;
            currentID = currentID + 1;
            bool aRegistered = RegisterHotKey(window.Handle, currentID,
                                        (uint)aModifier | MOD_NOREPEAT,
                                        (uint)aVirtualKeyCode);

            if (aRegistered)
            {
                registeredHotKeys.Add(new HotKeyWithAction(aModifier, aVirtualKeyCode, aAction));
            }
            return aRegistered;
        }

        public void Dispose()
        {
            // unregister all the registered hot keys.
            for (int i = currentID; i > 0; i--)
            {
                UnregisterHotKey(window.Handle, i);
            }

            // dispose the inner native window.
            window.Dispose();
        }

        static GlobalHotKey()
        {
            window.KeyPressed += (s, e) =>
            {
                registeredHotKeys.ForEach(x =>
                {
                    if (e.Modifier == x.Modifier && e.Key == x.Key)
                    {
                        x.Action();
                    }
                });
            };
        }

        /// <summary>
        /// Parses a gesture string like "Control + Alt + F5".
        /// Modifier tokens (case-insensitive): Control/Ctrl, Alt, Shift, Windows/Win;
        /// the remaining token is parsed as a <see cref="Keys"/> value.
        /// </summary>
        private static void ParseKeyGesture(string aKeyGestureString, out HotKeyModifiers aModifier, out Keys aKey)
        {
            if (aKeyGestureString is null)
            {
                throw new ArgumentNullException(nameof(aKeyGestureString));
            }

            aModifier = HotKeyModifiers.None;
            aKey = Keys.None;

            foreach (string token in aKeyGestureString.Split('+'))
            {
                switch (token.Trim().ToUpperInvariant())
                {
                    case "CONTROL":
                    case "CTRL":
                        aModifier |= HotKeyModifiers.Control;
                        break;
                    case "ALT":
                        aModifier |= HotKeyModifiers.Alt;
                        break;
                    case "SHIFT":
                        aModifier |= HotKeyModifiers.Shift;
                        break;
                    case "WINDOWS":
                    case "WIN":
                        aModifier |= HotKeyModifiers.Windows;
                        break;
                    default:
                        if (aKey != Keys.None || !Enum.TryParse(token.Trim(), true, out aKey) || aKey == Keys.None)
                        {
                            throw new ArgumentException($"Invalid key gesture: \"{aKeyGestureString}\"", nameof(aKeyGestureString));
                        }
                        break;
                }
            }

            if (aKey == Keys.None)
            {
                throw new ArgumentException($"Invalid key gesture: \"{aKeyGestureString}\"", nameof(aKeyGestureString));
            }
        }

        private static readonly InvisibleWindowForMessages window = new InvisibleWindowForMessages();
        private static int currentID;
        private static uint MOD_NOREPEAT = 0x4000;
        private static List<HotKeyWithAction> registeredHotKeys = new List<HotKeyWithAction>();

        private class HotKeyWithAction
        {

            public HotKeyWithAction(HotKeyModifiers modifier, Keys key, Action action)
            {
                Modifier = modifier;
                Key = key;
                Action = action;
            }

            public HotKeyModifiers Modifier { get; }
            public Keys Key { get; }
            public Action Action { get; }
        }

        // Registers a hot key with Windows.
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        // Unregisters the hot key with Windows.
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private class InvisibleWindowForMessages : System.Windows.Forms.NativeWindow, IDisposable
        {
            public InvisibleWindowForMessages()
            {
                CreateHandle(new System.Windows.Forms.CreateParams());
            }

            private static int WM_HOTKEY = 0x0312;
            protected override void WndProc(ref System.Windows.Forms.Message m)
            {
                base.WndProc(ref m);

                if (m.Msg == WM_HOTKEY)
                {
                    var aKey = (Keys)(((int)m.LParam >> 16) & 0xFFFF);
                    HotKeyModifiers modifier = (HotKeyModifiers)((int)m.LParam & 0xFFFF);
                    if (KeyPressed != null)
                    {
                        KeyPressed(this, new HotKeyPressedEventArgs(modifier, aKey));
                    }
                }
            }

            public class HotKeyPressedEventArgs : EventArgs
            {
                private HotKeyModifiers _modifier;
                private Keys _key;

                internal HotKeyPressedEventArgs(HotKeyModifiers modifier, Keys key)
                {
                    _modifier = modifier;
                    _key = key;
                }

                public HotKeyModifiers Modifier
                {
                    get { return _modifier; }
                }

                public Keys Key
                {
                    get { return _key; }
                }
            }


            public event EventHandler<HotKeyPressedEventArgs> KeyPressed;

            #region IDisposable Members

            public void Dispose()
            {
                this.DestroyHandle();
            }

            #endregion
        }
    }
}
