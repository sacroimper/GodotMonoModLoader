namespace GodotMonoModLoader.Atomcraft;

public interface IPostMaterialsLoadProvider : IHook
{

    /**
     * <summary>Called after loading all the materials, game and mods.<br/>
     * This would be the place if modifications are needed to be applied to materials, for example to assign custom material classes (using <c>Materials.AddBaseMaterial()</c>).<br/>
     * No more materials should be created at this point or after.</summary>
     *
     * <param name="context">Contains the list of materials read from this mod files.</param>
     *
     */
    public void PostMaterialsLoad(MaterialsLoadContext context);
}