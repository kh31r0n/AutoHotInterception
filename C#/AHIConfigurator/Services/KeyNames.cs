using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using AHIConfigurator.Models;

namespace AHIConfigurator.Services
{
    /// <summary>
    ///     Display names for scan codes (AHK numbering: extended keys are + 0x100), using the current keyboard layout
    /// </summary>
    public static class KeyNames
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetKeyNameText(int lParam, StringBuilder lpString, int cchSize);

        // Keys that GetKeyNameText names poorly or not at all
        private static readonly Dictionary<int, string> Overrides = new Dictionary<int, string>
        {
            { 0x45, "Pause" },
            { 0x145, "Num Lock" },
            { 0x15B, "Left Windows" },
            { 0x15C, "Right Windows" },
            { 0x15D, "Menu" },
            { 0x137, "Print Screen" },
            { 0x11C, "Num Enter" },
            { 0x135, "Num /" }
        };

        public static string GetName(int scanCode)
        {
            if (Overrides.TryGetValue(scanCode, out var name)) return name;
            var lParam = ((scanCode & 0xFF) << 16) | ((scanCode & 0x100) != 0 ? 1 << 24 : 0);
            var sb = new StringBuilder(64);
            var len = GetKeyNameText(lParam, sb, sb.Capacity);
            if (len == 0) return string.Format("SC{0:X3}", scanCode);
            name = sb.ToString();
            // Windows returns most names in upper case ("CAPS LOCK", "BLOQ MAYUS"); single characters stay as they are
            return name.Length > 1 && name == name.ToUpper()
                ? System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower())
                : name;
        }

        // ------------------------------------------------------------------ AHK Send syntax

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        private const uint MAPVK_VSC_TO_VK_EX = 3;

        /// <summary>Modifier scan codes and their AHK Send prefix</summary>
        private static readonly Dictionary<int, char> ModifierPrefixes = new Dictionary<int, char>
        {
            { 0x1D, '^' }, { 0x11D, '^' }, // Ctrl
            { 0x38, '!' }, { 0x138, '!' }, // Alt
            { 0x2A, '+' }, { 0x136, '+' }, // Shift (AHI and AHK number RShift 0x136)
            { 0x15B, '#' }, { 0x15C, '#' } // Win
        };

        /// <summary>
        ///     AHK key names for keys that do not type a character. Letters and digits are looked up via the current layout;
        ///     anything else is sent as {scXXX}, which avoids characters that have a meaning in Send syntax (^ + ! # { }).
        /// </summary>
        public static readonly Dictionary<int, string> AhkNames = new Dictionary<int, string>
        {
            { 0x01, "Esc" }, { 0x0E, "Backspace" }, { 0x0F, "Tab" }, { 0x1C, "Enter" }, { 0x39, "Space" },
            { 0x3A, "CapsLock" }, { 0x46, "ScrollLock" }, { 0x145, "NumLock" }, { 0x45, "Pause" }, { 0x137, "PrintScreen" },
            { 0x3B, "F1" }, { 0x3C, "F2" }, { 0x3D, "F3" }, { 0x3E, "F4" }, { 0x3F, "F5" }, { 0x40, "F6" },
            { 0x41, "F7" }, { 0x42, "F8" }, { 0x43, "F9" }, { 0x44, "F10" }, { 0x57, "F11" }, { 0x58, "F12" },
            { 0x64, "F13" }, { 0x65, "F14" }, { 0x66, "F15" }, { 0x67, "F16" }, { 0x68, "F17" }, { 0x69, "F18" },
            { 0x6A, "F19" }, { 0x6B, "F20" }, { 0x6C, "F21" }, { 0x6D, "F22" }, { 0x6E, "F23" }, { 0x76, "F24" },
            { 0x147, "Home" }, { 0x14F, "End" }, { 0x149, "PgUp" }, { 0x151, "PgDn" }, { 0x152, "Insert" }, { 0x153, "Delete" },
            { 0x148, "Up" }, { 0x150, "Down" }, { 0x14B, "Left" }, { 0x14D, "Right" }, { 0x15D, "AppsKey" },
            { 0x52, "Numpad0" }, { 0x4F, "Numpad1" }, { 0x50, "Numpad2" }, { 0x51, "Numpad3" }, { 0x4B, "Numpad4" },
            { 0x4C, "Numpad5" }, { 0x4D, "Numpad6" }, { 0x47, "Numpad7" }, { 0x48, "Numpad8" }, { 0x49, "Numpad9" },
            { 0x53, "NumpadDot" }, { 0x11C, "NumpadEnter" }, { 0x4E, "NumpadAdd" }, { 0x4A, "NumpadSub" },
            { 0x37, "NumpadMult" }, { 0x135, "NumpadDiv" },
            { 0x1D, "LCtrl" }, { 0x11D, "RCtrl" }, { 0x2A, "LShift" }, { 0x136, "RShift" },
            { 0x38, "LAlt" }, { 0x138, "RAlt" }, { 0x15B, "LWin" }, { 0x15C, "RWin" },
            { 0x122, "Media_Play_Pause" }, { 0x124, "Media_Stop" }, { 0x110, "Media_Prev" }, { 0x119, "Media_Next" },
            { 0x120, "Volume_Mute" }, { 0x12E, "Volume_Down" }, { 0x130, "Volume_Up" }
        };

        public static bool IsModifier(int scanCode) => ModifierPrefixes.ContainsKey(scanCode);

        /// <summary>One key in Send syntax, eg "a", "5" or "{Esc}"</summary>
        public static string ToAhkSendKey(int scanCode)
        {
            if (AhkNames.TryGetValue(scanCode, out var name)) return "{" + name + "}";
            var vk = MapVirtualKey((uint)(scanCode & 0xFF) | ((scanCode & 0x100) != 0 ? 0xE000u : 0), MAPVK_VSC_TO_VK_EX);
            if (vk >= 'A' && vk <= 'Z') return ((char)(vk + 32)).ToString();
            if (vk >= '0' && vk <= '9') return ((char)vk).ToString();
            // Not string.Format: "}}}" after a format item is parsed as part of the format string
            return "{sc" + scanCode.ToString("X3") + "}";
        }

        /// <summary>A captured combination in Send syntax, eg ^+{Esc}. Modifiers are generic (^ not &lt;^).</summary>
        public static string ToAhkSend(ComboResult combo)
        {
            var prefix = new StringBuilder();
            foreach (var m in combo.Modifiers)
            {
                var p = ModifierPrefixes[m];
                if (prefix.ToString().IndexOf(p) < 0) prefix.Append(p);
            }
            return prefix + ToAhkSendKey(combo.Code);
        }

        /// <summary>Human-readable combination, eg Ctrl + Shift + Esc</summary>
        public static string DescribeCombo(ComboResult combo)
        {
            var parts = new List<string>();
            foreach (var m in combo.Modifiers)
            {
                string name;
                switch (ModifierPrefixes[m])
                {
                    case '^': name = "Ctrl"; break;
                    case '!': name = "Alt"; break;
                    case '+': name = "Shift"; break;
                    default: name = "Win"; break;
                }
                if (!parts.Contains(name)) parts.Add(name);
            }
            parts.Add(GetName(combo.Code));
            return string.Join(" + ", parts);
        }

        public static string Describe(InputKind kind, int code)
        {
            return kind == InputKind.Mouse ? MouseCodes.GetName(code) : GetName(code);
        }

        /// <summary>
        ///     Keys for the "choose a key" list: every scan code the layout has a name for
        /// </summary>
        public static List<KeyValuePair<int, string>> AllKeys()
        {
            var list = new List<KeyValuePair<int, string>>();
            var seen = new HashSet<string>();
            for (var sc = 1; sc < 0x80; sc++)
            {
                foreach (var code in new[] { sc, sc | 0x100 })
                {
                    var name = GetName(code);
                    if (name.StartsWith("SC") || !seen.Add(name)) continue;
                    list.Add(new KeyValuePair<int, string>(code, name));
                }
            }
            list.Sort((a, b) => string.Compare(a.Value, b.Value, System.StringComparison.CurrentCultureIgnoreCase));
            return list;
        }
    }
}
