namespace GodotMonoModLoader.Atomcraft;

public interface IOnReactionsLoadProvider : IHook
{

    /**
     * <summary>Called after the game has loaded the base reactions, but before loading the reactions added by this mod through files.<br/>
     * More reactions can be dynamically created and added to context.ModReactions for them to be loaded.</summary>
     *
     * <param name="context">Contains the list of reactions read from this mod files.</param>
     *
     */
    public void OnReactionsLoad(ReactionsLoadContext context);
}