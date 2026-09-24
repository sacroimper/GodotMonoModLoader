using Atomcraft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GodotMonoModLoader.Atomcraft;

public class UniverseLoadContext : ModEntryContext
{
    public readonly SaveData_Universe Universe;
    public SaveData_World World => Universe.World;
    protected object? _modData;
    
    
    public UniverseLoadContext(SaveData_Universe universe)
    {
        Universe = universe;
    }
    
    internal UniverseLoadContext(SaveData_Universe universe, object? modData) : this(universe)
    {
        _modData = modData;
    }
}

public class UniverseLoadContext<T> : UniverseLoadContext
    where T : IModSaveData?
{
    public UniverseLoadContext(SaveData_Universe universe, JToken? modData) : base(universe, ParseModSaveData(modData)) { }

    public T? ModData => (T?)_modData;

    private static T? ParseModSaveData(JToken? modData)
    {
        if (modData == null)
        {
            return default;
        }

        if (typeof(T) == typeof(JTokenModSaveData)) return (T)(object)new JTokenModSaveData(modData);
        
        try
        {
            return modData.ToObject<T>(new JsonSerializer());
        }
        catch (Exception)
        {
            return default;
        }
    }
}