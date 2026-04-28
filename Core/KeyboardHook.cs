using System;
using System.Runtime.InteropServices;

namespace FlowWheel.Core
{
    public class KeyboardHook : BaseHook
    {
        private readonly NativeMethods.LowLevelKeyboardProc _proc;

        public event EventHandler<KeyboardEventArgs>? KeyboardEvent;

        protected override IntPtr SetHook()
        {
            IntPtr moduleHandle = GetModuleHandle();
            return NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc, moduleHandle, 0);
        }

        public KeyboardHook()
        {
            _proc = HookCallback;
            InitializeHook(); // Must be called AFTER _proc is initialized
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = (int)wParam;
                NativeMethods.KBDLLHOOKSTRUCT? hookStruct = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

                if (hookStruct != null)
                {
                    var args = new KeyboardEventArgs(msg, (int)hookStruct.Value.vkCode);
                    KeyboardEvent?.Invoke(this, args);

                    if (args.Handled)
                    {
                        return (IntPtr)1;
                    }
                }
            }
            return CallNextHook(nCode, wParam, lParam);
        }
    }

    public class KeyboardEventArgs : EventArgs
    {
        public int Message { get; }
        public int VkCode { get; }
        public bool Handled { get; set; }

        public KeyboardEventArgs(int msg, int vkCode)
        {
            Message = msg;
            VkCode = vkCode;
            Handled = false;
        }
    }
}
