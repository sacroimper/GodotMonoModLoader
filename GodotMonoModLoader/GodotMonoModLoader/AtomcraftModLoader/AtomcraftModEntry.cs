using Atomcraft;
using HarmonyLib;

namespace GodotMonoModLoader.Atomcraft;

public abstract class AtomcraftModEntry : GMMLModEntry
{
    internal readonly List<Serializable_MaterialType> MaterialsToAdd = [];
    internal readonly List<ReactionType> ReactionsToAdd = [];

    /**
     * <summary>Initializes Harmony using ModuleInfo.UniqueId as Harmony's ID.</summary>
     */
    public Harmony InitializeHarmony()
    {
        return new Harmony(ModuleInfo.UniqueId);
    }
    
    /**
     * <summary>Initializes Harmony using ModuleInfo.UniqueId as Harmony's ID and applies all patches for the Mod Entry assembly.</summary>
     */
    public Harmony HarmonyPatchAll()
    {
        Harmony harmony = InitializeHarmony();
        harmony.PatchAll(GetType().Assembly);
        return harmony;
    }

    /**
     * <summary>Called after the game has loaded the base materials, but before loading the materials added by this mod through files.<br/>
     * More materials can be dynamically created and added to context.ModMaterials for them to be loaded.</summary>
     *
     * <param name="context">Contains the list of materials read from this mod files.</param>
     * 
     */
    public virtual void OnMaterialsLoad(MaterialsLoadContext context) { }
    
    /**
     * <summary>Called after loading all the materials, game and mods.<br/>
     * This would be the place if modifications are needed to be applied to materials, for example to assign custom material classes (using <c>Materials.AddBaseMaterial()</c>).<br/>
     * No more materials should be created at this point or after.</summary>
     *
     * <param name="context">Contains the list of materials read from this mod files.</param>
     *
     */
    public virtual void PostMaterialsLoad(MaterialsLoadContext context) { }
    
    /**
     * <summary>Called after all the base game craftables have been defined.</summary>
     *
     * <return>A list with all the information of the craftables to be created.</return>
     *
     */
    public virtual List<AMLCraftable> PostCraftablesInit() => [];
    
    /**
     * <summary>Called after the game has loaded the base reactions, but before loading the reactions added by this mod through files.<br/>
     * More reactions can be dynamically created and added to context.ModReactions for them to be loaded.</summary>
     *
     * <param name="context">Contains the list of reactions read from this mod files.</param>
     *
     */
    public virtual void OnReactionsLoad(ReactionsLoadContext context) { }

}