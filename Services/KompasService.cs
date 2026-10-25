using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using KompasTrochoidCam.Models;

namespace KompasTrochoidCam.Services
{
    /// <summary>
    /// Подключение к запущенному КОМПАС-3D через внешний COM API 7.
    /// Чтение выполняется поздним связыванием (reflection), поэтому ссылки на
    /// библиотеки КОМПАС не требуются при сборке.
    /// </summary>
    public class KompasService
    {
        private object? _kompasApp;
        private object? _activeDocument;

        // Ошибки, которые возвращает GetActiveObject.
        private const int MkEUnavailable = unchecked((int)0x800401E3);
        private const int CoEClassString = unchecked((int)0x800401F3);
        private const int RegDbEClassNotReg = unchecked((int)0x80040154);

        /// <summary>
        /// ProgID, под которыми регистрируется КОМПАС-3D. Официальный ProgID приложения —
        /// «KOMPAS.Application.7» (см. SDK: IApplication); ряд версий регистрирует также API 5.
        /// </summary>
        private static readonly string[] KompasProgIds =
        {
            "KOMPAS.Application.7",
            "KOMPAS.Application.6",
            "KOMPAS.Application.5",
            "KOMPAS.Application"
        };

        /// <summary>Подробный отчёт о последней попытке подключения — для окна диагностики.</summary>
        public string LastDiagnostics { get; private set; } = string.Empty;

        // Нативная функция Windows: берёт уже запущенный объект из Running Object Table.
        [DllImport("oleaut32.dll", PreserveSig = true)]
        private static extern int GetActiveObject(ref Guid rclsid, IntPtr pReserved, [MarshalAs(UnmanagedType.Interface)] out object? ppunk);

        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable? pprot);

        public bool IsConnected => _kompasApp != null;

        public string ActiveDocumentName => GetActiveDocName();

        private object? GetProperty(object? target, string propertyName, object[]? args = null)
        {
            if (target == null) return null;
            try
            {
                return target.GetType().InvokeMember(
                    propertyName,
                    BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                    null,
                    target,
                    args);
            }
            catch
            {
                return null;
            }
        }

        private static object? GetItem(object? collection, int index)
        {
            if (collection == null) return null;
            try
            {
                return collection.GetType().InvokeMember(
                    "Item",
                    BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
                    null,
                    collection,
                    new object[] { index });
            }
            catch
            {
                return null;
            }
        }

