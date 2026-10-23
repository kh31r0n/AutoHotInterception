using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using AHIConfigurator.Services;
using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    class KeyNamesTests
    {
        private const int LCtrl = 0x1D, RCtrl = 0x11D, LShift = 0x2A, LAlt = 0x38, LWin = 0x15B;
        private const int Esc = 0x01, C = 0x2E, One = 0x02, OpenBracket = 0x1A;

        private static ComboResult Record(params (int code, int state)[] events)
        {
            var recorder = new ComboRecorder();
            foreach (var (code, state) in events) recorder.OnKey(code, state);
            return recorder.Result.Task.IsCompleted ? recorder.Result.Task.Result : null;
        }

        [Test]
        public void RecordsModifiersHeldWithKey()
        {
            var combo = Record((LCtrl, 1), (LShift, 1), (Esc, 1));
            Assert.That(KeyNames.ToAhkSend(combo), Is.EqualTo("^+{Esc}"));
            Assert.That(KeyNames.DescribeCombo(combo), Does.StartWith("Ctrl + Shift + "));
        }

        [Test]
        public void ModifiersReleasedBeforeTheKeyAreNotIncluded()
        {
            var combo = Record((LCtrl, 1), (LAlt, 1), (LCtrl, 0), (C, 1));
            Assert.That(KeyNames.ToAhkSend(combo), Is.EqualTo("!c"));
        }

        [Test]
        public void ModifiersOnlyAreDecidedWhenAllAreReleased()
        {
            Assert.That(KeyNames.ToAhkSend(Record((LWin, 1), (LWin, 0))), Is.EqualTo("{LWin}"));
            Assert.That(Record((LCtrl, 1), (LWin, 1), (LWin, 0)), Is.Null, "Ctrl is still down");
            Assert.That(KeyNames.ToAhkSend(Record((LCtrl, 1), (LWin, 1), (LWin, 0), (LCtrl, 0))), Is.EqualTo("^{LWin}"));
        }

        [Test]
        public void RightShiftUsesAhkNumbering()
        {
            Assert.That(KeyNames.ToAhkSend(Record((0x136, 1), (One, 1))), Is.EqualTo("+1"));
        }

        [Test]
        public void IgnoresReleasesOfKeysHeldBeforeCapture()
        {
            Assert.That(Record((LCtrl, 0), (Esc, 0)), Is.Null);
            Assert.That(KeyNames.ToAhkSend(Record((LShift, 0), (Esc, 1))), Is.EqualTo("{Esc}"));
        }

        [Test]
        public void AllReleasedWaitsForEveryKey()
        {
            var recorder = new ComboRecorder();
            recorder.OnKey(LCtrl, 1);
            recorder.OnKey(C, 1);
            recorder.OnKey(C, 0);
            Assert.That(recorder.AllReleased.IsCompleted, Is.False, "Ctrl is still down");
            recorder.OnKey(LCtrl, 0);
            Assert.That(recorder.AllReleased.IsCompleted, Is.True);
        }

        [Test]
        public void LeftAndRightModifiersGiveOnePrefix()
        {
            Assert.That(KeyNames.ToAhkSend(Record((LCtrl, 1), (RCtrl, 1), (C, 1))), Is.EqualTo("^c"));
        }

        [Test]
        public void CharacterKeysUseTheLayoutOrScanCode()
        {
            Assert.That(KeyNames.ToAhkSendKey(One), Is.EqualTo("1"));
            // Punctuation differs between layouts and may clash with Send syntax, so it is sent by scan code
            Assert.That(KeyNames.ToAhkSendKey(OpenBracket), Is.EqualTo("{sc01A}"));
        }

        /// <summary>Every name in the table must mean the same scan code to AutoHotkey</summary>
        [Test, Category("AutoHotkey")]
        public void AhkNamesMatchAutoHotkeyScanCodes()
        {
            var ahk = ScriptRunner.FindAutoHotkey(null);
            if (ahk == null) Assert.Ignore("AutoHotkey v2 is not installed");
            var dir = Path.Combine(Path.GetTempPath(), "AHI-KeyNamesTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var script = new StringBuilder("out := \"\"\n");
                foreach (var name in KeyNames.AhkNames.Values)
                    script.Append($"out .= \"{name}=\" GetKeySC(\"{name}\") \"`n\"\n");
                script.Append("FileAppend(out, A_ScriptDir \"\\out.txt\")\nExitApp\n");
                var path = Path.Combine(dir, "names.ahk");
                File.WriteAllText(path, script.ToString(), new UTF8Encoding(true));
                using (var p = Process.Start(new ProcessStartInfo(ahk, $"/ErrorStdOut \"{path}\"") { UseShellExecute = false, CreateNoWindow = true }))
                {
                    Assert.That(p.WaitForExit(15000), Is.True);
                }

                var actual = File.ReadAllLines(Path.Combine(dir, "out.txt"))
                    .Select(l => l.Split('=')).ToDictionary(p => p[0], p => int.Parse(p[1]));
                var mismatches = KeyNames.AhkNames
                    .Where(kv => actual[kv.Value] != kv.Key)
                    .Select(kv => $"{kv.Value}: table 0x{kv.Key:X} vs AHK 0x{actual[kv.Value]:X}").ToList();
                Assert.That(mismatches, Is.Empty);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
