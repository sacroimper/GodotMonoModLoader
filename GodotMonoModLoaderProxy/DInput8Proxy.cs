using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GodotMonoModLoaderProxy;

internal static unsafe class DInput8Proxy
{
    private const int S_OK = 0;
    private const int E_FAIL = unchecked((int)0x80004005);
    private const uint HostFxrLoadAssemblyAndGetFunctionPointer = 5;
    private static readonly object Sync = new();
    private static nint _realDInput8;
    private static delegate* unmanaged<nint, uint, nint, nint*, nint, int> _directInput8Create;
    private static delegate* unmanaged<int> _dllCanUnloadNow;
    private static delegate* unmanaged<nint, nint, nint*, int> _dllGetClassObject;
    private static delegate* unmanaged<int> _dllRegisterServer;
    private static delegate* unmanaged<int> _dllUnregisterServer;
    private static int _bootstrapStarted;
    private static string? _bootstrapLogPath;

    [UnmanagedCallersOnly(EntryPoint = "DirectInput8Create")]
    public static int DirectInput8Create(
        nint instance,
        uint version,
        nint interfaceId,
        nint* output,
        nint outer)
    {
        StartBootstrap();
        return EnsureRealDInput8() && _directInput8Create != null
            ? _directInput8Create(instance, version, interfaceId, output, outer)
            : E_FAIL;
    }

    [UnmanagedCallersOnly(EntryPoint = "DllCanUnloadNow")]
    public static int DllCanUnloadNow()
    {
        StartBootstrap();
        return EnsureRealDInput8() && _dllCanUnloadNow != null
            ? _dllCanUnloadNow()
            : E_FAIL;
    }

    [UnmanagedCallersOnly(EntryPoint = "DllGetClassObject")]
    public static int DllGetClassObject(nint classId, nint interfaceId, nint* output)
    {
        StartBootstrap();
        return EnsureRealDInput8() && _dllGetClassObject != null
            ? _dllGetClassObject(classId, interfaceId, output)
            : E_FAIL;
    }

    [UnmanagedCallersOnly(EntryPoint = "DllRegisterServer")]
    public static int DllRegisterServer()
    {
        StartBootstrap();
        return EnsureRealDInput8() && _dllRegisterServer != null
            ? _dllRegisterServer()
            : E_FAIL;
    }

    [UnmanagedCallersOnly(EntryPoint = "DllUnregisterServer")]
    public static int DllUnregisterServer()
    {
        StartBootstrap();
        return EnsureRealDInput8() && _dllUnregisterServer != null
            ? _dllUnregisterServer()
            : E_FAIL;
    }

    private static bool EnsureRealDInput8()
    {
        if (_realDInput8 != 0)
        {
            return true;
        }

        lock (Sync)
        {
            if (_realDInput8 != 0)
            {
                return true;
            }

            char[] systemDirectory = new char[32768];
            uint length = GetSystemDirectory(systemDirectory, systemDirectory.Length);
            if (length == 0 || length >= systemDirectory.Length)
            {
                return false;
            }

            string path = string.Concat(
                new string(systemDirectory, 0, (int)length),
                "\\dinput8.dll");
            nint module = LoadLibrary(path);
            if (module == 0)
            {
                return false;
            }

            _directInput8Create = (delegate* unmanaged<nint, uint, nint, nint*, nint, int>)
                GetProcAddress(module, "DirectInput8Create");
            _dllCanUnloadNow = (delegate* unmanaged<int>)GetProcAddress(module, "DllCanUnloadNow");
            _dllGetClassObject = (delegate* unmanaged<nint, nint, nint*, int>)
                GetProcAddress(module, "DllGetClassObject");
            _dllRegisterServer = (delegate* unmanaged<int>)GetProcAddress(module, "DllRegisterServer");
            _dllUnregisterServer = (delegate* unmanaged<int>)GetProcAddress(module, "DllUnregisterServer");

            if (_directInput8Create == null)
            {
                FreeLibrary(module);
                return false;
            }

            _realDInput8 = module;
            return true;
        }
    }

    private static void StartBootstrap()
    {
        if (Interlocked.Exchange(ref _bootstrapStarted, 1) != 0)
        {
            return;
        }

        Thread thread = new(BootstrapThread)
        {
            IsBackground = true,
            Name = "GodotMonoModLoader bootstrap"
        };
        thread.Start();
    }

