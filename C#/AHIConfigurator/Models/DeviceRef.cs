namespace AHIConfigurator.Models
{
    /// <summary>
    /// A keyboard or mouse that has mappings. Interception IDs (1-20) change between boots / replugs,
    /// so devices are stored by hardware handle + instance (with VID/PID as a fallback) and resolved at runtime.
    /// </summary>
    public class DeviceRef
    {
        /// <summary>Stable key referenced by mappings, eg "kb1" or "m1"</summary>
        public string Key { get; set; }

        /// <summary>User-visible name. Defaults to the name Windows reports, can be renamed.</summary>
        public string Name { get; set; }

        public bool IsMouse { get; set; }

        /// <summary>Hardware ID string as reported by Interception, eg HID\VID_046D&amp;PID_C52B&amp;REV_1211&amp;MI_00</summary>
        public string Handle { get; set; } = "";

        public int Vid { get; set; }
        public int Pid { get; set; }

        /// <summary>1-based index among connected devices with the same handle (for identical devices)</summary>
        public int Instance { get; set; } = 1;

        public bool Matches(bool isMouse, string handle, int vid, int pid, int instance)
        {
            if (IsMouse != isMouse || Instance != instance) return false;
            if (!string.IsNullOrEmpty(Handle)) return string.Equals(Handle, handle, System.StringComparison.OrdinalIgnoreCase);
            return Vid == vid && Pid == pid;
        }
    }
}
