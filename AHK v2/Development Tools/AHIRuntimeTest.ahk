/*
Tests for Lib\AHIRuntime.ahk. Does not need the Interception driver - AHI is replaced with a stub.
Run from a console: AutoHotkey64.exe /ErrorStdOut "AHIRuntimeTest.ahk" | more
Exit code = number of failed tests.
*/
#Requires AutoHotkey v2.0
#include %A_LineFile%\..\..\Lib\AHIRuntime.ahk

global Failures := 0
global ActiveWindow := ""
; Report runtime errors on stdout instead of showing a dialog
OnError((e, *) => (FileAppend("ERROR: " e.Message " (" e.What ", line " e.Line ")`n", "*"), ExitApp(99)))

; Records every call the runtime makes to AHI
class StubManager {
	__New() {
		this.Calls := []
		this.Subs := Map()
	}
	GetDeviceIdFromHandle(isMouse, handle, instance) {
		if (handle = "HID\KB1")
			return 1
		if (handle = "HID\MOUSE1")
			return 11
		return 0
	}
	GetDeviceId(isMouse, vid, pid, instance) {
		return (vid = 0x1234) ? 2 : 0
	}
	SubscribeKey(id, code, block, cb, concurrent) {
		this.Subs["k" id "|" code] := cb
	}
	SubscribeMouseButton(id, btn, block, cb, concurrent) {
		this.Subs["m" id "|" btn] := cb
	}
	SendKeyEvent(id, code, state) {
		this.Calls.Push("key " id " " Format("{:X}", code) " " state)
	}
	SendMouseButtonEvent(id, btn, state) {
		this.Calls.Push("mouse " id " " btn " " state)
	}
	SetState(state) {
		this.Calls.Push("state " state)
	}
}

class TestRuntime extends AHIRuntime {
	SendAhk(keys) {
		this.AHI.Calls.Push("send " keys)
	}
	SendText(text) {
		this.AHI.Calls.Push("text " text)
	}
	RunTarget(target, workingDir) {
		this.AHI.Calls.Push("run " target)
	}
	IsWindowActive(winTitle) {
		return winTitle = ActiveWindow
	}
	Notify(msg, icon := "") {
		this.AHI.Calls.Push("notify " msg)
	}
	BuildTrayMenu() {
	}
}

Config := Map(
	"devices", [
		Map("key", "kb1", "name", "Keyboard", "isMouse", 0, "handle", "HID\KB1", "vid", 0, "pid", 0, "instance", 1),
		Map("key", "kb2", "name", "Old keyboard", "isMouse", 0, "handle", "", "vid", 0x1234, "pid", 0x1, "instance", 1),
		Map("key", "m1", "name", "Mouse", "isMouse", 1, "handle", "HID\MOUSE1", "vid", 0, "pid", 0, "instance", 1),
		Map("key", "gone", "name", "Unplugged pad", "isMouse", 0, "handle", "HID\GONE", "vid", 0, "pid", 0, "instance", 1)
	],
	"profiles", [
		Map("name", "Notepad", "match", "ahk_exe notepad.exe", "mappings", [
			Map("dev", "kb1", "input", "key", "code", 0x3B, "action", "text", "value", "hello", "args", ""),
			Map("dev", "kb1", "input", "key", "code", 0x3A, "action", "passthru", "value", "", "args", "")
		]),
		Map("name", "Default", "match", "", "mappings", [
			Map("dev", "kb1", "input", "key", "code", 0x3A, "action", "key", "value", 0x01, "args", ""),
			Map("dev", "kb1", "input", "key", "code", 0x52, "action", "block", "value", "", "args", ""),
			Map("dev", "kb1", "input", "key", "code", 0x3C, "action", "run", "value", "calc.exe", "args", ""),
			Map("dev", "kb1", "input", "key", "code", 0x3D, "action", "mouse", "value", 0, "args", ""),
			Map("dev", "kb2", "input", "key", "code", 0x1E, "action", "key", "value", 0x30, "args", ""),
			Map("dev", "m1", "input", "mouse", "code", 3, "action", "send", "value", "^c", "args", ""),
			Map("dev", "m1", "input", "mouse", "code", 4, "action", "key", "value", 0x11D, "args", ""),
			Map("dev", "m1", "input", "mouse", "code", 2, "action", "mouse", "value", 1, "args", ""),
			Map("dev", "m1", "input", "mouse", "code", 5, "action", "key", "value", 0x148, "args", ""),
			Map("dev", "gone", "input", "key", "code", 0x10, "action", "block", "value", "", "args", "")
		])
	]
)

