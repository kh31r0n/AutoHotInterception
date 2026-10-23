using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AHIConfigurator.Models;
using AHIConfigurator.ViewModels;
using Microsoft.Win32;

namespace AHIConfigurator.Views
{
    public partial class MainWindow : Window, IDialogs
    {
        private readonly DispatcherTimer _statusTimer;

        public MainWindow()
        {
            InitializeComponent();
            // Picks up the script being stopped from its tray menu, or crashing
            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _statusTimer.Tick += (s, e) => ViewModel?.RefreshStatus();
            _statusTimer.Start();
        }

        private MainViewModel ViewModel => DataContext as MainViewModel;

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            if (ViewModel?.StartupWarning != null) ShowError(ViewModel.StartupWarning);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (ViewModel != null && !ViewModel.ConfirmClose()) e.Cancel = true;
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _statusTimer.Stop();
            ViewModel?.Dispose();
            base.OnClosed(e);
        }

        private void Mapping_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel?.SelectedMapping != null) ViewModel.EditMappingCommand.Execute(null);
        }

        private void MappingGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && ViewModel.DeleteMappingCommand.CanExecute(null))
            {
                ViewModel.DeleteMappingCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && ViewModel.EditMappingCommand.CanExecute(null))
            {
                ViewModel.EditMappingCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void Device_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel.RenameDeviceCommand.CanExecute(null)) ViewModel.RenameDeviceCommand.Execute(null);
        }

        private void Profile_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel.EditProfileCommand.CanExecute(null)) ViewModel.EditProfileCommand.Execute(null);
        }

        // ------------------------------------------------------------------ IDialogs

        public MappingDialogResult EditMapping(Mapping existing, DeviceItem device, IList<DeviceItem> devices, Action beforeCapture)
        {
            var dialog = new MappingDialog(existing, device, devices, ViewModel.Capture, beforeCapture) { Owner = this };
            return dialog.ShowDialog() == true ? dialog.Result : null;
        }

        public Profile EditProfile(Profile existing)
        {
            var dialog = new ProfileDialog(existing) { Owner = this };
            return dialog.ShowDialog() == true ? dialog.Result : null;
        }

        public string Prompt(string title, string message, string initial)
        {
            var dialog = new PromptDialog(title, message, initial) { Owner = this };
            return dialog.ShowDialog() == true ? dialog.Value : null;
        }

        public bool? AskYesNoCancel(string message)
        {
            switch (MessageBox.Show(this, message, Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question))
            {
                case MessageBoxResult.Yes: return true;
                case MessageBoxResult.No: return false;
                default: return null;
            }
        }

        public bool Confirm(string message)
        {
            return MessageBox.Show(this, message, Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;
        }

        public void ShowError(string message)
        {
            MessageBox.Show(this, message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public void ShowInfo(string message)
        {
            MessageBox.Show(this, message, Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public string AskSavePath(string defaultName)
        {
            var dialog = new SaveFileDialog
            {
                FileName = defaultName,
                Filter = "AutoHotkey script (*.ahk)|*.ahk",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            return dialog.ShowDialog(this) == true ? dialog.FileName : null;
        }

        public string AskAutoHotkeyPath()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Locate AutoHotkey v2 (AutoHotkey64.exe)",
                Filter = "AutoHotkey (AutoHotkey*.exe)|AutoHotkey*.exe|Programs (*.exe)|*.exe"
            };
            return dialog.ShowDialog(this) == true ? dialog.FileName : null;
        }
    }
}
