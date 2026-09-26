namespace GodotMonoModLoader;

internal static class GMMLUtils
{
    
    internal static ModLoaderLogger Logger => GodotMonoModLoader.Instance.Logger;
    /**
     * This caches all the IModEntry mod entries so that it isn't needed to loop all modules each time. 
     */
    internal static class ModEntryCache<T> 
        where T : class, IModEntry
    {
        private static T[]? _modEntries;
        internal static T[] ModEntries =>
            _modEntries ??= GMML.LoadedModules
                .Where(module => module.ModEntry is T)
                .Select(module => module.ModEntry as T)
                .ToArray()!;
    }
    
    internal static void ExecuteForEachLoadedModule<T>(Action<ModuleInfo, T?> executeMethodDelegate,
        Action<ModuleInfo, Exception> onErrorDelegate)
        where T : class, IModEntry
    {
        foreach (ModuleInfo module in GMML.LoadedModules)
        {
            try
            { 
                executeMethodDelegate(module, module.ModEntry as T);
            }
            catch (Exception e)
            {
                onErrorDelegate(module, e);
            }
        }
    }
    
    internal static void ExecuteForEachModEntry<T>(Action<T> executeMethodDelegate,
        Action<ModuleInfo, Exception> onErrorDelegate)
        where T : class, IModEntry
    {
        foreach (T modEntry in ModEntryCache<T>.ModEntries)
        {
            try
            {
                executeMethodDelegate(modEntry);
            }
            catch (Exception e)
            {
                onErrorDelegate(modEntry.ModuleInfo, e);
            }
        }
    }

    internal static Action<ModuleInfo, Exception> GenericOnErrorWhileLoading(string errorMessage, string errorLog) =>
        (module, e) =>
        {
            module.ErrorMessage += "\n" + errorMessage;
            module.State = ModuleState.PartialError;
            Logger.LogError(errorLog + module.ModuleId);
            Logger.LogError(e);
        };
    
    internal static Action<ModuleInfo, Exception> GenericOnError(string errorLog) =>
        (module, e) =>
        {
            Logger.LogError(errorLog + module.ModuleId);
            Logger.LogError(e);
        };
}