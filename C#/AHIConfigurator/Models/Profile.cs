using System.Collections.Generic;
using Newtonsoft.Json;

namespace AHIConfigurator.Models
{
    public enum MatchType
    {
        /// <summary>Always active (the default profile)</summary>
        None,
        /// <summary>Active when the focused window belongs to a process, eg "notepad.exe"</summary>
        Exe,
        /// <summary>Active when the focused window title contains the text</summary>
        Title,
        /// <summary>Active when the focused window has this window class</summary>
        Class
    }

    public class Profile
    {
        public const string DefaultName = "Default (all applications)";

        public string Name { get; set; }
        public MatchType MatchType { get; set; }
        public string MatchValue { get; set; } = "";
        public List<Mapping> Mappings { get; set; } = new List<Mapping>();

        [JsonIgnore]
        public bool IsDefault => MatchType == MatchType.None;

        public static Profile CreateDefault()
        {
            return new Profile { Name = DefaultName, MatchType = MatchType.None };
        }

        /// <summary>
        /// AHK WinTitle used by the runtime with WinActive(). Title matching relies on SetTitleMatchMode(2) (contains).
        /// </summary>
        public string ToWinTitle()
        {
            switch (MatchType)
            {
                case MatchType.Exe: return "ahk_exe " + MatchValue;
                case MatchType.Class: return "ahk_class " + MatchValue;
                case MatchType.Title: return MatchValue ?? "";
                default: return "";
            }
        }

        [JsonIgnore]
        public string MatchDescription
        {
            get
            {
                switch (MatchType)
                {
                    case MatchType.Exe: return "When " + MatchValue + " is active";
                    case MatchType.Class: return "When window class is " + MatchValue;
                    case MatchType.Title: return "When window title contains \"" + MatchValue + "\"";
                    default: return "Everywhere, unless a profile above handles the input";
                }
            }
        }
    }
}
