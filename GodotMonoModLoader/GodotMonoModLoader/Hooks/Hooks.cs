using System.Reflection;
using Godot;

namespace GodotMonoModLoader;

internal static class Hooks
{
    private static readonly MethodInfo RegisterTypedMethod =
        typeof(Hooks).GetMethod(
            nameof(RegisterTyped),
            BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo UnregisterTypedMethod =
        typeof(Hooks).GetMethod(
            nameof(UnregisterTyped),
            BindingFlags.NonPublic | BindingFlags.Static)!;

    internal static void Register(GMMLModEntry modEntry)
    {
        if (modEntry is IHook hook)
        {
            Register(modEntry, hook);
        }
    }
    
    internal static void Register(GMMLModEntry modEntry, IHook hook)
    {
        foreach (Type hookType in hook.GetType()
                     .GetInterfaces()
                     .Where(i => i != typeof(IHook)
                                 && typeof(IHook).IsAssignableFrom(i)))
        {
            RegisterTypedMethod
                .MakeGenericMethod(hookType)
                .Invoke(null, [modEntry, hook]);
        }
    }

    private static void RegisterTyped<T>(GMMLModEntry modEntry, T hook)
        where T : class, IHook
    {
        Hooks<T>.Register(modEntry, hook);
    }
    
    internal static void Unregister(GMMLModEntry modEntry)
    {
        if (modEntry is IHook hook)
        {
            Unregister(modEntry, hook);
        }
    }
    
    internal static void Unregister(GMMLModEntry modEntry, IHook hook)
    {
        foreach (Type hookType in hook.GetType()
                     .GetInterfaces()
                     .Where(i => i != typeof(IHook)
                                 && typeof(IHook).IsAssignableFrom(i)))
        {
            UnregisterTypedMethod
                .MakeGenericMethod(hookType)
                .Invoke(null, [modEntry, hook]);
        }
    }

    private static void UnregisterTyped<T>(GMMLModEntry modEntry, T hook)
        where T : class, IHook
    {
        Hooks<T>.Unregister(modEntry, hook);
    }
}

/**
 * This is a cache of all hooks for each IHook.
 */
internal static class Hooks<T>
    where T : class, IHook
{
    
    private static (GMMLModEntry modEntry, T hook)[] _registered = [];

    internal static void Register(GMMLModEntry modEntry, T hook)
    {
        if (_registered.Any(entry =>
                ReferenceEquals(entry.modEntry, modEntry) &&
                ReferenceEquals(entry.hook, hook)))
        {
            return;
        }
        _registered = [.._registered, (modEntry, hook)];
    }
    internal static void Unregister(GMMLModEntry modEntry, T hook)
    {
        _registered = _registered
            .Where(entry =>
                !ReferenceEquals(entry.modEntry, modEntry) ||
                !ReferenceEquals(entry.hook, hook))
            .ToArray();
    }

    internal static void Invoke(Action<GMMLModEntry, T> executeMethodDelegate,
        Action<ModuleInfo, Exception> onErrorDelegate)
    {
        foreach ((GMMLModEntry modEntry, T hook) in _registered)
        {
            try
            {
                executeMethodDelegate(modEntry, hook);
            }
            catch (Exception e)
            {
                try
                {
                    onErrorDelegate(modEntry.ModuleInfo, e);
                }
                catch (Exception ex)
                {
                    GD.PrintErr(ex);
                    GD.PrintErr("Original error:");
                    GD.PrintErr(e);
                }
            }
        }
    }
}