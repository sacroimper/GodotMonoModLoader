using Atomcraft;
using Godot;
using HarmonyLib;
using Newtonsoft.Json;

namespace GodotMonoModLoader;

public partial class AtomcraftModLoader : Node
{

    public static List<Serializable_MaterialType> MaterialsToAdd = [];
    public static List<ReactionType> ReactionsToAdd = [];

    public int LoadMaterials(string zipPath, string path)
    {
        try
        {
            using ZipReader reader = new ZipReader();
            reader.Open(zipPath);
            if (path.EndsWith(".json"))
            {
                LoadMaterials(reader, path);
            }
            else
            {
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
            GD.PrintErr("[GodotMonoModLoader] ", e);
            return 1;
        }

        return 0;
    }

    private void LoadMaterials(ZipReader reader, string file)
    {
        GD.Print("[GodotMonoModLoader] Loading materials file: " + file);
        string fileAsText = System.Text.Encoding.UTF8.GetString(reader.ReadFile(file));
        
        try
        {
            MaterialsToAdd.AddRange(JsonConvert.DeserializeObject<List<Serializable_MaterialType>>(fileAsText));
        }
        catch (Exception ex)
        {
            GD.PrintErr("[GodotMonoModLoader] Error deserializing JSON: " + file);
            throw ex;
        }
    }
    
    public int LoadReactions(string zipPath, string path)
    {
        try
        {
            using ZipReader reader = new ZipReader();
            reader.Open(zipPath);
            if (path.EndsWith(".json"))
            {
                LoadReactions(reader, path);
            }
            else
            {
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
            GD.PrintErr("[GodotMonoModLoader] ", e);
            return 1;
        }

        return 0;
    }

    private void LoadReactions(ZipReader reader, string file)
    {
        GD.Print("[GodotMonoModLoader] Loading reactions file: " + file);
        string fileAsText = System.Text.Encoding.UTF8.GetString(reader.ReadFile(file));
        
        try
        {
            ReactionsToAdd.AddRange(JsonConvert.DeserializeObject<List<ReactionType>>(fileAsText));
        }
        catch (Exception ex)
        {
            GD.PrintErr("[GodotMonoModLoader] Error deserializing JSON: " + file);
            throw ex;
        }
    }
    
    public int LoadTranslations(string zipPath, string path)
    {
        try
        {
            using ZipReader reader = new ZipReader();
            reader.Open(zipPath);
            if (path.EndsWith(".json"))
            {
                LoadTranslations(reader, path);
            }
            else
            {
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
            GD.PrintErr("[GodotMonoModLoader] ", e);
            return 1;
        }

        return 0;
    }

    private void LoadTranslations(ZipReader reader, string file)
    {
        GD.Print("[GodotMonoModLoader] Loading translations file: " + file);
        string fileAsText = System.Text.Encoding.UTF8.GetString(reader.ReadFile(file));
        
        try
        {
            Dictionary<string, Dictionary<string, string>> translations = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(fileAsText);

            foreach (var localeTranslations in translations)
            {
                foreach (var pair in localeTranslations.Value)
                {
                    LoadTranslation(localeTranslations.Key, pair.Key, pair.Value);
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr("[GodotMonoModLoader] Error deserializing JSON: " + file);
            throw ex;
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
            GD.Print("[GodotMonoModLoader] Loading Materials...");
            foreach (Serializable_MaterialType item in MaterialsToAdd)
            {
                MaterialType materialType = new MaterialType(item);
                Materials.AddMaterialType(materialType, overwrite: true);
                // GD.Print("[GodotMonoModLoader] Loaded material type: " + materialType.Name);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(FileManager.LoadReactionsFromUserDirectory))]
        public static void ReactionsPostfix()
        {
            GD.Print("[GodotMonoModLoader] Loading Reactions...");
            List<BaseMaterial> list = new List<BaseMaterial>();
            foreach (ReactionType item in ReactionsToAdd)
            {
                ReactionTypes.Add(item, overwrite: true);
                Reaction reaction = Reactions.Add(item, overwrite: true);
                BaseMaterial baseMaterial = item.PrimaryInput.ToMaterial();
                if (baseMaterial == null)
                {
                    GD.PrintErr("[GodotMonoModLoader] Material not found: " + item.PrimaryInput);
                    continue;
                }

                baseMaterial.AddReaction(reaction);
                list.Add(baseMaterial);
                // GD.Print("[ReactionsLoader] Loaded reaction type: " + item.Name);
            }

            foreach (BaseMaterial item2 in list)
            {
                item2?.ConvertReactionList();
            }
        }
    }
}