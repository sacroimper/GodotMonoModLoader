using Godot;
using Newtonsoft.Json;

namespace GodotMonoModLoader;

[Serializable]
public partial class ModInfo : RefCounted
{
    public class ModuleDictionaryConverter : JsonConverter<Dictionary<string, ModuleInfo>>
    {
        public override Dictionary<string, ModuleInfo> ReadJson(
            JsonReader reader,
            Type objectType,
            Dictionary<string, ModuleInfo>? existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            List<ModuleInfo>? items = serializer.Deserialize<List<ModuleInfo>>(reader);

            return items?.ToDictionary(x => x.ModuleId) ?? [];
        }

        public override void WriteJson(
            JsonWriter writer,
            Dictionary<string, ModuleInfo>? value,
            JsonSerializer serializer)
        {
            serializer.Serialize(writer, value?.Values);
        }
    }
    
    [JsonProperty(nameof(Id))]
    public string Id { get; internal set; } = null!;
    
    [JsonProperty(nameof(Name))]
    public string Name { get; internal set; } = "Not defined";
    
    [JsonProperty(nameof(Description))]
    public string Description { get; internal set; } = "Not defined";
    
    [JsonProperty(nameof(Author))]
    public string Author { get; internal set; } = "Not defined";
    
    [JsonIgnore]
    public Version ParsedVersion { get; internal set; } = new (1, 0);
    
    [JsonProperty(nameof(Version))]
    public string Version { 
        get => ParsedVersion.ToString();
        internal set => ParsedVersion = new Version(value);
    }
    
    [JsonConverter(typeof(ModuleDictionaryConverter))]
    [JsonProperty(nameof(Modules))]
    public Dictionary<string, ModuleInfo> Modules { get; internal set; } = [];
    
    [JsonConverter(typeof(ModuleDictionaryConverter))]
    [JsonProperty("DllModules")]
    internal Dictionary<string, ModuleInfo> DeprecatedDllModules
    {
        get => Modules;
        set
        {
            if (Modules.Count == 0)
                Modules = value;
        }
    }

    [JsonIgnore]
    public string Path { get; internal set; } = "";
    
    [JsonIgnore]
    public bool LoadedAsResourcePack { get; internal set; }
 
    public override string ToString()
    {
        string modulesString = "{ " + string.Join(", ", this.Modules.Select(kv => kv.Key + ": " + kv.Value)) + " }";
        return "{ Id: " + this.Id + ", Name: " + this.Name + ", Description: " + this.Description +
               ", Author: " + this.Author + ", Version: " + this.Version + ", Modules: " + modulesString +
               ", Path: " + this.Path + " }";
    }
}