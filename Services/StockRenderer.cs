using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using System;

namespace KompasTrochoidCam.Services
{
    public class StockRenderer
    {
        private const int GridSize = 100;
        private double[,] _heightGrid = new double[GridSize, GridSize];
        private bool _isInitialized = false;

        private readonly double _stockMinX = -55.0;
        private readonly double _stockMaxX = 55.0;
        private readonly double _stockMinY = -70.0;
        private readonly double _stockMaxY = 70.0;
        private readonly double _stockThickness = 20.0;

        public void Initialize()
        {
            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    _heightGrid[x, y] = _stockThickness;
                }
            }
            _isInitialized = true;
        }

        /// <summary>
        /// Математика вычитания объема фрезы из тела заготовки Д16Т
        /// </summary>
        public void CutMaterial(double toolX, double toolY, double toolZ, double toolRadius)
        {
            if (!_isInitialized) Initialize();

            for (int x = 0; x < GridSize; x++)
            {
                double physicalX = _stockMinX + ((double)x / GridSize) * (_stockMaxX - _stockMinX);
                for (int y = 0; y < GridSize; y++)
                {
                    double physicalY = _stockMinY + ((double)y / GridSize) * (_stockMaxY - _stockMinY);
                    double distance = Math.Sqrt(Math.Pow(physicalX - toolX, 2) + Math.Pow(physicalY - toolY, 2));

                    if (distance <= toolRadius)
                    {
                        if (toolZ < _heightGrid[x, y])
                        {
                            _heightGrid[x, y] = toolZ < 0 ? 0 : toolZ;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Отрисовка контрастной 3D-сетки: светлая заготовка + темный прорезанный паз
        /// </summary>
        public void Render(Canvas canvas, CamSceneContext ctx)
        {
            if (!_isInitialized) Initialize();

            var rawGeometry = new StreamGeometry();
            var cutGeometry = new StreamGeometry();

            using (var rawCtx = rawGeometry.Open())
            using (var cutCtx = cutGeometry.Open())
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

                        var pScreen = ctx.Project3D(physicalX, physicalY, physicalZ, canvas.Bounds.Width, canvas.Bounds.Height);

                        if (physicalZ < _stockThickness - 0.01)
                        {
                            if (isCutFirst) { cutCtx.BeginFigure(pScreen, false); isCutFirst = false; }
                            cutCtx.LineTo(pScreen);
                            isRawFirst = true;
                        }
                        else
                        {
                            if (isRawFirst) { rawCtx.BeginFigure(pScreen, false); isRawFirst = false; }
                            rawCtx.LineTo(pScreen);
                            isCutFirst = true;
                        }
                    }
                }
            }

            canvas.Children.Add(new Path { Data = rawGeometry, Stroke = new SolidColorBrush(Colors.LightGray, 0.45), StrokeThickness = 0.9 });
            canvas.Children.Add(new Path { Data = cutGeometry, Stroke = new SolidColorBrush(Colors.Black, 0.75), StrokeThickness = 1.3 });
        }
    }
}