        private static int GetCount(object? collection)
        {
            if (collection == null) return 0;
            try
            {
                object? count = collection.GetType().InvokeMember(
                    "Count",
                    BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                    null,
                    collection,
                    null);
                return count == null ? 0 : Convert.ToInt32(count, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        private static double ToDouble(object? value)
        {
            if (value == null) return 0;
            try { return Convert.ToDouble(value, CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        private static int ToInt(object? value)
        {
            if (value == null) return 0;
            try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        private static TrochoidEngine.Point2D ToPoint2D(object? point)
            => new TrochoidEngine.Point2D(ToDouble(GetMember(point, "X")), ToDouble(GetMember(point, "Y")));

        private static object? GetMember(object? target, string name)
        {
            if (target == null) return null;
            try
            {
                return target.GetType().InvokeMember(
                    name,
                    BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                    null,
                    target,
                    null);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Подключается к запущенному КОМПАС-3D.
        /// CLSID не зашит жёстко: он берётся из ProgID через реестр COM, потому что GUID
        /// зависит от версии и всегда был главной причиной ошибки MK_E_UNAVAILABLE.
        /// </summary>
        public bool Connect(out string errorMessage)
        {
            _kompasApp = null;
            _activeDocument = null;

            var log = new List<string> { "=== Подключение к КОМПАС-3D ===", $"Время: {DateTime.Now:yyyy-MM-dd HH:mm:ss}" };
            var attempts = new List<string>();

            var registered = new List<(string ProgId, Guid Clsid)>();
            foreach (string progId in KompasProgIds)
            {
                Guid? clsid = ResolveClsidFromProgId(progId);
                if (clsid.HasValue) registered.Add((progId, clsid.Value));
            }

            log.Add(registered.Count > 0
                ? "Реестр COM: " + string.Join("; ", registered.Select(r => $"{r.ProgId} = {{{r.Clsid}}}"))
                : "Реестр COM: ProgID КОМПАС не найден (проверены KOMPAS.Application.7/.6/.5).");

            var processes = GetKompasProcesses();
            log.Add(processes.Count > 0
                ? "Процессы КОМПАС: " + string.Join(", ", processes)
                : "Процессы КОМПАС: не запущены.");

            log.Add($"Наше приложение: PID {Environment.ProcessId}, сессия {Process.GetCurrentProcess().SessionId}, " +
                    (Environment.IsPrivilegedProcess ? "права администратора" : "обычные права"));

            object? attached = null;
            string attachedVia = string.Empty;
            int lastHresult = 0;

            // --- Попытка 1: штатный путь «ProgID → CLSID → GetActiveObject». ---
            foreach (var (progId, clsid) in registered)
            {
                int hr = TryGetActiveObject(clsid, out object? instance);
                lastHresult = hr;

                if (hr == 0 && instance != null)
                {
                    object? application = ResolveApplicationObject(instance, out string via);
                    if (application != null)
                    {
                        attached = application;
                        attachedVia = $"{progId}, {via}";
                        break;
                    }

                    attempts.Add($"{progId}: объект получен, но приложение КОМПАС не опознано");
                }
                else
                {
                    attempts.Add($"{progId}: GetActiveObject = 0x{hr:X8} ({DescribeHresult(hr)})");
                }
            }

            // --- Попытка 2: перебор объектов в Running Object Table. ---
            if (attached == null)
            {
                var running = EnumerateRunningObjects();
                log.Add(running.Count > 0
                    ? $"ROT: объектов {running.Count}: " + string.Join("; ", running.Take(10).Select(FormatRotEntry))
                    : "ROT: пусто.");

                foreach (var entry in running)
                {
                    object? application = ResolveApplicationObject(entry.Instance, out string via);
                    if (application == null) continue;

                    // В ROT живут и чужие приложения (Word, Excel). Свой объект берём только если
                    // его CLSID совпал с зарегистрированным ProgID КОМПАС либо сработал
                    // специфичный для API 5 метод ksGetApplication7.
                    bool knownClsid = entry.Clsid.HasValue &&
                                      registered.Any(r => r.Clsid == entry.Clsid.Value);
                    bool kompasMarker = via == Api5ApplicationVia;

                    if (!knownClsid && !kompasMarker) continue;

                    attached = application;
                    attachedVia = $"ROT: {FormatRotEntry(entry)}, {via}";
                    break;
                }
            }

            if (attached == null)
            {
                log.Add("Результат: подключиться не удалось.");
                log.AddRange(attempts.Select(a => "  • " + a));
                log.Add(string.Empty);
                log.Add(BuildRecommendations(registered.Count, processes.Count));
                LastDiagnostics = string.Join(Environment.NewLine, log);

                errorMessage = BuildConnectFailure(registered.Count, processes.Count, lastHresult);
                return false;
            }

            _kompasApp = attached;
            _activeDocument = GetProperty(_kompasApp, "ActiveDocument");

            log.AddRange(attempts.Select(a => "  • " + a));
            log.Add($"Результат: подключено ({attachedVia}).");
            log.Add("Активный документ: " + (_activeDocument != null ? GetActiveDocName() : "не открыт"));
            LastDiagnostics = string.Join(Environment.NewLine, log);

            errorMessage = _activeDocument != null
                ? $"Подключено к КОМПАС-3D · документ: {GetActiveDocName()}"
                : "Подключено к КОМПАС-3D, но активный документ не открыт — откройте деталь .m3d и нажмите «Обновить геометрию»";
            return true;
        }

        private const string SelfObjectVia = "сам объект";
        private const string Api5ApplicationVia = "метод ksGetApplication7";

        /// <summary>CLSID класса по ProgID — читается из реестра COM (HKCR), ничего не зашито.</summary>
        private static Guid? ResolveClsidFromProgId(string progId)
        {
            try
            {
                Type? type = Type.GetTypeFromProgID(progId, throwOnError: false);
                if (type == null || type.GUID == Guid.Empty) return null;
                return type.GUID;
            }
            catch
            {
                return null;
            }
        }

        private static int TryGetActiveObject(Guid clsid, out object? instance)
        {
            instance = null;
            try
            {
                return GetActiveObject(ref clsid, IntPtr.Zero, out instance);
            }
            catch (Exception ex)
            {
                return ex is COMException com ? com.HResult : unchecked((int)0x80004005);
            }
        }

        private static string DescribeHresult(int hr) => hr switch
        {
            0 => "успешно",
            MkEUnavailable => "объект не найден в Running Object Table (MK_E_UNAVAILABLE)",
            CoEClassString => "некорректный ProgID (CO_E_CLASSSTRING)",
            RegDbEClassNotReg => "CLSID не зарегистрирован (REGDB_E_CLASSNOTREG)",
            _ => "ошибка COM"
        };

        /// <summary>Проверяет, является ли объект приложением КОМПАС, и возвращает интерфейс приложения.</summary>
        private object? ResolveApplicationObject(object? candidate, out string via)
        {
            via = string.Empty;
            if (candidate == null) return null;

            object? application = GetProperty(candidate, "Application");
            if (LooksLikeApplication(application))
            {
                via = "свойство Application";
                return application;
            }

            object? api7 = InvokeMethod(candidate, "ksGetApplication7");
            if (LooksLikeApplication(api7))
            {
                via = Api5ApplicationVia;
                return api7;
            }

            if (LooksLikeApplication(candidate))
            {
                via = SelfObjectVia;
                return candidate;
            }

            return null;
        }

        private static bool LooksLikeApplication(object? candidate)
            => TryGetProperty(candidate, "ActiveDocument", out _)
               || TryGetProperty(candidate, "Documents", out _);

        private static bool TryGetProperty(object? target, string name, out object? value)
        {
            value = null;
            if (target == null) return false;

            try
            {
                value = target.GetType().InvokeMember(
                    name,
                    BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                    null,
                    target,
                    null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static object? InvokeMethod(object? target, string methodName)
        {
            if (target == null) return null;

            try
            {
                return target.GetType().InvokeMember(
                    methodName,
                    BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
                    null,
                    target,
                    null);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Список объектов, зарегистрированных в Running Object Table.</summary>
        private static List<(string DisplayName, Guid? Clsid, object? Instance)> EnumerateRunningObjects()
        {
            var entries = new List<(string DisplayName, Guid? Clsid, object? Instance)>();

            try
            {
                if (GetRunningObjectTable(0, out IRunningObjectTable? rot) != 0 || rot == null) return entries;

                rot.EnumRunning(out IEnumMoniker enumMoniker);
                if (enumMoniker == null) return entries;

                IntPtr fetched = Marshal.AllocCoTaskMem(sizeof(int));
                try
                {
                    var monikers = new IMoniker[1];
                    while (enumMoniker.Next(1, monikers, fetched) == 0 && Marshal.ReadInt32(fetched) == 1)
                    {
                        IMoniker? moniker = monikers[0];
                        monikers[0] = null!;
                        if (moniker == null) continue;

                        string display = string.Empty;
                        try { moniker.GetDisplayName(null!, null!, out display); } catch { /* необязательно */ }

                        object? instance = null;
                        try
                        {
                            if (rot.GetObject(moniker, out object? bound) == 0) instance = bound;
                        }
                        catch
                        {
                            // объект мог уже завершиться
                        }

                        entries.Add((display ?? string.Empty, ParseClassClsid(display), instance));
                    }
                }
                finally
                {
                    Marshal.FreeCoTaskMem(fetched);
                }
            }
            catch
            {
                // Диагностика не должна мешать подключению.
            }

            return entries;
        }

        private static string FormatRotEntry((string DisplayName, Guid? Clsid, object? Instance) entry)
            => string.IsNullOrWhiteSpace(entry.DisplayName) ? "(без имени)" : entry.DisplayName;

        /// <summary>Класс-моникер в ROT выглядит как «!{CLSID}».</summary>
        private static Guid? ParseClassClsid(string? displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return null;

            string name = displayName.Trim();
            if (!name.StartsWith("!", StringComparison.Ordinal)) return null;

            name = name.Substring(1).Trim();
            if (name.StartsWith("{", StringComparison.Ordinal) && name.EndsWith("}", StringComparison.Ordinal))
                name = name.Substring(1, name.Length - 2);

            return Guid.TryParse(name, out Guid clsid) ? clsid : null;
        }

        private static List<string> GetKompasProcesses()
        {
            var result = new List<string>();

            try
            {
                foreach (Process process in Process.GetProcesses())
                {
                    try
                    {
                        string name = process.ProcessName;
                        if (!name.StartsWith("kompas", StringComparison.OrdinalIgnoreCase) &&
                            !name.StartsWith("ksinvisible", StringComparison.OrdinalIgnoreCase))
                            continue;

                        result.Add($"{name}.exe (PID {process.Id})");
                    }
                    catch
                    {
                        // защищённые процессы недоступны
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch
            {
                // перечисление процессов недоступно — не критично
            }

            return result;
        }

        private static string BuildConnectFailure(int registeredCount, int processCount, int lastHresult)
        {
            if (registeredCount == 0)
            {
                return "КОМПАС-3D не зарегистрирован в COM: не найден ProgID KOMPAS.Application.7. " +
                       "Проверьте установку, а если КОМПАС 32-битный — соберите приложение под x86. Подробности — кнопка «Диагностика».";
            }

            if (processCount == 0)
            {
                return "КОМПАС-3D не запущен. Запустите программу, откройте деталь .m3d и повторите подключение.";
            }

            string hint = Environment.IsPrivilegedProcess
                ? "Приложение запущено от администратора, а КОМПАС — с обычными правами. Запустите оба одинаково."
                : "Если КОМПАС запущен от администратора, а приложение — нет, уровни прав нужно уравнять.";

            return $"КОМПАС-3D запущен, но COM-объект недоступен (0x{lastHresult:X8}). {hint} " +
                   "Подробности — кнопка «Диагностика».";
        }

        private static string BuildRecommendations(int registeredCount, int processCount)
        {
            var lines = new List<string> { "Что проверить:" };

            if (registeredCount == 0)
            {
                lines.Add("1. КОМПАС-3D установлен и зарегистрирован в COM.");
                lines.Add("1a. Разрядность: 32-битный КОМПАС регистрирует COM только в WOW6432Node и не виден 64-битному приложению — соберите проект под x86.");
            }

            if (processCount == 0)
                lines.Add("1. КОМПАС-3D запущен и в нём открыта деталь .m3d.");

            lines.Add("2. Приложение и КОМПАС запущены с одинаковыми правами (оба от администратора либо оба без).");
            lines.Add("3. Оба работают в одной сессии Windows под одним пользователем.");
            lines.Add("4. Антивирус не блокирует межпроцессное взаимодействие.");

            return string.Join(Environment.NewLine, lines);
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

        /// <summary>Перечитывает указатель на активный документ (пользователь мог переключить файл).</summary>
        private void RefreshActiveDocument()
        {
            if (_kompasApp == null) return;
            var document = GetProperty(_kompasApp, "ActiveDocument");
            if (document != null) _activeDocument = document;
        }

        /// <summary>
        /// Читает всю геометрию активной детали: плоские грани с контурами и рёбра тел.
        /// </summary>
        public PartGeometry ReadPartGeometry(out string message)
        {
            var geometry = new PartGeometry
            {
                LoadedAt = DateTime.Now,
                Source = "kompas"
            };

            if (_kompasApp == null)
            {
                message = "Нет подключения к КОМПАС-3D";
                return geometry;
            }

            RefreshActiveDocument();

            if (_activeDocument == null)
            {
                message = "В КОМПАС-3D не открыт документ";
                return geometry;
            }

            geometry.DocumentName = GetActiveDocName();

            try
            {
                object? topPart = GetProperty(_activeDocument, "TopPart");
                object? bodies = GetProperty(topPart, "Bodies");
                int bodyCount = GetCount(bodies);

                if (bodyCount == 0)
                {
                    message = "В документе нет тел. Откройте деталь .m3d с твёрдым телом.";
                    return geometry;
                }

                int faceId = 0;
                int edgeId = 0;
                var loopEdges = new List<PartEdge>();

                for (int bodyIndex = 0; bodyIndex < bodyCount; bodyIndex++)
                {
                    object? body = GetItem(bodies, bodyIndex);
                    if (body == null) continue;

                    object? faces = GetProperty(body, "Faces");
                    int faceCount = GetCount(faces);

                    for (int faceIndex = 0; faceIndex < faceCount; faceIndex++)
                    {
                        object? face = GetItem(faces, faceIndex);
                        if (face == null) continue;
                        var partFace = ReadFace(face, ++faceId, loopEdges);
                        if (partFace != null) geometry.Faces.Add(partFace);
                    }

                    object? bodyEdges = GetProperty(body, "Edges");
                    int bodyEdgeCount = GetCount(bodyEdges);

                    for (int edgeIndex = 0; edgeIndex < bodyEdgeCount; edgeIndex++)
                    {
                        object? edge = GetItem(bodyEdges, edgeIndex);
                        var partEdge = ReadEdge(edge, ++edgeId, -1);
                        if (partEdge != null) geometry.Edges.Add(partEdge);
                    }
                }

                // Если тело не отдало коллекцию рёбер, берём рёбра из контуров граней.
                if (geometry.Edges.Count == 0 && loopEdges.Count > 0)
                {
                    foreach (var edge in loopEdges)
                    {
                        edge.Id = ++edgeId;
                        geometry.Edges.Add(edge);
                    }
                }

                geometry.RecalculateBounds();

                message = geometry.IsEmpty
                    ? "В активном документе не найдено плоских граней и рёбер."
                    : $"Геометрия загружена: граней {geometry.Faces.Count}, рёбер {geometry.Edges.Count}";

                return geometry;
            }
            catch (Exception ex)
            {
                message = $"Ошибка чтения геометрии: {ex.Message}";
                return geometry;
            }
        }

        private PartFace? ReadFace(object face, int id, List<PartEdge> loopEdges)
        {
            object? surface = GetProperty(face, "Geometry");
            int surfaceType = ToInt(GetProperty(surface, "SurfaceType"));

            // 1 = плоская поверхность. Неизвестный тип (0) тоже пробуем прочитать.
            if (surfaceType != 1 && surfaceType != 0) return null;

            object? origin = GetProperty(surface, "Origin");
            double z = ToDouble(GetMember(origin, "Z"));

            double normalX = 0, normalY = 0, normalZ = 1;
            object? normal = GetProperty(surface, "Normal");
            if (normal != null)
            {
                normalX = ToDouble(GetMember(normal, "X"));
                normalY = ToDouble(GetMember(normal, "Y"));
                normalZ = ToDouble(GetMember(normal, "Z"));
            }
            else
            {
                // Запасной путь: нормаль = AxisX × AxisY.
                var axisX = GetProperty(surface, "AxisX");
                var axisY = GetProperty(surface, "AxisY");
                if (axisX != null && axisY != null)
                {
                    double ax = ToDouble(GetMember(axisX, "X")), ay = ToDouble(GetMember(axisX, "Y")), az = ToDouble(GetMember(axisX, "Z"));
                    double bx = ToDouble(GetMember(axisY, "X")), by = ToDouble(GetMember(axisY, "Y")), bz = ToDouble(GetMember(axisY, "Z"));
                    normalX = ay * bz - az * by;
                    normalY = az * bx - ax * bz;
                    normalZ = ax * by - ay * bx;

                    double length = Math.Sqrt(normalX * normalX + normalY * normalY + normalZ * normalZ);
                    if (length > 1e-9)
                    {
                        normalX /= length; normalY /= length; normalZ /= length;
                    }
                    else
                    {
                        normalX = normalY = 0; normalZ = 1;
                    }
                }
            }

            var partFace = new PartFace
            {
                Id = id,
                Name = $"Грань #{id}",
                Z = z,
                NormalX = normalX,
                NormalY = normalY,
                NormalZ = normalZ
            };

            object? loops = GetProperty(face, "Loops");
            int loopCount = GetCount(loops);

            for (int loopIndex = 0; loopIndex < loopCount; loopIndex++)
            {
                object? loop = GetItem(loops, loopIndex);
                if (loop == null) continue;

                bool isOuter = loopIndex == 0;
                object? outerFlag = GetProperty(loop, "IsOuter");
                if (outerFlag is bool flag) isOuter = flag;

                var partLoop = new PartLoop { IsOuter = isOuter };
                object? edges = GetProperty(loop, "Edges");
                int edgeCount = GetCount(edges);

                for (int edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
                {
                    object? edge = GetItem(edges, edgeIndex);
                    var partEdge = ReadEdge(edge, 0, id);
                    if (partEdge == null) continue;

                    loopEdges.Add(partEdge);

                    var sampled = GeometryMath.SampleEdge(partEdge);
                    foreach (var point in sampled)
                    {
                        if (partLoop.Points.Count == 0 || !GeometryMath.SamePoint(partLoop.Points[partLoop.Points.Count - 1], point))
                            partLoop.Points.Add(point);
                    }
                }

                var normalized = GeometryMath.NormalizeLoop(partLoop.Points);
                if (normalized.Count >= 3)
                {
                    partLoop.Points = normalized;
                    partFace.Loops.Add(partLoop);
                }
            }

            if (partFace.Loops.Count == 0) return null;

            partFace.Area = GeometryMath.FaceArea(partFace);
            return partFace;
        }

        private PartEdge? ReadEdge(object? edge, int id, int faceId)
        {
            if (edge == null) return null;

            object? edgeGeometry = GetProperty(edge, "Geometry");
            object? startPoint = GetProperty(edgeGeometry, "PointStart");
            object? endPoint = GetProperty(edgeGeometry, "PointEnd");

            if (startPoint == null && endPoint == null) return null;

            var start = ToPoint2D(startPoint ?? endPoint);
            var end = ToPoint2D(endPoint ?? startPoint);

            var partEdge = new PartEdge
            {
                Id = id,
                Name = $"Ребро #{id}",
                Kind = PartCurveKind.Line,
                Start = start,
                End = end,
                Z = ToDouble(GetMember(startPoint ?? endPoint, "Z")),
                FaceId = faceId
            };

            object? center = GetProperty(edgeGeometry, "Center");
            object? radiusValue = GetProperty(edgeGeometry, "Radius");

            if (center != null && radiusValue != null)
            {
                double radius = ToDouble(radiusValue);
                if (radius > 1e-6)
                {
                    partEdge.Kind = PartCurveKind.Arc;
                    partEdge.Center = ToPoint2D(center);
                    partEdge.Radius = radius;
                    partEdge.Clockwise = true;

                    double a0 = Math.Atan2(start.Y - partEdge.Center.Y, start.X - partEdge.Center.X);
                    double a1 = Math.Atan2(end.Y - partEdge.Center.Y, end.X - partEdge.Center.X);
                    double ccw = GeometryMath.NormalizeAngle(a1 - a0);

                    bool fullCircle = GeometryMath.SamePoint(start, end, 1e-3);
                    if (fullCircle)
                    {
                        partEdge.Sweep = Math.PI * 2;
                    }
                    else if (ccw <= Math.PI)
                    {
                        // Кратчайшая дуга против часовой.
                        partEdge.Clockwise = false;
                        partEdge.Sweep = ccw;
                    }
                    else
                    {
                        // Кратчайшая дуга по часовой.
                        partEdge.Clockwise = true;
                        partEdge.Sweep = Math.PI * 2 - ccw;
                    }
                }
            }

            return partEdge;
        }

        /// <summary>
        /// Устаревший сценарий: контур выделенной плоской грани.
        /// Оставлен для совместимости; основной путь — <see cref="ReadPartGeometry"/>.
        /// </summary>
        public List<TrochoidEngine.Point2D> GetSelectedFaceContours(out double faceZ)
        {
            faceZ = 0.0;
            var points = new List<TrochoidEngine.Point2D>();
            if (_activeDocument == null) return points;

            try
            {
                object? selectionManager = GetProperty(_activeDocument, "SelectionManager");
                object? selectedObjects = GetProperty(selectionManager, "SelectedObjects");
                int count = GetCount(selectedObjects);
                if (count == 0) return points;

                object? selectedObject = GetItem(selectedObjects, 0);
                if (selectedObject == null) return points;

                int objType = ToInt(GetProperty(selectedObject, "ObjectType"));
                if (objType != 4) return points; // oType_Face

                object? surface = GetProperty(selectedObject, "Geometry");
                int surfaceType = ToInt(GetProperty(surface, "SurfaceType"));
                if (surfaceType != 1) return points;

                object? planeOrigin = GetProperty(surface, "Origin");
                faceZ = ToDouble(GetMember(planeOrigin, "Z"));

                object? loops = GetProperty(selectedObject, "Loops");
                int loopsCount = GetCount(loops);
                if (loopsCount == 0) return points;

                object? outerLoop = GetItem(loops, 0);
                object? edges = GetProperty(outerLoop, "Edges");
                int edgesCount = GetCount(edges);

                for (int i = 0; i < edgesCount; i++)
                {
                    object? edge = GetItem(edges, i);
                    object? edgeGeometry = GetProperty(edge, "Geometry");
                    object? startPoint = GetProperty(edgeGeometry, "PointStart");
                    if (startPoint == null) continue;
                    points.Add(ToPoint2D(startPoint));
                }

                if (points.Count > 0) points.Add(points[0]);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка парсинга грани: {ex.Message}");
            }

            return points;
        }
    }
}
