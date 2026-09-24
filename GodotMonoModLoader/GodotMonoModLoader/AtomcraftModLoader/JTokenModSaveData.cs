using Newtonsoft.Json.Linq;

namespace GodotMonoModLoader.Atomcraft;

public class JTokenModSaveData(JToken? token) : IModSaveData
{
    public readonly JToken? Token = token;
}