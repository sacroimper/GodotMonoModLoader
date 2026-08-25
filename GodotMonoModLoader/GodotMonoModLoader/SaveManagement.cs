using System.Reflection;
using Atomcraft;
using Godot;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Console = System.Console;
using FileAccess = Godot.FileAccess;

namespace GodotMonoModLoader;

public static class SaveManagement
{
    private static SaveData_ModdedUniverse? _moddedUniverse;

    [Serializable]
    public class SaveData_ModdedSpaceship
    {
        public Dictionary<string, int> Inventory = [];
    }

    [Serializable]
    public class SaveData_ModdedPlayer(string playerName)
    {
        public string PlayerName = playerName;
        public Dictionary<string, int> Inventory = [];
    }

    [Serializable]
    public class SaveData_ModdedUniverse(string worldName)
    {
        public string WorldName = worldName;
        public Dictionary<string, SaveData_ModdedPlayer> Players = [];
        public SaveData_ModdedSpaceship Spaceship = new();
        public Dictionary<string, JToken?> ModsData = new();
    }
    
    public static SaveData_ModdedUniverse GetOrCreateModdedUniverse(string worldName)
    {
        _moddedUniverse ??= new SaveData_ModdedUniverse(worldName);
        _moddedUniverse.WorldName = worldName;
        return _moddedUniverse;
    }

    public static bool LoadFile(string filePath, out string content)
    {
        FileAccess fileAccess = FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
        if (fileAccess == null)
        {
            GD.PrintErr("[GodotMonoModLoader] Failed to open file: " + filePath);
            content = string.Empty;
            return false;
        }

        Error error = fileAccess.GetError();
        switch (error)
        {
            case Error.Ok:
                content = fileAccess.GetAsText();
                fileAccess.Close();
                return true;
            case Error.AlreadyInUse:
                GD.PrintErr("[GodotMonoModLoader] Access denied to file: " + filePath);
                break;
            default:
                GD.PrintErr("[GodotMonoModLoader] Error loading file: " + error);
                break;
            case Error.FileNotFound:
                break;
        }

        content = string.Empty;
        return false;
    }

