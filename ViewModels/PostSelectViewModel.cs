using System;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KompasTrochoidCam.ViewModels
{
    public partial class PostSelectViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<string> _availablePosts = new();

        [ObservableProperty]
        private string? _selectedPost;

        public bool IsConfirmed { get; private set; } = false;

        private readonly Action _closeAction;

        public PostSelectViewModel(Action closeAction)
        {
            _closeAction = closeAction;
            LoadPostprocessors();
        }

        private void LoadPostprocessors()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string rootDir = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\"));
                string postPath = Path.Combine(rootDir, "POSTPROCESSORS");

                if (Directory.Exists(postPath))
                {
                    var files = Directory.GetFiles(postPath, "*.*");
                    foreach (var file in files)
                    {
                        AvailablePosts.Add(Path.GetFileName(file));
                    }
                }

                if (AvailablePosts.Count > 0)
                {
                    SelectedPost = AvailablePosts[0]; // Выбираем первый по умолчанию
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки постов: {ex.Message}");
            }
        }

        [RelayCommand]
        private void Confirm()
        {
            if (!string.IsNullOrEmpty(SelectedPost))
            {
                IsConfirmed = true;
                _closeAction(); // Закрываем окно
            }
        }
    }
}
