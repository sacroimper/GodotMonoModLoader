
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using Godot;
using Godot.Bridge;
using static System.String;
using MethodInfo = System.Reflection.MethodInfo;

namespace Atomcraft;

[ScriptPath("res://GodotMonoModLoaderPatch/GodotMonoModLoaderPatch.cs")]
public partial class GodotMonoModLoaderPatch : Node
{
	
	public int LoadDllFromZip(string zipPath, string dllPath, string? initClass)
	{
		Assembly? assembly;
		try
		{
			using ZipArchive archive = ZipFile.OpenRead(zipPath);

			ZipArchiveEntry? entry = archive.GetEntry(dllPath);

			if (entry == null) {
				throw new Exception("DLL not found: " + dllPath);
			}

			using Stream input = entry.Open();
			using MemoryStream memory = new MemoryStream();

			input.CopyTo(memory);

			memory.Position = 0;
			
			assembly = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly())?.LoadFromStream(memory);
			
			if (assembly == null)
			{
				throw new Exception("Could not load DLL: " + dllPath);
			}
			
			ScriptManagerBridge.LookupScriptsInAssembly(assembly);
			
			GD.Print($"[DllLoader] DLL Loaded: {assembly.FullName}");

		}
		catch (Exception e)
		{
			GD.PrintErr(e);
			return 1;
		}

		
		try
		{
			if (!IsNullOrEmpty(initClass))
			{
				if (!InitializeMod(assembly, initClass))
				{
					return 2;
				}
			}
		}
		catch (Exception e)
		{
			GD.PrintErr("[DllLoader] ", e);
			return 1;
		}

		return 0;
	}

	public int LoadDllFromPath(string dllPath, string? initClass)
	{
		Assembly? assembly;
		try
		{
			assembly = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly())?.LoadFromAssemblyPath(dllPath);

			if (assembly == null)
			{
				throw new Exception("[DllLoader] Could not load DLL: " + dllPath);
			}
			
			ScriptManagerBridge.LookupScriptsInAssembly(assembly);

			GD.Print($"[DllLoader] DLL Loaded: {assembly.FullName}");

		}
		catch (Exception e)
		{
			GD.PrintErr(e);
			return 1;
		}

		
		try
		{
			if (!IsNullOrEmpty(initClass))
			{
				if (!InitializeMod(assembly, initClass))
				{
					return 2;
				}
			}
		}
		catch (Exception e)
		{
			GD.PrintErr("[DllLoader] ", e);
			return 1;
		}

		return 0;
	}
	
	private bool InitializeMod(Assembly assembly, string initClass)
	{
		string? dllName = assembly.GetName().Name;

		Type? modEntryType = assembly.GetType(initClass);

		if (modEntryType == null)
		{
			throw new Exception($"Class {initClass} not found");
		}

		MethodInfo? initializeMethod = modEntryType.GetMethod(
				"Initialize",
				BindingFlags.Public |
				BindingFlags.Static
			);

		if (initializeMethod == null)
		{
			throw new Exception($"Public Static method {initClass}.Initialize() not found");
		}

		initializeMethod.Invoke(null, null);

		GD.Print($"[DllLoader] DLL {dllName} initialized!");

		return true;
	}
}
