# Godot Mono Mod Loader bootstrap

This directory contains an optional, standalone bootstrap path for the existing
GodotMonoModLoader. It does not modify the existing C# projects, patcher, or
`GodotMonoModLoader.gd`.

The Windows proxy is named `dinput8.dll`. It forwards the DirectInput exports
to the real system DLL and starts a background bootstrap thread. The thread
waits for the game's embedded CoreCLR/GodotSharp runtime, then uses
`GodotMonoModLoaderBootstrap.runtimeconfig.json` and `hostfxr.dll` to invoke
`GodotMonoModLoaderBootstrap.dll`.

The managed bootstrap loads the existing
`GodotMonoModLoaderPatch.dll` and registers it through
`Godot.Bridge.ScriptManagerBridge`. The current GDScript and patcher therefore
continue to work unchanged.

## Build

### Managed bootstrap

```powershell
dotnet publish .\GodotMonoModLoaderBootstrap.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o .\publish
```

Copy `publish\GodotMonoModLoaderBootstrap.dll` and
`publish\GodotMonoModLoaderBootstrap.deps.json` next to `AtomCraft.exe`.

### NativeAOT proxy

Publish the C# NativeAOT proxy project:

```powershell
dotnet publish ..\GodotMonoModLoaderProxy\GodotMonoModLoaderProxy.csproj `
  -c Release `
  -r win-x64 `
  -o .\proxy-publish
```

Copy the resulting `dinput8.dll` next to `AtomCraft.exe`.

NativeAOT publishing requires the Visual Studio **Desktop development with C++**
workload because the NativeAOT linker uses the MSVC platform linker.

The proxy expects these files next to the executable:

```text
AtomCraft.exe
dinput8.dll
GodotMonoModLoader\GodotMonoModLoaderBootstrap.runtimeconfig.json
GodotMonoModLoaderBootstrap.dll
GodotMonoModLoader\ModLoaderPatch.dll
```

The existing `GodotMonoModLoader` directory and `GodotMonoModLoader.gd` remain
unchanged.

## Diagnostics

Bootstrap diagnostics are written to:

```text
<game directory>\GodotMonoModLoaderBootstrap.log
```

If the log says that CoreCLR was not found, the proxy was loaded too early or
the game uses a different runtime layout. If it reports that
`Godot.Bridge.ScriptManagerBridge` was not found, the proxy must be tested
against the exact GodotSharp build shipped by the game.

The existing patched path remains available. Remove `dinput8.dll` and
`GodotMonoModLoaderBootstrap.dll` to return to the current behavior.
