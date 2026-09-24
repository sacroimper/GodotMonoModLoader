using System.Diagnostics;
using System.Text;
using Godot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using FileAccess = Godot.FileAccess;

namespace GodotMonoModLoader;

public static class FileUtils
{
    public static string ModsGameDirectory = OS.GetExecutablePath().GetBaseDir().PathJoin("Mods");
    public static string ModsUserDirectory = OS.GetUserDataDir().PathJoin("Mods"); 
    public static string ModsConfigDirectory = OS.GetUserDataDir().PathJoin("ModsConfig");

    public static string ModConfigFilePath(string moduleId)
    {
        return ModConfigFilePath(moduleId.ToModuleInfo());
    }

    public static string ModConfigFilePath(ModuleInfo module)
    {
        string fileName = SanitizeFileName(module.UniqueId);
        return Path.Combine(ModsConfigDirectory, fileName + ".modConfig.json");
    }

    private static string SanitizeFileName(string fileName)
    {
        StringBuilder sanitizedFileName = new(fileName.Length);

        foreach (char character in fileName)
        {
            if (character is '/' or '\\')
            {
                sanitizedFileName.Append('.');
            }
            else if (Array.IndexOf(Path.GetInvalidFileNameChars(), character) < 0)
            {
                sanitizedFileName.Append(character);
            }
        }

        if (sanitizedFileName.Length == 0)
        {
            sanitizedFileName.Append("invalidModuleId");
        }

        return sanitizedFileName.ToString();
    }

    public static bool LoadModConfig(ModuleInfo module, out JToken? modConfig)
    {
        modConfig = null;
        string path = ModConfigFilePath(module);
        
        if (!FileAccess.FileExists(path) || !LoadFile(path, out string content))
        {
            return false;
        }
        
        modConfig = JsonConvert.DeserializeObject<JToken>(content);
        return true;
    }

    public static bool SaveModConfig(ModuleInfo module, IModConfig? modConfig)
    {
        if (modConfig == null)
        {
            return false;
        }

        if (!EnsureDirExists(ModsConfigDirectory))
        {
            return false;
        }

        return SaveFile(ModConfigFilePath(module), JsonConvert.SerializeObject(modConfig is JTokenModConfig modConfigJson ? modConfigJson.Token : modConfig, Formatting.Indented));
    }

    public static bool LoadFile(string filePath, out string content)
    {
        FileAccess fileAccess = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);

        Error error = fileAccess?.GetError() ?? FileAccess.GetOpenError();
        switch (error)
        {
            case Error.Ok:
                Debug.Assert(fileAccess != null, nameof(fileAccess) + " != null");
                content = fileAccess.GetAsText();
                fileAccess.Close();
                return true;
            case Error.FileNotFound:
                GD.PrintErr("[GodotMonoModLoader] File not found: " + filePath);
                break;
            case Error.AlreadyInUse:
                GD.PrintErr("[GodotMonoModLoader] Access denied to file: " + filePath);
                break;
            default:
                GD.PrintErr("[GodotMonoModLoader] Error loading file: " + error);
                break;
        }

        content = string.Empty;
        return false;
    }

    public static bool SaveFile(string filePath, string content)
    {
        FileAccess fileAccess = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
        Error error = fileAccess?.GetError() ?? FileAccess.GetOpenError();
        switch (error)
        {
            case Error.Ok:
                Debug.Assert(fileAccess != null, nameof(fileAccess) + " != null");
                fileAccess.StoreString(content);
                fileAccess.Close();
                return true;
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
        return false;
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
}