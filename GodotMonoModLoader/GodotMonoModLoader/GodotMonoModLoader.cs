
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Godot;
using Godot.Bridge;
using GodotMonoModLoader.Atomcraft;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MethodInfo = System.Reflection.MethodInfo;

namespace GodotMonoModLoader;

public partial class GodotMonoModLoader : RefCounted
{
	internal static GodotMonoModLoader Instance { get; private set; } = null!;

	internal ModLoaderLogger Logger { get; private set; }
	internal readonly AtomcraftModLoader AtomcraftModLoader;
	internal readonly Dictionary<string, ModInfo> Mods = [];
	internal readonly List<ModuleInfo> LoadedModules = [];
	internal readonly Dictionary<string, ModuleInfo> Modules = [];
	internal readonly Dictionary<string, ModuleInfo> ModulesByAssembly = [];
	internal readonly Dictionary<string, object?> ModsConfig = [];
	
	public GodotMonoModLoader()
	{
		Logger = new ModLoaderLogger();
		AtomcraftModLoader = new AtomcraftModLoader().Init(this);

		Instance ??= this;
	}

	public static void Initialize()
    {
	    var harmony = new Harmony("GodotMonoModLoader");
	    harmony.PatchAll();
    }
    
    public ModLoaderLogger InitLogger(ulong startTime, string[] logHistory)
    {
	    return Logger.Init(startTime, [..logHistory]);
    }

    public bool LoadDllFromZip(ModInfo mod, ModuleInfo module)
    {
		Assembly? assembly;
		try
		{
			using ZipArchive archive = ZipFile.OpenRead(mod.Path);

			Debug.Assert(module.Dll != null, "module.Dll != null");
			string dllPath = mod.Id.PathJoin(module.Dll);
			ZipArchiveEntry? entry = archive.GetEntry(dllPath);

			if (entry == null) {
				throw new Exception("DLL not found: " + dllPath);
			}

			using Stream input = entry.Open();
			using MemoryStream memory = new();

			input.CopyTo(memory);

			memory.Position = 0;

			// Check for Harmony namespace before load
			// using AssemblyDefinition assemblyDef = Assembly.ReflectionOnlyLoad.ReadAssembly(memory) {
			//
			// 	bool hasHarmony = assemblyDef.Modules.Any(definition => definition.GetTypes().Select(t =>
			// 	{
			// 		string ns = t.Namespace ?? "";
			// 		int firstDot = ns.IndexOf('.');
			// 		return firstDot == -1 ? ns : ns.Substring(0, firstDot);
			// 	}).Any(ns => ns.Equals("0Harmony")));
			// 	
			// 	
			// 	memory.Position = 0;
			// }
			
			assembly = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly())?.LoadFromStream(memory);
			
			if (assembly == null)
			{
				throw new Exception("Could not load DLL: " + dllPath);
			}

			string assemblyName = assembly.GetName().Name
				?? throw new Exception("Loaded assembly does not have a name.");
			ModulesByAssembly[assemblyName] = module;
			Modules[module.ModuleId] = module;
			
			ScriptManagerBridge.LookupScriptsInAssembly(assembly);
			
			Logger.LogMessage($"DLL Loaded: {assembly.FullName}");

		}
		catch (Exception e)
		{
			module.ErrorMessage = "Error while trying to load the dll: " + module.Dll;
			Logger.LogError("Error loading DLL: " + module.Dll);
			Logger.LogError(e.ToString());
			return false;
		}

		
		if (!string.IsNullOrEmpty(module.EntryClass))
		{
			return TryInitializeMod(mod, module, assembly);
		}

