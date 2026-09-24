#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <unknwn.h>
#include <dinput.h>

#include <atomic>
#include <filesystem>
#include <fstream>
#include <string>

namespace
{
    using DirectInput8CreateFn = HRESULT(WINAPI*)(HINSTANCE, DWORD, REFIID, LPVOID*, LPUNKNOWN);
    using DllCanUnloadNowFn = HRESULT(WINAPI*)();
    using DllGetClassObjectFn = HRESULT(WINAPI*)(REFCLSID, REFIID, LPVOID*);
    using DllRegisterServerFn = HRESULT(WINAPI*)();
    using DllUnregisterServerFn = HRESULT(WINAPI*)();
    using GetdfDIJoystickFn = LPCDIDATAFORMAT(WINAPI*)();

    using hostfxr_initialize_for_runtime_config_fn = int(*)(const wchar_t*, void*, void**);
    using hostfxr_get_runtime_delegate_fn = int(*)(void*, int, void**);
    using hostfxr_close_fn = int(*)(void*);
    using hostfxr_set_error_writer_fn = void(*)(void*);
    using load_assembly_and_get_function_pointer_fn =
        int(*)(const wchar_t*, const wchar_t*, const wchar_t*, const wchar_t*, void*, void**);
    using bootstrap_entry_point_fn = void(*)();

    constexpr int HostFxrLoadAssemblyAndGetFunctionPointer = 5;
    constexpr int HostAlreadyInitialized = 1;

    std::atomic<bool> g_bootstrapStarted = false;
    std::atomic<bool> g_proxyInitialized = false;
    HMODULE g_realDInput8 = nullptr;
    DirectInput8CreateFn g_directInput8Create = nullptr;
    DllCanUnloadNowFn g_dllCanUnloadNow = nullptr;
    DllGetClassObjectFn g_dllGetClassObject = nullptr;
    DllRegisterServerFn g_dllRegisterServer = nullptr;
    DllUnregisterServerFn g_dllUnregisterServer = nullptr;
    GetdfDIJoystickFn g_getdfDIJoystick = nullptr;
    std::wstring g_logPath;

    std::wstring GetGameDirectory()
    {
        wchar_t buffer[MAX_PATH] = {};
        DWORD length = GetModuleFileNameW(nullptr, buffer, ARRAYSIZE(buffer));
        if (length == 0 || length == ARRAYSIZE(buffer))
        {
            return {};
        }

        std::filesystem::path path(buffer, buffer + length);
        return path.parent_path().wstring();
    }

    void WriteLog(const std::wstring& message)
    {
        if (g_logPath.empty())
        {
            return;
        }

        std::error_code error;
        std::filesystem::create_directories(
            std::filesystem::path(g_logPath).parent_path(),
            error);

        std::wofstream log(g_logPath, std::ios::app);
        if (log)
        {
            SYSTEMTIME time = {};
            GetSystemTime(&time);
            log << L"[" << time.wYear << L"-"
                << (time.wMonth < 10 ? L"0" : L"") << time.wMonth << L"-"
                << (time.wDay < 10 ? L"0" : L"") << time.wDay << L"T"
                << (time.wHour < 10 ? L"0" : L"") << time.wHour << L":"
                << (time.wMinute < 10 ? L"0" : L"") << time.wMinute << L":"
                << (time.wSecond < 10 ? L"0" : L"") << time.wSecond << L"Z] "
                << message << std::endl;
        }
    }

