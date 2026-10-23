using System;
using System.IO;
using System.Linq;
using AHIConfigurator.Models;
using AHIConfigurator.Services;
using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    class ConfigStoreTests
    {
        private string _dir;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "AHI-ConfigStoreTests-" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_dir, "sub", "config.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void MissingFileGivesDefaultConfig()
        {
            var store = new ConfigStore(_path);
            var config = store.Load();
            Assert.That(config.Profiles, Has.Count.EqualTo(1));
            Assert.That(config.Profiles[0].IsDefault, Is.True);
            Assert.That(store.LoadWarning, Is.Null);
        }

        [Test]
        public void RoundTrip()
        {
            var config = AppConfig.CreateDefault();
            config.RunAtLogin = true;
            config.AutoHotkeyPath = @"C:\AHK\AutoHotkey64.exe";
            config.Devices.Add(new DeviceRef { Key = "kb1", Name = "My keyboard", Handle = @"HID\VID_1&PID_2", Vid = 1, Pid = 2, Instance = 2 });
            config.Profiles.Insert(0, new Profile { Name = "Notepad", MatchType = MatchType.Exe, MatchValue = "notepad.exe" });
            config.DefaultProfile.Mappings.Add(new Mapping { DeviceKey = "kb1", Code = 0x11D, Action = ActionType.Run, Value = "calc.exe", Args = @"C:\" });

            var store = new ConfigStore(_path);
            store.Save(config);
            store.Save(config); // second save replaces the existing file
            var loaded = store.Load();

            Assert.That(loaded.RunAtLogin, Is.True);
            Assert.That(loaded.AutoHotkeyPath, Is.EqualTo(config.AutoHotkeyPath));
            Assert.That(loaded.Devices.Single().Instance, Is.EqualTo(2));
            Assert.That(loaded.Profiles.Select(p => p.Name), Is.EqualTo(new[] { "Notepad", Profile.DefaultName }));
            var m = loaded.DefaultProfile.Mappings.Single();
            Assert.That((m.Code, m.Action, m.Value, m.Args), Is.EqualTo((0x11D, ActionType.Run, "calc.exe", @"C:\")));
            Assert.That(File.ReadAllText(_path), Does.Contain("\"Action\": \"Run\""), "enums are stored by name");
            Assert.That(File.Exists(_path + ".tmp"), Is.False);
        }

        [Test]
        public void LoadsHandWrittenConfig()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, @"{
  ""Version"": 1,
  ""Devices"": [ { ""Key"": ""kb1"", ""Name"": ""Test keyboard"", ""IsMouse"": false, ""Handle"": ""HID\\VID_046D&PID_C31C"", ""Vid"": 1133, ""Pid"": 49948, ""Instance"": 1 } ],
  ""Profiles"": [
    { ""Name"": ""Notepad"", ""MatchType"": ""Exe"", ""MatchValue"": ""notepad.exe"", ""Mappings"": [ { ""DeviceKey"": ""kb1"", ""Input"": ""Key"", ""Code"": 59, ""Action"": ""Text"", ""Value"": ""hello"" } ] },
    { ""Name"": ""Default"", ""MatchType"": ""None"", ""Mappings"": [ { ""DeviceKey"": ""kb1"", ""Input"": ""Key"", ""Code"": 58, ""Action"": ""Key"", ""TargetCode"": 1 } ] }
  ],
  ""RunAtLogin"": false
}");
            var store = new ConfigStore(_path);
            var config = store.Load();
            Assert.That(store.LoadWarning, Is.Null);
            Assert.That(config.Devices.Single().Handle, Is.EqualTo(@"HID\VID_046D&PID_C31C"));
            Assert.That(config.Profiles.Select(p => p.Mappings.Count), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(config.DefaultProfile.Mappings[0].TargetCode, Is.EqualTo(1));
        }

        [Test]
        public void CorruptFileIsBackedUpAndDefaultsLoaded()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, "{ this is not json");
            var store = new ConfigStore(_path);

            var config = store.Load();

            Assert.That(config.Profiles.Single().IsDefault, Is.True);
            Assert.That(store.LoadWarning, Does.Contain(".bad-"));
            Assert.That(File.Exists(_path), Is.False);
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(_path), "config.json.bad-*"), Has.Length.EqualTo(1));
        }

        [Test]
        public void NormalizeMergesDuplicateDefaultsAndMovesItLast()
        {
            var config = new AppConfig();
            config.Profiles.Add(new Profile { Name = "A", Mappings = { new Mapping { Code = 1 } } });
            config.Profiles.Add(new Profile { Name = "App", MatchType = MatchType.Exe, MatchValue = "x.exe" });
            config.Profiles.Add(new Profile { Name = "B", Mappings = { new Mapping { Code = 2 } } });

            config.Normalize();

            Assert.That(config.Profiles.Select(p => p.Name), Is.EqualTo(new[] { "App", "A" }));
            Assert.That(config.DefaultProfile.Mappings.Select(m => m.Code), Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void NewDeviceKeySkipsUsedKeys()
        {
            var config = AppConfig.CreateDefault();
            config.Devices.Add(new DeviceRef { Key = "kb1" });
            config.Devices.Add(new DeviceRef { Key = "kb2" });
            Assert.That(config.NewDeviceKey(false), Is.EqualTo("kb3"));
            Assert.That(config.NewDeviceKey(true), Is.EqualTo("m1"));
        }
    }
}
