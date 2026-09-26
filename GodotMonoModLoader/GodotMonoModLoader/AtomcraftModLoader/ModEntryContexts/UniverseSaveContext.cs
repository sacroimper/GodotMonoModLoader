using Atomcraft;

namespace GodotMonoModLoader.Atomcraft;

public class UniverseSaveContext : ModEntryContext
{
    public SaveData_Universe Universe { get; }
    public SaveData_World World => Universe.World;

    public UniverseSaveContext(SaveData_Universe universe)
    {
        Universe = universe;
    }
}