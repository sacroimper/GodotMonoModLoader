namespace GodotMonoModLoader.Atomcraft;

public interface IPostSimulationInitProvider : IHook
{

    /**
     * <summary>Called after the Init method of Simulation has finished.<br/>
     * Perfect to place the initialization code that requires the game to be fully loaded.</summary>
     *
     */
    public void PostSimulationInit(SimulationInitContext  context);
}