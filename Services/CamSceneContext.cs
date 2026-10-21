using System;

namespace KompasTrochoidCam.Services
{
    public class CamSceneContext
    {
        // Углы вращения, масштаб и смещения теперь живут здесь
        public double RotX { get; set; } = 30 * Math.PI / 180.0;
        public double RotZ { get; set; } = -45 * Math.PI / 180.0;
        public double Scale { get; set; } = 3.5;
        public double OffsetX { get; set; } = 0.0;
        public double OffsetY { get; set; } = 0.0;

        /// <summary>
        /// Универсальная математическая матрица проекции 3D-точки на 2D-экран Avalonia
        /// </summary>
        public Avalonia.Point Project3D(double x, double y, double z, double canvasWidth, double canvasHeight)
        {
            double cx = (canvasWidth / 2) + OffsetX;
            double cy = (canvasHeight / 2) + OffsetY;
            if (canvasWidth == 0) cx = 300 + OffsetX;
            if (canvasHeight == 0) cy = 300 + OffsetY;

            // Поворот вокруг оси Z
            double x1 = x * Math.Cos(RotZ) - y * Math.Sin(RotZ);
            double y1 = x * Math.Sin(RotZ) + y * Math.Cos(RotZ);

            // Поворот вокруг оси X и финальная проекция
            double x2 = x1;
            double y2 = y1 * Math.Cos(RotX) - z * Math.Sin(RotX);

            return new Avalonia.Point(cx + x2 * Scale, cy - y2 * Scale);
        }
    }
}
