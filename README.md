
This is a Mod Loader for the game Atomcraft. 

# Warning

**Currently, Atomcraft doesn't have official support for loading custom dlls into the game. 
For this mod loader to work, the game needs to be patched first (and every time it updates). 
A backup file is created to be able to restore the game if needed.**
(The patched file is `data_Atomcraft_windows_x86_64/Atomcraft.dll`) 

---

# Installation instructions

These are the steps to launch the game with mods:

1. Download the GodotMonoModLoader.zip from Release ([Download](https://github.com/sacroimper/GodotMonoModLoader/raw/refs/heads/main/Release/GodotMonoModLoader.zip)).
2. Extract all contents into the game installation folder (next to Atomcraft.exe).
3. Launch AtomcraftPatcher.exe (or AromcraftPatcher on Linux). It will confirm that the patch has been applied, it can also be used to restore the original file.
4. Install the mods into `%AppData%/Godot/app_userdata/Atomcraft/Mods`, or the corresponfing folder on Linux. (Alternatively, mods can also be installed in a Mods folder inside the game installation folder).
5. Execute the game with the launch parameter `-s GodotMonoModLoader.gd`. This can be configured in Steam > Right-click the game in the library > Properties > General tab > Launch Options.

Step 3 will need to be repeated each time the game updates.


# Mods

Here is a list of the mods I've made: [AtomcraftMods](https://github.com/sacroimper/AtomcraftMods). I'm sure the community will share more through Atomcraft Official discord.

---

# Modders

To make a mod that loads with this Mod Loader:

- It has to be packed as a zip and files have to be placed inside a folder named with the ModId.
- The zip must contain one file named mod.json with the following format:

```json
{
  "id": "<ModId>",
  "name": "<Mod Name>",
  "description": "<Desription>",
  "author": "<author>",
  "version": "<version>",
  "dllModules": [
    {
      "moduleId": "<ModId/ModuleId>",
      "dll": "<Path/To/Dll.dll>",
      "initClass": "<Namespace.ClassName>",
      "dependencies": [ "<moduleId that is required to be loaded before this one>", ... ],
      "optionalDependencies": [ "<moduleId that is NOT required to be loaded, but if it exists, load it before this one>", ... ]
    }, ...
  ]
}
```

- If initClass is defined, once the library is loaded, a public static method named `Initiallize` will be called. 

---

# Contact

For any issue or comment about the mod loader, you can find me in the oficial Atomcraft Discord as @sacroimper.
