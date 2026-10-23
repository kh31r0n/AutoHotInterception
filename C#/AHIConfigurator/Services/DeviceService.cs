using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using AutoHotInterception;
using Microsoft.Win32;

namespace AHIConfigurator.Services
{
    public class ConnectedDevice
    {
        public int Id { get; set; }
        public bool IsMouse { get; set; }
        public int Vid { get; set; }
        public int Pid { get; set; }
        public string Handle { get; set; }
        public int Instance { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    ///     Owns the AHI <see cref="Manager" /> used by the configurator (device listing and capture)
    /// </summary>
    public class DeviceService : IDisposable
    {
        public Manager Manager { get; }

        /// <summary>
        ///     Why AHI could not be loaded, or null if it loaded fine
        /// </summary>
        public string LoadError { get; }

        public DeviceService()
        {
            try
            {
                Manager = new Manager();
                // Touch the device context, so a missing interception.dll is reported here rather than later
                Manager.GetDeviceList();
            }
            catch (Exception ex)
            {
                Manager = null;
                LoadError = (ex.InnerException ?? ex).Message;
            }
        }

        /// <summary>
        ///     Interception copies keyboard.sys / mouse.sys and registers them as the "keyboard" and "mouse" services.
        ///     Same check as the installer (Installer\AutoHotInterception.iss).
        /// </summary>
        public static bool IsDriverInstalled()
        {
            var drivers = System.IO.Path.Combine(Environment.SystemDirectory, "drivers");
            if (!System.IO.File.Exists(System.IO.Path.Combine(drivers, "keyboard.sys")) ||
                !System.IO.File.Exists(System.IO.Path.Combine(drivers, "mouse.sys"))) return false;
            using (var services = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services"))
            {
                if (services == null) return false;
                using (var kb = services.OpenSubKey("keyboard"))
                using (var mouse = services.OpenSubKey("mouse"))
                {
                    return kb != null && mouse != null;
                }
            }
        }

        public List<ConnectedDevice> GetDevices()
        {
            var result = new List<ConnectedDevice>();
            if (Manager == null) return result;

            var names = GetFriendlyNames();
            var handleCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in Manager.GetDeviceList())
            {
                // Instance numbering matches HelperFunctions.GetDeviceIdFromHandle: per device type, in ID order
                var countKey = (d.IsMouse ? "M|" : "K|") + d.Handle;
                handleCount.TryGetValue(countKey, out var count);
                handleCount[countKey] = ++count;

                result.Add(new ConnectedDevice
                {
                    Id = d.Id,
                    IsMouse = d.IsMouse,
                    Vid = d.Vid,
                    Pid = d.Pid,
                    Handle = d.Handle,
                    Instance = count,
                    Name = PickName(names, d.Handle, count) ?? DefaultName(d.IsMouse, d.Vid, d.Pid)
                });
            }
            return result;
        }

        public static string DefaultName(bool isMouse, int vid, int pid)
        {
            var kind = isMouse ? "Mouse" : "Keyboard";
            return vid == 0 && pid == 0 ? kind : $"{kind} {vid:X4}:{pid:X4}";
        }

        private static string PickName(Dictionary<string, List<string>> names, string handle, int instance)
        {
            if (!names.TryGetValue(handle, out var list) || list.Count == 0) return null;
            return list[Math.Min(instance, list.Count) - 1];
        }

        /// <summary>
        ///     Maps hardware IDs to the names Device Manager shows. Best effort - returns an empty map on failure.
        /// </summary>
        private static Dictionary<string, List<string>> GetFriendlyNames()
        {
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                           "SELECT Name, HardwareID FROM Win32_PnPEntity WHERE PNPClass = 'Keyboard' OR PNPClass = 'Mouse'"))
                {
                    foreach (var obj in searcher.Get().Cast<ManagementObject>())
                    {
                        var name = obj["Name"] as string;
                        if (!(obj["HardwareID"] is string[] ids) || string.IsNullOrEmpty(name)) continue;
                        foreach (var id in ids)
                        {
                            if (!map.TryGetValue(id, out var list)) map[id] = list = new List<string>();
                            list.Add(name);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // WMI unavailable - fall back to VID/PID names
            }
            return map;
        }

        public void Dispose()
        {
            Manager?.Dispose();
        }
    }
}
