using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using KompasTrochoidCam.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace KompasTrochoidCam.Views
{
    public partial class GCodeWindow : Window
    {
        private List<Point3D> _toolPathPoints = new List<Point3D>();

        // Переменная для хранения текущей выделенной строки кода
        private int _selectedLineIndex = -1;

        private double _rotX = 30 * Math.PI / 180.0;
        private double _rotZ = -45 * Math.PI / 180.0;
        private double _scale = 3.5;

        // Переменные смещения (панорамирования) экрана
        private double _offsetX = 0.0;
        private double _offsetY = 0.0;

        // Флаги отслеживания кнопок мыши
        private bool _isPressed = false;
        private bool _isRightPressed = false; // <-- ТЕПЕРЬ ОБЪЯВЛЕНО КОРРЕКТНО
        private Avalonia.Point _lastMousePos;

        public struct Point3D
        {
            public double X; public double Y; public double Z;
            public int LineIndex; // Индекс строки G-кода, породившей эту точку
            public Point3D(double x, double y, double z, int lineIndex)
            {
                X = x; Y = y; Z = z; LineIndex = lineIndex;
            }
        }

        public GCodeWindow()
        {
            InitializeComponent();

            // Подписываемся на событие изменения контекста данных (DataContext)
            this.DataContextChanged += (sender, e) =>
            {
                if (DataContext is GCodeWindowViewModel vm)
                {
                    // Когда пользователь кликает по строке в ListBox, ViewModel меняет SelectedLineIndex.
                    // Мы перехватываем это и мгновенно перерисовываем 3D-сцену!
                    vm.PropertyChanged += (s, ev) =>
                    {
                        if (ev.PropertyName == nameof(GCodeWindowViewModel.SelectedLineIndex))
                        {
                            _selectedLineIndex = vm.SelectedLineIndex;
                            ReDraw();
                        }
                    };
                }
            };
        }

        public void ParseAndBuildTrajectory(string gcode)
        {
            _toolPathPoints.Clear();
            string[] lines = gcode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            double lastX = 0, lastY = 0, lastZ = 50;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string[] parts = line.Split('(');
                string cleanLine = parts[0].Trim();

                if (string.IsNullOrEmpty(cleanLine)) continue;

                double x = GetCoord(cleanLine, 'X', lastX);
                double y = GetCoord(cleanLine, 'Y', lastY);
                double z = GetCoord(cleanLine, 'Z', lastZ);

                if (cleanLine.Contains("G0") || cleanLine.Contains("G1"))
                {
                    _toolPathPoints.Add(new Point3D(x, y, z, i));
                }
                else if (cleanLine.Contains("G2"))
                {
                    double iParam = GetCoord(cleanLine, 'I', 0);
                    double jParam = GetCoord(cleanLine, 'J', 0);

                    double centerX = lastX + iParam;
                    double centerY = lastY + jParam;
                    double radius = Math.Sqrt(iParam * iParam + jParam * jParam);
                    double startAngle = Math.Atan2(lastY - centerY, lastX - centerX);

                    for (int step = 0; step <= 12; step++)
                    {
                        double angle = startAngle - (step * (Math.PI * 2 / 12));
                        double arcX = centerX + Math.Cos(angle) * radius;
                        double arcY = centerY + Math.Sin(angle) * radius;
                        _toolPathPoints.Add(new Point3D(arcX, arcY, z, i)); // Передаем индекс текущей строки i
                    }
                }
                lastX = x; lastY = y; lastZ = z;
            }
            ReDraw();
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
            var canvas = this.FindControl<Canvas>("GCodeCanvas3D");
            if (canvas == null || _toolPathPoints.Count < 2) return;
            canvas.Children.Clear();

            // Вычисляем динамический центр с учетом перетаскивания правой кнопкой мыши
            double cx = (canvas.Bounds.Width / 2) + _offsetX;
            double cy = (canvas.Bounds.Height / 2) + _offsetY;
            if (canvas.Bounds.Width == 0) cx = 300 + _offsetX;
            if (canvas.Bounds.Height == 0) cy = 300 + _offsetY;

            var workGeometry = new StreamGeometry();
            var rapidGeometry = new StreamGeometry();
            var selectGeometry = new StreamGeometry(); // Отдельная геометрия для выделенного кадра

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

                    // ЕСЛИ ЭТО ВЫДЕЛЕННАЯ СТРОКА - рисуем в приоритетную подсвеченную геометрию
                    if (_toolPathPoints[i].LineIndex == _selectedLineIndex)
                    {
                        if (isSelectFirst) { selectCtx.BeginFigure(p1, false); isSelectFirst = false; }
                        selectCtx.LineTo(p2);
                    }
                    // Иначе - обычные траектории (Быстрые/Рабочие)
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

            // Стандартный рендер траекторий
            var workPath = new Path { Data = workGeometry, Stroke = Brushes.SpringGreen, StrokeThickness = 1.2 };
            canvas.Children.Add(workPath);

            var rapidPath = new Path { Data = rapidGeometry, Stroke = Brushes.Red, StrokeThickness = 0.8 };
            canvas.Children.Add(rapidPath);

            // Рендер ПОДСВЕЧЕННОЙ части траектории (Сделаем её толстой и Ярко-Желтой/Золотой)
            var selectPath = new Path { Data = selectGeometry, Stroke = Brushes.Yellow, StrokeThickness = 3.5 };
            canvas.Children.Add(selectPath);
        }

        private Avalonia.Point Project3D(Point3D pt, double rotX, double rotZ, double scale, double cx, double cy)
        {
            double x1 = pt.X * Math.Cos(rotZ) - pt.Y * Math.Sin(rotZ);
            double y1 = pt.X * Math.Sin(rotZ) + pt.Y * Math.Cos(rotZ);
            double x2 = x1;
            double y2 = y1 * Math.Cos(rotX) - pt.Z * Math.Sin(rotX);
            return new Avalonia.Point(cx + x2 * scale, cy - y2 * scale);
        }

        // --- НОВЫЙ ОБРАБОТЧИК КЛИКА ПО СТРОКАМ G-КОДА ---
        private void OnCaretMoved(TextBox textBox)
        {
            if (string.IsNullOrEmpty(textBox.Text)) return;

            int caretIndex = textBox.CaretIndex;

            // Защита от выхода за границы текста
            if (caretIndex > textBox.Text.Length) caretIndex = textBox.Text.Length;

            string textUpToCaret = textBox.Text.Substring(0, caretIndex);

            // Вычисляем текущую строку
            int currentLine = textUpToCaret.Split('\n').Length - 1;

            if (_selectedLineIndex != currentLine)
            {
                _selectedLineIndex = currentLine;
                ReDraw(); // Мгновенно перерисовываем сцену с желтой подсветкой
            }
        }

        // --- ОБРАБОТЧИКИ МЫШКИ СТРОГО ПО СТАНДАРТУ КОМПАС-3D ---
        private void Canvas_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            var pointerProperties = e.GetCurrentPoint(this).Properties;

            // КОМПАС-3D: Вращение завязано на ПРАВУЮ кнопку мыши (ПКМ)
            if (pointerProperties.IsRightButtonPressed)
            {
                _isPressed = true; // Флаг вращения
                _isRightPressed = false;
            }
            // КОМПАС-3D: Перемещение (Панорамирование) завязано на СРЕДНЮЮ кнопку (Колесико / СКМ)
            else if (pointerProperties.IsMiddleButtonPressed)
            {
                _isRightPressed = true; // Флаг сдвига
                _isPressed = false;
            }

            _lastMousePos = e.GetPosition(this);
            e.Handled = true;
        }

        private void Canvas_PointerMoved(object sender, PointerEventArgs e)
        {
            if (!_isPressed && !_isRightPressed) return;

            var currentPos = e.GetPosition(this);
            double deltaX = currentPos.X - _lastMousePos.X;
            double deltaY = currentPos.Y - _lastMousePos.Y;

            // РЕЖИМ ВРАЩЕНИЯ (ПКМ): Крутим 3D-сцену
            if (_isPressed)
            {
                _rotZ += deltaX * 0.007;
                _rotX -= deltaY * 0.007;

                // Ограничиваем угол наклона по X, чтобы сцена не переворачивалась вверх ногами
                //if (_rotX < 0) _rotX = 0;
                //if (_rotX > Math.PI / 2) _rotX = Math.PI / 2;
            }
            // РЕЖИМ СДВИГА (СКМ / Колесико): Перетаскиваем деталь по экрану
            else if (_isRightPressed)
            {
                _offsetX += deltaX;
                _offsetY += deltaY;
            }

            _lastMousePos = currentPos;

            // Мгновенная плавная перерисовка
            ReDraw();
            e.Handled = true;
        }

        private void Canvas_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            // Сбрасываем все флаги при отпускании кнопок
            _isPressed = false;
            _isRightPressed = false;
            e.Handled = true;
        }

        // --- ОБРАБОТЧИК УМНОГО ПРИБЛИЖЕНИЯ/УДАЛЕНИЯ ВСЛЕД ЗА КУРСОРOM МЫШИ (КАК В КОМПАС-3D) ---
        // --- ОБРАБОТЧИК УМНОГО ПРИБЛИЖЕНИЯ/УДАЛЕНИЯ ВСЛЕД ЗА КУРСОРOM МЫШИ (КАК В КОМПАС-3D) ---
        private void Canvas_PointerWheelChanged(object sender, Avalonia.Input.PointerWheelEventArgs e)
        {
            var canvas = this.FindControl<Canvas>("GCodeCanvas3D");
            if (canvas == null) return;

            // 1. Получаем текущие экранные координаты курсора мыши относительно холста
            Avalonia.Point mousePos = e.GetPosition(canvas);

            // Вычисляем текущий геометрический центр холста (без учета смещений)
            double cx = canvas.Bounds.Width / 2;
            double cy = canvas.Bounds.Height / 2;
            if (cx == 0) cx = 300; if (cy == 0) cy = 300;

            // 2. ИСПРАВЛЕННЫЙ ПЕРЕСЧЕТ: Переводим экранную позицию мыши в координаты модели.
            // Учитываем, что в Project3D экранный Y вычисляется как (cy - y2 * scale).
            // Следовательно, обратная формула должна учитывать этот разворот знаков:
            double mouseModelX = (mousePos.X - cx - _offsetX) / _scale;
            double mouseModelY = -(mousePos.Y - cy - _offsetY) / _scale; // Поменяли знак инверсии оси Y

            // 3. Запоминаем старый масштаб и вычисляем новый
            double oldScale = _scale;
            if (e.Delta.Y > 0)
            {
                _scale *= 1.25; // Приближаем
            }
            else
            {
                _scale /= 1.25; // Удаляем
            }

            // Ограничители масштаба
            if (_scale < 0.1) _scale = 0.1;
            if (_scale > 200.0) _scale = 200.0;

            // 4. ИСПРАВЛЕННАЯ КОРРЕКЦИЯ СДВИГА:
            // Корректируем смещения экрана так, чтобы точка под курсором осталась неподвижной.
            // Для оси Y учитываем обратный знак проекции:
            _offsetX -= mouseModelX * (_scale - oldScale);
            _offsetY += mouseModelY * (_scale - oldScale); // Поменяли минус на плюс для оси Y

            // 5. Мгновенно перерисовываем сцену
            ReDraw();
            e.Handled = true;
        }
    }
}
