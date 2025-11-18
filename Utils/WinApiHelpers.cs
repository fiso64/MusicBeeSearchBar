using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MusicBeePlugin.Utils
{
    public static class WinApiHelpers
    {
        const int WM_GETTEXT = 0x000D;
        const int WM_GETTEXTLENGTH = 0x000E;
        const int WM_SETTEXT = 0x000C;
        const uint EM_SETCUEBANNER = 0x1501;

        const int WM_KEYDOWN = 0x0100;
        const int WM_KEYUP = 0x0101;
        const int VK_RETURN = 0x0D;

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, StringBuilder lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, string lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetFocus();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public Point ptMinPosition;
            public Point ptMaxPosition;
            public Rectangle rcNormalPosition;
        }

        public enum WindowState
        {
            Minimized = -1,
            None = 0,
            Maximized = 1
        }

        public static WindowState WinGetMinMax(IntPtr hwnd)
        {
            WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(placement);
            GetWindowPlacement(hwnd, ref placement);

            switch (placement.showCmd)
            {
                case 1: // SW_SHOWNORMAL
                case 9: // SW_RESTORE
                    return WindowState.None;
                case 2: // SW_SHOWMINIMIZED
                    return WindowState.Minimized;
                case 3: // SW_SHOWMAXIMIZED
                    return WindowState.Maximized;
                default:
                    return WindowState.None;
            }
        }

        public static bool IsWindowFocused(IntPtr hWnd)
        {
            return hWnd == GetForegroundWindow();
        }

        public static void WinRestore(IntPtr hwnd)
        {
            ShowWindow(hwnd, 9); // SW_RESTORE
        }

        public static string GetText(IntPtr hwnd)
        {
            int length = SendMessage(hwnd, WM_GETTEXTLENGTH, 0, IntPtr.Zero);
            if (length > 0)
            {
                StringBuilder windowText = new StringBuilder(length + 1);
                SendMessage(hwnd, WM_GETTEXT, windowText.Capacity, windowText);
                return windowText.ToString();
            }

            return string.Empty;
        }

        public static bool IsEdit(IntPtr hwnd)
        {
            return GetClassNN(hwnd).Contains("EDIT");
        }

        public static bool IsEditFocused()
        {
            return GetClassNN(GetFocus()).Contains("EDIT");
        }

        public static string GetClassNN(IntPtr hwnd)
        {
            StringBuilder className = new StringBuilder(256);
            GetClassName(hwnd, className, className.Capacity);
            return className.ToString();
        }

        public static void SetEditText(IntPtr hwndEdit, string text)
        {
            SendMessage(hwndEdit, WM_SETTEXT, 0, text);
        }

        public static void SendEnterKey(IntPtr hwndEdit)
        {
            SendMessage(hwndEdit, WM_KEYDOWN, VK_RETURN, IntPtr.Zero);
            SendMessage(hwndEdit, WM_KEYUP, VK_RETURN, IntPtr.Zero);
        }

        public static bool SendAccepts(Keys keys)
        {
            var modifiers = keys & Keys.Modifiers;
            return modifiers == (modifiers & (Keys.Control | Keys.Alt | Keys.Shift));
        }

        public static void SendKey(Keys keys)
        {
            var modifiers = keys & Keys.Modifiers;

            string keysToSend = "";

            // these are the only modifiers that SendKeys.Send supports
            if ((modifiers & Keys.Control) == Keys.Control)
                keysToSend += "^";
            if ((modifiers & Keys.Shift) == Keys.Shift)
                keysToSend += "+";
            if ((modifiers & Keys.Alt) == Keys.Alt)
                keysToSend += "%";

            var input = keys & ~Keys.Modifiers;

            switch (input)
            {
                case Keys.Back: keysToSend += "{BACKSPACE}"; break;
                case Keys.CapsLock: keysToSend += "{CAPSLOCK}"; break;
                case Keys.Delete: keysToSend += "{DELETE}"; break;
                case Keys.Down: keysToSend += "{DOWN}"; break;
                case Keys.End: keysToSend += "{END}"; break;
                case Keys.Enter: keysToSend += "{ENTER}"; break;
                case Keys.Escape: keysToSend += "{ESC}"; break;
                case Keys.Help: keysToSend += "{HELP}"; break;
                case Keys.Home: keysToSend += "{HOME}"; break;
                case Keys.Insert: keysToSend += "{INSERT}"; break;
                case Keys.Left: keysToSend += "{LEFT}"; break;
                case Keys.NumLock: keysToSend += "{NUMLOCK}"; break;
                case Keys.PageDown: keysToSend += "{PGDN}"; break;
                case Keys.PageUp: keysToSend += "{PGUP}"; break;
                case Keys.PrintScreen: keysToSend += "{PRTSC}"; break;
                case Keys.Right: keysToSend += "{RIGHT}"; break;
                case Keys.Scroll: keysToSend += "{SCROLLLOCK}"; break;
                case Keys.Tab: keysToSend += "{TAB}"; break;
                case Keys.Up: keysToSend += "{UP}"; break;
                case Keys.F1: keysToSend += "{F1}"; break;
                case Keys.F2: keysToSend += "{F2}"; break;
                case Keys.F3: keysToSend += "{F3}"; break;
                case Keys.F4: keysToSend += "{F4}"; break;
                case Keys.F5: keysToSend += "{F5}"; break;
                case Keys.F6: keysToSend += "{F6}"; break;
                case Keys.F7: keysToSend += "{F7}"; break;
                case Keys.F8: keysToSend += "{F8}"; break;
                case Keys.F9: keysToSend += "{F9}"; break;
                case Keys.F10: keysToSend += "{F10}"; break;
                case Keys.F11: keysToSend += "{F11}"; break;
                case Keys.F12: keysToSend += "{F12}"; break;
                case Keys.F13: keysToSend += "{F13}"; break;
                case Keys.F14: keysToSend += "{F14}"; break;
                case Keys.F15: keysToSend += "{F15}"; break;
                case Keys.F16: keysToSend += "{F16}"; break;
                case Keys.Add: keysToSend += "{ADD}"; break;
                case Keys.Subtract: keysToSend += "{SUBTRACT}"; break;
                case Keys.Multiply: keysToSend += "{MULTIPLY}"; break;
                case Keys.Divide: keysToSend += "{DIVIDE}"; break;
                default: keysToSend += input.ToString().ToLower(); break;
            }

            SendKeys.SendWait(keysToSend);
        }

        public static void CenterForm(Form form, IntPtr parentHandle)
        {
            if (parentHandle != IntPtr.Zero)
            {
                var parentWindow = Control.FromHandle(parentHandle);
                if (parentWindow != null)
                {
                    var parentBounds = parentWindow.Bounds;
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(
                        parentBounds.X + (parentBounds.Width - form.Width) / 2,
                        parentBounds.Y + (parentBounds.Height - form.Height) / 2
                    );
                }
            }
        }

        public static List<Control> GetAllControls(Control parent)
        {
            var controls = new List<Control>();
            GetAllControlsRecursive(parent, controls);
            return controls;
        }

        private static void GetAllControlsRecursive(Control parent, List<Control> controls)
        {
            foreach (Control child in parent.Controls)
            {
                controls.Add(child);
                GetAllControlsRecursive(child, controls);
            }
        }

        [DllImport("user32.dll")]
        static extern bool SetFocus(IntPtr hWnd);

        public static bool SetFocus(Control control)
        {
            if (control == null || control.Handle == IntPtr.Zero)
                return false;

            return SetFocus(control.Handle);
        }

        public static Rectangle GetWindowRect(IntPtr hwnd)
        {
            RECT rect;
            GetWindowRect(hwnd, out rect);
            return new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        const int KEYEVENTF_KEYUP = 0x0002;
        const byte VK_SHIFT = 0x10;
        const byte VK_CONTROL = 0x11;
        const byte VK_MENU = 0x12;  // ALT key
        const byte VK_TAB = 0x09;

        public static void ReleaseAllModifierKeys()
        {
            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public static void SendShiftTab()
        {
            ReleaseAllModifierKeys();

            keybd_event(VK_SHIFT, 0, 0, UIntPtr.Zero);

            keybd_event(VK_TAB, 0, 0, UIntPtr.Zero);
            keybd_event(VK_TAB, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public static void SendTab()
        {
            ReleaseAllModifierKeys();

            keybd_event(VK_TAB, 0, 0, UIntPtr.Zero);
            keybd_event(VK_TAB, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public static void SetCueBanner(IntPtr hwnd, string text, bool showWhenFocused = true)
        {
            SendMessage(hwnd, (int)EM_SETCUEBANNER, showWhenFocused ? 1 : 0, text);
        }
    }
}
