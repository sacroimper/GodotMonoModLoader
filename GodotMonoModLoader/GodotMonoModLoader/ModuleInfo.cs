using Godot;
using Newtonsoft.Json;

namespace GodotMonoModLoader;

[Serializable]
public partial class ModuleInfo : RefCounted
{
    [JsonProperty(nameof(ModuleId))]
    public string ModuleId { get; internal set; } = null!;
    
    [JsonProperty(nameof(Dll))]
    public string? Dll { get; internal set; }
    
    [JsonProperty(nameof(EntryClass))]
    public string? EntryClass { get; internal set; }
    
    [JsonProperty("InitClass")]
    internal string? DeprecatedInitClass
    {
        get => EntryClass;
        set => EntryClass ??= value;
    }
    
    [JsonProperty(nameof(Materials))]
    public string? Materials { get; internal set; }
    
    [JsonProperty(nameof(Reactions))]
    public string? Reactions { get; internal set; }
    
    [JsonProperty(nameof(Translations))]
    public string? Translations { get; internal set; }
    
    [JsonProperty(nameof(LoadAsResourcePack))]
    public bool LoadAsResourcePack { get; internal set; }
    
    [JsonProperty(nameof(Optional))]
    public bool Optional { get; internal set; }
    
    [JsonProperty(nameof(Dependencies))]
    public List<string> Dependencies { get; internal set; } = [];
    
    [JsonProperty(nameof(OptionalDependencies))]
    public List<string> OptionalDependencies { get; internal set; } = [];
    
    [JsonIgnore]
    public ModInfo Mod { get; internal set; } = null!;

    [JsonIgnore]
    public ModuleState State { get; internal set; } = ModuleState.Default;
    
    [JsonIgnore]
    public string? ErrorMessage { get; internal set; }
    
    [JsonIgnore]
    public GMMLModEntry? ModEntry { get; internal set; }

    public string UniqueId => Mod.Author + "." + ModuleId;
    
    public override string ToString()
    {
        return "{ ModId: " + Mod.Id + ", ModuleId: " + this.ModuleId + ", DLL: " + this.Dll + ", EntryClass: " + this.EntryClass +
               ", Materials: " + this.Materials + ", Reactions: " + this.Reactions +
               ", Translations: " + this.Translations + ", LoadAsResourcePack: " + this.LoadAsResourcePack +
               ", Optional: " + this.Optional + ", Dependencies: [" + string.Join(", ", this.Dependencies) +
               "], OptionalDependencies: [" + string.Join(", ", this.OptionalDependencies) +
               "], State: " + this.State + ", ErrorMessage: " + this.ErrorMessage + " }";
    }
}