using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;

namespace LOLSummonerTiming
{
    // Global keyboard hook + keyboard input simulation
    // Usage:
    //   var kb = new GlobalKeyboard();
    //   kb.Start();
    //   kb.KeyDown += (s, e) => { /* e.VirtualKey, e.Key, e.Ctrl, e.Shift, e.Alt */ };
    //   kb.TypeText("Hello");
    //   kb.TypeChar('!');
    //   kb.SendKeyPress(Key.Enter);
    public sealed class GlobalKeyboard : IDisposable
    {
        public event EventHandler<GlobalKeyEventArgs>? KeyDown;
        public event EventHandler<GlobalKeyEventArgs>? KeyUp;

        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc? _proc;
        private bool _disposed;

        public bool IsHookActive => _hookId != IntPtr.Zero;

        public void Start()
        {
            if (IsHookActive) return;
            _proc = HookCallback;
            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            IntPtr hModule = GetModuleHandle(curModule?.ModuleName);
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, hModule, 0);
            if (_hookId == IntPtr.Zero)
                ThrowLastWin32Error("SetWindowsHookEx failed");
        }

        public void Stop()
        {
            if (!IsHookActive) return;
            if (!UnhookWindowsHookEx(_hookId))
                ThrowLastWin32Error("UnhookWindowsHookEx failed");
            _hookId = IntPtr.Zero;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                var vkCode = (int)data.vkCode;
                var key = KeyInterop.KeyFromVirtualKey(vkCode);
                var args = new GlobalKeyEventArgs(vkCode, key,
                    ctrl: IsKeyDown(VK_CONTROL) || IsKeyDown(VK_LCONTROL) || IsKeyDown(VK_RCONTROL),
                    shift: IsKeyDown(VK_SHIFT) || IsKeyDown(VK_LSHIFT) || IsKeyDown(VK_RSHIFT),
                    alt: IsKeyDown(VK_MENU) || IsKeyDown(VK_LMENU) || IsKeyDown(VK_RMENU));

                switch (msg)
                {
                    case WM_KEYDOWN:
                    case WM_SYSKEYDOWN:
                        KeyDown?.Invoke(this, args);
                        break;
                    case WM_KEYUP:
                    case WM_SYSKEYUP:
                        KeyUp?.Invoke(this, args);
                        break;
                }

                if (args.Handled)
                {
                    return (IntPtr)1; // swallow the key
                }
            }

            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private static bool IsKeyDown(int vk) => (GetKeyState(vk) & 0x8000) != 0;

