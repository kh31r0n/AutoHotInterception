using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using AHIConfigurator.Models;
using AHIConfigurator.Services;

namespace AHIConfigurator.ViewModels
{
    public class MappingDialogResult
    {
        public Mapping Mapping { get; set; }
        public DeviceItem Device { get; set; }
    }

    /// <summary>Everything the view model needs from the UI (implemented by MainWindow)</summary>
    public interface IDialogs
    {
        MappingDialogResult EditMapping(Mapping existing, DeviceItem device, IList<DeviceItem> devices, Action beforeCapture);
        Profile EditProfile(Profile existing);
        string Prompt(string title, string message, string initial);
        bool? AskYesNoCancel(string message);
        bool Confirm(string message);
        void ShowError(string message);
        void ShowInfo(string message);
        string AskSavePath(string defaultName);
        string AskAutoHotkeyPath();
    }

    public class MainViewModel : ObservableObject, IDisposable
    {
        private readonly ConfigStore _store;
        private readonly DeviceService _deviceService;
        private readonly IDialogs _dialogs;
        private Profile _selectedProfile;
        private MappingItem _selectedMapping;
        private DeviceItem _selectedDevice;
        private bool _isDirty;
        private string _scriptStatus;
        private string _banner;

        public MainViewModel(ConfigStore store, DeviceService deviceService, IDialogs dialogs)
        {
            _store = store;
            _deviceService = deviceService;
            _dialogs = dialogs;
            Capture = deviceService.Manager == null ? null : new CaptureService(deviceService.Manager);

            Config = store.Load();
            Profiles = new ObservableCollection<Profile>(Config.Profiles);
            Devices = new ObservableCollection<DeviceItem>();
            Mappings = new ObservableCollection<MappingItem>();

            AddMappingCommand = new RelayCommand(AddMapping, () => Devices.Any());
            EditMappingCommand = new RelayCommand(EditMapping, () => SelectedMapping != null);
            DeleteMappingCommand = new RelayCommand(DeleteMapping, () => SelectedMapping != null);
            AddProfileCommand = new RelayCommand(AddProfile);
            EditProfileCommand = new RelayCommand(EditProfile, () => SelectedProfile != null && !SelectedProfile.IsDefault);
            DeleteProfileCommand = new RelayCommand(DeleteProfile, () => SelectedProfile != null && !SelectedProfile.IsDefault);
            MoveProfileUpCommand = new RelayCommand(() => MoveProfile(-1), () => CanMoveProfile(-1));
            MoveProfileDownCommand = new RelayCommand(() => MoveProfile(1), () => CanMoveProfile(1));
            RenameDeviceCommand = new RelayCommand(RenameDevice, () => SelectedDevice != null);
            ForgetDeviceCommand = new RelayCommand(ForgetDevice, () => SelectedDevice != null && !SelectedDevice.IsConnected);
            RefreshDevicesCommand = new RelayCommand(RefreshDevices);
            ApplyCommand = new RelayCommand(() => Apply());
            StopCommand = new RelayCommand(Stop);
            ExportCommand = new RelayCommand(Export);
            ChooseAutoHotkeyCommand = new RelayCommand(ChooseAutoHotkey);

            RefreshDevices();
            SelectedProfile = Profiles.Last();
            RefreshStatus();
        }

        /// <summary>Shown by the window once it is visible (a message box shown earlier has no owner and can get lost)</summary>
        public string StartupWarning => _store.LoadWarning;

        public AppConfig Config { get; }
        public CaptureService Capture { get; }
        public ObservableCollection<Profile> Profiles { get; }
        public ObservableCollection<DeviceItem> Devices { get; }
        public ObservableCollection<MappingItem> Mappings { get; }

        public RelayCommand AddMappingCommand { get; }
        public RelayCommand EditMappingCommand { get; }
        public RelayCommand DeleteMappingCommand { get; }
        public RelayCommand AddProfileCommand { get; }
        public RelayCommand EditProfileCommand { get; }
        public RelayCommand DeleteProfileCommand { get; }
        public RelayCommand MoveProfileUpCommand { get; }
        public RelayCommand MoveProfileDownCommand { get; }
        public RelayCommand RenameDeviceCommand { get; }
        public RelayCommand ForgetDeviceCommand { get; }
        public RelayCommand RefreshDevicesCommand { get; }
        public RelayCommand ApplyCommand { get; }
        public RelayCommand StopCommand { get; }
        public RelayCommand ExportCommand { get; }
        public RelayCommand ChooseAutoHotkeyCommand { get; }

