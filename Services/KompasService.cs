using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace KompasTrochoidCam.Services
{
    public class KompasService
    {
        private object? _kompasApp;
        private object? _activeDocument;

        // Нативная функция Windows для принудительного извлечения активного COM-объекта из реестра (ROT)
        [DllImport("oleaut32.dll", PreserveSig = false)]
        private static extern void GetActiveObject(ref Guid rclsid, IntPtr pReserved, [MarshalAs(UnmanagedType.Interface)] out object ppunk);

        private object? GetProperty(object? target, string propertyName, object[]? args = null)
        {
            if (target == null) return null;
            return target.GetType().InvokeMember(
                propertyName,
                BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                null,
                target,
                args);
        }

        public bool Connect(out string errorMessage)
        {
            errorMessage = "Неизвестная ошибка";
            try
            {
                // Уникальный глобальный GUID интерфейса "Kompas.Application.7" в реестре Windows
                // Он одинаков для всех версий КОМПАС (v17 - v23)
                Guid clsid = new Guid("E26CE0D3-162F-4A93-87CE-52E2010A3816");

                try
                {
                    // Напрямую выдергиваем COM-сервер запущенного приложения из ядра Windows
                    GetActiveObject(ref clsid, IntPtr.Zero, out object kompasObject);
                    _kompasApp = kompasObject;
                }
                catch (Exception ex)
                {
                    errorMessage = $"КОМПАС-3D не запущен в системе или заблокирован Windows: {ex.Message}";
                    return false;
                }

                if (_kompasApp == null)
                {
                    errorMessage = "Не удалось связаться с COM-сервером АСКОН.";
                    return false;
                }

                // Теперь в _kompasApp со 100% гарантией лежит корень IApplication (API v7)
                // Запрашиваем текущий открытый документ через Рефлексию
                try
                {
                    _activeDocument = GetProperty(_kompasApp, "ActiveDocument");
                }
                catch (Exception ex)
                {
                    errorMessage = $"Связь установлена, но произошел сбой чтения ActiveDocument: {ex.Message}";
                    return false;
                }

                if (_activeDocument == null)
                {
                    errorMessage = "КОМПАС успешно подключен, но в нем не открыта ни одна деталь (.m3d)!";
                    return false;
                }

                errorMessage = "Успешно подключено";
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Критический сбой: {ex.Message}";
                return false;
            }
        }

        public string GetActiveDocName()
        {
            if (_activeDocument == null) return "Нет подключения";
            try
            {
                var nameObj = GetProperty(_activeDocument, "Name");
                return nameObj?.ToString() ?? "Без имени";
            }
            catch
            {
                return "Ошибка чтения имени";
            }
        }

        public List<TrochoidEngine.Point2D> GetSelectedFaceContours(out double faceZ)
        {
            faceZ = 0.0;
            var points = new List<TrochoidEngine.Point2D>();
            if (_activeDocument == null) return points;

            try
            {
                object? selectionManager = GetProperty(_activeDocument, "SelectionManager");
                if (selectionManager == null) return points;

                object? selectedObjects = GetProperty(selectionManager, "SelectedObjects");
                if (selectedObjects == null) return points;

                int count = (int)(GetProperty(selectedObjects, "Count") ?? 0);
                if (count == 0) return points;

                object? selectedObject = selectedObjects.GetType().InvokeMember(
                    "Item",
                    BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
                    null,
                    selectedObjects,
                    new object[] { 0 });

                if (selectedObject == null) return points;

                int objType = (int)(GetProperty(selectedObject, "ObjectType") ?? 0);

                if (objType == 4) // oType_Face
                {
                    object? surface = GetProperty(selectedObject, "Geometry");
                    int surfaceType = (int)(GetProperty(surface, "SurfaceType") ?? 0);

                    if (surfaceType == 1) // Plane
                    {
                        object? planeOrigin = GetProperty(surface, "Origin");
                        faceZ = (double)(GetProperty(planeOrigin, "Z") ?? 0.0);

                        object? loops = GetProperty(selectedObject, "Loops");
                        int loopsCount = (int)(GetProperty(loops, "Count") ?? 0);

                        if (loops != null && loopsCount > 0)
                        {
                            object? outerLoop = loops.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, loops, new object[] { 0 });
                            object? edges = GetProperty(outerLoop, "Edges");
                            int edgesCount = (int)(GetProperty(edges, "Count") ?? 0);

                            for (int i = 0; i < edgesCount; i++)
                            {
                                object? edge = edges.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, edges, new object[] { i });
                                object? edgeGeometry = GetProperty(edge, "Geometry");
                                object? startPoint = GetProperty(edgeGeometry, "PointStart");

                                double x = (double)(GetProperty(startPoint, "X") ?? 0.0);
                                double y = (double)(GetProperty(startPoint, "Y") ?? 0.0);

                                points.Add(new TrochoidEngine.Point2D(x, y));
                            }

                            if (points.Count > 0)
                            {
                                object? firstEdge = edges.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, edges, new object[] { 0 });
                                object? edgeGeometry = GetProperty(firstEdge, "Geometry");
                                object? startPoint = GetProperty(edgeGeometry, "PointStart");

                                double x = (double)(GetProperty(startPoint, "X") ?? 0.0);
                                double y = (double)(GetProperty(startPoint, "Y") ?? 0.0);
                                points.Add(new TrochoidEngine.Point2D(x, y));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка парсинга грани: {ex.Message}");
            }
            return points;
        }
    }
}
