
This is a Mod Loader for the game Atomcraft. 


# Installation instructions

These are the steps to launch the game with mods:

1. Download the GodotMonoModLoader.zip from Release ([Download](https://github.com/sacroimper/GodotMonoModLoader/raw/refs/heads/main/Release/GodotMonoModLoader.zip)).
2. Extract all contents into the game installation folder (next to Atomcraft.exe) without creating extra folders.
3. Install the mods as a Zip (don't extract) into `%AppData%/Godot/app_userdata/Atomcraft/Mods`, or the corresponding folder on Linux. (Alternatively, mods can also be installed in a Mods folder inside the game installation folder).
4. Execute the game with the launch parameter ` -s GodotMonoModLoader.gd`. This can be configured in Steam > Right-click the game in the library > Properties > General tab > Launch Options.

The mod loader will automatically patch and restart the game when launching if needed, but only if the main entry point fails or takes too long. It can also be manually patched or restored using the AtomcraftPatcher.exe included in the zip.

# Warning

**Currently, Atomcraft doesn't have official support for loading custom dlls into the game.<br/>
If the main entry point for the mod loader doesn't work, the game will be patched and restarted.<br/>
When the game is patched a backup file is created to be able to restore the game if needed.
The game can still be played without mods even when patched (launching without `-s GodotMonoModLoader.gd`).**<br/>
(The patched file is `data_Atomcraft_windows_x86_64/Atomcraft.dll`)

---

# Mods

Here is a list of the mods I've made: [AtomcraftMods](https://github.com/sacroimper/AtomcraftMods). I'm sure the community will share more through [Atomcraft Official discord](https://discord.com/invite/triplejump).

---

# Contact

For any issue or comment about the mod loader, you can find me in the official Atomcraft Discord as @sacroimper.

---

# For Modders

To make a mod that loads with this Mod Loader:

- It has to be packed as a zip and files must be placed inside a folder named with the ModId.
- The zip must contain one file named mod.json with the following format:

```json
{
  "id": "<ModId>",
  "name": "<Mod Name>",
  "description": "<Description>",
  "author": "<author>",
  "version": "<version>",
  "modules": [
    {
      "moduleId": "<ModId/ModuleId>",
      "dll": "<Path/To/Dll.dll>",
      "entryClass": "<Namespace.ClassName>",
      "materials": "<File or Folder to load Materials, same format as game JSON files>",
      "reactions": "<File or Folder to load Reactions, same format as game JSON files>",
      "translations": "<File or Folder to load translations, see JSON format below>",
      "loadAsResourcePack": "<true or false, needed to be able to access resources from the zip with 'res://'"
      "optional": "<true or false, with true this module will only be loaded if it is a dependency (or optionalDependency) of another module>"
      "dependencies": [
        "<moduleId that is required to be loaded before this one>",
        ...
      ],
      "optionalDependencies": [
        "<moduleId that is NOT required to be loaded, but if it exists, load it before this one>",
        ...
      ]
    },
    ...
  ]
}
```

- For `modules`, only `moduleId` is mandatory. The other fields can be used only when needed.
- The Harmony library is already loaded by default (version 2.4.2), don't include the dll in the mod.
- For `materials`, `reactions` and `translations`, files mush have `.json` extension. If a folder is defined, all JSON files in that folder, recursively, will be loaded.
- The JSON files for translations must have the following format:

```json
{
  "<language code, as in the game files>": {
    "<key>": "<string>",
    ...
  },
  ...
}
```
- If `entryClass` is defined it will be used as the Mod Entry type:

  - **If the type doesn't implement AtomcraftModEntry (Legacy mode):**<br/>Once the library is loaded, a **public static** method named ``Initialize`` will be called.
    Additionally, **public static** methods `OnUniverseLoad` and `OnUniverseSave` will also be called before loading and saving a world. A Serializable object can be received/returned on these methods to save data into the world file (it will be stored in a file <saveDir>/<world_name>.moddedUniverse).

  - **If the type implements AtomcraftModEntry:** An instance will be created after loading the library. Additionally:
    - This type can implement IModInitializationProvider to be called after loading the library. A custom serializable type can be defined with the interface to be used as the Mod Config (Ex: `IModInitializationProvider<MyModConfig>`).
    - This type can implement IUniverseLoadSaveProvider to be called after loading and before saving a save. A custom serializable type can be defined with the interface to be used as the save data (Ex: `IUniverseLoadSaveProvider<MySaveData>`).

## Running mods on the extracted game when launching from Godot

Follow this steps to be able to load mods when launching the game from Godot:

- Add the file ``GodotMonoModLoaderPatch.cs`` from the ``ModLoaderPatch`` project into the Godot project folder (any place where there are ``.cs`` files). 
- Add ``GodotMonoModLoader.gd`` from the download zip into the Godot project folder (preferably at the root).
- Go to ``Project > Project Settings > General (tab) > Editor > Run`` and set ``-s path/to/GodotMonoModLoader.gd`` into the property ``Main Run Args``.

Now the game will launch with the mod loader.

## Running the patcher from a script

The patcher can also be used from a terminal or a custom script. When stdin is not a terminal (a pipe, a file, or a CI log) the patcher skips the "Press ANY key" prompt and reports the outcome through its exit code:

```sh
AtomcraftPatcher /path/to/data_Atomcraft_windows_x86_64/Atomcraft.dll < /dev/null
```

| Option                    | Effect                                                                                                                                                                                                         |
|---------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `-y`, `--non-interactive` | Never wait for a keypress, even on a terminal.                                                                                                                                                                 |
| `--restore`               | Restore the backup instead of patching.                                                                                                                                                                        |
| `--fail-if-patched`       | Exit 6 instead of 0 when the target is already patched.                                                                                                                                                        |
| `--quiet`                 | Suppress progress output. Errors still go to stderr.                                                                                                                                                           |
| `--launch-game`           | Will launch the game on success, either applying patch or restoring.                                                                                                                                           |
| `--restart-game=<pid>`    | Will wait for the pid process to stop, then apply/restore the patch, and then launch the game on success.                                                                                                      |
| `--kill-game=<pid>`       | Will kill the pid process before applying the patch or restoring the backup.<br/>Can be used in combination with `--restart-game`. In this case the `<pid>` of this argument will be ignored (can be omited).  |
| `--headless`              | In combination with `--launch-game` or `--restart-game`, the game will be launched in headless mode.                                                                                                           |
| `--`                      | End of arguments. In combination with `--launch-game` or `--restart-game`, any extra argument will be passed to the game, otherwise will be ignored.                                                           | 
| `-h`, `--help`            | Show usage.                                                                                                                                                                                                    |

| Exit code | Meaning                                             |
|-----------|-----------------------------------------------------|
| 0         | Patch applied, or already patched                   |
| 1         | Unclassified failure                                |
| 2         | Usage error                                         |
| 3         | Atomcraft.dll or backup not found                   |
| 4         | ModLoaderPatch.dll missing or unusable              |
| 5         | Old patch detected, restore required                |
| 6         | Already patched, with `--fail-if-patched`           |
| 7         | Error while waiting for or trying to close the game |

A successful patch always exits 0, so `AtomcraftPatcher ... < /dev/null || exit 1` is enough to detect a failure. Use `--fail-if-patched` when a no-op needs to be distinguished from work actually done.

