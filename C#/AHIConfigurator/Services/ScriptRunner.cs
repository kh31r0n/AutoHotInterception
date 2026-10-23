using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using Microsoft.Win32;

namespace AHIConfigurator.Services
{
    /// <summary>
    ///     Finds AutoHotkey v2 and starts / stops the generated script
    /// </summary>
    public static class ScriptRunner
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "AutoHotInterception";

        /// <summary>
        ///     Returns the AutoHotkey v2 exe matching this process' bitness, or null if not found
        /// </summary>
        public static string FindAutoHotkey(string configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath)) return configuredPath;

            var exe = Environment.Is64BitOperatingSystem ? "AutoHotkey64.exe" : "AutoHotkey32.exe";
            foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                using (var key = root.OpenSubKey(@"SOFTWARE\AutoHotkey"))
                {
                    var version = key?.GetValue("Version") as string;
                    var dir = key?.GetValue("InstallDir") as string;
                    if (string.IsNullOrEmpty(dir) || version == null || !version.StartsWith("2")) continue;
                    foreach (var candidate in new[] { Path.Combine(dir, "v2", exe), Path.Combine(dir, exe) })
                    {
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
            var programFiles = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "AutoHotkey", "v2", exe);
            return File.Exists(programFiles) ? programFiles : null;
        }

        /// <summary>
        ///     Loads the script without running it. Returns null if OK, otherwise AHK's error message.
        /// </summary>
        public static string Validate(string ahkPath, string scriptPath)
        {
            var psi = new ProcessStartInfo(ahkPath, $"/ErrorStdOut=UTF-8 /validate \"{scriptPath}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };
            using (var p = Process.Start(psi))
            {
                var stdout = p.StandardOutput.ReadToEndAsync();
                var stderr = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(15000))
                {
                    p.Kill();
                    return "AutoHotkey did not finish validating the script.";
                }
                if (p.ExitCode == 0) return null;
                var message = (stderr + stdout.Result).Trim();
                return message.Length > 0 ? message : "AutoHotkey reported an error (exit code " + p.ExitCode + ").";
            }
        }

        public static void Start(string ahkPath, string scriptPath)
        {
            // #SingleInstance Force in the script replaces an already running copy
            Process.Start(new ProcessStartInfo(ahkPath, $"\"{scriptPath}\"")
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(scriptPath)
            });
        }

        public static void Stop(string scriptPath)
        {
            foreach (var pid in FindProcesses(scriptPath))
            {
                try
                {
                    using (var p = Process.GetProcessById(pid))
                    {
                        p.Kill();
                        p.WaitForExit(3000);
                    }
                }
                catch (ArgumentException)
                {
                    // Already exited
                }
            }
        }

        public static bool IsRunning(string scriptPath)
        {
            return FindProcesses(scriptPath).Any();
        }

        private static IEnumerable<int> FindProcesses(string scriptPath)
        {
            var result = new List<int>();
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                           "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name LIKE 'AutoHotkey%'"))
                {
                    foreach (var obj in searcher.Get().Cast<ManagementObject>())
                    {
                        var cmd = obj["CommandLine"] as string;
                        if (cmd != null && cmd.IndexOf(scriptPath, StringComparison.OrdinalIgnoreCase) >= 0)
                            result.Add(Convert.ToInt32(obj["ProcessId"]));
                    }
                }
            }
            catch (ManagementException)
            {
                // WMI unavailable - treat as not running
            }
            return result;
        }

        public static void SetRunAtLogin(bool enable, string ahkPath, string scriptPath)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enable)
                    key.SetValue(RunValueName, $"\"{ahkPath}\" \"{scriptPath}\"");
                else if (key.GetValue(RunValueName) != null)
                    key.DeleteValue(RunValueName);
            }
        }
    }
}
