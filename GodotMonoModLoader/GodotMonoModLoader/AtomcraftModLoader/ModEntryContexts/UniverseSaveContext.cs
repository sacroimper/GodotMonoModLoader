using Atomcraft;

namespace GodotMonoModLoader.Atomcraft;

public class UniverseSaveContext : ModEntryContext
{
    public readonly SaveData_Universe Universe;
    public SaveData_World World => Universe.World;

    public UniverseSaveContext(SaveData_Universe universe)
    {
        Universe = universe;
    }
}