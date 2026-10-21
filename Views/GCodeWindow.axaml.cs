using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using KompasTrochoidCam.Services;
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

        private readonly CamSceneContext _sceneContext = new CamSceneContext();
        private readonly StockRenderer _stockRenderer = new StockRenderer();
        private readonly ToolRenderer _toolRenderer = new ToolRenderer();
        private readonly TrajectoryRenderer _trajectoryRenderer = new TrajectoryRenderer();

        private bool _isPressed = false;
        private bool _isRightPressed = false;
        private Avalonia.Point _lastMousePos;

        private double minX, maxX, minY, maxY, minZ, maxZ;
        private double _totalTimeSeconds = 0;
        private DispatcherTimer? _playbackTimer;
        private int _currentSimulationIndex = 0;
        private int _speedMultiplier = 1;

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

            _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _playbackTimer.Tick += PlaybackTimer_Tick;

            this.DataContextChanged += (sender, e) =>
            {
                if (DataContext is GCodeWindowViewModel vm)
                {
                    vm.PropertyChanged += (s, ev) =>
                    {
                        if (ev.PropertyName == nameof(GCodeWindowViewModel.SelectedLineIndex))
                        {
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

        public void ParseAndBuildTrajectory(string gcode)
        {
            _toolPathPoints.Clear();
            _stockRenderer.Initialize();

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

                if (line.Contains("Фрезерование d"))
                {
                    int dIdx = line.IndexOf("d");
                    if (dIdx != -1) double.TryParse(line.Substring(dIdx + 1, 2), CultureInfo.InvariantCulture, out double d);
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
            var canvas = this.FindControl<Canvas>("GCodeCanvas3D");
            if (canvas == null || _toolPathPoints.Count < 2) return;
            canvas.Children.Clear();

            _stockRenderer.Render(canvas, _sceneContext);
            _trajectoryRenderer.Render(canvas, _sceneContext, _toolPathPoints, _selectedLineIndex);

            if (_currentSimulationIndex < _toolPathPoints.Count)
            {
                Point3D tPos = _toolPathPoints[_currentSimulationIndex];
                _toolRenderer.Render(canvas, _sceneContext, tPos.X, tPos.Y, tPos.Z);
            }

            DrawCoordinateSystem(canvas);
        }

        private void DrawCoordinateSystem(Canvas canvas)
        {
            double axisCenterX = 60;
            double axisCenterY = canvas.Bounds.Height > 0 ? canvas.Bounds.Height - 60 : 550;
            double axisLength = 35.0;

            // ИСПРАВЛЕННЫЙ БЛОК: Оси ЧПУ теперь проецируются без синтаксических ошибок
            var zeroScreen = _sceneContext.Project3D(0, 0, 0, 0, 0);
            var xScreen = _sceneContext.Project3D(axisLength, 0, 0, 0, 0);
            var yScreen = _sceneContext.Project3D(0, axisLength, 0, 0, 0);
            var zScreen = _sceneContext.Project3D(0, 0, axisLength, 0, 0);

            var screenX = new Avalonia.Point(axisCenterX + (xScreen.X - zeroScreen.X), axisCenterY + (xScreen.Y - zeroScreen.Y));
            var screenY = new Avalonia.Point(axisCenterX + (yScreen.X - zeroScreen.X), axisCenterY + (yScreen.Y - zeroScreen.Y));
            var screenZ = new Avalonia.Point(axisCenterX + (zScreen.X - zeroScreen.X), axisCenterY + (zScreen.Y - zeroScreen.Y));

            DrawArrow(canvas, new Avalonia.Point(axisCenterX, axisCenterY), screenX, Brushes.Red, "X");
            DrawArrow(canvas, new Avalonia.Point(axisCenterX, axisCenterY), screenY, Brushes.LimeGreen, "Y");
            DrawArrow(canvas, new Avalonia.Point(axisCenterX, axisCenterY), screenZ, Brushes.DodgerBlue, "Z");
        }

        private void DrawArrow(Canvas canvas, Avalonia.Point start, Avalonia.Point end, IBrush color, string label)
        {
            canvas.Children.Add(new Line { StartPoint = start, EndPoint = end, Stroke = color, StrokeThickness = 2.0 });
            canvas.Children.Add(new TextBlock { Text = label, Foreground = color, FontSize = 10, FontWeight = FontWeight.Bold, Margin = new Thickness(end.X - 4, end.Y - 6, 0, 0) });
        }

        private void PlaybackTimer_Tick(object? sender, EventArgs e)
        {
            if (_toolPathPoints.Count == 0) return;

            _currentSimulationIndex += _speedMultiplier;
            if (_currentSimulationIndex >= _toolPathPoints.Count)
            {
                _currentSimulationIndex = _toolPathPoints.Count - 1;
                _playbackTimer?.Stop();
            }

            if (_currentSimulationIndex < _toolPathPoints.Count)
            {
                Point3D toolPos = _toolPathPoints[_currentSimulationIndex];
                _stockRenderer.CutMaterial(toolPos.X, toolPos.Y, toolPos.Z, _toolRenderer.Diameter / 2.0);
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
                if (_toolPathPoints[i].LineIndex >= _selectedLineIndex) { _currentSimulationIndex = i; break; }
            }
            UpdateProgressText();
        }
        private void UpdateProgressText()
        {
            if (DataContext is GCodeWindowViewModel vm) vm.SimulationProgress = $"Точка: {_currentSimulationIndex} / {_toolPathPoints.Count - 1}";
        }
        private void BtnPlay_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_currentSimulationIndex >= _toolPathPoints.Count - 1) _currentSimulationIndex = 0;
            _playbackTimer?.Start();
        }
        private void BtnPause_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e) => _playbackTimer?.Stop();
        private void BtnStop_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _playbackTimer?.Stop();
            _currentSimulationIndex = 0;
            _stockRenderer.Initialize();
            if (DataContext is GCodeWindowViewModel vm) vm.SelectedLineIndex = 0;
            UpdateProgressText(); ReDraw();
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
            var combo = sender as ComboBox; if (combo == null) return;
            int[] multipliers = { 1, 2, 5, 10, 25 };
            if (combo.SelectedIndex >= 0 && combo.SelectedIndex < multipliers.Length) _speedMultiplier = multipliers[combo.SelectedIndex];
        }
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
            if (!_isPressed && !_isRightPressed)
            {
                int hoveredLineIndex = -1;
                double closestDistance = 5.0;
                for (int i = 0; i < _toolPathPoints.Count - 1; i++)
                {
                    var pScreen1 = _sceneContext.Project3D(_toolPathPoints[i].X, _toolPathPoints[i].Y, _toolPathPoints[i].Z, canvas.Bounds.Width, canvas.Bounds.Height);
                    var pScreen2 = _sceneContext.Project3D(_toolPathPoints[i + 1].X, _toolPathPoints[i + 1].Y, _toolPathPoints[i + 1].Z, canvas.Bounds.Width, canvas.Bounds.Height);
                    double dist = DistanceFromPointToLine(currentPos, pScreen1, pScreen2);
                    if (dist < closestDistance) { closestDistance = dist; hoveredLineIndex = _toolPathPoints[i].LineIndex; }
                }
                if (hoveredLineIndex != -1)
                {
                    this.Cursor = new Cursor(StandardCursorType.SizeAll);
                    if (_selectedLineIndex != hoveredLineIndex)
                    {
                        _selectedLineIndex = hoveredLineIndex;
                        if (DataContext is GCodeWindowViewModel vm) vm.SelectedLineIndex = _selectedLineIndex;
                        ReDraw();
                    }
                }
                else this.Cursor = new Cursor(StandardCursorType.Arrow);
                return;
            }
            double deltaX = currentPos.X - _lastMousePos.X;
            double deltaY = currentPos.Y - _lastMousePos.Y;
            this.Cursor = new Cursor(StandardCursorType.Arrow);
            if (_isPressed)
            {
                _sceneContext.RotZ += deltaX * 0.007;
                _sceneContext.RotX -= deltaY * 0.007;
            }
            else if (_isRightPressed)
            {
                _sceneContext.OffsetX += deltaX;
                _sceneContext.OffsetY += deltaY;
            }
            _lastMousePos = currentPos;
            ReDraw();
            e.Handled = true;
        }
        private double DistanceFromPointToLine(Avalonia.Point p, Avalonia.Point a, Avalonia.Point b)
        {
            double dx = b.X - a.X; double dy = b.Y - a.Y;
            if (Math.Abs(dx) < 0.00001 && Math.Abs(dy) < 0.00001) return Math.Sqrt(Math.Pow(p.X - a.X, 2) + Math.Pow(p.Y - a.Y, 2));
            double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy);
            if (t < 0.0) t = 0.0; else if (t > 1.0) t = 1.0;
            return Math.Sqrt(Math.Pow(p.X - (a.X + t * dx), 2) + Math.Pow(p.Y - (a.Y + t * dy), 2));
        }
        private void Canvas_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            _isPressed = false; _isRightPressed = false; e.Handled = true;
        }
        // ИСПРАВЛЕННЫЙ ТИП: Переименовали в PointerWheelEventArgs и заменили FindControl на Find
        private void Canvas_PointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            var canvas = this.FindControl<Canvas>("GCodeCanvas3D");
            if (canvas == null) return;
            Avalonia.Point mousePos = e.GetPosition(canvas);
            double cx = canvas.Bounds.Width / 2; double cy = canvas.Bounds.Height / 2;
            if (cx == 0) cx = 300; if (cy == 0) cy = 300;
            double mouseModelX = (mousePos.X - cx - _sceneContext.OffsetX) / _sceneContext.Scale;
            double mouseModelY = -(mousePos.Y - cy - _sceneContext.OffsetY) / _sceneContext.Scale;
            double oldScale = _sceneContext.Scale;
            if (e.Delta.Y > 0) _sceneContext.Scale *= 1.25; else _sceneContext.Scale /= 1.25;
            if (_sceneContext.Scale < 0.1) _sceneContext.Scale = 0.1;
            if (_sceneContext.Scale > 200.0) _sceneContext.Scale = 200.0;
            _sceneContext.OffsetX -= mouseModelX * (_sceneContext.Scale - oldScale);
            _sceneContext.OffsetY += mouseModelY * (_sceneContext.Scale - oldScale);
            ReDraw();
            e.Handled = true;
        }
    }
}

