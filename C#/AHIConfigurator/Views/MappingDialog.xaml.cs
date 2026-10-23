using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using AHIConfigurator.Models;
using AHIConfigurator.Services;
using AHIConfigurator.ViewModels;
using Microsoft.Win32;

namespace AHIConfigurator.Views
{
    public partial class MappingDialog : Window
    {
        private static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(15);

        private readonly IList<DeviceItem> _devices;
        private readonly CaptureService _capture;
        private readonly Action _beforeCapture;
        private readonly List<KeyValuePair<int, string>> _keys = KeyNames.AllKeys();
        private readonly List<KeyValuePair<int, string>> _mouseCodes =
            MouseCodes.Names.Select((name, code) => new KeyValuePair<int, string>(code, name)).ToList();
        private CancellationTokenSource _captureCts;

        public MappingDialog(Mapping existing, DeviceItem device, IList<DeviceItem> devices, CaptureService capture, Action beforeCapture)
        {
            InitializeComponent();
            _devices = devices;
            _capture = capture;
            _beforeCapture = beforeCapture;
            Title = existing == null ? "Add mapping" : "Edit mapping";

            DeviceCombo.ItemsSource = devices;
            TargetKeyCombo.ItemsSource = _keys;
            TargetMouseCombo.ItemsSource = _mouseCodes;
            ActionCombo.ItemsSource = new[]
            {
                new KeyValuePair<ActionType, string>(ActionType.Key, "Press another key"),
                new KeyValuePair<ActionType, string>(ActionType.Mouse, "Press a mouse button / scroll"),
                new KeyValuePair<ActionType, string>(ActionType.Block, "Do nothing (block)"),
                new KeyValuePair<ActionType, string>(ActionType.Text, "Type text"),
                new KeyValuePair<ActionType, string>(ActionType.Send, "Send a shortcut / key sequence"),
                new KeyValuePair<ActionType, string>(ActionType.Run, "Open a program, file or website"),
                new KeyValuePair<ActionType, string>(ActionType.PassThrough, "Keep the original function")
            };

            if (capture == null)
            {
                CaptureKeyButton.IsEnabled = CaptureMouseButton.IsEnabled = CaptureTargetButton.IsEnabled = CaptureSendButton.IsEnabled = false;
                CaptureStatus.Text = "Capturing is unavailable because the AutoHotInterception library could not be loaded.";
            }

            DeviceCombo.SelectedItem = device ?? devices.FirstOrDefault();
            if (existing != null)
            {
                TriggerCombo.SelectedValue = EnsureItem(TriggerCombo, existing.Code);
                ActionCombo.SelectedValue = existing.Action;
                switch (existing.Action)
                {
                    case ActionType.Key: TargetKeyCombo.SelectedValue = EnsureItem(TargetKeyCombo, existing.TargetCode); break;
                    case ActionType.Mouse: TargetMouseCombo.SelectedValue = existing.TargetCode; break;
                    case ActionType.Text: TextValue.Text = existing.Value; break;
                    case ActionType.Send: SendValue.Text = existing.Value; break;
                    case ActionType.Run:
                        RunValue.Text = existing.Value;
                        RunArgs.Text = existing.Args;
                        break;
                }
            }
            else
            {
                ActionCombo.SelectedValue = ActionType.Key;
            }
        }

        public MappingDialogResult Result { get; private set; }

        private DeviceItem SelectedDevice => DeviceCombo.SelectedItem as DeviceItem;

        private void DeviceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var previous = TriggerCombo.SelectedValue;
            TriggerCombo.ItemsSource = SelectedDevice?.IsMouse == true ? _mouseCodes : new List<KeyValuePair<int, string>>(_keys);
            TriggerCombo.SelectedValue = previous;
        }

