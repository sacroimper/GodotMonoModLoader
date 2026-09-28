using Godot;

namespace GodotMonoModLoader;

internal static class GMMLUtils
{
    
    internal static ModLoaderLogger Logger => GodotMonoModLoader.Instance.Logger;
    internal static void ExecuteForEachLoadedModule<T>(Action<ModuleInfo, T?> executeMethodDelegate,
        Action<ModuleInfo, Exception> onErrorDelegate)
        where T : class, IHook
    {
        foreach (ModuleInfo module in GMML.LoadedModules)
        {
            try
            { 
                executeMethodDelegate(module, module.ModEntry as T);
            }
            catch (Exception e)
            {
                try
                {
                    onErrorDelegate(module, e);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex);
                    Logger.LogError("Original error:");
                    Logger.LogError(e);
                }
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