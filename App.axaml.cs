using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KompasTrochoidCam.ViewModels;
using KompasTrochoidCam.Views;

namespace KompasTrochoidCam;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(), // <-- Ставим нашу модель с ЧПУ
            };

            base.OnFrameworkInitializationCompleted();
        }
    }
}