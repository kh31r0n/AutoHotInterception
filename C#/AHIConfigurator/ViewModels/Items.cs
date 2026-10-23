using AHIConfigurator.Models;
using AHIConfigurator.Services;

namespace AHIConfigurator.ViewModels
{
    /// <summary>
    /// A row in the device list: a connected device, a configured device that is not connected, or both.
    /// </summary>
    public class DeviceItem : ObservableObject
    {
        private DeviceRef _ref;

        public DeviceItem(ConnectedDevice connected, DeviceRef deviceRef)
        {
            Connected = connected;
            _ref = deviceRef;
        }

        /// <summary>Null when the device is configured but not plugged in</summary>
        public ConnectedDevice Connected { get; }

        /// <summary>Null until the device gets its first mapping</summary>
        public DeviceRef Ref
        {
            get => _ref;
            set
            {
                _ref = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Name));
            }
        }

        public bool IsMouse => Connected?.IsMouse ?? Ref.IsMouse;
        public bool IsConnected => Connected != null;
        public string Name => Ref?.Name ?? Connected?.Name ?? "?";
        public string Kind => IsMouse ? "Mouse" : "Keyboard";
        public string Icon => IsMouse ? "\U0001F5B1" : "⌨";

        public string Detail
        {
            get
            {
                var vid = Connected?.Vid ?? Ref.Vid;
                var pid = Connected?.Pid ?? Ref.Pid;
                var ids = vid == 0 && pid == 0 ? Kind : $"{Kind} · VID {vid:X4} PID {pid:X4}";
                return IsConnected ? $"{ids} · ID {Connected.Id}" : $"{ids} · not connected";
            }
        }

        public void Renamed() => OnPropertyChanged(nameof(Name));

        public override string ToString() => Name;
    }

    /// <summary>A row in the mapping grid</summary>
    public class MappingItem
    {
        public MappingItem(Mapping mapping, string deviceName)
        {
            Mapping = mapping;
            DeviceName = deviceName;
        }

        public Mapping Mapping { get; }
        public string DeviceName { get; }
        public string Trigger => KeyNames.Describe(Mapping.Input, Mapping.Code);
        public string Action => Describe(Mapping);

        public static string Describe(Mapping m)
        {
            switch (m.Action)
            {
                case ActionType.Key: return "Key: " + KeyNames.GetName(m.TargetCode);
                case ActionType.Mouse: return "Mouse: " + MouseCodes.GetName(m.TargetCode);
                case ActionType.Block: return "Block (do nothing)";
                case ActionType.PassThrough: return "Keep original";
                case ActionType.Text: return "Type text: " + Shorten(m.Value);
                case ActionType.Send: return "Send keys: " + Shorten(m.Value);
                case ActionType.Run: return "Run: " + Shorten(m.Value);
                default: return m.Action.ToString();
            }
        }

        private static string Shorten(string s)
        {
            s = (s ?? "").Replace("\r", "").Replace("\n", " ⏎ ");
            return s.Length > 60 ? s.Substring(0, 57) + "..." : s;
        }
    }
}
