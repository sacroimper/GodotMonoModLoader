using Atomcraft;

namespace GodotMonoModLoader.Atomcraft;

public class SimulationStepContext : ModEntryContext
{
    public SimSnapshot State { get; }

    public SimulationStepContext(SimSnapshot state)
    {
        State = state;
    }
}