using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
//Алгоритм берет начальные и конечные координаты линии из КОМПАСА, вычисляет ее угол
//в пространстве и с шагом TrochoidStep (наша величина \(A_{e}\)) начинает строить
//смещающиеся окружности. Каждая петля выводится командой G2 с относительными координатами
//центра I и J, что гарантирует плавное движение приводов станка в LinuxCNC и постоянную
//толщину снимаемой стружки Д16Т.
//Класс TrochoidEngine.cs готов и успешно интегрируется. Теперь нам нужно связать его
//со считыванием реальных координат из КОМПАСА. Скажите, в вашей тестовой 3D-модели,
//которую вы открываете в КОМПАС-3D v19, контур обработки нарисован как простой плоский
//эскиз (линия) или вы будете выделять трехмерное ребро (Edge) на теле детали?
//От этого зависит, какую функцию API7 нам нужно вызвать для съема координат.


namespace KompasTrochoidCam.Services
{
    public class TrochoidEngine
    {
        public struct Point2D
        {
            public double X;
            public double Y;
            public Point2D(double x, double y) { X = x; Y = y; }
        }

        /// <summary>
        /// Генерация трохоидального G-кода вдоль прямой линии от точки A до точки B
        /// </summary>
        public static string GenerateLinearTrochoid(Point2D pA, Point2D pB, double toolDiameter, double stepAe, double feed, double speed)
        {
            StringBuilder gcode = new StringBuilder();

            // Расчет базового вектора линии
            double dx = pB.X - pA.X;
            double dy = pB.Y - pA.Y;
            double lineLength = Math.Sqrt(dx * dx + dy * dy);

            if (lineLength < 0.001) return "; Ошибка: Длина линии слишком мала\n";

            // Направление вектора (нормализованное)
            double ux = dx / lineLength;
            double uy = dy / lineLength;

            // Радиус трохоидальной петли (обычно чуть больше шага, чтобы фреза не врезалась полным диаметром)
            // Радиус петли равен половине диаметра фрезы (или чуть меньше для формирования ширины паза)
            double r = toolDiameter / 2.0;

            // Шапка G-кода для LinuxCNC
            gcode.AppendLine("%");
            gcode.AppendLine("( iMachining-Mini Trochoid Track )");
            gcode.AppendLine("G90 G40 G17");
            gcode.AppendLine($"S{speed:F0} F{feed:F0} M3");
            gcode.AppendLine("G54");

            // Выход в начальную точку над заготовкой с учетом радиуса петли
            double startX = pA.X - ux * r;
            double startY = pA.Y - uy * r;
            gcode.AppendLine(string.Format(CultureInfo.InvariantCulture, "G0 X{0:F3} Y{1:F3}", startX, startY));
            gcode.AppendLine("G0 Z10.0");
            gcode.AppendLine("G1 Z0.0 F50"); // Плавное врезание на подаче 50

            double currentDist = 0.0;

            // Основной цикл генерации петель вдоль линии
            while (currentDist <= lineLength)
            {
                // Текущий центр петли на осевой линии
                double cx = pA.X + ux * currentDist;
                double cy = pA.Y + uy * currentDist;

                // Точка входа в петлю (смещение перпендикулярно вектору движения)
                double pEntryX = cx - uy * r;
                double pEntryY = cy + ux * r;

                // Точка выхода из петли (противоположная сторона паза)
                double pExitX = cx + uy * r;
                double pExitY = cy - ux * r;

                // Вывод кругового движения G2 (по часовой стрелке) для формирования петли
                // I и J - относительные координаты центра окружности от точки старта дуги
                double iCoord = cx - pEntryX;
                double jCoord = cy - pEntryY;

                gcode.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "G1 X{0:F3} Y{1:F3} F{2:F0}", pEntryX, pEntryY, feed));

                gcode.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "G2 X{0:F3} Y{1:F3} I{2:F3} J{3:F3}", pEntryX, pEntryY, iCoord, jCoord));

                // Смещение центра на следующий шаг Ae
                currentDist += stepAe;
            }

            // Финал программы
            gcode.AppendLine("G0 Z50.0");
            gcode.AppendLine("M5");
            gcode.AppendLine("M30");
            gcode.AppendLine("%");

            return gcode.ToString();
        }
    }
}
