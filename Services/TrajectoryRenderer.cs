using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using System.Collections.Generic;

namespace KompasTrochoidCam.Services
{
    public class TrajectoryRenderer
    {
        /// <summary>
        /// Отрисовка путей фрезы G0/G1/G2 и жирной желтой подсветки кадра под курсором
        /// </summary>
        public void Render(Canvas canvas, CamSceneContext ctx, List<Views.GCodeWindow.Point3D> points, int selectedLineIndex)
        {
            if (points == null || points.Count < 2) return;

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

                for (int i = 0; i < points.Count - 1; i++)
                {
                    var p1 = ctx.Project3D(points[i].X, points[i].Y, points[i].Z, canvas.Bounds.Width, canvas.Bounds.Height);
                    var p2 = ctx.Project3D(points[i + 1].X, points[i + 1].Y, points[i + 1].Z, canvas.Bounds.Width, canvas.Bounds.Height);

                    // ЕСЛИ ЭТО ВЫДЕЛЕННАЯ СТРОКА — в приоритетную подсвеченную геометрию
                    if (points[i].LineIndex == selectedLineIndex)
                    {
                        if (isSelectFirst) { selectCtx.BeginFigure(p1, false); isSelectFirst = false; }
                        selectCtx.LineTo(p2);
                    }
                    // Иначе — обычные траектории (Быстрые/Рабочие)
                    else if (points[i].Z > 0.1)
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

            // Добавляем все три слоя траекторий на холст
            // Добавляем три слоя траекторий на холст с УВЕЛИЧЕННОЙ видимостью
            // Рабочую траекторию съема (G1/G2) делаем толстой (2.2 пикселя) и неоново-зеленой (Cyan или сочный SpringGreen)
            canvas.Children.Add(new Path { Data = workGeometry, Stroke = Brushes.Cyan, StrokeThickness = 2.2 });

            // Быстрые перемещения G0 оставляем тонкими и контрастно красными
            canvas.Children.Add(new Path { Data = rapidGeometry, Stroke = Brushes.Red, StrokeThickness = 0.8 });

            // Выделенную строку делаем максимально жирной (4.5 пикселя) неоново-желтой
            canvas.Children.Add(new Path { Data = selectGeometry, Stroke = Brushes.Yellow, StrokeThickness = 4.5 });
        }
    }
}
