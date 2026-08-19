
using System.ComponentModel;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Loader;
using Godot;
using Godot.Bridge;
using Godot.NativeInterop;
using static System.String;
using MethodInfo = System.Reflection.MethodInfo;

namespace Atomcraft;

[ScriptPath("res://GodotMonoModLoaderPatch/GodotMonoModLoaderPatch.cs")]
public partial class GodotMonoModLoaderPatch : Node
{

	public new class MethodName : Node.MethodName
	{
		public static readonly StringName LoadDllFromZip = "LoadDllFromZip";

		public static readonly StringName LoadDllFromPath = "LoadDllFromPath";
	}

	public new class PropertyName : Node.PropertyName
	{
	}

	public new class SignalName : Node.SignalName
	{
	}
	
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

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static List<Godot.Bridge.MethodInfo> GetGodotMethodList()
    {
        List<Godot.Bridge.MethodInfo> list = new List<Godot.Bridge.MethodInfo>(2);
        list.Add(new Godot.Bridge.MethodInfo(MethodName.LoadDllFromZip, new Godot.Bridge.PropertyInfo(Variant.Type.Int, "", PropertyHint.None, "", PropertyUsageFlags.Default, exported: false), MethodFlags.Normal, new List<Godot.Bridge.PropertyInfo>
        {
            new Godot.Bridge.PropertyInfo(Variant.Type.String, "zipPath", PropertyHint.None, "", PropertyUsageFlags.Default, exported: false),
            new Godot.Bridge.PropertyInfo(Variant.Type.String, "dllPath", PropertyHint.None, "", PropertyUsageFlags.Default, exported: false),
            new Godot.Bridge.PropertyInfo(Variant.Type.String, "initClass", PropertyHint.None, "", PropertyUsageFlags.Default, exported: false)
        }, null));
        list.Add(new Godot.Bridge.MethodInfo(MethodName.LoadDllFromPath, new Godot.Bridge.PropertyInfo(Variant.Type.Int, "", PropertyHint.None, "", PropertyUsageFlags.Default, exported: false), MethodFlags.Normal, new List<Godot.Bridge.PropertyInfo>
        {
            new Godot.Bridge.PropertyInfo(Variant.Type.String, "dllPath", PropertyHint.None, "", PropertyUsageFlags.Default, exported: false),
            new Godot.Bridge.PropertyInfo(Variant.Type.String, "initClass", PropertyHint.None, "", PropertyUsageFlags.Default, exported: false)
        }, null));
        return list;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override bool InvokeGodotClassMethod(in godot_string_name method, NativeVariantPtrArgs args, out godot_variant ret)
    {
        if (method == MethodName.LoadDllFromZip && args.Count == 3)
        {
            ret = VariantUtils.CreateFrom<int>(LoadDllFromZip(VariantUtils.ConvertTo<string>(in args[0]), VariantUtils.ConvertTo<string>(in args[1]), VariantUtils.ConvertTo<string>(in args[2])));
            return true;
        }
        if (method == MethodName.LoadDllFromPath && args.Count == 2)
        {
            ret = VariantUtils.CreateFrom<int>(LoadDllFromPath(VariantUtils.ConvertTo<string>(in args[0]), VariantUtils.ConvertTo<string>(in args[1])));
            return true;
        }
        return base.InvokeGodotClassMethod(in method, args, out ret);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override bool HasGodotClassMethod(in godot_string_name method)
    {
        if (method == MethodName.LoadDllFromZip)
        {
            return true;
        }
        if (method == MethodName.LoadDllFromPath)
        {
            return true;
        }
        return base.HasGodotClassMethod(in method);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override void SaveGodotObjectData(GodotSerializationInfo info)
    {
        base.SaveGodotObjectData(info);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override void RestoreGodotObjectData(GodotSerializationInfo info)
    {
        base.RestoreGodotObjectData(info);
    }
}