    private static void BootstrapThread()
    {
        string directory = AppContext.BaseDirectory;
        string runtimeConfig = Path.Combine(
            directory,
            "GodotMonoModLoader",
            "Bootstrap",
            "GodotMonoModLoaderBootstrap.runtimeconfig.json");
        string assembly = Path.Combine(directory, "GodotMonoModLoader", "Bootstrap", "GodotMonoModLoaderBootstrap.dll");
        string hostFxrPath = Path.Combine(directory, "data_Atomcraft_windows_x86_64", "hostfxr.dll");
        string logPath = Path.Combine(
            directory,
            "GodotMonoModLoader",
            "Logs",
            "GodotMonoModLoaderBootstrap.log");
        _bootstrapLogPath = logPath;

        WriteBootstrapLog(
            logPath,
            $"Proxy base directory: {directory}{Environment.NewLine}" +
            $"Bootstrap runtime config: {runtimeConfig} (exists={File.Exists(runtimeConfig)}){Environment.NewLine}" +
            $"hostfxr: {hostFxrPath} (exists={File.Exists(hostFxrPath)}){Environment.NewLine}" +
            $"Bootstrap assembly: {assembly} (exists={File.Exists(assembly)})");

        if (!File.Exists(runtimeConfig) ||
            !File.Exists(assembly) ||
            !File.Exists(hostFxrPath))
        {
            return;
        }
        
        for (int attempt = 0; attempt < 600; attempt++)
        {
            if (GetModuleHandle("coreclr.dll") != 0)
            {
                break;
            }

            Thread.Sleep(50);
        }

        if (GetModuleHandle("coreclr.dll") == 0)
        {
            WriteBootstrapLog(logPath, "CoreCLR was not loaded before timeout.");
            return;
        }

        nint hostFxr = LoadLibrary(hostFxrPath);
        if (hostFxr == 0)
        {
            WriteBootstrapLog(logPath, $"LoadLibrary(hostfxr) failed. Win32 error: {Marshal.GetLastWin32Error()}");
            return;
        }

        var initialize = (delegate* unmanaged<char*, nint, nint*, int>)
            GetProcAddress(hostFxr, "hostfxr_initialize_for_runtime_config");
        var getDelegate = (delegate* unmanaged<nint, uint, nint*, int>)
            GetProcAddress(hostFxr, "hostfxr_get_runtime_delegate");
        var close = (delegate* unmanaged<nint, int>)
            GetProcAddress(hostFxr, "hostfxr_close");
        var setErrorWriter = (delegate* unmanaged<nint, void>)
            GetProcAddress(hostFxr, "hostfxr_set_error_writer");
        if (initialize == null || getDelegate == null || close == null || setErrorWriter == null)
        {
            WriteBootstrapLog(logPath, "Required hostfxr exports were not found.");
            return;
        }
        setErrorWriter((nint)(delegate* unmanaged<char*, void>)&HostFxrErrorWriter);

        nint context = 0;
        nint loadAssembly = 0;
        nint entryPoint = 0;
        fixed (char* runtimeConfigPointer = runtimeConfig)
        fixed (char* assemblyPointer = assembly)
        fixed (char* typeNamePointer = "GodotMonoModLoaderBootstrap.Bootstrap, GodotMonoModLoaderBootstrap, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null")
        fixed (char* methodNamePointer = "Initialize")
        fixed (char* delegateTypePointer = "GodotMonoModLoaderBootstrap.BootstrapEntryPoint, GodotMonoModLoaderBootstrap")
        {
            int initializeResult = initialize(runtimeConfigPointer, 0, &context);
            const int HostAlreadyInitialized = 1;
            if ((initializeResult != S_OK && initializeResult != HostAlreadyInitialized) ||
                context == 0)
            {
                WriteBootstrapLog(
                    logPath,
                    $"hostfxr_initialize_for_runtime_config returned 0x{initializeResult:X8}; context=0x{context:X}");
                return;
            }

            int delegateResult = getDelegate(
                context,
                HostFxrLoadAssemblyAndGetFunctionPointer,
                &loadAssembly);
            if (delegateResult != S_OK || loadAssembly == 0)
            {
                WriteBootstrapLog(
                    logPath,
                    $"hostfxr_get_runtime_delegate returned 0x{delegateResult:X8}; delegate=0x{loadAssembly:X}");
                close(context);
                return;
            }

            close(context);
            var loadAssemblyAndGetFunctionPointer =
                (delegate* unmanaged<char*, char*, char*, char*, nint, nint*, int>)loadAssembly;
            int loadAssemblyResult = loadAssemblyAndGetFunctionPointer(
                assemblyPointer,
                typeNamePointer,
                methodNamePointer,
                delegateTypePointer,
                0,
                &entryPoint);
            
            if (loadAssemblyResult != S_OK ||
                entryPoint == 0)
            {
                WriteBootstrapLog(
                    logPath,
                    $"hostfxr_load_assembly_and_get_function_pointer returned 0x{loadAssemblyResult:X8}; " +
                    $"entryPoint=0x{entryPoint:X}; " +
                    "type=GodotMonoModLoaderBootstrap.Bootstrap, GodotMonoModLoaderBootstrap, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null; " +
                    "method=Initialize; delegate=GodotMonoModLoaderBootstrap.BootstrapEntryPoint, GodotMonoModLoaderBootstrap");
                return;
            }
        }

        ((delegate* unmanaged<void>)entryPoint)();
    }

    [UnmanagedCallersOnly]
    private static void HostFxrErrorWriter(char* message)
    {
        if (message == null || _bootstrapLogPath == null)
        {
            return;
        }

        WriteBootstrapLog(_bootstrapLogPath, $"hostfxr: {new string(message)}");
    }

    private static void WriteBootstrapLog(string path, string message)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(
                path,
                $"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostics must not terminate the game process.
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetSystemDirectory([Out] char[] buffer, int size);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint LoadLibrary(string path);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern nint GetProcAddress(nint module, string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(nint module);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern nint GetModuleHandle(string name);
}