        public void Dispose()
        {
            if (_disposed) return;
            Stop();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~GlobalKeyboard()
        {
            try { Stop(); } catch { /* ignore finalizer errors */ }
        }

        // ================= Keyboard layout helpers =================
        public const ushort LANG_EN_US = 0x0409;
        public const ushort LANG_RU_RU = 0x0419;

        public static ushort GetForegroundLanguageId()
        {
            var hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return GetLangIdFromHkl(GetKeyboardLayout(0));
            uint tid = GetWindowThreadProcessId(hWnd, out _);
            var hkl = GetKeyboardLayout(tid);
            return GetLangIdFromHkl(hkl);
        }

        public static bool EnsureForegroundLanguage(ushort langId)
        {
            var hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return false;

            uint tid = GetWindowThreadProcessId(hWnd, out _);
            var current = GetKeyboardLayout(tid);
            if (GetLangIdFromHkl(current) == langId) return true;

            string klid = KlidFromLangId(langId);
            // Load or get handle to desired layout
            var targetHkl = LoadKeyboardLayout(klid, KLF_ACTIVATE | KLF_SETFORPROCESS);
            if (targetHkl == IntPtr.Zero)
            {
                targetHkl = LoadKeyboardLayout(klid, KLF_ACTIVATE);
                if (targetHkl == IntPtr.Zero) return false;
            }

            // Ask the foreground window to switch its input language
            SendMessage(hWnd, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, targetHkl);

            // Best effort verification
            System.Threading.Thread.Sleep(10);
            var after = GetKeyboardLayout(tid);
            return GetLangIdFromHkl(after) == langId;
        }

        private static ushort GetLangIdFromHkl(IntPtr hkl) => (ushort)((ulong)hkl & 0xFFFF);
        private static string KlidFromLangId(ushort langId) => "0000" + langId.ToString("X4");

        public static string GetCurrentKlid()
        {
            var sb = new StringBuilder(KL_NAMELENGTH);
            if (GetKeyboardLayoutName(sb)) return sb.ToString();
            return string.Empty;
        }

        // ================= Keyboard simulation =================
        // Sends characters using UNICODE events. Works in classic apps, many games ignore it.
        public void TypeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var inputs = new INPUT[text.Length * 2];
            int idx = 0;
            foreach (var ch in text)
            {
                // key down (unicode)
                inputs[idx++] = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    U = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = ch,
                            dwFlags = KEYEVENTF_UNICODE,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };
                // key up (unicode)
                inputs[idx++] = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    U = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = ch,
                            dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };
            }

            var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
            if (sent != (uint)inputs.Length)
                ThrowLastWin32Error("SendInput (TypeText) failed");
        }

        public void TypeChar(char ch)
        {
            var inputs = new INPUT[2];
            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = ch,
                        dwFlags = KEYEVENTF_UNICODE,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
            inputs[1] = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = ch,
                        dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
            if (sent != (uint)inputs.Length)
                ThrowLastWin32Error("SendInput (TypeChar) failed");
        }

        public void SendKeyPress(Key key)
        {
            int vk = KeyInterop.VirtualKeyFromKey(key);
            SendKeyPress(vk);
        }

        public void SendKeyPress(int virtualKey)
        {
            SendKeyDown(virtualKey);
            SendKeyUp(virtualKey);
        }

        public void SendKeyDown(Key key) => SendKeyDown(KeyInterop.VirtualKeyFromKey(key));
        public void SendKeyUp(Key key) => SendKeyUp(KeyInterop.VirtualKeyFromKey(key));

        // Use scan codes so games using Raw Input/DirectInput accept the events
        public void SendKeyDown(int virtualKey)
        {
            var layout = GetKeyboardLayout(0);
            uint scan = MapVirtualKeyEx((uint)virtualKey, MAPVK_VK_TO_VSC, layout);
            var flags = KEYEVENTF_SCANCODE | (IsExtendedKey(virtualKey) ? KEYEVENTF_EXTENDEDKEY : 0);
            var input = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = (ushort)scan,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
            var sent = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
            if (sent != 1)
                ThrowLastWin32Error("SendInput (KeyDown) failed");
        }

        public void SendKeyUp(int virtualKey)
        {
            var layout = GetKeyboardLayout(0);
            uint scan = MapVirtualKeyEx((uint)virtualKey, MAPVK_VK_TO_VSC, layout);
            var flags = KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP | (IsExtendedKey(virtualKey) ? KEYEVENTF_EXTENDEDKEY : 0);
            var input = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = (ushort)scan,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
            var sent = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
            if (sent != 1)
                ThrowLastWin32Error("SendInput (KeyUp) failed");
        }

        // Layout-aware physical typing using scan codes (works in most games)
        public void TypeTextPhysical(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var hWnd = GetForegroundWindow();
            uint tid = hWnd != IntPtr.Zero ? GetWindowThreadProcessId(hWnd, out _) : 0u;
            var layout = tid != 0 ? GetKeyboardLayout(tid) : GetKeyboardLayout(0);

            foreach (var ch in text)
            {
                short vkWithMods = VkKeyScanEx(ch, layout);
                if (vkWithMods == -1)
                {
                    // Fallback to UNICODE for unknown chars
                    TypeChar(ch);
                    continue;
                }

                int vk = vkWithMods & 0xFF;
                int mods = (vkWithMods >> 8) & 0xFF;

                bool needShift = (mods & 1) != 0;
                // We intentionally ignore Ctrl/Alt here to prevent sending chat-killing combos

                if (needShift) SendKeyDown(VK_SHIFT);
                SendKeyDown(vk);
                SendKeyUp(vk);
                if (needShift) SendKeyUp(VK_SHIFT);
            }
        }

        private static bool IsExtendedKey(int vk)
        {
            return vk switch
            {
                0x21 or // VK_PRIOR (Page Up)
                0x22 or // VK_NEXT (Page Down)
                0x23 or // VK_END
                0x24 or // VK_HOME
                0x25 or // VK_LEFT
                0x26 or // VK_UP
                0x27 or // VK_RIGHT
                0x28 or // VK_DOWN
                0x2D or // VK_INSERT
                0x2E or // VK_DELETE
                0x5B or // VK_LWIN
                0x5C or // VK_RWIN
                0x5D or // VK_APPS
                0x6F or // VK_DIVIDE (numpad)
                0xA3 or // VK_RCONTROL
                0xA5     // VK_RMENU (Right Alt)
                => true,
                _ => false
            };
        }

        private static void ThrowLastWin32Error(string message)
        {
            int err = Marshal.GetLastPInvokeError();
            throw new System.ComponentModel.Win32Exception(err, message + $" (0x{err:X})");
        }

        // ================== Win32 interop ==================
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;

        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const uint KEYEVENTF_SCANCODE = 0x0008;

        private const uint MAPVK_VK_TO_VSC = 0;

        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12; // Alt
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_LMENU = 0xA4; // Left Alt
        private const int VK_RMENU = 0xA5; // Right Alt

        private const int KL_NAMELENGTH = 9; // including null terminator
        private const uint KLF_ACTIVATE = 0x00000001;
        private const uint KLF_SETFORPROCESS = 0x00000100;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKeyEx(uint uCode, uint uMapType, IntPtr dwhkl);

        [DllImport("user32.dll")]
        private static extern short VkKeyScanEx(char ch, IntPtr dwhkl);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetKeyboardLayoutName(StringBuilder pwszKLID);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

        [DllImport("user32.dll")]
        private static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint Flags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }
    }

    public sealed class GlobalKeyEventArgs : EventArgs
    {
        internal GlobalKeyEventArgs(int virtualKey, Key key, bool ctrl, bool shift, bool alt)
        {
            VirtualKey = virtualKey;
            Key = key;
            Ctrl = ctrl; Shift = shift; Alt = alt;
        }

        public int VirtualKey { get; }
        public Key Key { get; }
        public bool Ctrl { get; }
        public bool Shift { get; }
        public bool Alt { get; }
        public bool Handled { get; set; }
    }
}
