using Newtonsoft.Json.Linq;

namespace GodotMonoModLoader;

public class JTokenModConfig(JToken? token) : IModConfig
{
    public readonly JToken? Token = token;
    
    public static IModConfig Default()
    {
        return new JTokenModConfig(JValue.CreateNull());
    }
}