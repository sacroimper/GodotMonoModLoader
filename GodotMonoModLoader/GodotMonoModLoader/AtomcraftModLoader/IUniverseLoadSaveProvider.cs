using Atomcraft;
using Newtonsoft.Json.Linq;

namespace GodotMonoModLoader.Atomcraft;
public interface IUniverseLoadSaveProvider
{
    
    /**
     * <summary>Called after loading the universe (game save).</summary>
     *
     * <param name="context">Contains the Universe object that has been loaded.</param>
     *
     */
    public void OnUniverseLoad(UniverseLoadContext context);
    
    /**
     * <summary>Called before saving the universe (game save).</summary>
     *
     * <param name="context">Contains the Universe object that will be saved.</param>
     *
     */
    public void OnUniverseSave(UniverseSaveContext context);

    internal void OnUniverseLoad(SaveData_Universe universe, JToken? modData)
    {
        OnUniverseLoad(new UniverseLoadContext(universe));
    }

    internal JToken? OnUniverseSave(SaveData_Universe universe)
    {
        OnUniverseSave(new UniverseSaveContext(universe));
        return null;
    }
}

public interface IUniverseLoadSaveProvider<T> : IUniverseLoadSaveProvider
    where T : IModSaveData?
{
    /**
     * <summary>Called after loading the universe (game save).</summary>
     *
     * <param name="context">Contains the Universe and ModData objects that have been loaded.</param>
     *
     */
    public void OnUniverseLoad(UniverseLoadContext<T> context);

    /**
     * <summary>Called before saving the universe (game save).</summary>
     *
     * <param name="context">Contains the Universe object that will be saved.</param>
     *
     * <returns>The ModData object to be saved</returns>
     */
    public new T? OnUniverseSave(UniverseSaveContext context);
    
    
    void IUniverseLoadSaveProvider.OnUniverseLoad(SaveData_Universe universe, JToken? modData)
    {
        OnUniverseLoad(new UniverseLoadContext<T>(universe, modData));
    }
    
    JToken? IUniverseLoadSaveProvider.OnUniverseSave(SaveData_Universe universe)
    {
        object? modData = OnUniverseSave(new UniverseSaveContext(universe));
        return modData != null ? (modData is JTokenModSaveData tokenModData ? tokenModData.Token : JToken.FromObject(modData)) : null;
    }
    
    void IUniverseLoadSaveProvider.OnUniverseLoad(UniverseLoadContext context)
    {
        OnUniverseLoad((UniverseLoadContext<T>) context);
    }
    
    void IUniverseLoadSaveProvider.OnUniverseSave(UniverseSaveContext context)
    {
        OnUniverseSave(context);
    }
    
}