using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AHIConfigurator.Models;
using AutoHotInterception;

namespace AHIConfigurator.Services
{
    public class CaptureResult
    {
        public int DeviceId { get; set; }
        public bool IsMouse { get; set; }
        public int Code { get; set; }
    }

    /// <summary>A key plus the modifiers held with it, eg Ctrl+Shift+Esc</summary>
    public class ComboResult
    {
        public List<int> Modifiers { get; set; } = new List<int>();
        public int Code { get; set; }
    }

    /// <summary>
    ///     Turns key events into a <see cref="ComboResult" />: the first non-modifier key pressed, with the modifiers held
    ///     at that moment. If only modifiers are pressed, the result is decided when all of them are released:
    ///     the last one pressed is the key, the others its modifiers (Win -> {LWin}, Ctrl then Win -> ^{LWin}).
    ///     Thread-safe.
    /// </summary>
    public class ComboRecorder
    {
        private readonly object _gate = new object();
        private readonly List<int> _held = new List<int>(); // press order, so modifiers keep the order they were pressed in
        private readonly List<int> _chord = new List<int>(); // modifiers pressed since all keys were last up
        private readonly TaskCompletionSource<bool> _allReleased = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private ComboResult _result;

        public TaskCompletionSource<ComboResult> Result { get; } = new TaskCompletionSource<ComboResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Completes once a result exists and every key pressed during the capture is up again</summary>
        public Task AllReleased => _allReleased.Task;

        public void OnKey(int code, int state)
        {
            lock (_gate)
            {
                if (state == 1)
                {
                    if (!_held.Contains(code)) _held.Add(code);
                    if (_result != null) return;
                    if (KeyNames.IsModifier(code))
                    {
                        if (!_chord.Contains(code)) _chord.Add(code);
                    }
                    else
                    {
                        SetResult(_held.Where(k => k != code && KeyNames.IsModifier(k)).ToList(), code);
                    }
                    return;
                }

                // Ignore releases of keys that were already down before the capture started
                if (!_held.Remove(code) || _held.Count > 0) return;
                if (_result == null && _chord.Count > 0)
                {
                    SetResult(_chord.Take(_chord.Count - 1).ToList(), _chord.Last());
                }
                _chord.Clear();
                if (_result != null) _allReleased.TrySetResult(true);
            }
        }

        private void SetResult(List<int> modifiers, int code)
        {
            _result = new ComboResult { Modifiers = modifiers, Code = code };
            Result.TrySetResult(_result);
        }
    }

    /// <summary>
    ///     "Press a key now": subscribes to every given device and returns the first press.
    ///     Input is blocked while capturing, so eg CapsLock does not toggle and a click does not land on a button.
    /// </summary>
    public class CaptureService
    {
        private static readonly TimeSpan ReleaseTimeout = TimeSpan.FromMilliseconds(1500);
        private readonly Manager _manager;

        public CaptureService(Manager manager)
        {
            _manager = manager;
        }

        /// <summary>
        ///     Must be called on the UI thread (as must <see cref="CaptureComboAsync" />): subscriptions are removed
        ///     after the await resumes there, never from inside an AHI callback (that would dispose the worker thread
        ///     running the callback).
        /// </summary>
        public Task<CaptureResult> CaptureAsync(bool mouse, IReadOnlyCollection<int> deviceIds, CancellationToken ct)
        {
            var pressed = new TaskCompletionSource<CaptureResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var released = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            CaptureResult first = null;
            var gate = new object();

            void OnInput(int id, int code, int state, bool hasRelease)
            {
                lock (gate)
                {
                    if (first == null)
                    {
                        if (state == 0) return; // Release of a key that was down before capture started
                        first = new CaptureResult { DeviceId = id, IsMouse = mouse, Code = code };
                        pressed.TrySetResult(first);
                        if (!hasRelease) released.TrySetResult(true);
                    }
                    else if (state == 0 && first.DeviceId == id && first.Code == code)
                    {
                        released.TrySetResult(true);
                    }
                }
            }

            return RunCaptureAsync(mouse, deviceIds, OnInput, pressed, released.Task, ct);
        }

        /// <summary>
        ///     Captures a key together with the modifiers held while pressing it (from any of the given keyboards).
        ///     Tapping a modifier on its own captures that modifier, eg LWin.
        /// </summary>
        public Task<ComboResult> CaptureComboAsync(IReadOnlyCollection<int> keyboardIds, CancellationToken ct)
        {
            var recorder = new ComboRecorder();
            return RunCaptureAsync(false, keyboardIds, (id, code, state, hasRelease) => recorder.OnKey(code, state),
                recorder.Result, recorder.AllReleased, ct);
        }

        /// <summary>
        ///     Subscribes (blocking) to all given devices, waits for <paramref name="result" />, then keeps blocking until
        ///     <paramref name="released" /> (or a timeout), so the app under the cursor does not get lone key-ups / clicks.
        /// </summary>
        private async Task<T> RunCaptureAsync<T>(bool mouse, IReadOnlyCollection<int> deviceIds,
            Action<int, int, int, bool> onInput, TaskCompletionSource<T> result, Task released, CancellationToken ct)
        {
            var subscribed = new List<int>();
            try
            {
                foreach (var id in deviceIds)
                {
                    var deviceId = id;
                    if (mouse)
                    {
                        _manager.SubscribeMouseButtons(deviceId, true, new Action<int, int>((btn, state) =>
                        {
                            var isWheel = btn >= 5;
                            onInput(deviceId, MouseCodes.FromAhi(btn, state), isWheel ? 1 : state, !isWheel);
                        }));
                    }
                    else
                    {
                        _manager.SubscribeKeyboard(deviceId, true, new Action<int, int>((code, state) => onInput(deviceId, code, state, true)));
                    }
                    subscribed.Add(deviceId);
                }

                using (ct.Register(() => result.TrySetCanceled()))
                {
                    var value = await result.Task;
                    await Task.WhenAny(released, Task.Delay(ReleaseTimeout));
                    return value;
                }
            }
            finally
            {
                foreach (var id in subscribed)
                {
                    if (mouse) _manager.UnsubscribeMouseButtons(id);
                    else _manager.UnsubscribeKeyboard(id);
                }
            }
        }
    }
}
