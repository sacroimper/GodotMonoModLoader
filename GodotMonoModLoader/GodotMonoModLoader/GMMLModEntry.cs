
namespace GodotMonoModLoader;

public abstract class GMMLModEntry
{
    public ModInfo ModInfo { get; internal set; } = null!;
    public ModuleInfo ModuleInfo { get; internal set; } = null!;
    
    internal readonly List<IHook> ModHooks = [];

    internal GMMLModEntry Init(ModInfo modInfo, ModuleInfo moduleInfo)
    {
        ModInfo = modInfo;
        ModuleInfo = moduleInfo;
        return this;
    }

    public void RegisterHook<T>(T hook)
        where T : class, IHook
    {
        if (ModHooks.Contains(hook) || ReferenceEquals(hook, this)) return;
        if (hook is IModInitializationProvider) throw new Exception("Only the entry class can have a ModInitialization hook.");
        Hooks.Register(this, hook);
        ModHooks.Add(hook);
    }

    internal void UnregisterHooks()
    {
        Hooks.Unregister(this);
        foreach (IHook hook in ModHooks)
        {
            Hooks.Unregister(this, hook);
        }
        ModHooks.Clear();
    }

}