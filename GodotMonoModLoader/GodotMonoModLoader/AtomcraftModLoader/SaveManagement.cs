using System.Reflection;
using Atomcraft;
using Godot;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Console = System.Console;
using FileAccess = Godot.FileAccess;

namespace GodotMonoModLoader.Atomcraft;

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

        try
        {
            SaveData_World world = universe.World;
            
            if ((FileAccess.FileExists(GetModdedUniversePath(world.Name))
                && FileUtils.LoadFile(GetModdedUniversePath(world.Name), out string content))
                || (DirAccess.DirExistsAbsolute(GetModdedSaveDir(world.Name)) // Old save dir
                    && FileUtils.LoadFile(GetModdedSavePath(world.Name), out content))) // Old modded save file
            {
                _moddedUniverse = JsonConvert.DeserializeObject<SaveData_ModdedUniverse>(content);
            }
            else
            {
                _moddedUniverse = null;
            }

            LoadModdedMaterials(universe);

            foreach (ModuleInfo module in GMML.LoadedModules)
            {
                ModOnUniverseLoad(module, universe, _moddedUniverse?.ModsData.GetValueOrDefault(module.ModuleId));
            }
            
            GD.Print("[GodotMonoModLoader] Modded universe loaded.");
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

            
            foreach (ModuleInfo module in GMML.LoadedModules)
            {
                if (ModOnUniverseSave(module, universe, out JToken? modData))
                {
                    SaveData_ModdedUniverse moddedUniverse = GetOrCreateModdedUniverse(worldName);

                    moddedUniverse.ModsData[module.ModuleId] = modData;
                }

            }

            if (_moddedUniverse != null)
            {
                string moddedSavePath = GetModdedUniversePath(worldName);
                FileUtils.SaveFile(moddedSavePath, JsonConvert.SerializeObject(_moddedUniverse, Formatting.None));
            }
            
            GD.Print("[GodotMonoModLoader] Modded universe saved.");
        }
        catch (Exception e)
        {
            GD.PrintErr("[GodotMonoModLoader] Error while saving modded world: ", e.Message);
            GD.PrintErr(e);
        }
    }
    
    public static void ModOnUniverseLoad(ModuleInfo module, SaveData_Universe universe, JToken? modData)
    {
        try
        {
            if (module.ModEntry is IUniverseLoadSaveProvider modEntry)
            {
                modEntry.OnUniverseLoad(universe, modData);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr("[GodotMonoModLoader] Error during mod OnUniverseLoad", e);
        }
    }

    public static bool ModOnUniverseSave(ModuleInfo module, SaveData_Universe universe, out JToken? modData)
    {
        modData = null;
        try
        {
            if (module.ModEntry is IUniverseLoadSaveProvider modEntry)
            {
                modData = modEntry.OnUniverseSave(universe);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr("[GodotMonoModLoader] Error during mod OnUniverseSave", e);
        }

        return modData != null;
    }

    private static void LoadModdedMaterials(SaveData_Universe universe)
    {
        if (_moddedUniverse == null)
        {
            return;
        }

        SaveData_World world = universe.World;
        
        foreach (KeyValuePair<string, int> material in _moddedUniverse.Spaceship.Inventory)
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

        foreach (SaveData_Player player in world.Players)
        {
            if (_moddedUniverse.Players.TryGetValue(player.PlayerName, out SaveData_ModdedPlayer? moddedPlayer))
            {
                List<SaveData_InventorySlot> inventorySlotsToAdd = [];
                foreach (KeyValuePair<string, int> material in moddedPlayer.Inventory)
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
        if (AtomcraftModLoader.Instance.MaterialsAdded > 0)
        {
            SaveData_World world = universe.World;

            List<short> materialIdsToSave = [];
            List<string> materialNamesToSave = [];

            foreach (ModuleInfo module in GMML.LoadedModules)
            {
                if (module.ModEntry is AtomcraftModEntry modEntry)
                {
                    materialIdsToSave.AddRange(modEntry.MaterialsToAdd.Select(material => material.Name.ToMaterialTypeId()));
                    materialNamesToSave.AddRange(modEntry.MaterialsToAdd.Select(material => material.Name));
                }
            }

            if (world.Players != null)
            {
                foreach (SaveData_Player saveDataPlayer in world.Players)
                {
                    SaveData_ModdedPlayer moddedPlayer = _moddedUniverse?.Players.GetValueOrDefault(
                        saveDataPlayer.PlayerName) ?? new SaveData_ModdedPlayer(saveDataPlayer.PlayerName);

                    saveDataPlayer.InventorySlots.DoIf(
                        i => materialIdsToSave.Contains(i.MaterialTypeId),
                        i => moddedPlayer.Inventory[i.MaterialTypeId.ToMaterialName()] = i.Amount);

                    if (moddedPlayer.Inventory.Count > 0)
                    {
                        GetOrCreateModdedUniverse(world.Name).Players[moddedPlayer.PlayerName] = moddedPlayer;
                    }
                }
            }

            SaveData_ModdedSpaceship moddedSpaceship = new SaveData_ModdedSpaceship();
            world.Spaceship?.InventorySlots?.DoIf(
                i => materialNamesToSave.Contains(i.MaterialTypeName),
                i => GetOrCreateModdedUniverse(world.Name).Spaceship.Inventory[i.MaterialTypeName] = i.Amount);
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
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (__result != null)
            {
                LoadModdedUniverse(__result);
            }
        }

        private static string? _currentWorldName;
        
        [HarmonyPrefix]
        [HarmonyPatch("WriteUniverseToDisk")]
        public static void WriteUniverseToDiskPrefix(ref SaveData_Universe universe)
        {
            SaveModdedUniverse(universe);
            _currentWorldName = universe?.World?.Name;
        }
        
        [HarmonyPrefix]
        [HarmonyPatch("SaveFileAtomic")]
        public static void SaveFileAtomicPrefix(string filePath, ref string content)
        {
            if (filePath.EndsWith(".universe") && Path.GetFileNameWithoutExtension(filePath) != _currentWorldName)
            {
                SaveData_Universe universe = JsonConvert.DeserializeObject<SaveData_Universe>(content)!;
                SaveModdedUniverse(universe);
                // content = JsonConvert.SerializeObject(universe);
            }
        }
        
        
    }
}