stub := StubManager()
rt := TestRuntime(Config, stub)
rt.Run()

Check("missing device is reported", stub.Calls, ["notify Not connected: Unplugged pad. Their mappings are inactive until you plug them in and click Apply in the Configurator (or remove them there with Forget)."])
CheckEqual("device ids resolved by handle and VID/PID", rt.DeviceIds["kb1"] "," rt.DeviceIds["kb2"] "," rt.DeviceIds["m1"], "1,2,11")
CheckEqual("unconnected device is not subscribed", stub.Subs.Has("k0|16"), false)
CheckEqual("vertical wheel subscribed once on button 5", stub.Subs.Has("m11|5"), true)

; Default profile: CapsLock -> Esc on the same keyboard
Press(stub, "k1|58", [1, 0], ["key 1 1 1", "key 1 1 0"], "key -> key remap")
; Unmapped input that shares no subscription is never seen; mapped input in no active profile passes through
Press(stub, "k1|82", [1, 0], [], "block swallows press and release")
Press(stub, "k1|60", [1, 1, 0], ["run calc.exe"], "run fires once despite auto-repeat")
Press(stub, "k1|61", [1, 1, 0], ["send {Blind}{LButton down}", "send {Blind}{LButton up}"], "key -> mouse button ignores auto-repeat")
Press(stub, "k2|30", [1, 0], ["key 2 30 1", "key 2 30 0"], "second keyboard remap uses its own id")
Press(stub, "m11|3", [1, 0], ["send ^c"], "mouse button -> send string on press only")
Press(stub, "m11|4", [1, 0], ["send {Blind}{sc11D down}", "send {Blind}{sc11D up}"], "mouse button -> extended key")
Press(stub, "m11|2", [1, 0], ["mouse 11 1 1", "mouse 11 1 0"], "mouse button -> mouse button")
Press(stub, "m11|5", [1], ["send {Blind}{sc148}"], "wheel up -> key tap")
Press(stub, "m11|5", [-1], ["mouse 11 5 -1"], "unmapped wheel down passes through")

; Per-app profile
ActiveWindow := "ahk_exe notepad.exe"
Press(stub, "k1|59", [1, 1, 0], ["text hello"], "app profile: text once")
Press(stub, "k1|58", [1, 0], ["key 1 3A 1", "key 1 3A 0"], "app profile: passthru overrides default")
ActiveWindow := ""
Press(stub, "k1|59", [1, 0], ["key 1 3B 1", "key 1 3B 0"], "app profile inactive: input passes through")

; Release goes to the mapping that handled the press, even if the active window changed in between
stub.Calls := []
stub.Subs["k1|58"](1)
ActiveWindow := "ahk_exe notepad.exe"
stub.Subs["k1|58"](0)
ActiveWindow := ""
Check("release uses the mapping of the press", stub.Calls, ["key 1 1 1", "key 1 1 0"])

FileAppend(Failures ? Failures " test(s) failed`n" : "All tests passed`n", "*")
ExitApp(Failures)

; ------------------------------------------------------------
Press(stub, sub, states, expected, name) {
	stub.Calls := []
	for state in states
		stub.Subs[sub](state)
	Check(name, stub.Calls, expected)
}

Check(name, actual, expected) {
	CheckEqual(name, Join(actual), Join(expected))
}

CheckEqual(name, actual, expected) {
	global Failures
	if (actual == expected) {
		FileAppend("PASS " name "`n", "*")
		return
	}
	Failures++
	FileAppend("FAIL " name "`n  expected: " expected "`n  actual:   " actual "`n", "*")
}

Join(arr) {
	s := ""
	for v in arr
		s .= (A_Index > 1 ? " | " : "") v
	return s
}
