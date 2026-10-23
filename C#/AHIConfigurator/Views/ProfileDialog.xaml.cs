using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using AHIConfigurator.Models;

namespace AHIConfigurator.Views
{
    public partial class ProfileDialog : Window
    {
        public class WindowInfo
        {
            public string Exe { get; set; }
            public string Title { get; set; }
            public string Class { get; set; }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        public ProfileDialog(Profile existing)
        {
            InitializeComponent();
            Title = existing == null ? "Add profile" : "Edit profile";
            MatchTypeCombo.ItemsSource = new[]
            {
                new KeyValuePair<MatchType, string>(MatchType.Exe, "This program is active (eg notepad.exe)"),
                new KeyValuePair<MatchType, string>(MatchType.Title, "The window title contains"),
                new KeyValuePair<MatchType, string>(MatchType.Class, "The window class is")
            };
            MatchTypeCombo.SelectedValue = existing?.MatchType ?? MatchType.Exe;
            NameBox.Text = existing?.Name ?? "";
            MatchValueBox.Text = existing?.MatchValue ?? "";
            WindowList.ItemsSource = GetWindows();
            Loaded += (s, e) => NameBox.Focus();
        }

        public Profile Result { get; private set; }

        private static List<WindowInfo> GetWindows()
        {
            var list = new List<WindowInfo>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.MainWindowHandle == IntPtr.Zero || string.IsNullOrEmpty(p.MainWindowTitle)) continue;
                    var cls = new StringBuilder(256);
                    GetClassName(p.MainWindowHandle, cls, cls.Capacity);
                    list.Add(new WindowInfo { Exe = p.ProcessName + ".exe", Title = p.MainWindowTitle, Class = cls.ToString() });
                }
                catch (InvalidOperationException)
                {
                    // Process exited while enumerating
                }
                finally
                {
                    p.Dispose();
                }
            }
            return list.OrderBy(w => w.Exe, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private void WindowList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(WindowList.SelectedItem is WindowInfo w)) return;
            switch ((MatchType)MatchTypeCombo.SelectedValue)
            {
                case MatchType.Exe: MatchValueBox.Text = w.Exe; break;
                case MatchType.Title: MatchValueBox.Text = w.Title; break;
                case MatchType.Class: MatchValueBox.Text = w.Class; break;
            }
            if (NameBox.Text.Trim().Length == 0) NameBox.Text = System.IO.Path.GetFileNameWithoutExtension(w.Exe);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var name = NameBox.Text.Trim();
            var value = MatchValueBox.Text.Trim();
            if (name.Length == 0 || value.Length == 0)
            {
                MessageBox.Show(this, "Enter a name and a value, or pick an open window.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Result = new Profile { Name = name, MatchType = (MatchType)MatchTypeCombo.SelectedValue, MatchValue = value };
            DialogResult = true;
        }
    }
}
