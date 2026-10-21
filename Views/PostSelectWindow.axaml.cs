using Avalonia.Controls;
using Avalonia.Markup.Xaml; // <-- Добавьте этот using

namespace KompasTrochoidCam.Views
{
    public partial class PostSelectWindow : Window
    {
        public PostSelectWindow()
        {
            // Вместо InitializeComponent выигрываем у багов кэша прямой вызов загрузчика:
            AvaloniaXamlLoader.Load(this);
        }
    }
}
