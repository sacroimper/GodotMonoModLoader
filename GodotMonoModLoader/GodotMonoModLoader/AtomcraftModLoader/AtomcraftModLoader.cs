using System.Diagnostics;
using Atomcraft;
using Godot;
using HarmonyLib;
using Newtonsoft.Json;

namespace GodotMonoModLoader.Atomcraft;

public class AtomcraftModLoader
{
    public static AtomcraftModLoader Instance { get; private set; } = null!;

    internal readonly List<Serializable_MaterialType> MaterialsToAdd = [];
    internal readonly List<ReactionType> ReactionsToAdd = [];
    
    internal GodotMonoModLoader ModLoader { get; private set; } = null!;
    internal ModLoaderLogger Logger { get; private set; } = null!;


    internal AtomcraftModLoader Init(GodotMonoModLoader modLoader)
    {
        ModLoader = modLoader;
        Logger = modLoader.Logger;
        Instance ??= this;
        
        return this;
    }


    internal bool LoadModule(ModInfo mod, ModuleInfo module)
    {
        if (!string.IsNullOrEmpty(module.Materials))
        {
            if (!TryLoadMaterials(mod, module))
            {
                return false;
            }
        }
	 
        if (!string.IsNullOrEmpty(module.Reactions))
        {
            if (!TryLoadReactions(mod, module))
            {
                return false;
            }
        }
	 
        if (!string.IsNullOrEmpty(module.Translations))
        {
            if (!TryLoadTranslations(mod, module))
            {
                return false;
            }
        }
        
        return true;
    }
    
    public bool TryLoadMaterials(ModInfo mod, ModuleInfo module)
    {
        Debug.Assert(module.Materials != null, "module.Materials != null");
        try
        {
            string zipPath = mod.Path;
            string path = mod.Id.PathJoin(module.Materials);
            
            using ZipReader reader = new();
            reader.Open(zipPath);
            if (path.EndsWith(".json"))
            {
                LoadMaterials(reader, path);
            }
            else
            {
                Logger.LogMessage("Loading materials from: " + path);
                foreach (string entry in reader.GetFiles())
                {
                    if (entry.StartsWith(path) && entry.EndsWith(".json"))
                    {
                        LoadMaterials(reader, entry);
                    }
                }
            }
        }
        catch (Exception e)
        {
            module.ErrorMessage = "Error while trying to load materials from: " + module.Materials;
            Logger.LogMessage("Error while trying to load materials from module: " + module.ModuleId);
            Logger.LogError(e.ToString());
            return false;
        }
        
        return true;
    }

    public void LoadMaterials(ZipReader reader, string file)
    {
        Logger.LogMessage("Loading materials file: " + file);
        
        try
        {
            string fileAsText = System.Text.Encoding.UTF8.GetString(reader.ReadFile(file));
            
            MaterialsToAdd.AddRange(JsonConvert.DeserializeObject<List<Serializable_MaterialType>>(fileAsText)!);
        }
        catch (Exception)
        {
            Logger.LogError("Error deserializing JSON: " + file);
            throw;
        }
    }
    
    public bool TryLoadReactions(ModInfo mod, ModuleInfo module)
    {
        Debug.Assert(module.Reactions != null, "module.Reactions != null");
        try
        {
            string zipPath = mod.Path;
            string path = mod.Id.PathJoin(module.Reactions);
            
            using ZipReader reader = new();
            reader.Open(zipPath);
            if (path.EndsWith(".json"))
            {
                LoadReactions(reader, path);
            }
            else
            {
                Logger.LogMessage("Loading reactions from: " + path);
                foreach (string entry in reader.GetFiles())
                {
                    if (entry.StartsWith(path) && entry.EndsWith(".json"))
                    {
                        LoadReactions(reader, entry);
                    }
                }
            }
        }
        catch (Exception e)
        {
            module.ErrorMessage = "Error while trying to load reactions from: " + module.Reactions;
            Logger.LogMessage("Error while trying to load reactions from module: " + module.ModuleId);
            Logger.LogError(e.ToString());
            return false;
        }

        return true;
    }