        public Profile SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (Set(ref _selectedProfile, value)) RefreshMappings();
            }
        }

        public MappingItem SelectedMapping
        {
            get => _selectedMapping;
            set => Set(ref _selectedMapping, value);
        }

        public DeviceItem SelectedDevice
        {
            get => _selectedDevice;
            set => Set(ref _selectedDevice, value);
        }

        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (Set(ref _isDirty, value)) OnPropertyChanged(nameof(Title));
            }
        }

        public string Title => "AutoHotInterception Configurator" + (IsDirty ? " *" : "");

        public bool RunAtLogin
        {
            get => Config.RunAtLogin;
            set
            {
                if (Config.RunAtLogin == value) return;
                Config.RunAtLogin = value;
                OnPropertyChanged();
                IsDirty = true;
            }
        }

        public string AutoHotkeyPath => ScriptRunner.FindAutoHotkey(Config.AutoHotkeyPath);

        public string DriverStatus =>
            _deviceService.LoadError != null ? "AHI library failed to load"
            : DeviceService.IsDriverInstalled() ? "Interception driver: installed"
            : "Interception driver: not installed";

        public string AhkStatus => AutoHotkeyPath != null ? "AutoHotkey v2: " + AutoHotkeyPath : "AutoHotkey v2: not found";

        public string ScriptStatus
        {
            get => _scriptStatus;
            private set => Set(ref _scriptStatus, value);
        }

        /// <summary>Setup problems shown above the editor; null when everything is fine</summary>
        public string Banner
        {
            get => _banner;
            private set => Set(ref _banner, value);
        }

        // ------------------------------------------------------------------ Devices

        public void RefreshDevices()
        {
            var selectedKey = SelectedDevice?.Ref?.Key;
            Devices.Clear();
            var matched = new HashSet<DeviceRef>();
            foreach (var c in _deviceService.GetDevices())
            {
                var r = Config.Devices.FirstOrDefault(d => !matched.Contains(d) && d.Matches(c.IsMouse, c.Handle, c.Vid, c.Pid, c.Instance));
                if (r != null) matched.Add(r);
                Devices.Add(new DeviceItem(c, r));
            }
            foreach (var r in Config.Devices.Where(d => !matched.Contains(d)))
            {
                Devices.Add(new DeviceItem(null, r));
            }
            SelectedDevice = Devices.FirstOrDefault(d => d.Ref != null && d.Ref.Key == selectedKey) ?? Devices.FirstOrDefault();
            RefreshMappings();
            RefreshStatus();
        }

        /// <summary>Gives a device a DeviceRef (stored in the config) the first time it gets a mapping</summary>
        private DeviceRef EnsureDeviceRef(DeviceItem item)
        {
            if (item.Ref != null) return item.Ref;
            var c = item.Connected;
            var r = new DeviceRef
            {
                Key = Config.NewDeviceKey(c.IsMouse),
                Name = c.Name,
                IsMouse = c.IsMouse,
                Handle = c.Handle ?? "",
                Vid = c.Vid,
                Pid = c.Pid,
                Instance = c.Instance
            };
            Config.Devices.Add(r);
            item.Ref = r;
            return r;
        }

        private void RenameDevice()
        {
            var item = SelectedDevice;
            var name = _dialogs.Prompt("Rename device", "Name for this device:", item.Name);
            if (string.IsNullOrWhiteSpace(name)) return;
            EnsureDeviceRef(item).Name = name.Trim();
            item.Renamed();
            RefreshMappings();
            IsDirty = true;
        }

        private void ForgetDevice()
        {
            var item = SelectedDevice;
            var count = Config.Profiles.Sum(p => p.Mappings.Count(m => m.DeviceKey == item.Ref.Key));
            if (!_dialogs.Confirm($"Forget \"{item.Name}\" and delete its {count} mapping(s)?")) return;
            foreach (var p in Config.Profiles) p.Mappings.RemoveAll(m => m.DeviceKey == item.Ref.Key);
            Config.Devices.Remove(item.Ref);
            IsDirty = true;
            RefreshDevices();
        }

        // ------------------------------------------------------------------ Mappings

        private void RefreshMappings()
        {
            var selected = SelectedMapping?.Mapping;
            Mappings.Clear();
            if (SelectedProfile == null) return;
            foreach (var m in SelectedProfile.Mappings)
            {
                var device = Config.FindDevice(m.DeviceKey);
                Mappings.Add(new MappingItem(m, device?.Name ?? "(unknown device)"));
            }
            SelectedMapping = Mappings.FirstOrDefault(i => i.Mapping == selected);
        }

        private void AddMapping()
        {
            var device = SelectedDevice ?? Devices.FirstOrDefault();
            var result = ShowMappingDialog(null, device);
            if (result == null) return;
            SelectedProfile.Mappings.Add(result);
            AfterMappingChange(result);
        }

        public void EditMapping()
        {
            var item = SelectedMapping;
            if (item == null) return;
            var device = Devices.FirstOrDefault(d => d.Ref != null && d.Ref.Key == item.Mapping.DeviceKey);
            var result = ShowMappingDialog(item.Mapping, device);
            if (result == null) return;
            var index = SelectedProfile.Mappings.IndexOf(item.Mapping);
            SelectedProfile.Mappings[index] = result;
            AfterMappingChange(result);
        }

        private void AfterMappingChange(Mapping changed)
        {
            // One mapping per input per profile: the newest wins
            SelectedProfile.Mappings.RemoveAll(m => m != changed && m.DeviceKey == changed.DeviceKey && m.Input == changed.Input && m.Code == changed.Code);
            IsDirty = true;
            RefreshMappings();
            SelectedMapping = Mappings.FirstOrDefault(i => i.Mapping == changed);
        }

        private Mapping ShowMappingDialog(Mapping existing, DeviceItem device)
        {
            // The running script blocks the keys it maps, so it is stopped while capturing and restarted afterwards
            var stopped = false;
            void BeforeCapture()
            {
                if (stopped || !ScriptRunner.IsRunning(AppPaths.ScriptFile)) return;
                ScriptRunner.Stop(AppPaths.ScriptFile);
                stopped = true;
            }

            try
            {
                var result = _dialogs.EditMapping(existing, device, Devices.ToList(), BeforeCapture);
                if (result == null) return null;
                result.Mapping.DeviceKey = EnsureDeviceRef(result.Device).Key;
                return result.Mapping;
            }
            finally
            {
                if (stopped && AutoHotkeyPath != null && File.Exists(AppPaths.ScriptFile))
                    ScriptRunner.Start(AutoHotkeyPath, AppPaths.ScriptFile);
                RefreshStatus();
            }
        }

        private void DeleteMapping()
        {
            SelectedProfile.Mappings.Remove(SelectedMapping.Mapping);
            IsDirty = true;
            RefreshMappings();
        }

        // ------------------------------------------------------------------ Profiles

        private void AddProfile()
        {
            var profile = _dialogs.EditProfile(null);
            if (profile == null) return;
            // New app profiles go just above the default profile
            var index = Config.Profiles.Count - 1;
            Config.Profiles.Insert(index, profile);
            Profiles.Insert(index, profile);
            SelectedProfile = profile;
            IsDirty = true;
        }

        private void EditProfile()
        {
            var edited = _dialogs.EditProfile(SelectedProfile);
            if (edited == null) return;
            var index = Profiles.IndexOf(SelectedProfile);
            edited.Mappings = SelectedProfile.Mappings;
            Config.Profiles[index] = edited;
            Profiles[index] = edited;
            SelectedProfile = edited;
            IsDirty = true;
        }

        private void DeleteProfile()
        {
            var profile = SelectedProfile;
            if (!_dialogs.Confirm($"Delete profile \"{profile.Name}\" and its {profile.Mappings.Count} mapping(s)?")) return;
            Config.Profiles.Remove(profile);
            Profiles.Remove(profile);
            SelectedProfile = Profiles.Last();
            IsDirty = true;
        }

        private bool CanMoveProfile(int delta)
        {
            if (SelectedProfile == null || SelectedProfile.IsDefault) return false;
            var target = Profiles.IndexOf(SelectedProfile) + delta;
            // The default profile always stays last
            return target >= 0 && target < Profiles.Count - 1;
        }

        private void MoveProfile(int delta)
        {
            var profile = SelectedProfile;
            var from = Profiles.IndexOf(profile);
            Profiles.Move(from, from + delta);
            Config.Profiles.RemoveAt(from);
            Config.Profiles.Insert(from + delta, profile);
            SelectedProfile = profile;
            IsDirty = true;
        }

        // ------------------------------------------------------------------ Apply / run

        /// <summary>Saves, regenerates the script and (re)starts it. Returns false if something went wrong.</summary>
        public bool Apply()
        {
            try
            {
                _store.Save(Config);
                IsDirty = false;

                var runtime = AppPaths.FindRuntime();
                if (runtime == null)
                {
                    _dialogs.ShowError("Settings saved, but AHIRuntime.ahk was not found next to the Configurator.\nPlease reinstall AutoHotInterception.");
                    return false;
                }
                var problem = CheckRuntimeFiles(runtime);
                if (problem != null)
                {
                    _dialogs.ShowError("Settings saved, but the mappings cannot run:\n" + problem);
                    return false;
                }
                var ahk = AutoHotkeyPath;
                if (ahk == null)
                {
                    _dialogs.ShowError("Settings saved, but AutoHotkey v2 was not found.\nInstall it from autohotkey.com, or use \"AutoHotkey path...\" to point to AutoHotkey64.exe.");
                    return false;
                }

                if (!Config.Profiles.Any(p => p.Mappings.Any()))
                {
                    ScriptRunner.Stop(AppPaths.ScriptFile);
                    ScriptRunner.SetRunAtLogin(false, ahk, AppPaths.ScriptFile);
                    return true;
                }

                Directory.CreateDirectory(AppPaths.DataDir);
                var script = ScriptGenerator.Generate(Config, runtime, AppPaths.ExeFile, DateTime.Now);
                File.WriteAllText(AppPaths.ScriptFile, script, new UTF8Encoding(true));

                var error = ScriptRunner.Validate(ahk, AppPaths.ScriptFile);
                if (error != null)
                {
                    _dialogs.ShowError("The generated script has an error, so it was not started:\n\n" + error);
                    return false;
                }
                ScriptRunner.Start(ahk, AppPaths.ScriptFile);
                ScriptRunner.SetRunAtLogin(Config.RunAtLogin, ahk, AppPaths.ScriptFile);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.ComponentModel.Win32Exception)
            {
                _dialogs.ShowError("Could not apply the mappings: " + ex.Message);
                return false;
            }
            finally
            {
                RefreshStatus();
            }
        }

        /// <summary>AutoHotInterception.ahk needs these DLLs next to it (see AHK v2\Lib\AutoHotInterception.ahk)</summary>
        private static string CheckRuntimeFiles(string runtime)
        {
            var lib = Path.GetDirectoryName(runtime);
            var missing = new[] { "AutoHotInterception.dll", @"x86\interception.dll", @"x64\interception.dll" }
                .Where(f => !File.Exists(Path.Combine(lib, f))).ToList();
            return missing.Count == 0 ? null : "Missing in " + lib + ":\n" + string.Join("\n", missing);
        }

        private void Stop()
        {
            ScriptRunner.Stop(AppPaths.ScriptFile);
            RefreshStatus();
        }

        private void Export()
        {
            var runtime = AppPaths.FindRuntime() ?? AppPaths.RuntimeRelativePath;
            var path = _dialogs.AskSavePath("My AHI mappings.ahk");
            if (path == null) return;
            File.WriteAllText(path, ScriptGenerator.Generate(Config, runtime, AppPaths.ExeFile, DateTime.Now), new UTF8Encoding(true));
            _dialogs.ShowInfo("Exported to " + path + ".\nThis copy is yours to edit; it is not changed by Apply.");
        }

        private void ChooseAutoHotkey()
        {
            var path = _dialogs.AskAutoHotkeyPath();
            if (path == null) return;
            Config.AutoHotkeyPath = path;
            IsDirty = true;
            RefreshStatus();
        }

        public void RefreshStatus()
        {
            ScriptStatus = ScriptRunner.IsRunning(AppPaths.ScriptFile) ? "Mappings: running" : "Mappings: stopped";
            OnPropertyChanged(nameof(DriverStatus));
            OnPropertyChanged(nameof(AhkStatus));

            var problems = new List<string>();
            if (_deviceService.LoadError != null)
                problems.Add("The AutoHotInterception library could not be loaded: " + _deviceService.LoadError);
            else if (!DeviceService.IsDriverInstalled())
                problems.Add("The Interception driver is not installed. Run the AutoHotInterception setup again and select the driver component, then restart Windows.");
            else if (!Devices.Any(d => d.IsConnected))
                problems.Add("No devices were found. If you just installed the Interception driver, restart Windows.");
            else
            {
                // The mapping script only finds devices when it starts, so these mappings stay off until the next Apply
                var offline = Devices
                    .Where(d => !d.IsConnected && Config.Profiles.Any(p => p.Mappings.Any(m => m.DeviceKey == d.Ref.Key)))
                    .Select(d => "\"" + d.Name + "\"").ToList();
                if (offline.Count > 0)
                    problems.Add("Not connected: " + string.Join(", ", offline) + ". Their mappings are inactive. " +
                                 "Plug the device in and click Apply again, or select it in Devices and click Forget.");
            }
            if (AutoHotkeyPath == null)
                problems.Add("AutoHotkey v2 was not found. Install it from autohotkey.com, or use \"AutoHotkey path...\".");
            Banner = problems.Count == 0 ? null : string.Join("\n", problems);
        }

        /// <summary>Called when the window is closing. Returns false to cancel closing.</summary>
        public bool ConfirmClose()
        {
            if (!IsDirty) return true;
            var answer = _dialogs.AskYesNoCancel("You have unapplied changes. Apply them before closing?");
            if (answer == null) return false;
            return answer == false || Apply();
        }

        public void Dispose()
        {
            _deviceService.Dispose();
        }
    }
}
