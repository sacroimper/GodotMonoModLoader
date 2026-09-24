using Atomcraft;

namespace GodotMonoModLoader.Atomcraft;

public class MaterialsLoadContext : ModEntryContext
{
    /**
     * All the materials read from the mod files. Not yet loaded into the game. More materials can be added to this list to be loaded.
     */
    public List<Serializable_MaterialType> ModMaterials;
    /**
     * All the materials the game has already loaded.
     */
    public DualKeyDictionary<MaterialType> LoadedMaterials => Materials.MaterialTypesDict;

    public MaterialsLoadContext(List<Serializable_MaterialType> modMaterials)
    {
        ModMaterials = modMaterials;
    }
}