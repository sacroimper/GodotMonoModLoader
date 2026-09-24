using Atomcraft;
using Godot;

namespace GodotMonoModLoader.Atomcraft;

public class AMLCraftable
{
    public required string MaterialTypeName;
    public required Dictionary<string, int> Inputs;
    public VideoStream? VideoStream;
    public required string LocIdDescription;
    public required CraftableCategoryIndex Category;

}