namespace GodotMonoModLoader;

public interface IPostModModuleLoadProvider : IHook
{
    public void PostModModuleLoad(ModModuleLoadContext context);
}