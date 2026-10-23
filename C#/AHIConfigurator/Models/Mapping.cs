namespace AHIConfigurator.Models
{
    public enum InputKind
    {
        Key,
        Mouse
    }

    public enum ActionType
    {
        /// <summary>Send another key (TargetCode = AHK scan code)</summary>
        Key,
        /// <summary>Send a mouse button or wheel (TargetCode = <see cref="MouseCodes"/>)</summary>
        Mouse,
        /// <summary>Swallow the input</summary>
        Block,
        /// <summary>Send the input unchanged. Lets an app profile cancel a mapping from the default profile.</summary>
        PassThrough,
        /// <summary>Type Value as text</summary>
        Text,
        /// <summary>Send Value using AHK Send syntax, eg ^c or {Home}+{End}</summary>
        Send,
        /// <summary>Run Value (program, document or URL), Args = working directory</summary>
        Run
    }

    /// <summary>
    /// Mouse codes used in configs and by AHIRuntime.ahk: 0-4 are AHI's button numbers, wheel directions are split out.
    /// </summary>
    public static class MouseCodes
    {
        public const int LButton = 0, RButton = 1, MButton = 2, XButton1 = 3, XButton2 = 4,
            WheelUp = 5, WheelDown = 6, WheelRight = 7, WheelLeft = 8;

        public static readonly string[] Names =
        {
            "Left button", "Right button", "Middle button", "Back button (X1)", "Forward button (X2)",
            "Wheel up", "Wheel down", "Wheel right", "Wheel left"
        };

        public static bool IsWheel(int code) => code >= WheelUp;

        public static string GetName(int code) => code >= 0 && code < Names.Length ? Names[code] : "Mouse " + code;

        /// <summary>AHI reports both directions of a wheel on button 5 / 6 with state = direction</summary>
        public static int FromAhi(int button, int state)
        {
            if (button == 5) return state > 0 ? WheelUp : WheelDown;
            if (button == 6) return state > 0 ? WheelRight : WheelLeft;
            return button;
        }
    }

    public class Mapping
    {
        public string DeviceKey { get; set; }
        public InputKind Input { get; set; }

        /// <summary>AHK scan code for keys (extended keys + 0x100), <see cref="MouseCodes"/> for mice</summary>
        public int Code { get; set; }

        public ActionType Action { get; set; }

        /// <summary>Used by <see cref="ActionType.Key"/> and <see cref="ActionType.Mouse"/></summary>
        public int TargetCode { get; set; }

        /// <summary>Used by <see cref="ActionType.Text"/>, <see cref="ActionType.Send"/> and <see cref="ActionType.Run"/></summary>
        public string Value { get; set; } = "";

        public string Args { get; set; } = "";

        public Mapping Clone() => (Mapping)MemberwiseClone();
    }
}
