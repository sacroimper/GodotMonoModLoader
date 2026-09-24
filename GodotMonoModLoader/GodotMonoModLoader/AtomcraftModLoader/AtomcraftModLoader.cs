using System.Diagnostics;
using Atomcraft;
using Godot;
using HarmonyLib;
using Newtonsoft.Json;
using Console = System.Console;

namespace GodotMonoModLoader.Atomcraft;

public class AtomcraftModLoader
{
    internal static AtomcraftModLoader Instance { get; private set; } = null!;

    internal int MaterialsAdded;
    internal int ReactionsAdded;
    
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
                module.MaterialsToAdd.AddRange(LoadMaterials(reader, path));
            }
            else
            {
                Logger.LogMessage("Loading materials from: " + path);
                foreach (string entry in reader.GetFiles())
                {
                    if (entry.StartsWith(path) && entry.EndsWith(".json"))
                    {
                        module.MaterialsToAdd.AddRange(LoadMaterials(reader, entry));
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

    public List<Serializable_MaterialType> LoadMaterials(ZipReader reader, string file)
    {
        Logger.LogMessage("Loading materials file: " + file);
        
        try
        {
            string fileAsText = System.Text.Encoding.UTF8.GetString(reader.ReadFile(file));
            
            return JsonConvert.DeserializeObject<List<Serializable_MaterialType>>(fileAsText)!;
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
                module.ReactionsToAdd.AddRange(LoadReactions(reader, path));
            }
            else
            {
                Logger.LogMessage("Loading reactions from: " + path);
                foreach (string entry in reader.GetFiles())
                {
                    if (entry.StartsWith(path) && entry.EndsWith(".json"))
                    {
                        module.ReactionsToAdd.AddRange(LoadReactions(reader, entry));
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

    public List<ReactionType> LoadReactions(ZipReader reader, string file)
    {
        Logger.LogMessage("Loading reactions file: " + file);
        
        try
        {
            string fileAsText = System.Text.Encoding.UTF8.GetString(reader.ReadFile(file));
            
            return JsonConvert.DeserializeObject<List<ReactionType>>(fileAsText)!;
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
            
            foreach (ModuleInfo module in GMML.LoadedModules)
            {
                try
                {

                    if (module.ModEntry is AtomcraftModEntry modEntry)
                    {
                        modEntry.OnMaterialsLoad(new MaterialsLoadContext(modEntry.MaterialsToAdd));
                    }
                    
                    foreach (Serializable_MaterialType material in module.MaterialsToAdd)
                    {
                        MaterialType materialType = new MaterialType(material);
                        Materials.AddMaterialType(materialType, overwrite: true);
                        Instance.MaterialsAdded++;
                    }
                }
                catch (Exception e)
                {
                    module.ErrorMessage += "\nError while trying to load materials.";
                    module.State = ModuleState.PartialError;
                    Instance.Logger.LogError("Error loading materials from module: " + module.ModuleId);
                    Instance.Logger.LogError(e);
                    throw;
                }

            }

            Instance.Logger.LogMessage(Instance.MaterialsAdded + " modded materials loaded.");
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(FileManager.LoadReactionsFromUserDirectory))]
        public static void ReactionsPostfix()
        {
            Instance.Logger.LogMessage("Loading modded reactions...");
            
            foreach (ModuleInfo module in GMML.LoadedModules)
            {
                try
                {

                    if (module.ModEntry is AtomcraftModEntry modEntry)
                    {
                        modEntry.OnReactionsLoad(new ReactionsLoadContext(modEntry.ReactionsToAdd));
                    }

                    List<BaseMaterial> baseMaterials = [];
                    foreach (ReactionType reactionType in module.ReactionsToAdd)
                    {
                        ReactionTypes.Add(reactionType, overwrite: true);
                        Reaction reaction = Reactions.Add(reactionType, overwrite: true);
                        BaseMaterial baseMaterial = reactionType.PrimaryInput.ToMaterial();
                        if (baseMaterial == null)
                        {
                            Instance.Logger.LogError("Material not found: " + reactionType.PrimaryInput);
                            module.ErrorMessage += "\nMaterial not found: " + reactionType.PrimaryInput;
                            module.State = ModuleState.PartialError;
                            continue;
                        }

                        baseMaterial.AddReaction(reaction);
                        baseMaterials.Add(baseMaterial);
                        Instance.ReactionsAdded++;
                    }

                    foreach (BaseMaterial baseMaterial in baseMaterials)
                    {
                        baseMaterial.ConvertReactionList();
                    }
                }
                catch (Exception e)
                {
                    module.ErrorMessage += "\nError while trying to load reactions.";
                    module.State = ModuleState.PartialError;
                    Instance.Logger.LogError("Error loading reactions from module: " + module.ModuleId);
                    Instance.Logger.LogError(e);
                }
            }

            Instance.Logger.LogMessage(Instance.ReactionsAdded + " modded reactions loaded.");
        }
    }
    
    [HarmonyPatch(typeof(Materials), "InitializeCustomClasses")]
    public class MaterialsPatch
    {
        public static void Postfix()
        {
            
            foreach (ModuleInfo module in GMML.LoadedModules)
            {
                try
                {
                    if (module.ModEntry is AtomcraftModEntry modEntry)
                    {
                        modEntry.PostMaterialsLoad(new MaterialsLoadContext(modEntry.MaterialsToAdd));
                    }
                }
                catch (Exception e)
                {
                    module.ErrorMessage += "\nError while trying to apply modifications to materials.";
                    module.State = ModuleState.PartialError;
                    Instance.Logger.LogError("Error applying modifications to materials for module: " + module.ModuleId);
                    Instance.Logger.LogError(e);
                }
            }
        }
    }
    
    [HarmonyPatch(typeof(Craftables), nameof(Craftables.Init))]
    public class CraftablesPatch
    {
        public static void Postfix()
        {
            
            foreach (ModuleInfo module in GMML.LoadedModules)
            {
                try
                {
                    if (module.ModEntry is not AtomcraftModEntry modEntry) continue;
                    
                    List<AMLCraftable> craftables = modEntry.PostCraftablesInit();
                    
                    foreach (AMLCraftable craftable in craftables ?? [])
                    {
                        Craftables.Add(craftable.MaterialTypeName, craftable.Inputs, craftable.VideoStream, craftable.LocIdDescription);
                        Craftables.GetCategory(craftable.Category).MaterialTypeIds
                            .Add(craftable.MaterialTypeName.ToMaterialTypeId());
                    }
                }
                catch (Exception e)
                {
                    module.ErrorMessage += "\nError while defining craftables.";
                    module.State = ModuleState.PartialError;
                    Instance.Logger.LogError("Error defining craftables for module: " + module.ModuleId);
                    Instance.Logger.LogError(e);
                    throw;
                }
            }
        }
    }
}