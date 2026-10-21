using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KompasTrochoidCam.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.IO;

namespace KompasTrochoidCam.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly KompasService _kompasService;

        public MainWindowViewModel()
        {
            _kompasService = new KompasService();
            LoadPostprocessors(); // Загружаем станки при старте программы
        }

        private void LoadPostprocessors()
        {
            try
            {
                AvailablePosts.Clear();

                // Читаем папку POSTPROCESSORS прямо там, где запустилась программа
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string postPath = Path.Combine(baseDir, "POSTPROCESSORS");

                if (Directory.Exists(postPath))
                {
                    var files = Directory.GetFiles(postPath, "*.*");
                    foreach (var file in files)
                    {
                        AvailablePosts.Add(Path.GetFileName(file));
                    }
                }
                else
                {
                    // Если папки вдруг нет, создадим её пустую, чтобы не было сбоев
                    Directory.CreateDirectory(postPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка чтения папки POSTPROCESSORS: {ex.Message}");
            }
        }



        // Автоматически генерируемые свойства через генераторы кода (CommunityToolkit)
        [ObservableProperty]
        private string _connectionStatus = "Нет подключения";

        [ObservableProperty]
        private IBrush _statusColor = Brushes.Red;

        [ObservableProperty]
        private string _toolDiameter = "10.0";

        [ObservableProperty]
        private string _trochoidStep = "1.0";

        [ObservableProperty]
        private string _spindleSpeed = "6000";

        [ObservableProperty]
        private string _feedRate = "400";

        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<string> _availablePosts = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPostSelected))] // Автоматически обновляет доступность кнопки
        private string? _selectedPost;

        // Свойство проверки: активна ли фиолетовая кнопка
        public bool IsPostSelected => !string.IsNullOrEmpty(SelectedPost);


        // Команда для кнопки "Подключиться к КОМПАС"
        [RelayCommand]
        private void Connect()
        {
            // Вызываем обновленный метод, передавая строку для ошибки
            bool isConnected = _kompasService.Connect(out string debugMessage);

            if (isConnected)
            {
                string docName = _kompasService.GetActiveDocName();
                ConnectionStatus = $"Подключено ({docName})";
                StatusColor = Brushes.Green;
            }
            else
            {
                // Выводим точную системную причину, почему Windows заблокировала связь
                ConnectionStatus = debugMessage;
                StatusColor = Brushes.Red;
            }
        }

        // Команда для кнопки "Считать контур и создать .ngc"
        [RelayCommand]
        private void GenerateGCode()
        {
            // ВРЕМЕННО КОММЕНТИРУЕМ ДЛЯ ТЕСТА БЕЗ КЛЮЧА КОМПАСА:
            // if (!ConnectionStatus.StartsWith("Подключено")) return;

            // Считываем контур выделенной грани и ее высоту Z
            List<TrochoidEngine.Point2D> polygonPoints = _kompasService.GetSelectedFaceContours(out double faceZ);

            // АВТОНОМНЫЙ ОБХОД: Если КОМПАС заблокирован и точек нет, создаем тестовую плиту 90х124 мм
            if (polygonPoints.Count < 2)
            {
                faceZ = 0.0; // Высота плоскости Z0
                polygonPoints = new List<TrochoidEngine.Point2D>
                {
                    new TrochoidEngine.Point2D(-45.723, -63.49), // Ваша реальная точка старта из G-кода
                    new TrochoidEngine.Point2D(44.223, -63.49),  // Идем вправо по X
                    new TrochoidEngine.Point2D(44.223, 61.0),    // Идем вверх по Y
                    new TrochoidEngine.Point2D(-45.723, 61.0),   // Идем влево по X
                    new TrochoidEngine.Point2D(-45.723, -63.49)  // Замыкаем контур в стартовой точке
                };
            }


            try
            {
                // Парсим параметры из текстовых полей интерфейса Avalonia
                double toolDia = double.Parse(ToolDiameter, System.Globalization.CultureInfo.InvariantCulture);
                double stepAe = double.Parse(TrochoidStep, System.Globalization.CultureInfo.InvariantCulture);
                double feed = double.Parse(FeedRate, System.Globalization.CultureInfo.InvariantCulture);
                double speed = double.Parse(SpindleSpeed, System.Globalization.CultureInfo.InvariantCulture);

                StringBuilder fullProgram = new StringBuilder();

                // Шапка управляющей программы
                fullProgram.AppendLine("%");
                fullProgram.AppendLine("( Mini-iMachining Face Path for LinuxCNC )");
                fullProgram.AppendLine("G90 G40 G17");
                fullProgram.AppendLine($"S{speed:F0} F{feed:F0} M3");
                fullProgram.AppendLine("G54");
                fullProgram.AppendLine("G0 Z50.0"); // Безопасная высота

                // Генерируем трохоидальные дорожки поочередно для каждой стороны выделенной плоскости
                for (int i = 0; i < polygonPoints.Count - 1; i++)
                {
                    TrochoidEngine.Point2D pointA = polygonPoints[i];
                    TrochoidEngine.Point2D pointB = polygonPoints[i + 1];

                    // Генерируем петли вдоль текущей линии ребра гранта
                    // Высоту Z опускаем на уровень считанной плоскости faceZ
                    string trackGCode = TrochoidEngine.GenerateLinearTrochoid(pointA, pointB, toolDia, stepAe, feed, speed);

                    // Очищаем шапку и хвост (%) из подпрограммных треков, чтобы склеить их корректно
                    string cleanTrack = CleanGCodeTrack(trackGCode, faceZ);
                    fullProgram.Append(cleanTrack);
                }

                // Хвост управляющей программы
                fullProgram.AppendLine("G0 Z50.0");
                fullProgram.AppendLine("M9");
                fullProgram.AppendLine("M5");
                fullProgram.AppendLine("M30");
                fullProgram.AppendLine("%");

                // 1. Получаем путь к папке, где запускается программа (bin/Debug/net10.0-windows)
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;

                // 2. Поднимаемся на 4 уровня вверх, чтобы выйти в корень проекта KompasTrochoidCam
                string projectRootDir = Path.GetFullPath(Path.Combine(exeDir, @"..\..\..\..\"));

                // 3. Формируем путь к целевой папке "g-cod" в корне проекта
                string gcodeFolderPath = Path.Combine(projectRootDir, "g-cod");

                // 4. Если папки "g-cod" еще не существует, автоматически создаем её
                if (!Directory.Exists(gcodeFolderPath))
                {
                    Directory.CreateDirectory(gcodeFolderPath);
                }

                // 5. Записываем файл .ngc внутрь этой папки
                string filePath = Path.Combine(gcodeFolderPath, "trochoid_output.ngc");
                File.WriteAllText(filePath, fullProgram.ToString());

                ConnectionStatus = "УП создана в папке g-cod проекта!";

                // --- НАШ КОД ВЫЗОВА ВТОРОЙ ФОРМЫ ---
                // Создаем контекст данных для второго окна и передаем туда текст программы
                var gcodeVM = new GCodeWindowViewModel
                {
                    GCodeText = fullProgram.ToString()
                };

                // Создаем само окно
                var gcodeWindow = new Views.GCodeWindow
                {
                    DataContext = gcodeVM
                };

                // ЗАМЕНИТЕ НА (Вызов нашего нового 3D-парсера):
                gcodeWindow.ParseAndBuildTrajectory(fullProgram.ToString());

                // Показываем окно пользователю
                gcodeWindow.Show();

                ConnectionStatus = "УП создана и открыта в визуализаторе!";


            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Ошибка расчета: {ex.Message}";
            }
        }

        /// <summary>
        /// Вспомогательный метод для очистки кусков кода при склейке траекторий
        /// </summary>
        private string CleanGCodeTrack(string rawGCode, double targetZ)
        {
            string[] lines = rawGCode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            StringBuilder cleanBlock = new StringBuilder();

            foreach (var line in lines)
            {
                // Пропускаем шапки, хвосты и пустые врезания базового генератора
                if (line.StartsWith("%") || line.StartsWith("G90") || line.StartsWith("S") ||
                    line.StartsWith("G54") || line.StartsWith("M") || line.StartsWith("( iMachining") ||
                    line.Contains("Z10.0") || line.Contains("Z50.0"))
                    continue;

                // Подставляем реальную высоту считанной плоскости Z вместо дефолтной Z0.0
                if (line.Contains("Z0.0"))
                {
                    cleanBlock.AppendLine($"G1 Z{targetZ:F3} F50");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    cleanBlock.AppendLine(line);
                }
            }
            return cleanBlock.ToString();
        }

        // Команда для кнопки "Открыть сторонний G-код"
        [RelayCommand]
        private async System.Threading.Tasks.Task OpenGCodeFile()
        {
            if (string.IsNullOrEmpty(SelectedPost)) return;

            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(desktop.MainWindow);
                if (topLevel == null) return;

                // Сразу открываем выбор управляющей программы под выбранный станок
                var options = new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = $"Импорт файла через постпроцессор: {SelectedPost}",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new Avalonia.Platform.Storage.FilePickerFileType("Файлы ЧПУ")
                        {
                            Patterns = new[] { "*.nc", "*.ngc", "*.tap", "*.gcode" }
                        }
                    }
                };

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);

                if (files != null && files.Count > 0)
                {
                    var selectedFile = files[0];

                    using var stream = await selectedFile.OpenReadAsync();
                    using var reader = new System.IO.StreamReader(stream);
                    string gcodeContent = await reader.ReadToEndAsync();

                    var gcodeVM = new GCodeWindowViewModel { GCodeText = gcodeContent };
                    var gcodeWindow = new Views.GCodeWindow { DataContext = gcodeVM };

                    gcodeWindow.ParseAndBuildTrajectory(gcodeContent);
                    gcodeWindow.Show();

                    ConnectionStatus = $"Код {selectedFile.Name} импортирован через {SelectedPost}";
                }
            }
        }




    }
}
