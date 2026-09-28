namespace GodotMonoModLoader.Atomcraft;

public interface IPostCraftablesInitProvider : IHook
{

    
    /**
     * <summary>Called after all the base game craftables have been defined.</summary>
     *
     * <return>A list with all the information of the craftables to be created.</return>
     *
     */
    public List<AMLCraftable> PostCraftablesInit(CraftablesInitContext context);
    
}