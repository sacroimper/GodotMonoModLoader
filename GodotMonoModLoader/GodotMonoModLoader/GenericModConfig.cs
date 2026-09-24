using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GodotMonoModLoader;

[Serializable]
public class GenericModConfig : Dictionary<string, object>, IModConfig
{
    
    /**
     * Used to generate a new instance with the default configuration.
     * In this case, an empty dictionary.
     */
    public static IModConfig Default()
    {
        return new GenericModConfig();
    }

    public override string ToString()
    {
        return JsonConvert.SerializeObject(this, Formatting.Indented);
    }
}