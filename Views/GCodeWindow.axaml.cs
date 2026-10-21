using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using KompasTrochoidCam.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace KompasTrochoidCam.Views
{
    public partial class GCodeWindow : Window
    {
        private List<Point3D> _toolPathPoints = new List<Point3D>();
        private int _selectedLineIndex = -1;

        private double _rotX = 30 * Math.PI / 180.0;
        private double _rotZ = -45 * Math.PI / 180.0;
        private double _scale = 3.5;

        private double _offsetX = 0.0;
        private double _offsetY = 0.0;

        private bool _isPressed = false;
        private bool _isRightPressed = false;
        private Avalonia.Point _lastMousePos;

        private double minX, maxX, minY, maxY, minZ, maxZ;
        private double _totalTimeSeconds = 0;

        // ДВИЖОК ИНТЕРАКТИВНОГО СИМУЛЯТОРА ЧПУ
        private DispatcherTimer? _playbackTimer;
        private int _currentSimulationIndex = 0; // Текущий индекс фрезы в массиве точек
        private int _speedMultiplier = 1;        // Коэффициент ускорения (1х, 2х и т.д.)
        private double _toolDiameterMm = 10.0;   // Диаметр фрезы по умолчанию

        // НАСТРОЙКИ СИМУЛЯЦИИ УДАЛЕНИЯ МАТЕРИАЛА
        private const int GridSize = 100; // Размерность сетки заготовки (100х100 ячеек для скорости)
        private double[,] _heightGrid = new double[GridSize, GridSize]; // Сетка высот металла
        private bool _isGridInitialized = false;

        // Физические размеры заготовки Д16Т (автономная плита из прошлого шага)
        private double _stockMinX = -55.0;
        private double _stockMaxX = 55.0;
        private double _stockMinY = -70.0;
        private double _stockMaxY = 70.0;
        private double _stockThickness = 20.0; // Толщина плиты 20 мм


        public struct Point3D
        {
            public double X; public double Y; public double Z;
            public int LineIndex;
            public Point3D(double x, double y, double z, int lineIndex)
            {
                X = x; Y = y; Z = z; LineIndex = lineIndex;
            }
        }

        public GCodeWindow()
        {
            InitializeComponent();

            // Настраиваем таймер симуляции движения фрезы
            _playbackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(30) // Базовый тик ~33 кадра/сек
            };
            _playbackTimer.Tick += PlaybackTimer_Tick;

            this.DataContextChanged += (sender, e) =>
            {
                if (DataContext is GCodeWindowViewModel vm)
                {
                    vm.PropertyChanged += (s, ev) =>
                    {
                        if (ev.PropertyName == nameof(GCodeWindowViewModel.SelectedLineIndex))
                        {
                            // Если пользователь вручную щелкнул по списку, синхронизируем симулятор
                            if (_selectedLineIndex != vm.SelectedLineIndex && !_playbackTimer.IsEnabled)
                            {
                                _selectedLineIndex = vm.SelectedLineIndex;
                                SyncSimulationIndexWithLine();
                                ReDraw();
                            }
                        }
                    };
                }
            };
        }

        /// <summary>
        /// Сброс и создание чистой заготовки Д16Т заданной толщины
        /// </summary>
        private void InitializeStockGrid()
        {
            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    _heightGrid[x, y] = _stockThickness; // На старте вся плита целая (высота 20 мм)
                }
            }
            _isGridInitialized = true;
        }


        public void ParseAndBuildTrajectory(string gcode)
        {
            _toolPathPoints.Clear();
            string[] lines = gcode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            double lastX = 0, lastY = 0, lastZ = 50;
            double currentFeed = 400;

            minX = minY = minZ = double.MaxValue;
            maxX = maxY = maxZ = double.MinValue;
            _totalTimeSeconds = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string[] parts = line.Split('(');
                string cleanLine = parts[0].Trim();

                if (string.IsNullOrEmpty(cleanLine)) continue;

                // Извлекаем актуальный диаметр фрезы, если встретится комментарий параметров инструмента
                if (line.Contains("Фрезерование d"))
                {
                    int dIdx = line.IndexOf("d");
                    if (dIdx != -1) double.TryParse(line.Substring(dIdx + 1, 2), CultureInfo.InvariantCulture, out _toolDiameterMm);
                }

                currentFeed = GetCoord(cleanLine, 'F', currentFeed);
                double x = GetCoord(cleanLine, 'X', lastX);
                double y = GetCoord(cleanLine, 'Y', lastY);
                double z = GetCoord(cleanLine, 'Z', lastZ);

                if (cleanLine.Contains("G0") || cleanLine.Contains("G1"))
                {
                    _toolPathPoints.Add(new Point3D(x, y, z, i));
                    UpdateBounds(x, y, z);

                    double dist = Math.Sqrt(Math.Pow(x - lastX, 2) + Math.Pow(y - lastY, 2) + Math.Pow(z - lastZ, 2));
                    if (cleanLine.Contains("G0")) _totalTimeSeconds += dist / 5000.0 * 60.0;
                    else _totalTimeSeconds += (currentFeed > 0) ? (dist / currentFeed * 60.0) : 0;
                }
                else if (cleanLine.Contains("G2"))
                {
                    double iParam = GetCoord(cleanLine, 'I', 0);
                    double jParam = GetCoord(cleanLine, 'J', 0);
                    double centerX = lastX + iParam;
                    double centerY = lastY + jParam;
                    double radius = Math.Sqrt(iParam * iParam + jParam * jParam);
                    double startAngle = Math.Atan2(lastY - centerY, lastX - centerX);

                    double arcLength = 2 * Math.PI * radius;
                    _totalTimeSeconds += (currentFeed > 0) ? (arcLength / currentFeed * 60.0) : 0;

                    for (int step = 0; step <= 12; step++)
                    {
                        double angle = startAngle - (step * (Math.PI * 2 / 12));
                        double arcX = centerX + Math.Cos(angle) * radius;
                        double arcY = centerY + Math.Sin(angle) * radius;
                        _toolPathPoints.Add(new Point3D(arcX, arcY, z, i));
                        UpdateBounds(arcX, arcY, z);
                    }
                }
                lastX = x; lastY = y; lastZ = z;
            }

            if (DataContext is GCodeWindowViewModel vm)
            {
                vm.BoundsX = minX == double.MaxValue ? "0.0" : $"{minX:F3} : {maxX:F3} мм";
                vm.BoundsY = minY == double.MaxValue ? "0.0" : $"{minY:F3} : {maxY:F3} мм";
                vm.BoundsZ = minZ == double.MaxValue ? "0.0" : $"{minZ:F3} : {maxZ:F3} мм";
                TimeSpan t = TimeSpan.FromSeconds(_totalTimeSeconds);
                vm.EstimatedTime = string.Format("{0:D2}:{1:D2}:{2:D2}", t.Hours, t.Minutes, t.Seconds);
            }

            _currentSimulationIndex = 0;
            UpdateProgressText();
            ReDraw();
        }

        private void UpdateBounds(double x, double y, double z)
        {
            if (x < minX) minX = x; if (x > maxX) maxX = x;
            if (y < minY) minY = y; if (y > maxY) maxY = y;
            if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
        }

        private double GetCoord(string line, char axis, double defaultValue)
        {
            int idx = line.IndexOf(axis);
            if (idx == -1) return defaultValue;
            int endIdx = line.IndexOf(' ', idx);
            string valStr = endIdx == -1 ? line.Substring(idx + 1) : line.Substring(idx + 1, endIdx - idx - 1);
            return double.TryParse(valStr, CultureInfo.InvariantCulture, out double res) ? res : defaultValue;
        }

        private void ReDraw()
        {
            var canvas = this.Find<Canvas>("GCodeCanvas3D");
            if (canvas == null || _toolPathPoints.Count < 2) return;
            canvas.Children.Clear();

            double cx = (canvas.Bounds.Width / 2) + _offsetX;
            double cy = (canvas.Bounds.Height / 2) + _offsetY;
            if (canvas.Bounds.Width == 0) cx = 300 + _offsetX;
            if (canvas.Bounds.Height == 0) cy = 300 + _offsetY;


            // --- СИНХРОННЫЙ РЕНДЕР ТРЕХМЕРНОЙ ЗАГОТОВКИ С УДАЛЕНИЕМ МАТЕРИАЛА ---
            // --- СИНХРОННЫЙ КОНТРАСТНЫЙ РЕНДЕР ЗАГОТОВКИ И РЕЗУЛЬТАТА СЪЕМА ---
            if (!_isGridInitialized) InitializeStockGrid();

            // Создаем два отдельных контекста: для целого металла и для зоны съема
            var rawStockGeometry = new StreamGeometry(); // Светлая заготовка
            var cutStockGeometry = new StreamGeometry(); // Темный прорезанный паз

            using (var rawCtx = rawStockGeometry.Open())
            using (var cutCtx = cutStockGeometry.Open())
            {
                for (int x = 0; x < GridSize; x += 4)
                {
                    bool isRawFirst = true;
                    bool isCutFirst = true;

                    for (int y = 0; y < GridSize; y += 2)
                    {
                        double physicalX = _stockMinX + ((double)x / GridSize) * (_stockMaxX - _stockMinX);
                        double physicalY = _stockMinY + ((double)y / GridSize) * (_stockMaxY - _stockMinY);
                        double physicalZ = _heightGrid[x, y];

                        Point3D pStock = new Point3D(physicalX, physicalY, physicalZ, 0);
                        var pScreen = Project3D(pStock, _rotX, _rotZ, _scale, cx, cy);

                        // ЕСЛИ МЕТАЛЛ СРЕЗАН (высота стала меньше исходных 20 мм)
                        if (physicalZ < _stockThickness - 0.01)
                        {
                            if (isCutFirst) { cutCtx.BeginFigure(pScreen, false); isCutFirst = false; }
                            cutCtx.LineTo(pScreen);

                            // Сбрасываем флаг сырой геометрии, чтобы разорвать светлую линию
                            isRawFirst = true;
                        }
                        else // ЕСЛИ МЕТАЛЛ ЦЕЛЫЙ
                        {
                            if (isRawFirst) { rawCtx.BeginFigure(pScreen, false); isRawFirst = false; }
                            rawCtx.LineTo(pScreen);

                            // Сбрасываем флаг прорезанной геометрии
                            isCutFirst = true;
                        }
                    }
                }
            }

            // 1. Отрисовываем нетронутую заготовку — СВЕТЛЫЙ МАТОВЫЙ АЛЮМИНИЙ
            var rawStockPath = new Path
            {
                Data = rawStockGeometry,
                Stroke = new SolidColorBrush(Colors.LightGray, 0.45), // Хорошо заметные светлые грани
                StrokeThickness = 0.9
            };
            canvas.Children.Add(rawStockPath);

            // 2. Отрисовываем РЕЗУЛЬТАТ СЪЕМА (Паз) — ГЛУБОКИЙ ТЕМНО-ГРАФИТОВЫЙ ЦВЕТ
            var cutStockPath = new Path
            {
                Data = cutStockGeometry,
                Stroke = new SolidColorBrush(Colors.Black, 0.75), // Темная сетка дна и стенок паза
                StrokeThickness = 1.3 // Сделаем линии паза чуть толще для максимального контраста
            };
            canvas.Children.Add(cutStockPath);




            var workGeometry = new StreamGeometry();
            var rapidGeometry = new StreamGeometry();
            var selectGeometry = new StreamGeometry();

            using (var workCtx = workGeometry.Open())
            using (var rapidCtx = rapidGeometry.Open())
            using (var selectCtx = selectGeometry.Open())
            {
                bool isWorkFirst = true;
                bool isRapidFirst = true;
                bool isSelectFirst = true;

                for (int i = 0; i < _toolPathPoints.Count - 1; i++)
                {
                    var p1 = Project3D(_toolPathPoints[i], _rotX, _rotZ, _scale, cx, cy);
                    var p2 = Project3D(_toolPathPoints[i + 1], _rotX, _rotZ, _scale, cx, cy);

                    if (_toolPathPoints[i].LineIndex == _selectedLineIndex)
                    {
                        if (isSelectFirst) { selectCtx.BeginFigure(p1, false); isSelectFirst = false; }
                        selectCtx.LineTo(p2);
                    }
                    else if (_toolPathPoints[i].Z > 0.1)
                    {
                        if (isRapidFirst) { rapidCtx.BeginFigure(p1, false); isRapidFirst = false; }
                        rapidCtx.LineTo(p2);
                    }
                    else
                    {
                        if (isWorkFirst) { workCtx.BeginFigure(p1, false); isWorkFirst = false; }
                        workCtx.LineTo(p2);
                    }
                }
            }

            canvas.Children.Add(new Path { Data = workGeometry, Stroke = Brushes.SpringGreen, StrokeThickness = 1.2 });
            canvas.Children.Add(new Path { Data = rapidGeometry, Stroke = Brushes.Red, StrokeThickness = 0.8 });
            canvas.Children.Add(new Path { Data = selectGeometry, Stroke = Brushes.Yellow, StrokeThickness = 3.5 });

            // --- СВЕРХТОЧНЫЙ РЕНДЕР ФРЕЗЫ В ВИДЕ ОБЪЕМНОГО 3D-ЦИЛИНДРА (КАК В SOLIDCAM) ---
            if (_currentSimulationIndex < _toolPathPoints.Count)
            {
                Point3D toolPos = _toolPathPoints[_currentSimulationIndex];

                // Параметры геометрии реального инструмента
                double r = _toolDiameterMm / 2.0; // Реальный радиус фрезы (5 мм)
                double toolHeight = 30.0;        // Высота рабочей части фрезы (30 мм)

                // Создаем векторные контуры для 3D-цилиндра
                var cylinderGeometry = new StreamGeometry();

                using (var toolCtx = cylinderGeometry.Open())
                {
                    int segments = 16; // Точность окружности цилиндра
                    bool isFirstBottom = true;
                    bool isFirstTop = true;

                    // Массивы для хранения спроецированных точек верхнего и нижнего торца фрезы
                    var bottomPoints = new Avalonia.Point[segments];
                    var topPoints = new Avalonia.Point[segments];

                    // 1. Математический расчет 3D-координат цилиндра фрезы в пространстве ЧПУ
                    for (int s = 0; s < segments; s++)
                    {
                        double angle = s * (Math.PI * 2 / segments);
                        double offsetXComponent = Math.Cos(angle) * r;
                        double offsetYComponent = Math.Sin(angle) * r;

                        // Нижний торец фрезы (касается заготовки Д16Т)
                        Point3D pBottom = new Point3D(toolPos.X + offsetXComponent, toolPos.Y + offsetYComponent, toolPos.Z, 0);
                        bottomPoints[s] = Project3D(pBottom, _rotX, _rotZ, _scale, cx, cy);

                        // Верхний торец фрезы (уходит вверх к патрону шпинделя)
                        Point3D pTop = new Point3D(toolPos.X + offsetXComponent, toolPos.Y + offsetYComponent, toolPos.Z + toolHeight, 0);
                        topPoints[s] = Project3D(pTop, _rotX, _rotZ, _scale, cx, cy);
                    }

                    // 2. Отрисовка нижнего основания (торца) фрезы
                    toolCtx.BeginFigure(bottomPoints[0], true); // Замкнутый контур
                    for (int s = 1; s < segments; s++) toolCtx.LineTo(bottomPoints[s]);

                    // 3. Отрисовка верхнего основания фрезы
                    toolCtx.BeginFigure(topPoints[0], true); // Замкнутый контур
                    for (int s = 1; s < segments; s++) toolCtx.LineTo(topPoints[s]);

                    // 4. Соединяем основания вертикальными ребрами для создания объема (4 главных ребра)
                    for (int s = 0; s < segments; s += segments / 4)
                    {
                        toolCtx.BeginFigure(bottomPoints[s], false);
                        toolCtx.LineTo(topPoints[s]);
                    }
                }

                // Выводим объемное полупрозрачное тело фрезы на холст
                var toolBodyPath = new Path
                {
                    Data = cylinderGeometry,
                    Stroke = Brushes.Yellow,         // Контур инструмента
                    StrokeThickness = 1.0,
                    Fill = new SolidColorBrush(Colors.Lime, 0.25) // Полупрозрачная салатовая заливка тела
                };
                canvas.Children.Add(toolBodyPath);

                // Отрисовываем маленькое перекрестие (ось шпинделя) строго на торце фрезы
                var pCenter = Project3D(toolPos, _rotX, _rotZ, _scale, cx, cy);
                canvas.Children.Add(new Line { StartPoint = new Avalonia.Point(pCenter.X - 5, pCenter.Y), EndPoint = new Avalonia.Point(pCenter.X + 5, pCenter.Y), Stroke = Brushes.Red, StrokeThickness = 1.0 });
                canvas.Children.Add(new Line { StartPoint = new Avalonia.Point(pCenter.X, pCenter.Y - 5), EndPoint = new Avalonia.Point(pCenter.X, pCenter.Y + 5), Stroke = Brushes.Red, StrokeThickness = 1.0 });
            }

            DrawCoordinateSystem(canvas);
        }
        private void DrawCoordinateSystem(Canvas canvas)
        {
            double axisCenterX = 60;
            double axisCenterY = canvas.Bounds.Height > 0 ? canvas.Bounds.Height - 60 : 550;
            double axisLength = 35.0;
            Point3D axisX = new Point3D(axisLength, 0, 0, 0);
            Point3D axisY = new Point3D(0, axisLength, 0, 0);
            Point3D axisZ = new Point3D(0, 0, axisLength, 0);
            Point3D center = new Point3D(0, 0, 0, 0);
            var pCenter = Project3D(center, _rotX, _rotZ, 1.0, axisCenterX, axisCenterY);
            var pX = Project3D(axisX, _rotX, _rotZ, 1.0, axisCenterX, axisCenterY);
            var pY = Project3D(axisY, _rotX, _rotZ, 1.0, axisCenterX, axisCenterY);
            var pZ = Project3D(axisZ, _rotX, _rotZ, 1.0, axisCenterX, axisCenterY);
            DrawArrow(canvas, pCenter, pX, Brushes.Red, "X");
            DrawArrow(canvas, pCenter, pY, Brushes.LimeGreen, "Y");
            DrawArrow(canvas, pCenter, pZ, Brushes.DodgerBlue, "Z");
        }
        private void DrawArrow(Canvas canvas, Avalonia.Point start, Avalonia.Point end, IBrush color, string label)
        {
            canvas.Children.Add(new Line { StartPoint = start, EndPoint = end, Stroke = color, StrokeThickness = 2.0 });
            canvas.Children.Add(new TextBlock { Text = label, Foreground = color, FontSize = 10, FontWeight = FontWeight.Bold, Margin = new Thickness(end.X - 4, end.Y - 6, 0, 0) });
        }
        private Avalonia.Point Project3D(Point3D pt, double rotX, double rotZ, double scale, double cx, double cy)
        {
            double x1 = pt.X * Math.Cos(rotZ) - pt.Y * Math.Sin(rotZ);
            double y1 = pt.X * Math.Sin(rotZ) + pt.Y * Math.Cos(rotZ);
            double x2 = x1;
            double y2 = y1 * Math.Cos(rotX) - pt.Z * Math.Sin(rotX);
            return new Avalonia.Point(cx + x2 * scale, cy - y2 * scale);
        }
        // --- ЛОГИКА ТАЙМЕРА И КНОПОК ПЛЕЕРА ЧПУ ---
        private void PlaybackTimer_Tick(object? sender, EventArgs e)
        {
            if (_toolPathPoints.Count == 0) return;

            // Шагаем вперед
            _currentSimulationIndex += _speedMultiplier;

            if (_currentSimulationIndex >= _toolPathPoints.Count)
            {
                _currentSimulationIndex = _toolPathPoints.Count - 1;
                _playbackTimer?.Stop();
            }

            // --- МАТЕМАТИКА ВЫЧИТАНИЯ МАТЕРИАЛА (СВЕРХТОЧНЫЙ СРЕЗ) ---
            if (_currentSimulationIndex < _toolPathPoints.Count)
            {
                Point3D toolPos = _toolPathPoints[_currentSimulationIndex];
                double r = _toolDiameterMm / 2.0;

                // Перебираем ячейки сетки заготовки, попавшие в зону резания
                for (int x = 0; x < GridSize; x++)
                {
                    // Пересчитываем индекс ячейки в физическую координату X на станке
                    double physicalX = _stockMinX + ((double)x / GridSize) * (_stockMaxX - _stockMinX);

                    for (int y = 0; y < GridSize; y++)
                    {
                        double physicalY = _stockMinY + ((double)y / GridSize) * (_stockMaxY - _stockMinY);

                        // Проверяем расстояние от центра фрезы до текущей точки заготовки
                        double distanceToTool = Math.Sqrt(Math.Pow(physicalX - toolPos.X, 2) + Math.Pow(physicalY - toolPos.Y, 2));

                        if (distanceToTool <= r)
                        {
                            // Если фреза идет ниже текущего уровня металла — срезаем заготовку до уровня Z фрезы
                            if (toolPos.Z < _heightGrid[x, y])
                            {
                                // Ограничиваем срез уровнем плоскости (например, Z=0)
                                _heightGrid[x, y] = toolPos.Z < 0 ? 0 : toolPos.Z;
                            }
                        }
                    }
                }
            }

            if (DataContext is GCodeWindowViewModel vm)
            {
                _selectedLineIndex = _toolPathPoints[_currentSimulationIndex].LineIndex;
                vm.SelectedLineIndex = _selectedLineIndex;
            }

            UpdateProgressText();
            ReDraw();
        }

        private void SyncSimulationIndexWithLine()
        {
            for (int i = 0; i < _toolPathPoints.Count; i++)
            {
                if (_toolPathPoints[i].LineIndex >= _selectedLineIndex)
                {
                    _currentSimulationIndex = i;
                    break;
                }
            }
            UpdateProgressText();
        }
        private void UpdateProgressText()
        {
            if (DataContext is GCodeWindowViewModel vm)
            {
                vm.SimulationProgress = $"Точка: {_currentSimulationIndex} / {_toolPathPoints.Count - 1}";
            }
        }
        // КЛИКИ ПО КНОПКАМ УПРАВЛЕНИЯ ПЛЕЕРА
        private void BtnPlay_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_currentSimulationIndex >= _toolPathPoints.Count - 1) _currentSimulationIndex = 0;
            _playbackTimer?.Start();
        }
        private void BtnPause_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _playbackTimer?.Stop();
        }
        private void BtnStop_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _playbackTimer?.Stop();
            _currentSimulationIndex = 0;
            if (DataContext is GCodeWindowViewModel vm) vm.SelectedLineIndex = 0;
            UpdateProgressText();
            ReDraw();
        }
        private void BtnStepForward_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _playbackTimer?.Stop();
            if (_currentSimulationIndex < _toolPathPoints.Count - 1)
            {
                _currentSimulationIndex++;
                if (DataContext is GCodeWindowViewModel vm) vm.SelectedLineIndex = _toolPathPoints[_currentSimulationIndex].LineIndex;
                UpdateProgressText(); ReDraw();
            }
        }
        private void BtnStepBack_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _playbackTimer?.Stop();
            if (_currentSimulationIndex > 0)
            {
                _currentSimulationIndex--;
                if (DataContext is GCodeWindowViewModel vm) vm.SelectedLineIndex = _toolPathPoints[_currentSimulationIndex].LineIndex;
                UpdateProgressText(); ReDraw();
            }
        }
        private void ComboSpeed_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            if (combo == null) return;
            // Устанавливаем множитель шагов: 1, 2, 5, 10 или 25 точек за один тик
            int[] multipliers = { 1, 2, 5, 10, 25 };
            if (combo.SelectedIndex >= 0 && combo.SelectedIndex < multipliers.Length)
            {
                _speedMultiplier = multipliers[combo.SelectedIndex];
            }
        }
        // --- ОБРАБОТЧИКИ НАВИГАЦИИ МЫШЬЮ ОСТАЮТСЯ ПРЕЖНИМИ ---
        private void Canvas_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            var pointerProperties = e.GetCurrentPoint(this).Properties;
            if (pointerProperties.IsRightButtonPressed) { _isPressed = true; _isRightPressed = false; }
            else if (pointerProperties.IsMiddleButtonPressed) { _isRightPressed = true; _isPressed = false; }
            _lastMousePos = e.GetPosition(this); e.Handled = true;
        }
        private void Canvas_PointerMoved(object sender, PointerEventArgs e)
        {
            var canvas = sender as Canvas;
            if (canvas == null || _toolPathPoints.Count < 2) return;

            var currentPos = e.GetPosition(canvas);

            // --- РЕЖИМ СВОБОДНОГО НАВЕДЕНИЯ КУРСOРА (ИНТЕРАКТИВНЫЙ ПОИСК КАДРА G-КОДА) ---
            if (!_isPressed && !_isRightPressed)
            {
                double cx = (canvas.Bounds.Width / 2) + _offsetX;
                double cy = (canvas.Bounds.Height / 2) + _offsetY;
                if (canvas.Bounds.Width == 0) cx = 300 + _offsetX;
                if (canvas.Bounds.Height == 0) cy = 300 + _offsetY;

                int hoveredLineIndex = -1;
                double closestDistance = 5.0; // Радиус поимки линии в пикселях (максимум 5 пикселей от курсора)

                // Сканируем всю траекторию и ищем, к какому отрезку мышь подошла ближе всего
                for (int i = 0; i < _toolPathPoints.Count - 1; i++)
                {
                    var pScreen1 = Project3D(_toolPathPoints[i], _rotX, _rotZ, _scale, cx, cy);
                    var pScreen2 = Project3D(_toolPathPoints[i + 1], _rotX, _rotZ, _scale, cx, cy);

                    double dist = DistanceFromPointToLine(currentPos, pScreen1, pScreen2);

                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        hoveredLineIndex = _toolPathPoints[i].LineIndex;
                    }
                }

                // Если нашли линию под курсором
                if (hoveredLineIndex != -1)
                {
                    // 1. Меняем форму курсора на волну/крестик (в Avalonia стандартный SizeAll/Hand идеально подходит)
                    this.Cursor = new Cursor(StandardCursorType.SizeAll);

                    // 2. Если эта строка еще не была выделена, мгновенно подсвечиваем её во ViewModel
                    if (_selectedLineIndex != hoveredLineIndex)
                    {
                        _selectedLineIndex = hoveredLineIndex;

                        if (DataContext is GCodeWindowViewModel vm)
                        {
                            vm.SelectedLineIndex = _selectedLineIndex; // Салатовый маркер в списке прыгает сюда
                        }

                        ReDraw(); // Перерисовываем 3D-сцену, чтобы этот виток вспыхнул жирным желтым
                    }
                }
                else
                {
                    // Если убрали мышь с траектории — возвращаем стандартный курсор-стрелку
                    this.Cursor = new Cursor(StandardCursorType.Arrow);
                }

                e.Handled = true;
                return;
            }

            // --- РЕЖИМ ЗАЖАТЫХ КНОПОК (ВРАЩЕНИЕ И СДВИГ СЦЕНЫ) ---
            double deltaX = currentPos.X - _lastMousePos.X;
            double deltaY = currentPos.Y - _lastMousePos.Y;

            // Во время вращения принудительно возвращаем курсор-стрелку, чтобы не мерцал
            this.Cursor = new Cursor(StandardCursorType.Arrow);

            if (_isPressed) // ПКМ — Вращение
            {
                _rotZ += deltaX * 0.007;
                _rotX -= deltaY * 0.007;
            }
            else if (_isRightPressed) // СКМ — Сдвиг
            {
                _offsetX += deltaX;
                _offsetY += deltaY;
            }

            _lastMousePos = currentPos;
            ReDraw();
            e.Handled = true;
        }

        private void Canvas_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            _isPressed = false; _isRightPressed = false; e.Handled = true;
        }
        private void Canvas_PointerWheelChanged(object sender, Avalonia.Input.PointerWheelEventArgs e)
        {
            var canvas = this.Find<Canvas>("GCodeCanvas3D");
            if (canvas == null) return;
            Avalonia.Point mousePos = e.GetPosition(canvas);
            double cx = canvas.Bounds.Width / 2; double cy = canvas.Bounds.Height / 2;
            if (cx == 0) cx = 300; if (cy == 0) cy = 300;
            double mouseModelX = (mousePos.X - cx - _offsetX) / _scale; double mouseModelY = -(mousePos.Y - cy - _offsetY) / _scale;
            double oldScale = _scale; if (e.Delta.Y > 0) _scale *= 1.25; else _scale /= 1.25;
            if (_scale < 0.1) _scale = 0.1; if (_scale > 200.0) _scale = 200.0;
            _offsetX -= mouseModelX * (_scale - oldScale); _offsetY += mouseModelY * (_scale - oldScale);
            ReDraw(); e.Handled = true;
        }

        /// <summary>
        /// Вычисляет кратчайшее расстояние от экранной точки (курсора) до плоского отрезка AB
        /// </summary>
        private double DistanceFromPointToLine(Avalonia.Point p, Avalonia.Point a, Avalonia.Point b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;

            if (Math.Abs(dx) < 0.00001 && Math.Abs(dy) < 0.00001)
            {
                return Math.Sqrt(Math.Pow(p.X - a.X, 2) + Math.Pow(p.Y - a.Y, 2));
            }

            // Вычисляем проекцию точки на отрезок
            double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy);

            if (t < 0.0) t = 0.0;
            else if (t > 1.0) t = 1.0;

            // Ближайшая точка на отрезке
            double closestX = a.X + t * dx;
            double closestY = a.Y + t * dy;

            return Math.Sqrt(Math.Pow(p.X - closestX, 2) + Math.Pow(p.Y - closestY, 2));
        }

    }
}
