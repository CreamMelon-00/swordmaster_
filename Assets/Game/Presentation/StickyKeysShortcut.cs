using System;
using System.Runtime.InteropServices;

namespace TurnLimbo.Presentation
{
    /// <summary>The Windows shortcut that opens the Sticky Keys prompt after five Shift presses in a row. 넘기기 is
    /// pressed on Shift in quick succession, so the game turns the shortcut off while it has focus
    /// (<see cref="StickyKeysShortcutGuard"/>) and puts it back afterwards. Only the running session changes: nothing
    /// is written to the user profile, so the shortcut also comes back at the next sign-in. Sticky Keys itself is
    /// never touched, and a player who has Sticky Keys on keeps the shortcut. Other platforms do nothing.</summary>
    public static class StickyKeysShortcut
    {
        // STICKYKEYS.dwFlags (winuser.h).
        public const uint StickyKeysOn = 0x1;
        public const uint HotkeyActive = 0x4;
        public const uint ConfirmHotkey = 0x8;
        /// <summary>The shortcut itself and its confirmation prompt.</summary>
        public const uint ShortcutFlags = HotkeyActive | ConfirmHotkey;

        private static uint originalShortcut;

        /// <summary>Whether the shortcut is currently turned off by the game.</summary>
        public static bool IsSuspended { get; private set; }

        /// <summary>The flags with the shortcut off, or null when nothing should change: Sticky Keys is on (its
        /// user keeps the way to turn it off) or the shortcut is already off.</summary>
        public static uint? WithoutShortcut(uint flags)
            => (flags & StickyKeysOn) != 0 || (flags & ShortcutFlags) == 0 ? (uint?)null : flags & ~ShortcutFlags;

        /// <summary>The current flags with the shortcut bits put back as they were before the game changed them.</summary>
        public static uint WithShortcut(uint current, uint original) => current & ~ShortcutFlags | original & ShortcutFlags;

        public static void Suspend()
        {
            if (IsSuspended || !TryGet(out uint flags)) return;
            uint? off = WithoutShortcut(flags);
            if (off == null || !TrySet(off.Value)) return;
            originalShortcut = flags & ShortcutFlags;
            IsSuspended = true;
        }

        public static void Restore()
        {
            if (!IsSuspended) return;
            IsSuspended = false;
            if (TryGet(out uint flags)) TrySet(WithShortcut(flags, originalShortcut));
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        public static bool IsSupported => true;

        private const uint GetStickyKeys = 0x003A, SetStickyKeys = 0x003B;

        [StructLayout(LayoutKind.Sequential)]
        private struct StickyKeys
        {
            public uint Size;
            public uint Flags;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SystemParametersInfo(uint action, uint parameter, ref StickyKeys value, uint winIni);

        private static bool TryGet(out uint flags)
        {
            var keys = new StickyKeys { Size = (uint)Marshal.SizeOf<StickyKeys>() };
            flags = 0;
            try
            {
                if (!SystemParametersInfo(GetStickyKeys, keys.Size, ref keys, 0)) return false;
            }
            catch (Exception) { return false; }
            flags = keys.Flags;
            return true;
        }

        // winIni 0: the running session only. Nothing is written to the user profile or broadcast.
        private static bool TrySet(uint flags)
        {
            var keys = new StickyKeys { Size = (uint)Marshal.SizeOf<StickyKeys>(), Flags = flags };
            try { return SystemParametersInfo(SetStickyKeys, keys.Size, ref keys, 0); }
            catch (Exception) { return false; }
        }
#else
        public static bool IsSupported => false;

        private static bool TryGet(out uint flags)
        {
            flags = 0;
            return false;
        }

        private static bool TrySet(uint flags) => false;
#endif
    }
}
