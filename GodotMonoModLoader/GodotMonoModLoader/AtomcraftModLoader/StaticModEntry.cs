using System.Reflection;
using Atomcraft;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace GodotMonoModLoader.Atomcraft;

internal class StaticModEntry : GMMLModEntry, IModInitializationProvider, IUniverseLoadSaveProvider<JTokenModSaveData?>
{
    internal Type ModEntryType;
    
    public StaticModEntry(Type modEntryType)
    {
        ModEntryType = modEntryType;
    }

    public void Initialize(InitializationContext context)
    {
        MethodInfo? initializeMethod = ModEntryType.GetMethod(
            "Initialize",
            BindingFlags.Public |
            BindingFlags.Static
        );

        if (initializeMethod == null)
        {
            throw new Exception($"Public Static method {ModEntryType.FullName}.Initialize() not found");
        }

        initializeMethod.Invoke(null, null);
    }

    public void OnUniverseLoad(UniverseLoadContext<JTokenModSaveData?> context)
    {
        
        MethodInfo? onLoadMethod = ModEntryType.GetMethod(
            "OnUniverseLoad",
            BindingFlags.Public |
            BindingFlags.Static
        );

        if (onLoadMethod == null)
        {
            return;
        }
        
        List<object?> parameters = PrepareParameters(onLoadMethod.GetParameters(), context.Universe, context.ModData?.Token);

        onLoadMethod.Invoke(null, [.. parameters]);
    }

    public JTokenModSaveData? OnUniverseSave(UniverseSaveContext context)
    {
        
        MethodInfo? onSaveMethod = ModEntryType.GetMethod(
            "OnUniverseSave",
            BindingFlags.Public |
            BindingFlags.Static
        );

        if (onSaveMethod == null)
        {
            return null;
        }

        List<object?> parameters = PrepareParameters(onSaveMethod.GetParameters(), context.Universe, null);

        object? o = onSaveMethod.Invoke(null, [.. parameters]);
        
        return o != null ? new JTokenModSaveData(JToken.FromObject(o)) : null;
    }

    private List<object?> PrepareParameters(ParameterInfo[] methodParameters, SaveData_Universe universe, JToken? modData)
    {
        
        List<object?> parameters = [];
        foreach (ParameterInfo parameterInfo in methodParameters)
        {
            if (parameterInfo.ParameterType == typeof(SaveData_Universe))
            {
                parameters.Add(universe);
            }
            else if (parameterInfo.ParameterType == typeof(SaveData_World))
            {
                parameters.Add(universe.World);
            }
            else if (parameterInfo.ParameterType == typeof(JToken))
            {
                parameters.Add(modData);
            }
            else if (modData != null)
            {
                try
                {
                    parameters.Add(modData.ToObject(parameterInfo.ParameterType));
                }
                catch (Exception)
                {
                    parameters.Add(parameterInfo.ParameterType.GetDefaultValue());
                }
            }
            else
            {
                parameters.Add(parameterInfo.ParameterType.GetDefaultValue());
            }
        }

        return parameters;
    }
}