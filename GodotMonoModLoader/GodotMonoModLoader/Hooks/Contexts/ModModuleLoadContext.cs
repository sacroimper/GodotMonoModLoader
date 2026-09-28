namespace GodotMonoModLoader;

public class ModModuleLoadContext : HookContext
{
    public ModuleInfo ModuleInfo;

    public ModModuleLoadContext(ModuleInfo moduleInfo)
    {
        ModuleInfo = moduleInfo;
    }
}