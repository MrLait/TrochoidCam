using Avalonia;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.IO;

namespace KompasTrochoidCam.ViewModels
{
    public partial class GCodeWindowViewModel : ViewModelBase
    {
        // Хранилище для полного сырого текста (нужно для буфера обмена)
        private string _rawGCodeText = string.Empty;

        [ObservableProperty]
        private string _boundsX = "X0.000 : X0.000";

        [ObservableProperty]
        private string _boundsY = "Y0.000 : Y0.000";

        [ObservableProperty]
        private string _boundsZ = "Z0.000 : Z0.000";

        [ObservableProperty]
        private string _estimatedTime = "00:00:00";

        [ObservableProperty]
        private string _simulationProgress = "Кадр: 0 / 0";



        public string GCodeText
        {
            get => _rawGCodeText;
            set
            {
                if (SetProperty(ref _rawGCodeText, value))
                {
                    // Как только прилетает новый G-код, автоматически нарезаем его на строки для ListBox
                    if (!string.IsNullOrEmpty(value))
                    {
                        GCodeLines = value.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    }
                }
            }
        }

        // Коллекция строк, которую прочитает ListBox в интерфейсе
        [ObservableProperty]
        private string[] _gCodeLines = Array.Empty<string>();

        // Индекс текущей выделенной строки (синхронизирован с TwoWay привязкой в XAML)
        [ObservableProperty]
        private int _selectedLineIndex = -1;

        [RelayCommand]
        private async System.Threading.Tasks.Task Copy()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var activeWindow = desktop.MainWindow;
                if (activeWindow?.Clipboard != null)
                {
                    await activeWindow.Clipboard.SetTextAsync(GCodeText);
                }
            }
        }
    }
}
