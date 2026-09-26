using Newtonsoft.Json.Linq;

namespace GodotMonoModLoader;

/**
 * <summary>Implemented by ModEntry classes that want to be called during mod initialization</summary>
 *
 */
public interface IModInitializationProvider : IModEntry
{
    internal InitializationContext Initialize(JToken? modConfig)
    {
        InitializationContext context = new();
        Initialize(context);
        return context;
    }
    
    /**
     * <summary>Called after loading the dll.</summary>
     *
     * <param name="context">The context. </param>
     *
     */
    public void Initialize(InitializationContext context);
}

/**
 * <summary>Implemented by ModEntry classes that want to be called during mod initialization</summary>
 * 
 * <typeparam name="T">The type used for the Mod Options</typeparam>
 */
public interface IModInitializationProvider<T> : IModInitializationProvider
    where T : IModConfig
{

    
    /**
     * <summary>Called after loading the dll. The module mod config is read before this call and saved to disk and memory after.</summary>
     *
     * <param name="context">The context. Contains the ModConfig object of this module.</param>
     *
     */
    public void Initialize(InitializationContext<T> context);

    void IModInitializationProvider.Initialize(InitializationContext context)
    {
        Initialize((InitializationContext<T>) context);
    }
    
    InitializationContext IModInitializationProvider.Initialize(JToken? modConfig)
    {
        InitializationContext context = new InitializationContext<T>(modConfig);
        Initialize(context);
        return context;
    }
}