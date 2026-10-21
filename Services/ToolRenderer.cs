using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace KompasTrochoidCam.Services
{
    public class ToolRenderer
    {
        public double Diameter { get; set; } = 10.0;
        public double Height { get; set; } = 30.0;

        /// <summary>
        /// Отрисовка фрезы в виде объемного 3D-цилиндра с перекрестием оси
        /// </summary>
        public void Render(Canvas canvas, CamSceneContext ctx, double x, double y, double z)
        {
            double r = Diameter / 2.0;
            int segments = 16;
            var cylinderGeometry = new StreamGeometry();

            using (var toolCtx = cylinderGeometry.Open())
            {
                var bottomPoints = new List<Avalonia.Point>(segments);
                var topPoints = new List<Avalonia.Point>(segments);

                // 1. Вычисляем пространственные координаты окружностей фрезы
                for (int s = 0; s < segments; s++)
                {
                    double angle = s * (Math.PI * 2 / segments);
                    double dx = Math.Cos(angle) * r;
                    double dy = Math.Sin(angle) * r;

                    bottomPoints.Add(ctx.Project3D(x + dx, y + dy, z, canvas.Bounds.Width, canvas.Bounds.Height));
                    topPoints.Add(ctx.Project3D(x + dx, y + dy, z + Height, canvas.Bounds.Width, canvas.Bounds.Height));
                }

                // 2. Корректная отрисовка нижнего основания (API Avalonia v11)
                toolCtx.BeginFigure(bottomPoints[0], true); // Стартуем с первой точки, контур ЗАМКНУТЫЙ
                for (int s = 1; s < segments; s++) toolCtx.LineTo(bottomPoints[s]);

                // 3. Корректная отрисовка верхнего основания
                toolCtx.BeginFigure(topPoints[0], true); // Стартуем с первой точки, контур ЗАМКНУТЫЙ
                for (int s = 1; s < segments; s++) toolCtx.LineTo(topPoints[s]);

                // 4. Соединяем основания четырьмя вертикальными ребрами для объема
                for (int s = 0; s < segments; s += segments / 4)
                {
                    toolCtx.BeginFigure(bottomPoints[s], false); // Контур НЕЗАМКНУТЫЙ (просто отрезок)
                    toolCtx.LineTo(topPoints[s]);
                }
            }


            canvas.Children.Add(new Path { Data = cylinderGeometry, Stroke = Brushes.Yellow, StrokeThickness = 1.0, Fill = new SolidColorBrush(Colors.Lime, 0.25) });

            // Перекрестие центра шпинделя
            var pCenter = ctx.Project3D(x, y, z, canvas.Bounds.Width, canvas.Bounds.Height);
            canvas.Children.Add(new Line { StartPoint = new Avalonia.Point(pCenter.X - 5, pCenter.Y), EndPoint = new Avalonia.Point(pCenter.X + 5, pCenter.Y), Stroke = Brushes.Red, StrokeThickness = 1.0 });
            canvas.Children.Add(new Line { StartPoint = new Avalonia.Point(pCenter.X, pCenter.Y - 5), EndPoint = new Avalonia.Point(pCenter.X, pCenter.Y + 5), Stroke = Brushes.Red, StrokeThickness = 1.0 });
        }
    }
}