    public static void SaveFile(string filePath, string content)
    {
        FileAccess fileAccess = FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Write);
        Error error = fileAccess?.GetError() ?? Error.CantOpen;
        switch (error)
        {
            case Error.Ok:
                fileAccess.StoreString(content);
                fileAccess.Close();
                break;
            case Error.FileNotFound:
                GD.PrintErr("[GodotMonoModLoader] File not found: " + filePath);
                break;
            case Error.AlreadyExists:
                GD.PrintErr("[GodotMonoModLoader] Access denied to file: " + filePath);
                break;
            default:
                GD.PrintErr("[GodotMonoModLoader] Error saving file: " + error);
                break;
        }
    }

    public static bool EnsureDirExists(string path)
    {
        if (!DirAccess.DirExistsAbsolute(path))
        {
            if (DirAccess.MakeDirAbsolute(path) != Error.Ok)
            {
                GD.PrintErr("[GodotMonoModLoader] Failed to create directory: " + path);
                return false;
            }

            GD.Print("[GodotMonoModLoader] Created directory: " + path);
        }

        return true;
    }

    public static string GetModdedSaveDir(string worldName)
    {
        return $"user://Worlds/{worldName}/modded"; // Old save dir
    }

    public static string GetModdedSavePath(string worldName)
    {
        return GetModdedSaveDir(worldName).PathJoin("world.json"); // Old modded save file
    }

    public static string GetModdedUniversePath(string worldName)
    {
        return $"user://Worlds/{worldName}.moddedUniverse";
    }

    private static void LoadModdedUniverse(SaveData_Universe universe)
    {
        GD.Print("[GodotMonoModLoader] Loading modded universe...");

        SaveData_World world = universe.World;
        
        try
        {
            if ((FileAccess.FileExists(GetModdedUniversePath(world.Name))
                && LoadFile(GetModdedUniversePath(world.Name), out var content))
                || (DirAccess.DirExistsAbsolute(GetModdedSaveDir(world.Name)) // Old save dir
                    && LoadFile(GetModdedSavePath(world.Name), out content))) // Old modded save file
            {
                _moddedUniverse = JsonConvert.DeserializeObject<SaveData_ModdedUniverse>(content);
            }
            else
            {
                _moddedUniverse = null;
            }

            LoadModdedMaterials(universe);

            foreach (var entryClass in GodotMonoModLoader.EntryClasses)
            {
                ModOnUniverseLoad(entryClass.Value, universe, _moddedUniverse?.ModsData.GetValueOrDefault(entryClass.Key));
            }
        }
        catch (Exception e)
        {
            GD.PrintErr("[GodotMonoModLoader] Error while loading modded world: ", e.Message);
            GD.PrintErr(e);
        }
    }
    
    private static void SaveModdedUniverse(SaveData_Universe universe)
    {
        GD.Print("[GodotMonoModLoader] Saving modded universe...");

        try
        {
            string worldName = universe.World.Name;
                
            SaveModdedMaterials(universe);

            foreach (KeyValuePair<string, Type> entryClass in GodotMonoModLoader.EntryClasses)
            {
                if (ModOnUniverseSave(entryClass.Value, universe, out var modData))
                {
                    SaveData_ModdedUniverse moddedUniverse = GetOrCreateModdedUniverse(worldName);

                    moddedUniverse.ModsData[entryClass.Key] = modData;
                }

                ;
            }

            if (_moddedUniverse != null)
            {
                string moddedSavePath = GetModdedUniversePath(worldName);
                SaveFile(moddedSavePath, JsonConvert.SerializeObject(_moddedUniverse, Formatting.None));
            }
        }
        catch (Exception e)
        {
            GD.PrintErr("[GodotMonoModLoader] Error while saving modded world: ", e.Message);
            GD.PrintErr(e);
        }
    }
    
    public static void ModOnUniverseLoad(Type entryClass, SaveData_Universe universe, JToken? modData)
    {
        try
        {
            MethodInfo? onLoadMethod = entryClass.GetMethod(
                "OnUniverseLoad",
                BindingFlags.Public |
                BindingFlags.Static
            );

            if (onLoadMethod != null)
            {
                var methodParameters = onLoadMethod.GetParameters();
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
                    else if (modData != null)
                    {
                        try
                        {
                            parameters.Add(modData.ToObject(parameterInfo.ParameterType, new JsonSerializer()));
                        }
                        catch (Exception e)
                        {
                            parameters.Add(parameterInfo.ParameterType.GetDefaultValue());
                        }
                    }
                    else
                    {
                        parameters.Add(parameterInfo.ParameterType.GetDefaultValue());
                    }
                }

                onLoadMethod.Invoke(null, [.. parameters]);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr("[GodotMonoModLoader] Error during mod OnWorldLoad", e);
        }
    }

    public static bool ModOnUniverseSave(Type entryClass, SaveData_Universe universe, out JToken? modData)
    {
        try
        {
            MethodInfo? onSaveMethod = entryClass.GetMethod(
                "OnUniverseSave",
                BindingFlags.Public |
                BindingFlags.Static
            );

            if (onSaveMethod != null)
            {
                var methodParameters = onSaveMethod.GetParameters();
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
                    else
                    {
                        parameters.Add(parameterInfo.ParameterType.GetDefaultValue());
                    }
                }

                object? oModData = onSaveMethod.Invoke(null, [.. parameters]);

                if (oModData != null)
                {
                    modData = JToken.FromObject(oModData);
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr("[GodotMonoModLoader] Error during mod OnWorldSave", e);
        }

        modData = null;
        return false;
    }

    private static void LoadModdedMaterials(SaveData_Universe universe)
    {
        if (_moddedUniverse == null)
        {
            return;
        }

        SaveData_World world = universe.World;
        
        foreach (var material in _moddedUniverse.Spaceship.Inventory)
        {
            if (material.Key.ToMaterialTypeId() != -1
                && world.Spaceship.InventorySlots.All(i => i.MaterialTypeName != material.Key))
            {
                world.Spaceship.InventorySlots.Add(new SaveData_NamedInventorySlot
                {
                    MaterialTypeName = material.Key,
                    Amount = material.Value
                });
            }
        }

        foreach (var player in world.Players)
        {
            if (_moddedUniverse.Players.TryGetValue(player.PlayerName, out var moddedPlayer))
            {
                List<SaveData_InventorySlot> inventorySlotsToAdd = [];
                foreach (var material in moddedPlayer.Inventory)
                {
                    short materialTypeId = material.Key.ToMaterialTypeId();
                    if (materialTypeId != -1
                        && !player.BaseMaterialIdLookupTable.ContainsValue(material.Key))
                    {
                        short id = materialTypeId;

                        // Not sure if at this point the LookupTable has the current Ids or the old ones.
                        while (!player.BaseMaterialIdLookupTable.TryAdd(id, material.Key))
                        {
                            id += 1;
                        }

                        inventorySlotsToAdd.Add(new SaveData_InventorySlot
                        {
                            MaterialTypeId = id,
                            Amount = material.Value
                        });
                    }
                }

                if (inventorySlotsToAdd.Count > 0)
                {
                    player.InventorySlots = [.. player.InventorySlots, .. inventorySlotsToAdd];
                }
            }
        }
    }

    private static void SaveModdedMaterials(SaveData_Universe universe)
    {
        if (AtomcraftModLoader.MaterialsToAdd.Count > 0)
        {
            SaveData_World world = universe.World;
            SaveData_ModdedUniverse moddedUniverse = GetOrCreateModdedUniverse(world.Name);

            List<short> materialIdsToSave =
                AtomcraftModLoader.MaterialsToAdd.ConvertAll(material => material.Name.ToMaterialTypeId());
            List<string> materialNamesToSave =
                AtomcraftModLoader.MaterialsToAdd.ConvertAll(material => material.Name);

            
            foreach (var saveDataPlayer in world.Players)
            {
                SaveData_ModdedPlayer moddedPlayer = moddedUniverse.Players.GetValueOrDefault(
                    saveDataPlayer.PlayerName,
                    new SaveData_ModdedPlayer(saveDataPlayer.PlayerName));

                saveDataPlayer.InventorySlots.DoIf(
                    i => materialIdsToSave.Contains(i.MaterialTypeId),
                    i => moddedPlayer.Inventory[i.MaterialTypeId.ToMaterialName()] = i.Amount);

                if (moddedPlayer.Inventory.Count > 0)
                {
                    moddedUniverse.Players[moddedPlayer.PlayerName] = moddedPlayer;
                }
            }

            SaveData_ModdedSpaceship moddedSpaceship = new SaveData_ModdedSpaceship();
            world.Spaceship.InventorySlots.DoIf(
                i => materialNamesToSave.Contains(i.MaterialTypeName),
                i => moddedUniverse.Spaceship.Inventory[i.MaterialTypeName] = i.Amount);
        }
    }

    [HarmonyPatch(typeof(FileManager))]
    public static class FileManagerPatch
    {
       
        [HarmonyPostfix]
        [HarmonyPatch("TryLoadUniverseFile")]
        public static void TryLoadUniverseFilePostfix(string filePath, ref SaveData_Universe universe, bool __result)
        {
            if (__result)
            {
                LoadModdedUniverse(universe);
            }
        }
        
        [HarmonyPostfix]
        [HarmonyPatch("BuildUniverseFromLegacyWorld")]
        public static void BuildUniverseFromLegacyWorldPostfix(string worldName, ref SaveData_Universe __result)
        {
            LoadModdedUniverse(__result);
        }
        
        [HarmonyPrefix]
        [HarmonyPatch("WriteUniverseToDisk")]
        public static void WriteUniverseToDiskPrefix(ref SaveData_Universe universe)
        {
            SaveModdedUniverse(universe);
        }
        
        [HarmonyPrefix]
        [HarmonyPatch("SaveFileAtomic")]
        public static void SaveFileAtomicPrefix(string filePath, ref string content)
        {
            if (filePath.EndsWith(".universe") && Path.GetFileNameWithoutExtension(filePath) != _moddedUniverse?.WorldName)
            {
                SaveData_Universe universe = JsonConvert.DeserializeObject<SaveData_Universe>(content);
                SaveModdedUniverse(universe);
                // content = JsonConvert.SerializeObject(universe);
            }
        }
        
        
    }
}