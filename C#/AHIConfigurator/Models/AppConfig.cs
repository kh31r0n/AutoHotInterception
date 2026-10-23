using System.Collections.Generic;
using System.Linq;

namespace AHIConfigurator.Models
{
    /// <summary>
    /// Everything the user configured. Persisted as JSON by <see cref="Services.ConfigStore"/>.
    /// </summary>
    public class AppConfig
    {
        public int Version { get; set; } = 1;
        public List<DeviceRef> Devices { get; set; } = new List<DeviceRef>();

        /// <summary>App profiles in priority order; the default profile (MatchType.None) is always last.</summary>
        public List<Profile> Profiles { get; set; } = new List<Profile>();

        /// <summary>Optional override for the AutoHotkey v2 executable. Empty = auto-detect.</summary>
        public string AutoHotkeyPath { get; set; } = "";

        public bool RunAtLogin { get; set; }

        public static AppConfig CreateDefault()
        {
            var config = new AppConfig();
            config.Normalize();
            return config;
        }

        // Not serialized: Json.NET would populate the existing default profile again on load, duplicating its mappings
        [Newtonsoft.Json.JsonIgnore]
        public Profile DefaultProfile => Profiles.First(p => p.IsDefault);

        /// <summary>
        /// Repairs a loaded config: exactly one default profile, placed last (extra ones are merged into it),
        /// and no null lists.
        /// </summary>
        public void Normalize()
        {
            Devices = Devices ?? new List<DeviceRef>();
            Profiles = (Profiles ?? new List<Profile>()).Where(p => p != null).ToList();
            foreach (var p in Profiles) p.Mappings = p.Mappings ?? new List<Mapping>();

            var defaults = Profiles.Where(p => p.IsDefault).ToList();
            var main = defaults.FirstOrDefault() ?? Profile.CreateDefault();
            foreach (var extra in defaults.Skip(1)) main.Mappings.AddRange(extra.Mappings);
            Profiles.RemoveAll(p => p.IsDefault);
            Profiles.Add(main);
        }

        public DeviceRef FindDevice(string key) => Devices.FirstOrDefault(d => d.Key == key);

        /// <summary>A device key not used yet, eg "kb3" or "m1"</summary>
        public string NewDeviceKey(bool isMouse)
        {
            var prefix = isMouse ? "m" : "kb";
            for (var i = 1; ; i++)
            {
                var key = prefix + i;
                if (FindDevice(key) == null) return key;
            }
        }
    }
}
