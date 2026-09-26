namespace GodotMonoModLoader;

public interface IModEntry
{
    public ModInfo ModInfo { get; }
    public ModuleInfo ModuleInfo { get; }
}