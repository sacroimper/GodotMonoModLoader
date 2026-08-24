using System.Reflection;
using Atomcraft;
using Godot;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Console = System.Console;

namespace GodotMonoModLoader;

public static class SaveManagement
{
    private static SaveData_ModdedWorld? ModdedWorld;

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
    public class SaveData_ModdedWorld
    {
        public Dictionary<string, SaveData_ModdedPlayer> Players = [];
        public SaveData_ModdedSpaceship Spaceship = new();
        public Dictionary<string, JToken?> ModsData = new();
    }

    public static bool LoadFile(string filePath, out string content)
    {
        Godot.FileAccess fileAccess = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
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
        Godot.FileAccess fileAccess = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Write);
        Error error = fileAccess.GetError();
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
        return $"user://Worlds/{worldName}/modded";
    }

    public static string GetModdedSavePath(string worldName)
    {
        return GetModdedSaveDir(worldName).PathJoin("world.json");
    }

    public static void ModOnWorldLoad(Type entryClass, SaveData_World worldData, JToken? modData)
    {
        try
        {
            MethodInfo? onLoadMethod = entryClass.GetMethod(
                "OnWorldLoad",
                BindingFlags.Public |
                BindingFlags.Static
            );

            if (onLoadMethod != null)
            {
                var methodParameters = onLoadMethod.GetParameters();
                List<object?> parameters = [];
                foreach (ParameterInfo parameterInfo in methodParameters)
                {
                    if (parameterInfo.ParameterType == typeof(SaveData_World))
                    {
                        parameters.Add(worldData);
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

                onLoadMethod.Invoke(null, parameters.ToArray());
            }
        }
        catch (Exception e)
        {
            GD.PrintErr("[GodotMonoModLoader] Error during mod OnWorldLoad", e);
        }
    }

    public static bool ModOnWorldSave(Type entryClass, SaveData_World worldData, out JToken? modData)
    {
        try
        {
            MethodInfo? onSaveMethod = entryClass.GetMethod(
                "OnWorldSave",
                BindingFlags.Public |
                BindingFlags.Static
            );

            if (onSaveMethod != null)
            {
                var methodParameters = onSaveMethod.GetParameters();
                List<object?> parameters = [];
                foreach (ParameterInfo parameterInfo in methodParameters)
                {
                    if (parameterInfo.ParameterType == typeof(SaveData_World))
                    {
                        parameters.Add(worldData);
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

    private static void LoadModdedMaterials(SaveData_World world)
    {
        if (ModdedWorld == null)
        {
            return;
        }

        foreach (var material in ModdedWorld.Spaceship.Inventory)
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
            if (ModdedWorld.Players.TryGetValue(player.PlayerName, out var moddedPlayer))
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

    private static void SaveModdedMaterials(SaveData_World world)
    {
        if (AtomcraftModLoader.MaterialsToAdd.Count > 0)
        {
            if (ModdedWorld == null)
            {
                ModdedWorld = new SaveData_ModdedWorld();
            }

            List<short> materialIdsToSave =
                AtomcraftModLoader.MaterialsToAdd.ConvertAll(material => material.Name.ToMaterialTypeId());
            List<string> materialNamesToSave =
                AtomcraftModLoader.MaterialsToAdd.ConvertAll(material => material.Name);

            foreach (var saveDataPlayer in world.Players)
            {
                SaveData_ModdedPlayer moddedPlayer = ModdedWorld.Players.GetValueOrDefault(
                    saveDataPlayer.PlayerName,
                    new SaveData_ModdedPlayer(saveDataPlayer.PlayerName));

                saveDataPlayer.InventorySlots.DoIf(
                    i => materialIdsToSave.Contains(i.MaterialTypeId),
                    i => moddedPlayer.Inventory[i.MaterialTypeId.ToMaterialName()] = i.Amount);

                if (moddedPlayer.Inventory.Count > 0)
                {
                    ModdedWorld.Players[moddedPlayer.PlayerName] = moddedPlayer;
                }
            }

            SaveData_ModdedSpaceship moddedSpaceship = new SaveData_ModdedSpaceship();
            world.Spaceship.InventorySlots.DoIf(
                i => materialNamesToSave.Contains(i.MaterialTypeName),
                i => ModdedWorld.Spaceship.Inventory[i.MaterialTypeName] = i.Amount);
        }
    }

    [HarmonyPatch(typeof(Game))]
    public static class GamePatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(Game.LoadWorldHeaderData))]
        public static void LoadWorldHeaderDataPrefix(ref SaveData_World world)
        {
            GD.Print("[GodotMonoModLoader] Loading world...");

            try
            {
                if (DirAccess.DirExistsAbsolute(GetModdedSaveDir(world.Name))
                    && LoadFile(GetModdedSavePath(world.Name), out var content))
                {
                    ModdedWorld = JsonConvert.DeserializeObject<SaveData_ModdedWorld>(content);
                }
                else
                {
                    ModdedWorld = null;
                }

                LoadModdedMaterials(world);

                foreach (var entryClass in GodotMonoModLoader.EntryClasses)
                {
                    ModOnWorldLoad(entryClass.Value, world, ModdedWorld?.ModsData.GetValueOrDefault(entryClass.Key));
                }
            }
            catch (Exception e)
            {
                GD.PrintErr("[GodotMonoModLoader] Error while loading modded world: ", e.Message);
                GD.PrintErr(e);
            }
        }
    }

    [HarmonyPatch(typeof(FileManager))]
    public static class FileManagerPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(FileManager.SaveWorldHeader), new Type[] { typeof(SaveData_World), typeof(bool) })]
        public static void SaveWorldHeaderPrefix(ref SaveData_World world, bool writeToDisk)
        {
            GD.Print("[GodotMonoModLoader] Saving world...");

            try
            {
                SaveModdedMaterials(world);

                foreach (var entryClass in GodotMonoModLoader.EntryClasses)
                {
                    if (ModOnWorldSave(entryClass.Value, world, out var modData))
                    {
                        if (ModdedWorld == null)
                        {
                            ModdedWorld = new SaveData_ModdedWorld();
                        }

                        ModdedWorld.ModsData[entryClass.Key] = modData;
                    }

                    ;
                }

                if (ModdedWorld != null)
                {
                    string moddedSaveDir = GetModdedSaveDir(world.Name);
                    if (EnsureDirExists(moddedSaveDir))
                    {
                        string moddedSavePath = GetModdedSavePath(world.Name);
                        SaveFile(moddedSavePath, JsonConvert.SerializeObject(ModdedWorld, Formatting.None));
                    }
                }
            }
            catch (Exception e)
            {
                GD.PrintErr("[GodotMonoModLoader] Error while saving modded world: ", e.Message);
                GD.PrintErr(e);
            }
        }
    }
}