using Avalonia.Controls;
using Avalonia.Interactivity;

namespace KompasTrochoidCam.Views
{
    /// <summary>Окно с подробным отчётом о попытке подключения к КОМПАС-3D.</summary>
    public partial class ConnectDiagnosticsWindow : Window
    {
        public ConnectDiagnosticsWindow()
        {
            InitializeComponent();
        }

        public ConnectDiagnosticsWindow(string report) : this()
        {
            this.FindControl<TextBox>("ReportBox")!.Text = report;
        }

        private async void CopyButton_Click(object? sender, RoutedEventArgs e)
        {
            if (Clipboard == null) return;
            await Clipboard.SetTextAsync(this.FindControl<TextBox>("ReportBox")?.Text ?? string.Empty);
        }

        private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();
    }
}
