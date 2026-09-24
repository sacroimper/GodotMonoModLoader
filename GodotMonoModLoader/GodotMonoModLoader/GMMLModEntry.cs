
namespace GodotMonoModLoader;

public abstract class GMMLModEntry
{
    public ModInfo ModInfo { get; internal set; } = null!;
    public ModuleInfo ModuleInfo { get; internal set; } = null!;

    internal GMMLModEntry Init(ModInfo modInfo, ModuleInfo moduleInfo)
    {
        ModInfo = modInfo;
        ModuleInfo = moduleInfo;
        return this;
    }
}