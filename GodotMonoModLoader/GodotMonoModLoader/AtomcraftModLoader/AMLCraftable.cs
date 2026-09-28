using Atomcraft;
using Godot;

namespace GodotMonoModLoader.Atomcraft;

public class AMLCraftable
{
    public readonly CraftableCategoryIndex Category;
    public readonly string MaterialTypeName;
    public readonly string LocIdDescription;
    public readonly Dictionary<string, int> Inputs;
    public VideoStream? VideoStream;

    public AMLCraftable(CraftableCategoryIndex category, string materialTypeName, string locIdDescription, Dictionary<string, int> inputs)
    {
        Category = category;
        MaterialTypeName = materialTypeName;
        LocIdDescription = locIdDescription;
        Inputs = inputs;
    }
}