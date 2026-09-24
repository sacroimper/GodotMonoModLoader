using HarmonyLib;

namespace GodotMonoModLoader.Atomcraft;

public abstract class AtomcraftModEntry : GMMLModEntry
{

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
    
}