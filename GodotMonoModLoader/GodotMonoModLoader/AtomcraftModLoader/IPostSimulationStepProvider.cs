namespace GodotMonoModLoader.Atomcraft;

public interface IPostSimulationStepProvider : IModEntry
{

    /**
     * <summary>Called after the Step method of Simulation has finished.<br/>
     * This gets executed every tick.</summary>
     *
     * <param name="context">Contains the Step method params.</param>
     *
     */
    public void PostSimulationStep(SimulationStepContext context);
}