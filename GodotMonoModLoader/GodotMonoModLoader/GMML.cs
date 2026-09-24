using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GodotMonoModLoader;

/**
 * Utility class for easy access to 
 */
public static class GMML
{
    
    /**
     * All mods found by the mod loader.
     */
    public static Dictionary<string, ModInfo> GetMods => GodotMonoModLoader.Instance.Mods;
    
    /**
     * Loaded modules by load order.
     */
    public static List<ModuleInfo> LoadedModules => GodotMonoModLoader.Instance.LoadedModules;

    
    /**
     * <summary>Reloads the mod config object from disk for the current module (detected by caller's assembly).</summary>
     *
     * <returns>The mod config object</returns>
     */
    public static T? ReloadModConfig<T>()
        where T : IModConfig
    {
        return ReloadModConfig<T>(GetExecutingModule());
    }
    
    /**
     * <summary>Reloads the mod config object from disk for the corresponding module.</summary>
     *
     * <param name="moduleId">The module id</param>
     *
     * <returns>The mod config object</returns>
     */
    public static T? ReloadModConfig<T>(string moduleId)
        where T : IModConfig
    {
        return ReloadModConfig<T>(moduleId.ToModuleInfo());
    }
    
    
    /**
     * <summary>Reloads the mod config object from disk for the corresponding module.</summary>
     *
     * <param name="module">The module</param>
     *
     * <returns>The mod config object</returns>
     */
    public static T? ReloadModConfig<T>(ModuleInfo module)
        where T : IModConfig
    {
        FileUtils.LoadModConfig(module, out JToken? modConfig);
        return (T?) (GodotMonoModLoader.Instance.ModsConfig[module.ModuleId] = ParseModConfig<T>(modConfig));
    }
    
    /**
     * <summary>Gets the mod config object from the current module (detected by caller's assembly).</summary>
     *
     * <returns>The mod config object</returns>
     */
    public static T? GetModConfig<T>()
        where T : IModConfig
    {
        return GetModConfig<T>(GetExecutingModule().ModuleId);
    }
    
    
    /**
     * <summary>Gets the mod config object from the corresponding module.</summary>
     *
     * <param name="moduleId">The module id</param>
     * 
     * <returns>The mod config object</returns>
     */
    public static T? GetModConfig<T>(string moduleId)
        where T : IModConfig
    {
        return (T?) GodotMonoModLoader.Instance.ModsConfig[moduleId];
    }

    /**
     * <summary>Saves the mod config object to disk for the current module (detected by caller's assembly).</summary>
     *
     * <param name="modConfig">The mod config object</param>
     * 
     * <returns>The mod config object</returns>
     */
    public static void SaveModConfig(IModConfig modConfig)
    {
        SaveModConfig(GetExecutingModule(), modConfig);
    }
    
    /**
     * <summary>Saves the mod config object to disk for the corresponding module.</summary>
     *
     * <param name="moduleId">The module id</param>
     * <param name="modConfig">The mod config object</param>
     * 
     * <returns>The mod config object</returns>
     */
    public static void SaveModConfig(string moduleId, IModConfig modConfig)
    {
        SaveModConfig(moduleId.ToModuleInfo(), modConfig);
    }

    /**
     * <summary>Saves the mod config object to disk for the corresponding module.</summary>
     *
     * <param name="module">The module</param>
     * <param name="modConfig">The mod config object</param>
     *
     * <returns>The mod config object</returns>
     */
    public static void SaveModConfig(ModuleInfo module, IModConfig modConfig)
    {
        FileUtils.SaveModConfig(module, modConfig);
    }
    
    public static ModInfo ToModInfo(this string modId)
    {
        return GodotMonoModLoader.Instance.Mods[modId];
    }
    
    public static ModuleInfo ToModuleInfo(this string moduleId)
    {
        return GodotMonoModLoader.Instance.Modules[moduleId];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static string GetExecutingAssemblyName()
    {
        Assembly currentAssembly = typeof(GMML).Assembly;

        return new StackTrace().GetFrames().Select(frame => frame.GetMethod()?.DeclaringType?.Assembly)
                .First(assembly => assembly != currentAssembly && GodotMonoModLoader.Instance.ModulesByAssembly.TryGetValue(currentAssembly.GetName().Name ?? "", out ModuleInfo? _))?.GetName().Name
            ?? string.Empty;
        
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static ModuleInfo GetExecutingModule()
    {
        return GodotMonoModLoader.Instance.ModulesByAssembly.GetValueOrDefault(GetExecutingAssemblyName()) ?? throw new Exception("Couldn't find a recognized called assembly");
    }
    
    internal static T? ParseModConfig<T>(JToken? modConfig)
        where T : IModConfig
    {
        if (modConfig == null)
        {
            return (T?) T.Default();
        }
        
        if (typeof(T) == typeof(JTokenModConfig)) return (T)(object)new JTokenModConfig(modConfig);

        try
        {
            return modConfig.ToObject<T>(new JsonSerializer());
        }
        catch (Exception)
        {
            return (T?) T.Default();
        }
    }
}