    public void LoadReactions(ZipReader reader, string file)
    {
        Logger.LogMessage("Loading reactions file: " + file);
        
        try
        {
            string fileAsText = System.Text.Encoding.UTF8.GetString(reader.ReadFile(file));
            
            ReactionsToAdd.AddRange(JsonConvert.DeserializeObject<List<ReactionType>>(fileAsText)!);
        }
        catch (Exception)
        {
            Logger.LogError("Error deserializing JSON: " + file);
            throw;
        }
    }
    
    public bool TryLoadTranslations(ModInfo mod, ModuleInfo module)
    {
        Debug.Assert(module.Translations != null, "module.Translations != null");
        try
        {
            string zipPath = mod.Path;
            string path = mod.Id.PathJoin(module.Translations);
            
            using ZipReader reader = new();
            reader.Open(zipPath);
            if (path.EndsWith(".json"))
            {
                LoadTranslations(reader, path);
            }
            else
            {
                Logger.LogMessage("Loading translations from: " + path);
                foreach (string entry in reader.GetFiles())
                {
                    if (entry.StartsWith(path) && entry.EndsWith(".json"))
                    {
                        LoadTranslations(reader, entry);
                    }
                }
            }
        }
        catch (Exception e)
        {
            module.ErrorMessage = "Error while trying to load translations from: " + module.Translations;
            Logger.LogMessage("Error while trying to load translations from module: " + module.ModuleId);
            Logger.LogError(e.ToString());
            return false;
        }

        return true;
    }

    public void LoadTranslations(ZipReader reader, string file)
    {
        Logger.LogMessage("Loading translations file: " + file);
        
        try
        {
            string fileAsText = System.Text.Encoding.UTF8.GetString(reader.ReadFile(file));
            
            Dictionary<string, Dictionary<string, string>> translations = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(fileAsText)!;

            foreach (KeyValuePair<string, Dictionary<string, string>> localeTranslations in translations)
            {
                foreach (KeyValuePair<string, string> pair in localeTranslations.Value)
                {
                    LoadTranslation(localeTranslations.Key, pair.Key, pair.Value);
                }
            }
        }
        catch (Exception)
        {
            Logger.LogError("Error deserializing JSON: " + file);
            throw;
        }
    }

    private void LoadTranslation(string locale, string key, string message)
    {
        Translation t = new();
        t.Locale = locale;
        t.AddMessage(key, message);
        TranslationServer.AddTranslation(t);
    }

    [HarmonyPatch(typeof(FileManager))]
    public static class FileManagerPatch
    {

        [HarmonyPostfix]
        [HarmonyPatch(nameof(FileManager.LoadMaterialTypesFromUserDirectory))]
        public static void MaterialsPostfix()
        {
            Instance.Logger.LogMessage("Loading modded materials...");
            foreach (Serializable_MaterialType item in Instance.MaterialsToAdd)
            {
                MaterialType materialType = new MaterialType(item);
                Materials.AddMaterialType(materialType, overwrite: true);
            }
            
            Instance.Logger.LogMessage(Instance.MaterialsToAdd.Count + " modded materials loaded.");
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(FileManager.LoadReactionsFromUserDirectory))]
        public static void ReactionsPostfix()
        {
            Instance.Logger.LogMessage("Loading modded reactions...");
            List<BaseMaterial> list = new List<BaseMaterial>();
            foreach (ReactionType item in Instance.ReactionsToAdd)
            {
                ReactionTypes.Add(item, overwrite: true);
                Reaction reaction = Reactions.Add(item, overwrite: true);
                BaseMaterial baseMaterial = item.PrimaryInput.ToMaterial();
                if (baseMaterial == null)
                {
                    Instance.Logger.LogError("Material not found: " + item.PrimaryInput);
                    continue;
                }

                baseMaterial.AddReaction(reaction);
                list.Add(baseMaterial);
            }

            foreach (BaseMaterial item2 in list)
            {
                item2?.ConvertReactionList();
            }

            Instance.Logger.LogMessage(Instance.ReactionsToAdd.Count + " modded reactions loaded.");
        }
    }
}