using System.Windows;

namespace AHIConfigurator.Views
{
    public partial class PromptDialog : Window
    {
        public PromptDialog(string title, string message, string initial)
        {
            InitializeComponent();
            Title = title;
            MessageText.Text = message;
            ValueBox.Text = initial ?? "";
            Loaded += (s, e) =>
            {
                ValueBox.Focus();
                ValueBox.SelectAll();
            };
        }

        public string Value => ValueBox.Text;

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
