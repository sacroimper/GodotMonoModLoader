extends SceneTree

var ref
var mod_loader
var old_mods: Dictionary[String, ModInfo]
var mods: Dictionary

var _logger: ModLoaderLogger


func _initialize():

	_logger = ModLoaderLogger.new()
	log_separator()
	
	var patcher_path = OS.get_executable_path().get_base_dir().path_join("AtomcraftPatcher.exe")

	if await load_mod_loader():
		mods = mod_loader.LoadMods();
	elif FileAccess.file_exists(patcher_path):
		log_message("Patching and restarting the game...")
		var my_pid = OS.get_process_id()
		var args = ["-y", str("--restart-game=", my_pid)]
		if OS.has_feature("headless"):
			args.append("--headless")
		OS.create_process(patcher_path, args)
		quit()
		return
	else:
		log_message("Patcher not found. Loading game without mods.")
		old_mods = {}
		register_bundled_mods(old_mods)


	load_game()
	

func _finalize():
	unload_current_scene()

class ModLoaderLogger:

	var start_time
	var history: Array[String] = []

	func _init():
		start_time = Time.get_ticks_msec()

	func LogSeparator():
		LogMessage("--------------------------------------------------------------------------------", true)
	
	func LogMessage(message: String, use_time = false):
		if use_time:
			var elapsed = Time.get_ticks_msec() - start_time
			message += str(" Time: ", elapsed, " ms")
		history.append(message)
		print(str("[GodotMonoModLoader]: ", message))

class ModInfo:
	static var json_fields = ["id", "name", "description", "author", "version", "modules"]

	var id: String
	var name: String
	var description: String
	var author: String
	var version: String
	var modules: Dictionary[String, ModuleInfo]
	var path: String

	func _to_string():
		return str("{ id: " + id + ", name: " + name + ", description: " + description + ", author: " + author + ", version: " + version + ", modules: " + str(modules) + ", path: " + path + " }")

enum ModuleState { OPTIONAL, DEFAULT, READY, LOADED, ERROR }

class ModuleInfo:
	static var json_fields = ["moduleId", "dll", "initClass", "materials", "reactions", "translations", "loadAsResourcePack", "optional", "dependencies", "optionalDependencies"]

	var modId: String
	var moduleId: String
	var dll: String
	var initClass: String
	var materials: String
	var reactions: String
	var translations: String
	var loadAsResourcePack: bool = false
	var optional: bool = false
	var dependencies: Array[String]
	var optionalDependencies: Array[String]
	var state = ModuleState.DEFAULT
	var errorMessage: String

	func _to_string():
		return str("{ moduleId: " + moduleId + ", dll: " + dll + ", initClass: " + initClass + ", materials: " + materials + ", reactions: " + reactions + ", translations: " + translations + ", loadAsResourcePack: " + str(loadAsResourcePack) + ", optional: " + str(optional) + ", dependencies: " + str(dependencies) + ", optionalDependencies: " + str(optionalDependencies) + ", state: " + str(ModuleState.keys()[state]) + " }")

func load_game() -> void:
	
	log_separator()
	
	var main_scene := ProjectSettings.get_setting("application/run/main_scene") as String

	log_message(str("Loading Game main scene: ", main_scene))
	change_scene_to_file(main_scene)

	await process_frame
	await process_frame
	await process_frame
	
	log_message("Game loaded.")
	log_separator()

	log_message("Loading report...")
	var mod_loader_report
	if mod_loader == null:
		mod_loader_report = load("./GodotMonoModLoader/ModLoaderReport.tscn").instantiate()
		mod_loader_report.initialize(old_mods, _logger.history, false)
	else:
		mod_loader_report = mod_loader.GetReport()

	self.root.add_child(mod_loader_report)
	log_message("Report loaded.")
	log_separator()

func load_mod_loader():

	log_message("Initializing mod loader...")
	
	var proxy_path = OS.get_executable_path().get_base_dir().path_join("dinput8.dll")

	var Patch
	var i = 0
	while i < 40:
		Patch = ResourceLoader.load("res://GodotMonoModLoaderPatch/GodotMonoModLoaderPatch.cs", "", ResourceLoader.CACHE_MODE_REPLACE)
		if (Patch != null and Patch.get_script_method_list().size() != 0) or !FileAccess.file_exists(proxy_path):
			break;
		
		await create_timer(0.5).timeout
		i += 1
	
	if Patch == null or Patch.get_script_method_list().size() == 0:
		log_message("The game needs to be patched to be able to load mods.")
		return false

	var patch = Patch.new()
	if patch == null:
		log_message("The game needs to be patched to be able to load mods.")
		return false

	var mod_loader_dir = OS.get_executable_path().get_base_dir().path_join("GodotMonoModLoader")

	var error = patch.LoadDllFromPath(mod_loader_dir.path_join("0Harmony.dll"), null)
	if error != 0:
		log_message("Error initializing mod loader.")
		return false

	error = patch.LoadDllFromPath(mod_loader_dir.path_join("GodotMonoModLoader.dll"), "GodotMonoModLoader.GodotMonoModLoader")
	if error != 0:
		log_message("Error initializing mod loader.")
		return false

	var ModLoader = load("res://GodotMonoModLoader/GodotMonoModLoader.cs")
	if ModLoader == null or ModLoader.get_script_method_list().size() == 0:
		log_message("Error initializing mod loader.")
		return false
	
	mod_loader = ModLoader.new()
	if mod_loader == null:
		log_message("Error initializing mod loader.")
		return false

	_logger = mod_loader.InitLogger(_logger.start_time, _logger.history);

	log_message("Mod loader initialized.")
	log_separator()
	return true


func register_bundled_mods(mod_list: Dictionary[String, ModInfo]):

	var mod: ModInfo = ModInfo.new()

	mod.id = "GodotMonoModLoader"
	mod.name = "Mod Loader"
	mod.description = "This mod loader is used to load all the mods."
	mod.author = "sacroimper"
	mod.version = "@VERSION@"
	mod.modules = {}
	mod.path = "bundled"

	mod_list.set(mod.id, mod)


	var modules: Dictionary[String, ModuleInfo] = {}
	
	var module: ModuleInfo = ModuleInfo.new()
	module.modId = "0Harmony"
	module.moduleId = "0Harmony/Library"
	module.state = ModuleState.ERROR
	module.errorMessage = "Couldn't load because the game is not patched"

	modules.set(module.moduleId, module)


	mod = ModInfo.new()

	mod.id = "0Harmony"
	mod.name = "Harmony"
	mod.description = "Harmony library, used by other mods."
	mod.author = "Andreas Pardeike (main author of the library)"
	mod.version = "@HARMONY_VERSION@"
	mod.modules = modules
	mod.path = "bundled"
	
	mod_list.set(mod.id, mod)

func log_separator():
	_logger.LogSeparator()

func log_message(message: String):
	_logger.LogMessage(message)
