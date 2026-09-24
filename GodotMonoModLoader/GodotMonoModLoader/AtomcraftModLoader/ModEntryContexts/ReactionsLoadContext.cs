using Atomcraft;

namespace GodotMonoModLoader.Atomcraft;

public class ReactionsLoadContext : ModEntryContext
{
    /**
     * All the materials read from the mod files. Not yet loaded into the game. More materials can be added to this list to be loaded.
     */
    public List<ReactionType> ModReactions;
    /**
     * All the materials the game has already loaded.
     */
    public DualKeyDictionary<MaterialType> LoadedMaterials => Materials.MaterialTypesDict;

    public ReactionsLoadContext(List<ReactionType> modReactions)
    {
        ModReactions = modReactions;
    }
}