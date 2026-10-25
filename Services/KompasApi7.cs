using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace KompasTrochoidCam.Services
{
    /// <summary>
    /// Позднее связывание с интерфейсами КОМПАС API 7, которые не являются
    /// интерфейсом по умолчанию.
    ///
    /// Обычный <c>Type.InvokeMember</c> работает только с интерфейсом по умолчанию:
    /// у <c>TopPart</c> это <c>IPart7</c>, в котором нет <c>ModelObjects</c> — отсюда
    /// DISP_E_UNKNOWNNAME. Нужный интерфейс (<c>IFeature7</c> и т. п.) получается
    /// через QueryInterface, а вызовы идут прямо через IDispatch-слоты его vtable —
    /// так же, как это делает pywin32 в <c>CastTo(obj, "IFeature7")</c>.
    /// </summary>
    internal static class KompasApi7
    {
        /// <summary>LIBID библиотеки типов API 7 (KompasAPI7).</summary>
        private static readonly Guid TypeLibId = new Guid("69AC2981-37C0-4379-84FD-5DD2F3C0A520");

        private const ushort MajorVersion = 1;
        private const ushort MinorVersion = 0;
        private const int RegKindNone = 0;

        private static readonly Dictionary<string, Guid> InterfaceIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        private static readonly object Sync = new object();

        private static ITypeLib? _typeLib;
        private static bool _typeLibChecked;

        [DllImport("oleaut32.dll", PreserveSig = true)]
        private static extern int LoadRegTypeLib(ref Guid rclsid, ushort wMajorVerNum, ushort wMinorVerNum, int lcid,
            [MarshalAs(UnmanagedType.Interface)] out ITypeLib? ptlib);

        [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int LoadTypeLibEx(string szFile, int regKind,
            [MarshalAs(UnmanagedType.Interface)] out ITypeLib? ptlib);

        /// <summary>Сообщение о последней неудаче — попадает в отчёт «Диагностика».</summary>
        public static string LastError { get; private set; } = string.Empty;

        /// <summary>
        /// Приводит COM-объект к интерфейсу по имени (QueryInterface) и возвращает
        /// обёртку для вызова его членов. null, если интерфейс недоступен.
        /// </summary>
        public static ComDispatch? Cast(object? comObject, string interfaceName)
        {
            if (comObject == null) return null;

            Guid? iid = GetInterfaceId(interfaceName);
            if (iid == null) return null;

            IntPtr unknown = IntPtr.Zero;
            try
            {
                unknown = Marshal.GetIUnknownForObject(comObject);

                Guid id = iid.Value;
                int hr = Marshal.QueryInterface(unknown, ref id, out IntPtr iface);
                if (hr != 0)
                {
                    LastError = $"QueryInterface({interfaceName}) = 0x{hr:X8}";
                    return null;
                }

                return new ComDispatch(iface, interfaceName);
            }
            catch (Exception ex)
            {
                LastError = $"{interfaceName}: {ex.Message}";
                return null;
            }
            finally
            {
                if (unknown != IntPtr.Zero) Marshal.Release(unknown);
            }
        }

        /// <summary>IID интерфейса из библиотеки типов КОМПАС API 7.</summary>
        private static Guid? GetInterfaceId(string interfaceName)
        {
            lock (Sync)
            {
                if (InterfaceIds.TryGetValue(interfaceName, out Guid cached)) return cached;

                ITypeLib? typeLib = GetTypeLib();
                if (typeLib == null) return null;

                Guid? iid = FindInterfaceId(typeLib, interfaceName);
                if (iid != null) InterfaceIds[interfaceName] = iid.Value;

                LastError = iid == null
                    ? $"в библиотеке типов КОМПАС API 7 нет интерфейса {interfaceName}"
                    : string.Empty;

                return iid;
            }
        }

        private static ITypeLib? GetTypeLib()
        {
            if (_typeLibChecked) return _typeLib;
            _typeLibChecked = true;

            Guid libId = TypeLibId;
            int hr = LoadRegTypeLib(ref libId, MajorVersion, MinorVersion, 0, out ITypeLib? registered);
            if (hr == 0 && registered != null)
            {
                _typeLib = registered;
                return _typeLib;
            }

            // Библиотека типов не зарегистрирована — ищем файл рядом с КОМПАС.
            foreach (string file in FindTypeLibFiles())
            {
                if (LoadTypeLibEx(file, RegKindNone, out ITypeLib? loaded) == 0 && loaded != null)
                {
                    _typeLib = loaded;
                    return _typeLib;
                }
            }

            LastError = "библиотека типов КОМПАС API 7 не найдена (LoadRegTypeLib = 0x" + hr.ToString("X8") + ")";
            return null;
        }

        private static IEnumerable<string> FindTypeLibFiles()
        {
            var folders = new List<string>();

            try
            {
                foreach (System.Diagnostics.Process process in System.Diagnostics.Process.GetProcesses())
                {
                    try
                    {
                        string name = process.ProcessName;
                        if (!name.StartsWith("kompas", StringComparison.OrdinalIgnoreCase) &&
                            !name.StartsWith("ksinvisible", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string? directory = Path.GetDirectoryName(process.MainModule?.FileName);
                        if (!string.IsNullOrEmpty(directory)) folders.Add(directory);
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
                // перечисление процессов недоступно
            }

            folders.Add(AppContext.BaseDirectory);

            foreach (string folder in folders)
            {
                string[] files;
                try
                {
                    files = Directory.GetFiles(folder, "*.tlb");
                }
                catch
                {
                    continue;
                }

                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    if (fileName.IndexOf("7", StringComparison.Ordinal) >= 0 &&
                        fileName.IndexOf("ompas", StringComparison.OrdinalIgnoreCase) >= 0)
                        yield return file;
                }
            }
        }

        private static Guid? FindInterfaceId(ITypeLib typeLib, string interfaceName)
        {
            int count = 0;
            try { count = typeLib.GetTypeInfoCount(); }
            catch { return null; }

            for (int i = 0; i < count; i++)
            {
                ITypeInfo? info = null;
                IntPtr attr = IntPtr.Zero;
                try
                {
                    typeLib.GetTypeInfo(i, out info);
                    if (info == null) continue;

                    info.GetDocumentation(-1, out string typeName, out _, out _, out _);
                    if (!string.Equals(typeName, interfaceName, StringComparison.OrdinalIgnoreCase)) continue;

                    info.GetTypeAttr(out attr);
                    if (attr == IntPtr.Zero) continue;

                    byte[] buffer = new byte[16];
                    Marshal.Copy(attr, buffer, 0, buffer.Length);
                    return new Guid(buffer);
                }
                catch
                {
                    // тип без документации — пропускаем
                }
                finally
                {
                    if (attr != IntPtr.Zero && info != null)
                    {
                        try { info.ReleaseTypeAttr(attr); } catch { /* уже освобождён */ }
                    }

                    if (info != null) Marshal.ReleaseComObject(info);
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Обёртка над интерфейсом КОМПАС, полученным через QueryInterface.
    /// Вызовы идут через IDispatch-слоты vtable самого интерфейса.
    /// </summary>
    internal sealed class ComDispatch : IDisposable
    {
        private readonly IntPtr _pointer;
        private readonly GetIdsDelegate? _getIdsOfNames;
        private readonly InvokeMethodDelegate? _invoke;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetIdsDelegate(ref Guid riid, IntPtr rgszNames, uint cNames, int lcid, IntPtr rgDispId);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int InvokeMethodDelegate(int dispIdMember, ref Guid riid, int lcid, ushort wFlags,
            ref Dispparams pDispParams, IntPtr pVarResult, IntPtr pExcepInfo, IntPtr puArgErr);

        [StructLayout(LayoutKind.Sequential)]
        private struct Dispparams
        {
            public IntPtr rgvarg;
            public IntPtr rgdispidNamedArgs;
            public int cArgs;
            public int cNamedArgs;
        }

        [DllImport("oleaut32.dll", PreserveSig = true)]
        private static extern int VariantClear(IntPtr pvarg);

        public ComDispatch(IntPtr pointer, string interfaceName)
        {
            _pointer = pointer;
            InterfaceName = interfaceName;

            IntPtr vtable = Marshal.ReadIntPtr(pointer);
            IntPtr getIds = Marshal.ReadIntPtr(vtable, 5 * IntPtr.Size);
            IntPtr invoke = Marshal.ReadIntPtr(vtable, 6 * IntPtr.Size);

            if (getIds != IntPtr.Zero) _getIdsOfNames = Marshal.GetDelegateForFunctionPointer<GetIdsDelegate>(getIds);
            if (invoke != IntPtr.Zero) _invoke = Marshal.GetDelegateForFunctionPointer<InvokeMethodDelegate>(invoke);
        }

        public string InterfaceName { get; }

        private static int VariantSize => IntPtr.Size == 8 ? 24 : 16;

        /// <summary>Чтение свойства или вызов метода интерфейса.</summary>
        public object? Get(string member, params object?[] args)
            => InvokeMember(member, args, isProperty: true);

        /// <summary>Вызов метода интерфейса.</summary>
        public object? Call(string member, params object?[] args)
            => InvokeMember(member, args, isProperty: false);

        private object? InvokeMember(string member, object?[] args, bool isProperty)
        {
            if (_pointer == IntPtr.Zero || _getIdsOfNames == null || _invoke == null) return null;

            IntPtr namePtr = IntPtr.Zero;
            IntPtr nameArray = IntPtr.Zero;
            IntPtr dispIds = IntPtr.Zero;
            IntPtr argumentBlock = IntPtr.Zero;
            IntPtr resultPtr = IntPtr.Zero;

            try
            {
                namePtr = Marshal.StringToCoTaskMemUni(member);
                nameArray = Marshal.AllocCoTaskMem(IntPtr.Size);
                Marshal.WriteIntPtr(nameArray, namePtr);

                dispIds = Marshal.AllocCoTaskMem(sizeof(int));

                Guid riid = Guid.Empty;
                if (_getIdsOfNames(ref riid, nameArray, 1, 0, dispIds) != 0) return null;

                int dispId = Marshal.ReadInt32(dispIds);

                int argumentCount = args?.Length ?? 0;
                if (argumentCount > 0)
                {
                    argumentBlock = Marshal.AllocCoTaskMem(VariantSize * argumentCount);
                    for (int i = 0; i < argumentCount; i++)
                    {
                        // В DISPPARAMS аргументы лежат в обратном порядке.
                        IntPtr slot = argumentBlock + VariantSize * (argumentCount - 1 - i);
                        Marshal.GetNativeVariantForObject(args![i], slot);
                    }
                }

                resultPtr = Marshal.AllocCoTaskMem(VariantSize);
                Marshal.WriteInt16(resultPtr, 0, 0); // VT_EMPTY

                var parameters = new Dispparams
                {
                    rgvarg = argumentBlock,
                    rgdispidNamedArgs = IntPtr.Zero,
                    cArgs = argumentCount,
                    cNamedArgs = 0
                };

                ushort flags = isProperty ? (ushort)0x0002 : (ushort)0x0001;
                int hr = _invoke(dispId, ref riid, 0, flags, ref parameters, resultPtr, IntPtr.Zero, IntPtr.Zero);

                if (hr != 0)
                {
                    KompasApi7Diagnostics.Report($"{InterfaceName}.{member}: 0x{hr:X8}");
                    return null;
                }

                return Marshal.GetObjectForNativeVariant(resultPtr);
            }
            catch (Exception ex)
            {
                KompasApi7Diagnostics.Report($"{InterfaceName}.{member}: {ex.Message}");
                return null;
            }
            finally
            {
                if (resultPtr != IntPtr.Zero)
                {
                    try { VariantClear(resultPtr); } catch { /* пустой вариант */ }
                    Marshal.FreeCoTaskMem(resultPtr);
                }

                if (argumentBlock != IntPtr.Zero)
                {
                    for (int i = 0; i < (args?.Length ?? 0); i++)
                    {
                        try { VariantClear(argumentBlock + VariantSize * i); } catch { /* уже очищен */ }
                    }

                    Marshal.FreeCoTaskMem(argumentBlock);
                }

                if (dispIds != IntPtr.Zero) Marshal.FreeCoTaskMem(dispIds);
                if (nameArray != IntPtr.Zero) Marshal.FreeCoTaskMem(nameArray);
                if (namePtr != IntPtr.Zero) Marshal.FreeCoTaskMem(namePtr);
            }
        }

        public void Dispose()
        {
            if (_pointer != IntPtr.Zero) Marshal.Release(_pointer);
        }
    }

    /// <summary>Журнал обращений к COM-интерфейсам КОМПАС (для окна «Диагностика»).</summary>
    internal static class KompasApi7Diagnostics
    {
        private static readonly List<string> Entries = new List<string>();

        public static void Report(string message)
        {
            lock (Entries)
            {
                Entries.Add(message);
                if (Entries.Count > 200) Entries.RemoveAt(0);
            }
        }

        public static void Clear()
        {
            lock (Entries) Entries.Clear();
        }

        public static IReadOnlyList<string> Snapshot()
        {
            lock (Entries) return Entries.ToArray();
        }
    }
}