        private void ActionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var action = (ActionType)(ActionCombo.SelectedValue ?? ActionType.Key);
            KeyPanel.Visibility = Show(action == ActionType.Key);
            MousePanel.Visibility = Show(action == ActionType.Mouse);
            TextPanel.Visibility = Show(action == ActionType.Text);
            SendPanel.Visibility = Show(action == ActionType.Send);
            RunPanel.Visibility = Show(action == ActionType.Run);
            BlockPanel.Visibility = Show(action == ActionType.Block);
            PassThroughPanel.Visibility = Show(action == ActionType.PassThrough);
        }

        private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Adds a scan code the layout has no name for, so it can still be selected</summary>
        private static int EnsureItem(ComboBox combo, int code)
        {
            if (combo.ItemsSource is List<KeyValuePair<int, string>> items && items.All(i => i.Key != code))
            {
                items.Add(new KeyValuePair<int, string>(code, KeyNames.GetName(code)));
                combo.Items.Refresh();
            }
            return code;
        }

        // ------------------------------------------------------------------ Capture

        private async void CaptureKey_Click(object sender, RoutedEventArgs e)
        {
            var result = await CaptureAsync(false, "Press the key you want to change, on the keyboard you want to change it on...");
            if (result == null) return;
            DeviceCombo.SelectedItem = _devices.First(d => d.IsConnected && d.Connected.Id == result.DeviceId);
            TriggerCombo.SelectedValue = EnsureItem(TriggerCombo, result.Code);
            CaptureStatus.Text = $"Captured {KeyNames.GetName(result.Code)} on {SelectedDevice.Name}.";
        }

        private async void CaptureMouse_Click(object sender, RoutedEventArgs e)
        {
            var result = await CaptureAsync(true, "Click or scroll with the mouse you want to change (Esc to cancel)...");
            if (result == null) return;
            DeviceCombo.SelectedItem = _devices.First(d => d.IsConnected && d.Connected.Id == result.DeviceId);
            TriggerCombo.SelectedValue = result.Code;
            CaptureStatus.Text = $"Captured {MouseCodes.GetName(result.Code)} on {SelectedDevice.Name}.";
        }

        private async void CaptureTarget_Click(object sender, RoutedEventArgs e)
        {
            var result = await CaptureAsync(false, "Press the key that should be sent instead (any keyboard)...");
            if (result == null) return;
            TargetKeyCombo.SelectedValue = EnsureItem(TargetKeyCombo, result.Code);
            CaptureStatus.Text = $"Will send {KeyNames.GetName(result.Code)}.";
        }

        private async void CaptureSend_Click(object sender, RoutedEventArgs e)
        {
            var combo = await RunCaptureAsync(false, "Hold Ctrl / Alt / Shift / Win if needed and press the key to send (any keyboard)...",
                (ids, ct) => _capture.CaptureComboAsync(ids, ct));
            if (combo == null) return;
            SendValue.Text = KeyNames.ToAhkSend(combo);
            CaptureStatus.Text = $"Will send {KeyNames.DescribeCombo(combo)}.";
        }

        private Task<CaptureResult> CaptureAsync(bool mouse, string prompt)
        {
            return RunCaptureAsync(mouse, prompt, (ids, ct) => _capture.CaptureAsync(mouse, ids, ct));
        }

        private async Task<T> RunCaptureAsync<T>(bool mouse, string prompt, Func<List<int>, CancellationToken, Task<T>> capture) where T : class
        {
            var ids = _devices.Where(d => d.IsConnected && d.IsMouse == mouse).Select(d => d.Connected.Id).ToList();
            if (ids.Count == 0)
            {
                CaptureStatus.Text = mouse ? "No mouse is connected." : "No keyboard is connected.";
                return null;
            }

            _beforeCapture();
            SetCapturing(true, prompt);
            _captureCts = new CancellationTokenSource(CaptureTimeout);
            try
            {
                return await capture(ids, _captureCts.Token);
            }
            catch (OperationCanceledException)
            {
                CaptureStatus.Text = "Capture cancelled.";
                return null;
            }
            finally
            {
                _captureCts.Dispose();
                _captureCts = null;
                SetCapturing(false, null);
            }
        }

        private void SetCapturing(bool capturing, string prompt)
        {
            if (prompt != null) CaptureStatus.Text = prompt;
            CaptureStatus.FontWeight = capturing ? FontWeights.SemiBold : FontWeights.Normal;
            CaptureKeyButton.Visibility = CaptureMouseButton.Visibility = Show(!capturing);
            CancelCaptureButton.Visibility = Show(capturing);
            CaptureTargetButton.IsEnabled = CaptureSendButton.IsEnabled = OkButton.IsEnabled = DeviceCombo.IsEnabled = !capturing;
        }

        private void CancelCapture_Click(object sender, RoutedEventArgs e)
        {
            _captureCts?.Cancel();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Keyboards are blocked during a key capture, so this only fires while capturing a mouse
            if (e.Key == Key.Escape && _captureCts != null)
            {
                _captureCts.Cancel();
                e.Handled = true;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _captureCts?.Cancel();
            base.OnClosing(e);
        }

        // ------------------------------------------------------------------ OK

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var device = SelectedDevice;
            if (device == null)
            {
                Warn("Choose a device.");
                return;
            }
            if (TriggerCombo.SelectedValue == null)
            {
                Warn("Choose the key or button to change, or use \"Press a key...\".");
                return;
            }

            var mapping = new Mapping
            {
                Input = device.IsMouse ? InputKind.Mouse : InputKind.Key,
                Code = (int)TriggerCombo.SelectedValue,
                Action = (ActionType)ActionCombo.SelectedValue
            };
            switch (mapping.Action)
            {
                case ActionType.Key:
                    if (TargetKeyCombo.SelectedValue == null) { Warn("Choose the key to send."); return; }
                    mapping.TargetCode = (int)TargetKeyCombo.SelectedValue;
                    break;
                case ActionType.Mouse:
                    if (TargetMouseCombo.SelectedValue == null) { Warn("Choose the mouse button to send."); return; }
                    mapping.TargetCode = (int)TargetMouseCombo.SelectedValue;
                    break;
                case ActionType.Text:
                    if (TextValue.Text.Length == 0) { Warn("Enter the text to type."); return; }
                    mapping.Value = TextValue.Text;
                    break;
                case ActionType.Send:
                    if (SendValue.Text.Trim().Length == 0) { Warn("Enter the keys to send, eg ^c"); return; }
                    mapping.Value = SendValue.Text.Trim();
                    break;
                case ActionType.Run:
                    if (RunValue.Text.Trim().Length == 0) { Warn("Enter what to open."); return; }
                    mapping.Value = RunValue.Text.Trim();
                    mapping.Args = RunArgs.Text.Trim();
                    break;
            }

            if (mapping.Input == InputKind.Mouse && mapping.Code == MouseCodes.LButton && mapping.Action != ActionType.PassThrough &&
                MessageBox.Show(this, "You are changing the LEFT button of this mouse.\nMake sure you can still click with another mouse, a touchpad or the keyboard.\n\nContinue?",
                    Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            {
                return;
            }

            Result = new MappingDialogResult { Mapping = mapping, Device = device };
            DialogResult = true;
        }

        private void Warn(string message)
        {
            MessageBox.Show(this, message, Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BrowseRun_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "Programs (*.exe)|*.exe|All files (*.*)|*.*" };
            if (dialog.ShowDialog(this) == true) RunValue.Text = "\"" + dialog.FileName + "\"";
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}
