using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Runtime.InteropServices;

namespace GodotMonoModLoaderBootstrap;

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate void BootstrapEntryPoint();

public static class Bootstrap
{
    private static int _initialized;
    private static string _logPath = string.Empty;
    
    public static void Initialize()
    {
        
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
        {
            return;
        }
        
        Console.WriteLine(AppContext.BaseDirectory);
        string loaderDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "..");
        _logPath = Path.Combine(
            loaderDirectory,
            "Logs",
            "GodotMonoModLoaderBootstrap.log");
        try
        {
            Assembly? gameAssembly = GetLoadedAssembly("Atomcraft");
            if (gameAssembly != null && gameAssembly.GetType("Atomcraft.GodotMonoModLoaderPatch") != null)
            {
                Console.WriteLine($"Game already patched. Skipping bootstrap");
                WriteLog($"Game already patched. Skipping bootstrap");
                return;
            }
            
            string patchPath = Path.Combine(
                loaderDirectory,
                "ModLoaderPatch.dll");

            if (!File.Exists(patchPath))
            {
                throw new FileNotFoundException(
                    "ModLoaderPatch.dll was not found.",
                    patchPath);
            }

            string? lastError = null;
            int maxAttempts = 200;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    Assembly godotAssembly = GetLoadedAssembly("GodotSharp")
                                             ?? throw new InvalidOperationException(
                                                 "The game's GodotSharp assembly is not loaded yet.");
                    Type bridgeType = godotAssembly.GetType(
                        "Godot.Bridge.ScriptManagerBridge",
                        throwOnError: true)!;
                    AssemblyLoadContext alc = AssemblyLoadContext.GetLoadContext(godotAssembly)
                                              ?? throw new InvalidOperationException(
                                                  "The game assembly has no load context.");
                    Assembly patchAssembly = GetLoadedAssembly("ModLoaderPatch")
                                             ?? alc.LoadFromAssemblyPath(patchPath);

                    MethodInfo lookupMethod = bridgeType.GetMethod(
                                                  "LookupScriptsInAssembly",
                                                  BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                                              ?? throw new MissingMethodException(
                                                  bridgeType.FullName,
                                                  "LookupScriptsInAssembly");

                    lookupMethod.Invoke(null, new object[] { patchAssembly });
                    WriteLog($"Bootstrap attempt {attempt} Registered {patchAssembly.FullName}");
                    Console.WriteLine($"Bootstrap attempt {attempt} loaded the patch correctly.");
                    return;
                }
                catch (Exception exception)
                {
                    string error = exception.ToString();
                    if (attempt % 20 == 0 || !error.Equals(lastError))
                    {
                        WriteLog($"Bootstrap attempt {attempt} Failed: {error}");
                        Console.WriteLine($"Bootstrap attempt {attempt} failed: {error}");
                    }
                    lastError = error;
                    
                    Thread.Sleep(100);
                }
            }
        }
        catch (Exception exception)
        {
            WriteLog($"Bootstrap failed: {exception}");
            Console.WriteLine($"Bootstrap failed: {exception}");
        }

        Volatile.Write(ref _initialized, 0);
    }

    private static Assembly? GetLoadedAssembly(string assemblyName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly =>
                string.Equals(
                    assembly.GetName().Name,
                    assemblyName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static void WriteLog(string message)
    {
        try
        {
            string? directory = Path.GetDirectoryName(_logPath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(
                _logPath,
                $"[{DateTime.Now:O}] {message}{Environment.NewLine}",
                Encoding.UTF8);
        }
        catch
        {
            // Diagnostics must not terminate the game process.
        }
    }
}