		return true;
	}
	
	private bool TryInitializeMod(ModInfo mod, ModuleInfo module, Assembly assembly)
	{
		string? dllName = assembly.GetName().Name;
		Debug.Assert(module.EntryClass != null, "module.EntryClass != null");
		string entryClass = module.EntryClass;

		try
		{
			Type? modEntryType = assembly.GetType(entryClass);

			if (modEntryType == null)
			{
				throw new Exception($"Class {entryClass} not found");
			}
			
			module.ModEntry = CreateModEntry(modEntryType).Init(mod, module);

			if (module.ModEntry is IModInitializationProvider modEntry)
			{
				FileUtils.LoadModConfig(module, out JToken? modConfig);
				
				InitializationContext context = modEntry.Initialize(modConfig);
				
				if (context._modConfig != null)
				{
					ModsConfig[module.ModuleId] = context._modConfig;
					FileUtils.SaveModConfig(module, context._modConfig);
				}
			}
		}
		catch (Exception e)
		{
			module.ErrorMessage = "Error while trying to initialize the dll: " + dllName;
			Logger.LogMessage($"Error initializing DLL: {dllName}");
			Logger.LogError(e.ToString());
			return false;
		}
		
		Logger.LogMessage($"DLL {dllName} initialized!");
		
		return true;
	}

	private static GMMLModEntry CreateModEntry(Type modEntryType)
	{
		GMMLModEntry? modEntry;
		if (modEntryType.IsAssignableTo(typeof(GMMLModEntry)))
		{
			modEntry = (GMMLModEntry?) Activator.CreateInstance(modEntryType);
				
			if (modEntry == null)
			{
				throw new Exception("Could not create mod entry instance from: " + modEntryType);
			}
				
		}
		else
		{
			modEntry = new StaticModEntry(modEntryType);
			// MethodInfo? initializeMethod = modEntryType.GetMethod(
			// 	"Initialize",
			// 	BindingFlags.Public |
			// 	BindingFlags.Static
			// );
			//
			// if (initializeMethod == null)
			// {
			// 	throw new Exception($"Public Static method {entryClass}.Initialize() not found");
			// }
			//
			// initializeMethod.Invoke(null, null);
		}

		return modEntry;
	}

	public JToken? ReadModConfig(ModuleInfo module)
	{
		
		return null;
	}
	
	public Godot.Collections.Dictionary<string, ModInfo> LoadMods()
	{
		try
		{
			Mods.Clear();
			LookupMods();
			GetLoadableModules(true);
			Logger.LogMessage("Modules loaded: " + LoadedModules.Count);
		}
		catch (Exception e)
		{
			Logger.LogError("Error while loading mods: " + e);
			throw;
		}

		return new Godot.Collections.Dictionary<string, ModInfo>(Mods);
	}
 
    public List<ModuleInfo> GetLoadableModules(bool loadNow)
    {
        Dictionary<string, ModuleInfo> modules = [];
        List<ModuleInfo> loadableModules = [];
        List<string> modulesToLoad = [];
 
        List<string> modIds = [.. Mods.Keys];
        modIds.Sort(StringComparer.Ordinal);
 
        foreach (string modId in modIds)
        {
            ModInfo mod = Mods[modId];
            foreach (string moduleId in mod.Modules.Keys)
            {
                ModuleInfo module = mod.Modules[moduleId];
                modules[moduleId] = module;
                if (module.State < ModuleState.Ready)
                {
                    modulesToLoad.Add(moduleId);
                    if (module.Optional)
                    {
                        module.State = ModuleState.Optional;
                    }
                }
            }
        }
 
        Logger.LogMessage("Modules to load: [" + string.Join(", ", modulesToLoad) + "]");
        Logger.LogSeparator();
        modulesToLoad.Reverse();
        
        bool doneSomething = true;
        bool lastLoop = false;

        Version apiVersion = new(AutogeneratedConstants.VERSION);
        
        while (modulesToLoad.Count > 0 && (doneSomething || lastLoop))
        {
            doneSomething = false;
            int i = modulesToLoad.Count;
 
            while (i > 0)
            {
                string moduleId = modulesToLoad[--i];
                ModuleInfo module = modules[moduleId];
 
                // Check API Version
                if (module.Mod.ParsedAPIVersion > apiVersion)
                {
	                module.State = ModuleState.Error;
	                doneSomething = true;
	                modulesToLoad.RemoveAt(i);
	                module.ErrorMessage = $"This mod requires a newer version of the mod loader (at least {module.Mod.APIVersion}, current: {AutogeneratedConstants.VERSION})";
	                Logger.LogMessage($"Mod {module.Mod.Id} requires a newer version of the mod loader (at least {module.Mod.APIVersion}, current: {AutogeneratedConstants.VERSION})");
	                Logger.LogSeparator();
	                
	                continue;
                }
                
                // Check if dependencies cannot load (don't exist or have errors)
                bool cannotLoad = false;
                foreach (string depId in module.Dependencies)
                {
                    if (!modules.TryGetValue(depId, out ModuleInfo? dep) || dep.State == ModuleState.Error)
                    {
	                    if (!cannotLoad)
	                    {
		                    cannotLoad = true;
		                    
		                    module.State = ModuleState.Error;
		                    doneSomething = true;
		                    modulesToLoad.RemoveAt(i);
		                    module.ErrorMessage = "Missing dependencies: " + depId;
		                    Logger.LogMessage("Missing dependencies for ", moduleId, ":");
	                    }
	                    else
	                    {
		                    module.ErrorMessage += ", " + moduleId;
	                    }
	                    Logger.LogMessage(" - ", depId);
                    }
                }

                if (cannotLoad)
                {
                    Logger.LogSeparator();

                    continue;
                }
                
                // Check if dependencies haven't been loaded yet
                if (module.Dependencies.Any(depId => modules.TryGetValue(depId, out ModuleInfo? dep)
                                                     && dep.State != ModuleState.Ready && dep.State != ModuleState.Loaded))
                {
	                
	                // Mark optional modules that need to load
                    if (module.Dependencies.All(depId => modules.GetValueOrDefault(depId)?.State is ModuleState.Optional or ModuleState.Ready or ModuleState.Loaded))
                    {
	                    module.Dependencies.DoIf(depId => modules[depId].State == ModuleState.Optional, 
		                    depId => modules[depId].State = ModuleState.Default);
                        
                        doneSomething = true;
                    }
                    continue;
                }
                
                // Check if optional dependencies haven't been loaded yet
                if (!lastLoop && module.OptionalDependencies.Any(depId =>
	                    modules.TryGetValue(depId, out ModuleInfo? dep) && dep.State < ModuleState.Ready))
                {
	                // Mark optional modules that need to load
	                if (module.OptionalDependencies.All(depId => !modules.TryGetValue(depId, out ModuleInfo? dep) || dep.State == ModuleState.Optional || dep.State >= ModuleState.Ready))
	                {
		                module.OptionalDependencies.DoIf(depId => modules.GetValueOrDefault(depId)?.State == ModuleState.Optional, 
			                depId => modules.GetValueOrDefault(depId)!.State = ModuleState.Default);
                        
		                doneSomething = true;
	                }
                    continue;
                }
                
                // Ignore Optional modules
                if (module.State == ModuleState.Optional)
                {
                    continue;
                }
                
                if (loadNow)
                {
                    if (LoadModule(module.Mod, module))
                    {
                        module.State = ModuleState.Loaded;
                        LoadedModules.Add(module);
                    }
                    else
                    {
                        module.State = ModuleState.Error;
                    }
                }
                else
                {
                    module.State = ModuleState.Ready;
                    loadableModules.Add(module);
                }
                doneSomething = true;
                modulesToLoad.RemoveAt(i);
                Logger.LogSeparator();
            }
 
            // When no more modules can be loaded due to dependency restrictions, it does an
            // extra iteration to load all modules that were waiting for optionalDependencies.
            lastLoop = !lastLoop && !doneSomething;
        }
 
        if (modulesToLoad.Count > 0)
        {
            Logger.LogMessage("This modules cannot be loaded because there are cyclic dependencies:");
            
            foreach (string moduleId in modulesToLoad)
            {
                if (modules[moduleId].State != ModuleState.Optional)
                {
                    Logger.LogMessage("Module: " + moduleId + ", Dependencies: [" +
                                       string.Join(", ", modules[moduleId].Dependencies) + "]");
                }
            }
        }
 
        return loadNow ? LoadedModules : loadableModules;
    }
 
    public bool LoadModule(ModInfo mod, ModuleInfo module)
    {
	    try
	    {
	        Logger.LogMessage("Loading Mod: " + mod.Id + ", Module: " + module.ModuleId);
	        Logger.LogMessage("Zip: " + mod.Path);
	 
	        if (module.LoadAsResourcePack)
	        {
	            ProjectSettings.LoadResourcePack(mod.Path);
	            mod.LoadedAsResourcePack = true;
	        }
	 
	        if (!string.IsNullOrEmpty(module.Dll))
	        {
	            if (!LoadDll(mod, module))
	            {
	                return false;
	            }
	        }

	        if (!AtomcraftModLoader.LoadModule(mod, module))
	        {
		        return false;
	        }

	    }
	    catch (Exception e)
	    {
		    Logger.LogError("Error loading module: " + module.ModuleId);
		    Logger.LogError(e.ToString());
		    return false;
	    }
	    Logger.LogMessage("Module loaded correctly: " + module.ModuleId);
	    return true;
    }
 
    public bool LoadDll(ModInfo mod, ModuleInfo module)
    {
        Logger.LogMessage("Loading Dll: " + module.Dll);
        if (!string.IsNullOrEmpty(module.EntryClass))
        {
            Logger.LogMessage("EntryClass: " + module.EntryClass);
        }

        Debug.Assert(module.Dll != null, "module.Dll != null");
        return LoadDllFromZip(mod, module);
    }
 
    public Dictionary<string, ModInfo> LookupMods()
    {
        Logger.LogMessage("Looking up for mods...");
 
        RegisterBundledMods(Mods);
        ReadMods(Mods, LookupZips(FileUtils.ModsGameDirectory));
        ReadMods(Mods, LookupZips(FileUtils.ModsUserDirectory));
        // TODO read mods from steam when it gets implemented
 
        return Mods;
    }

    public void RegisterBundledMods(Dictionary<string, ModInfo> modList)
    {
	    ModInfo mod = new ModInfo
	    {
		    Id = "GodotMonoModLoader",
		    Name = "Mod Loader",
		    Description = "This mod loader is used to load all the mods.",
		    Author = "sacroimper",
		    Version = AutogeneratedConstants.VERSION,
		    Path = "bundled"
	    };

	    modList[mod.Id] = mod;

	    mod = new ModInfo
	    {
		    Id = "0Harmony",
		    Name = "Harmony",
		    Description = "Harmony library, used by other mods.",
		    Author = "Andreas Pardeike (main author of the library)",
		    Version = AutogeneratedConstants.HARMONY_VERSION,
		    Path = "bundled"
	    };

	    mod.Modules = new Dictionary<string, ModuleInfo> {
		    ["0Harmony/Library"] = new ModuleInfo
		    {
			    Mod = mod,
			    ModuleId = "0Harmony/Library",
			    State = ModuleState.Loaded
		    }
        };


    modList[mod.Id] = mod;
    }
 
    public List<string> LookupZips(string path)
    {
	    List<string> zipPaths = [];
 
	    if (DirAccess.DirExistsAbsolute(path))
	    {
		    string[] files = DirAccess.GetFilesAt(path);
		    foreach (string file in files)
		    {
			    if (file.GetExtension() == "zip")
			    {
				    zipPaths.Add(path.PathJoin(file));
			    }
		    }
	    }
 
	    return zipPaths;
    }
 
    public void ReadMods(Dictionary<string, ModInfo> modList, List<string> modPaths)
    {
        foreach (string modPath in modPaths)
        {
            if (!TryReadMod(modPath, out ModInfo? modInfo))
            {
                Logger.LogMessage("Could not load Mod: ", modPath);
                continue;
            }
            
            if (modList.TryGetValue(modInfo!.Id, out ModInfo? previousModInfo))
            {
	            bool newer = previousModInfo.Path != "bundled" && modInfo.ParsedVersion > previousModInfo.ParsedVersion;
	            Logger.LogMessage($"Duplicated Mod {modInfo.Id}");
	            Logger.LogMessage($" Version: {previousModInfo.Version} {(newer ? "": "(keeping)")} Path: " + previousModInfo.Path);
	            Logger.LogMessage($" Version: {modInfo.Version} {(newer ? "(keeping)": "")} Path: " + modInfo.Path);
	            if (!newer)
	            {
		            continue;
	            }
            }
            
            modList[modInfo.Id] = modInfo;
        }
    }
 
    public bool TryReadMod(string modPath, out ModInfo? modInfo)
    {
        modInfo = null;
        if (!TryReadModJson(modPath, out string? jsonAsText))
        {
            return false;
        }
 
	    Debug.Assert(jsonAsText != null, nameof(jsonAsText) + " != null");
        try
        {
	        modInfo = JsonConvert.DeserializeObject<ModInfo>(jsonAsText);
        }
        catch (Exception e)
        {
            Logger.LogMessage("Error reading mod.json: ", modPath);
            Logger.LogMessage(e.ToString());
            return false;
        }
 
        if (modInfo == null || string.IsNullOrEmpty(modInfo.Id))
        {
            Logger.LogMessage("Error reading mod.json: ", modPath);
            return false;
        }
        
        ModInfo mod = modInfo;
        modInfo.Modules.Values.Do(m =>
        {
	        m.Mod = mod;
	        if (!m.ModuleId.StartsWith(mod.Author) && !m.ModuleId.StartsWith(mod.Id))
	        {
		        m.ModuleId = mod.Id + "/" + m.ModuleId;
	        }
        });
        modInfo.Path = modPath;
        return true;
    }
    
    public bool TryReadModJson(string modPath, out string? jsonAsText)
    {
        jsonAsText = null;
        using ZipReader reader = new();
        //ZipReader reader = new();
        Error err = reader.Open(modPath);
        if (err != Error.Ok)
        {
            Logger.LogMessage("Error opening mod file: ", modPath);
            Logger.LogMessage(err.ToString());
            return false;
        }
 
        string[] files = reader.GetFiles();
        foreach (string file in files)
        {
            if (file.GetFile() == "mod.json")
            {
                byte[] bytes = reader.ReadFile(file);
                jsonAsText = Encoding.UTF8.GetString(bytes);
                return true;
            }
        }
        
        Logger.LogMessage("File mod.json couldn't be located in mod file: ", modPath);
        return false;
    }

    public ModLoaderReport GetReport()
    {
	    PackedScene packedScene = GD.Load<PackedScene>(
		    Path.Combine(OS.GetExecutablePath().GetBaseDir(), "GodotMonoModLoader", "Resources", "UI", "ModLoaderReport.tscn"));
	    ModLoaderReport scene = packedScene.Instantiate<ModLoaderReport>().Init(Mods, Logger);
	    return scene;
    }
}