    bool LoadRealDInput8()
    {
        if (g_proxyInitialized)
        {
            return true;
        }

        wchar_t systemDirectory[MAX_PATH] = {};
        UINT length = GetSystemDirectoryW(systemDirectory, ARRAYSIZE(systemDirectory));
        if (length == 0 || length >= ARRAYSIZE(systemDirectory))
        {
            return false;
        }

        std::filesystem::path realPath(systemDirectory);
        realPath /= L"dinput8.dll";
        HMODULE module = LoadLibraryW(realPath.c_str());
        if (module == nullptr)
        {
            return false;
        }

        g_directInput8Create = reinterpret_cast<DirectInput8CreateFn>(
            GetProcAddress(module, "DirectInput8Create"));
        g_dllCanUnloadNow = reinterpret_cast<DllCanUnloadNowFn>(
            GetProcAddress(module, "DllCanUnloadNow"));
        g_dllGetClassObject = reinterpret_cast<DllGetClassObjectFn>(
            GetProcAddress(module, "DllGetClassObject"));
        g_dllRegisterServer = reinterpret_cast<DllRegisterServerFn>(
            GetProcAddress(module, "DllRegisterServer"));
        g_dllUnregisterServer = reinterpret_cast<DllUnregisterServerFn>(
            GetProcAddress(module, "DllUnregisterServer"));
        g_getdfDIJoystick = reinterpret_cast<GetdfDIJoystickFn>(
            GetProcAddress(module, "GetdfDIJoystick"));

        if (g_directInput8Create == nullptr)
        {
            FreeLibrary(module);
            return false;
        }

        g_realDInput8 = module;
        g_proxyInitialized = true;
        return true;
    }

    void BootstrapThread()
    {
        const std::wstring gameDirectory = GetGameDirectory();
        if (gameDirectory.empty())
        {
            return;
        }

        const std::filesystem::path loaderDirectory =
            std::filesystem::path(gameDirectory) / L"GodotMonoModLoader";
        const std::filesystem::path runtimeConfig =
            loaderDirectory / L"Bootstrap" / L"GodotMonoModLoaderBootstrap.runtimeconfig.json";
        const std::filesystem::path bootstrapAssembly =
            loaderDirectory / L"Bootstrap" / L"GodotMonoModLoaderBootstrap.dll";
        const std::filesystem::path hostFxrPath =
            std::filesystem::path(gameDirectory) /
            L"data_Atomcraft_windows_x86_64" /
            L"hostfxr.dll";
        g_logPath = (loaderDirectory / L"Logs" / L"GodotMonoModLoaderBootstrap.log").wstring();

        WriteLog(L"Native bootstrap started.");

        for (int attempt = 0; attempt < 600; ++attempt)
        {
            if (GetModuleHandleW(L"coreclr.dll") != nullptr &&
                std::filesystem::exists(runtimeConfig) &&
                std::filesystem::exists(bootstrapAssembly) &&
                std::filesystem::exists(hostFxrPath))
            {
                break;
            }
            Sleep(50);
        }

        if (GetModuleHandleW(L"coreclr.dll") == nullptr)
        {
            WriteLog(L"CoreCLR was not loaded before timeout.");
            return;
        }

        HMODULE hostFxr = LoadLibraryW(hostFxrPath.c_str());
        if (hostFxr == nullptr)
        {
            WriteLog(L"Could not load hostfxr.dll.");
            return;
        }

        auto initialize = reinterpret_cast<hostfxr_initialize_for_runtime_config_fn>(
            GetProcAddress(hostFxr, "hostfxr_initialize_for_runtime_config"));
        auto getDelegate = reinterpret_cast<hostfxr_get_runtime_delegate_fn>(
            GetProcAddress(hostFxr, "hostfxr_get_runtime_delegate"));
        auto close = reinterpret_cast<hostfxr_close_fn>(
            GetProcAddress(hostFxr, "hostfxr_close"));
        auto setErrorWriter = reinterpret_cast<hostfxr_set_error_writer_fn>(
            GetProcAddress(hostFxr, "hostfxr_set_error_writer"));

        if (initialize == nullptr || getDelegate == nullptr ||
            close == nullptr || setErrorWriter == nullptr)
        {
            WriteLog(L"Required hostfxr exports were not found.");
            return;
        }

        void* context = nullptr;
        int result = initialize(runtimeConfig.c_str(), nullptr, &context);
        if ((result != 0 && result != HostAlreadyInitialized) || context == nullptr)
        {
            WriteLog(L"hostfxr_initialize_for_runtime_config failed.");
            return;
        }

        void* loadAssembly = nullptr;
        result = getDelegate(
            context,
            HostFxrLoadAssemblyAndGetFunctionPointer,
            &loadAssembly);
        close(context);
        if (result != 0 || loadAssembly == nullptr)
        {
            WriteLog(L"hostfxr_get_runtime_delegate failed.");
            return;
        }

        auto loadAssemblyAndGetFunctionPointer =
            reinterpret_cast<load_assembly_and_get_function_pointer_fn>(loadAssembly);
        void* entryPoint = nullptr;
        result = loadAssemblyAndGetFunctionPointer(
            bootstrapAssembly.c_str(),
            L"GodotMonoModLoaderBootstrap.Bootstrap, GodotMonoModLoaderBootstrap",
            L"Initialize",
            L"GodotMonoModLoaderBootstrap.BootstrapEntryPoint, GodotMonoModLoaderBootstrap",
            nullptr,
            &entryPoint);
        if (result != 0 || entryPoint == nullptr)
        {
            WriteLog(L"hostfxr_load_assembly_and_get_function_pointer failed.");
            return;
        }

        WriteLog(L"Managed bootstrap entry point resolved.");
        reinterpret_cast<bootstrap_entry_point_fn>(entryPoint)();
        WriteLog(L"Managed bootstrap returned.");
    }

