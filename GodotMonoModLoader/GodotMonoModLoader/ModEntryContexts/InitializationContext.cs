using System.ComponentModel;
using System.Transactions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GodotMonoModLoader;

[EditorBrowsable(EditorBrowsableState.Never)]
public class InitializationContext : ModEntryContext
{
    
    internal IModConfig? _modConfig;

    public InitializationContext() { }

    internal InitializationContext(IModConfig? modConfig)
    {
        _modConfig = modConfig;
    }
}

public class InitializationContext<T> : InitializationContext
    where T : IModConfig
{
    public T ModConfig { get => (T)_modConfig!; set => _modConfig = value; }

    public InitializationContext(JToken? modConfig) : base(GMML.ParseModConfig<T>(modConfig)) {}

    
    
}