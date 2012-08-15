using System;
using System.Runtime.InteropServices;

namespace Mongo2Go.Helper
{
    internal static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowText(IntPtr hwnd, String lpString);
    }
}
