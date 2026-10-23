using System;
using System.IO;
using System.Reflection;

namespace AHIConfigurator.Services
{
    /// <summary>
    /// Where the Configurator keeps its data and where it finds the shipped AHK runtime.
    /// User data lives in %APPDATA% because the install folder (Program Files) is not writable.
    /// </summary>
    public static class AppPaths
    {
        public const string RuntimeRelativePath = @"AHK v2\Lib\AHIRuntime.ahk";

        public static string DataDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoHotInterception");

        public static string ConfigFile => Path.Combine(DataDir, "config.json");

        public static string ScriptFile => Path.Combine(DataDir, "AHI-Mappings.ahk");

        public static string ExeFile => Assembly.GetEntryAssembly()?.Location ?? "";

        /// <summary>
        /// Finds AHIRuntime.ahk. Installed layout is {app}\Configurator\AHIConfigurator.exe + {app}\AHK v2\Lib;
        /// when running from a source checkout, the repo root is found by walking up from the exe.
        /// </summary>
        public static string FindRuntime()
        {
            var dir = Path.GetDirectoryName(ExeFile);
            while (!string.IsNullOrEmpty(dir))
            {
                var candidate = Path.Combine(dir, RuntimeRelativePath);
                if (File.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }
    }
}
