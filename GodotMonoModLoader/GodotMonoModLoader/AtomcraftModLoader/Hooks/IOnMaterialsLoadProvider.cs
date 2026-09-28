namespace GodotMonoModLoader.Atomcraft;

public interface IOnMaterialsLoadProvider : IHook
{

    /**
     * <summary>Called after the game has loaded the base materials, but before loading the materials added by this mod through files.<br/>
     * More materials can be dynamically created and added to context.ModMaterials for them to be loaded.</summary>
     *
     * <param name="context">Contains the list of materials read from this mod files.</param>
     *
     */
    public void OnMaterialsLoad(MaterialsLoadContext context);
}