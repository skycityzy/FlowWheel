using System.Diagnostics;

namespace FlowWheel.Core
{
    /// <summary>
    /// Base class for Windows low-level hooks, eliminating code duplication between MouseHook and KeyboardHook.
    /// </summary>
    public abstract class BaseHook : IDisposable
    {
        private IntPtr _hookId = IntPtr.Zero;
        private bool _disposed = false;

        protected BaseHook()
        {
            // Do NOT call SetHook() here - derived class fields are not yet initialized.
            // Derived classes must call InitializeHook() at the end of their constructor.
        }

        /// <summary>
        /// Called by derived classes at the end of their constructor to install the hook.
        /// This ensures delegate fields are initialized before the hook is set.
        /// </summary>
        protected void InitializeHook()
        {
            _hookId = SetHook();
            if (_hookId == IntPtr.Zero)
            {
                Debug.WriteLine($"Failed to install {GetType().Name}");
            }
        }

        /// <summary>
        /// Subclasses implement this to call SetWindowsHookEx with their specific delegate type.
        /// </summary>
        protected abstract IntPtr SetHook();

        protected IntPtr CallNextHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        /// <summary>
        /// Helper to get the module handle for the current process, used by subclass SetHook implementations.
        /// </summary>
        protected static IntPtr GetModuleHandle()
        {
            using Process curProcess = Process.GetCurrentProcess();
            using ProcessModule? curModule = curProcess.MainModule;
            
            IntPtr moduleHandle = IntPtr.Zero;
            if (curModule != null)
            {
                moduleHandle = NativeMethods.GetModuleHandle(curModule.ModuleName);
            }
            
            if (moduleHandle == IntPtr.Zero)
            {
                moduleHandle = NativeMethods.GetModuleHandle(null);
            }

            return moduleHandle;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_hookId != IntPtr.Zero)
                {
                    NativeMethods.UnhookWindowsHookEx(_hookId);
                    _hookId = IntPtr.Zero;
                }
                _disposed = true;
            }
        }

        ~BaseHook()
        {
            Dispose(false);
        }
    }
}
