using System.Windows;
using System.Windows.Threading;
using AHIConfigurator.Services;
using AHIConfigurator.ViewModels;
using AHIConfigurator.Views;

namespace AHIConfigurator
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += OnUnhandledException;

            var window = new MainWindow();
            window.DataContext = new MainViewModel(new ConfigStore(AppPaths.ConfigFile), new DeviceService(), window);
            MainWindow = window;
            window.Show();
        }

        private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.ToString(), "AutoHotInterception Configurator - unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