    void StartBootstrap()
    {
        bool expected = false;
        if (!g_bootstrapStarted.compare_exchange_strong(expected, true))
        {
            return;
        }

        HANDLE thread = CreateThread(
            nullptr,
            0,
            [](LPVOID) -> DWORD
            {
                BootstrapThread();
                return 0;
            },
            nullptr,
            0,
            nullptr);
        if (thread != nullptr)
        {
            CloseHandle(thread);
        }
    }
}

extern "C" HRESULT WINAPI DirectInput8Create(
    HINSTANCE instance,
    DWORD version,
    REFIID interfaceId,
    LPVOID* output,
    LPUNKNOWN outer)
{
    StartBootstrap();
    return LoadRealDInput8() && g_directInput8Create != nullptr
        ? g_directInput8Create(instance, version, interfaceId, output, outer)
        : E_FAIL;
}

extern "C" HRESULT WINAPI DllCanUnloadNow()
{
    StartBootstrap();
    return LoadRealDInput8() && g_dllCanUnloadNow != nullptr
        ? g_dllCanUnloadNow()
        : E_FAIL;
}

extern "C" HRESULT WINAPI DllGetClassObject(
    REFCLSID classId,
    REFIID interfaceId,
    LPVOID* output)
{
    StartBootstrap();
    return LoadRealDInput8() && g_dllGetClassObject != nullptr
        ? g_dllGetClassObject(classId, interfaceId, output)
        : E_FAIL;
}

extern "C" HRESULT WINAPI DllRegisterServer()
{
    StartBootstrap();
    return LoadRealDInput8() && g_dllRegisterServer != nullptr
        ? g_dllRegisterServer()
        : E_FAIL;
}

extern "C" HRESULT WINAPI DllUnregisterServer()
{
    StartBootstrap();
    return LoadRealDInput8() && g_dllUnregisterServer != nullptr
        ? g_dllUnregisterServer()
        : E_FAIL;
}

extern "C" LPCDIDATAFORMAT WINAPI GetdfDIJoystick()
{
    StartBootstrap();
    return LoadRealDInput8() && g_getdfDIJoystick != nullptr
        ? g_getdfDIJoystick()
        : nullptr;
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID reserved)
{
    UNREFERENCED_PARAMETER(reserved);

    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(instance);
        StartBootstrap();
    }

    return TRUE;
}
