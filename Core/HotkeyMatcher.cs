using System;

namespace FlowWheel.Core
{
    /// <summary>
    /// Caches parsed hotkey components and matches against keyboard events.
    /// Eliminates duplicated hotkey parsing/matching logic between Toggle and ReadingMode hotkeys.
    /// </summary>
    internal class HotkeyMatcher
    {
        private string _lastParsedHotkey = "";
        private bool _needsCtrl = false;
        private bool _needsShift = false;
        private bool _needsAlt = false;
        private int _vkCode = 0;

        /// <summary>
        /// Check if the given vkCode with current modifier state matches the specified hotkey string.
        /// Only re-parses when the hotkey string changes.
        /// </summary>
        public bool IsMatch(int vkCode, string hotkey)
        {
            if (string.IsNullOrEmpty(hotkey)) return false;

            ParseIfChanged(hotkey);

            if (_needsCtrl != NativeMethods.IsCtrlPressed()) return false;
            if (_needsAlt != NativeMethods.IsAltPressed()) return false;
            if (_needsShift != NativeMethods.IsShiftPressed()) return false;
            if (_vkCode > 0 && vkCode == _vkCode) return true;
            return false;
        }

        private void ParseIfChanged(string hotkey)
        {
            if (hotkey == _lastParsedHotkey) return;
            _lastParsedHotkey = hotkey;

            _needsCtrl = false;
            _needsShift = false;
            _needsAlt = false;
            _vkCode = 0;

            string[] parts = hotkey.Split('+');
            foreach (var part in parts)
            {
                var p = part.Trim();
                if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) _needsCtrl = true;
                else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase)) _needsAlt = true;
                else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase)) _needsShift = true;
                else
                {
                    if (p.Length == 1 && char.IsLetter(p[0]))
                        _vkCode = (int)char.ToUpper(p[0]);
                    else if (p.StartsWith("F") && int.TryParse(p.Substring(1), out int fNum) && fNum >= 1 && fNum <= 24)
                        _vkCode = NativeMethods.VK_F1 + fNum - 1;
                }
            }
        }
    }
}
