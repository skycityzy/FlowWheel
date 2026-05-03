using System;
using System.Collections.Generic;

namespace FlowWheel.Core
{
    internal class HotkeyMatcher
    {
        private string _lastParsedHotkey = "";
        private bool _needsCtrl = false;
        private bool _needsShift = false;
        private bool _needsAlt = false;
        private int _vkCode = 0;

        private static readonly HashSet<string> _modifierKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "Ctrl", "Control", "Alt", "Shift", "Win", "Windows", "LWin", "RWin"
        };

        private static readonly HashSet<string> _mouseKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "MiddleMouse", "Middle", "XButton1", "XButton2"
        };

        public static string NormalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "";
            var parts = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in key.Split('+'))
            {
                var part = p.Trim();
                if (string.IsNullOrEmpty(part)) continue;
                parts.Add(part);
            }
            return string.Join("+", parts);
        }

        public static bool AreKeysEqual(string key1, string key2)
        {
            if (string.IsNullOrWhiteSpace(key1) && string.IsNullOrWhiteSpace(key2)) return true;
            if (string.IsNullOrWhiteSpace(key1) || string.IsNullOrWhiteSpace(key2)) return false;
            return string.Equals(NormalizeKey(key1), NormalizeKey(key2), StringComparison.OrdinalIgnoreCase);
        }

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
