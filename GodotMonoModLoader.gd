extends SceneTree

var ref
var mod_loader
var log_history: Array[String] = []

func _initialize():
	load_game()
	load_mods()


func _finalize():
	unload_current_scene()


class ModInfo:
	static var json_fields = ["id", "name", "description", "author", "version", "dllModules"]

	var id: String
	var name: String
	var description: String
	var author: String
	var version: String
	var dllModules: Dictionary[String, DllModuleInfo]
	var path: String

	func _to_string():
		return str("{ id: " + id + ", name: " + name + ", description: " + description + ", author: " + author + ", version: " + version + ", dllModules: " + str(dllModules) + ", path: " + path + " }")

enum State { DEFAULT, READY, LOADED, ERROR }

class DllModuleInfo:
	static var json_fields = ["moduleId", "dll", "initClass", "dependencies", "optionalDependencies"]

	var modId: String
	var moduleId: String
	var dll: String
	var initClass: String
	var dependencies: Array[String]
	var optionalDependencies: Array[String]
	var state = State.DEFAULT

	func _to_string():
		return str("{ moduleId: " + moduleId + ", dll: " + dll + ", initClass: " + initClass + ", dependencies: " + str(dependencies) + ", optionalDependencies: " + str(optionalDependencies) + ", state: " + str(State.keys()[state]) + " }")

func load_game() -> void:
	var main_scene := ProjectSettings.get_setting("application/run/main_scene") as String

	log_message(str("Loading Game main scene: ", main_scene))
	change_scene_to_file(main_scene)

	await process_frame
	await process_frame
	log_message("Game loaded")


func load_mods() -> void:
	var mods: Dictionary[String, ModInfo] = lookup_mods()
	var loadable_modules = get_loadable_modules(mods, true)
	log_message(str("Mods loaded: ", loadable_modules.size()))

func get_loadable_modules(mods: Dictionary[String, ModInfo], load_now: bool) -> Array[DllModuleInfo]:
	var modules: Dictionary[String, DllModuleInfo] = {}
	var loadable_modules: Array[DllModuleInfo] = []
	var modulesId: Array[String] = []
	for modId in mods:
		var mod = mods[modId]
		for moduleId in mod.dllModules:
			modulesId.append(moduleId)
			modules[moduleId] = mod.dllModules[moduleId]

	#modulesId.sort()
	log_message(str("Modules to load: ", modulesId))
	log_message("--------------------------------------------------------------------------------")
	modulesId.reverse()

	var doneSomething = true
	var lastLoop = false

	while not modulesId.is_empty() and (doneSomething or lastLoop):
		doneSomething = false
		var i = modulesId.size()

		while i > 0:
			i -= 1
			var moduleId = modulesId[i]
			var module: DllModuleInfo = modules[moduleId]
			if module.dependencies and module.dependencies.any(func (dependencyId): return modules[dependencyId] == null or modules[dependencyId].state == State.ERROR):
				module.state = State.ERROR
				log_message(str("Missing dependency for ", moduleId))
				doneSomething = true
				modulesId.remove_at(i)
				log_message("--------------------------------------------------------------------------------")
				continue
			elif module.dependencies and module.dependencies.any(func (dependencyId): return modules[dependencyId].state != State.READY and modules[dependencyId].state != State.LOADED):
				continue
			elif not lastLoop and module.optionalDependencies and module.optionalDependencies.any(func (dependencyId): return modules[dependencyId] != null and modules[dependencyId].state < State.READY):
				continue
			else:
				if load_now:
					if load_module(mods[module.modId], module):
						loadable_modules.append(module)
				else:
					module.state = State.READY
					loadable_modules.append(module)
				doneSomething = true
				modulesId.remove_at(i)
				log_message("--------------------------------------------------------------------------------")


		# When no more modules can be loaded due to dependency restrictions, it does an extra iteration to load all modules that were waiting for optionalDependencies
		lastLoop = not lastLoop and not doneSomething

	if !modulesId.is_empty():
		log_message("This modules cannot be loaded because there are cyclic dependencies:")
		for moduleId in modulesId:
			log_message(str("Module: ", moduleId, ", Dependencies: ", modules[moduleId].dependencies))


	return loadable_modules

func load_module(mod: ModInfo, module: DllModuleInfo) -> bool:

	if mod_loader == null:
		var ModLoader = load("res://GodotMonoModLoader/GodotMonoModLoader.cs")
		if ModLoader == null:
			module.state = State.ERROR
			log_message(str("Could not load module ", module.moduleId, ". The game has to be patched to be able to load custom dlls"))
			return false
		mod_loader = ModLoader.new()
		if mod_loader == null:
			module.state = State.ERROR
			log_message(str("Could not load module ", module.moduleId, ". The game has to be patched to be able to load custom dlls"))
			return false

	log_message(str("Loading Mod: ", mod.id, ", Module: ", module.moduleId, ", Dll: ", module.dll))
	log_message(str("Path: ", mod.path))
	if module.initClass:
		log_message(str("InitClass: ", module.initClass))

	var error = mod_loader.LoadDllFromZip(mod.path, mod.id.path_join(module.dll), module.initClass)
	if error == 0:
		module.state = State.LOADED
		log_message(str("Module loaded: ", module.moduleId))
	else:
		module.state = State.ERROR
		if error == 1:
			log_message(str("Error while trying to load module: ", module.moduleId))
		elif error == 2:
			log_message(str("Error while trying to initialize module: ", module.moduleId))
	return error == 0

func lookup_mods() -> Dictionary[String, ModInfo]:
	var mod_list: Dictionary[String, ModInfo] = {}

	read_mods(mod_list, lookup_zips(OS.get_executable_path().get_base_dir().path_join("Mods")))
	read_mods(mod_list, lookup_zips(OS.get_user_data_dir().path_join("Mods")))
	# TODO read mods from steam

	return mod_list

func read_mods(mod_list : Dictionary[String, ModInfo], mod_paths: Array[String]):

	for mod_path in mod_paths:
		var mod_info = read_mod(mod_path)
		if mod_info == null or not mod_info.id:
			log_message(str("Could not load Mod: ", mod_path))
		elif mod_list.get(mod_info.id):
			log_message(str("Duplicated Mod ", mod_info.id, ": ", mod_path))
			log_message(str("Keeping ", mod_info.path))
		else:
			mod_list[mod_info.id] = mod_info


func read_mod(mod_path : String) -> ModInfo:


	var json_as_text = read_mod_json(mod_path)
	if json_as_text == "":
		return

	var json = JSON.new()
	var error = json.parse(json_as_text)
	if error == OK:
		var json_as_dict = json.data
		if typeof(json_as_dict) == TYPE_DICTIONARY :

			var mod_info = parse_mod_info(json_as_dict)

			mod_info.path = mod_path
			return mod_info
	return

func read_mod_json(mod_path : String) -> String:
	var reader = ZIPReader.new()
	var err = reader.open(mod_path)
	if err != OK:
		log_message(str("Error reading mod.json: ", mod_path))
		log_message(err)
		return ""

	for file in reader.get_files():
		if file.get_file() == "mod.json":
			var json_as_text = reader.read_file(file)
			reader.close()
			return json_as_text.get_string_from_utf8()

	reader.close()
	return ""

func parse_mod_info(json_as_dict: Dictionary) -> ModInfo:
	var mod_info = ModInfo.new()

	var properties : Array = mod_info.get_property_list()

	for property in properties:
		var key = property.name
		if ModInfo.json_fields.has(key):
			var value = json_as_dict[key]

			if value != null:
				if property.type == TYPE_STRING and typeof(value) == TYPE_STRING:
					mod_info.set(key, value)
				elif key == "dllModules" and typeof(value) == TYPE_ARRAY:
					var modules = parse_dll_modules(mod_info, value)
					mod_info.dllModules = modules

	return mod_info

func parse_dll_modules(mod_info: ModInfo, modules_as_json_array: Array) -> Dictionary[String, DllModuleInfo]:
	var dll_modules: Dictionary[String, DllModuleInfo] = {}
	for module_as_json in modules_as_json_array:
		var dll_module_info = DllModuleInfo.new()

		var properties : Array = dll_module_info.get_property_list()
		for property in properties:
			var key = property.name
			if DllModuleInfo.json_fields.has(key):
				var value = module_as_json.get(key)

				if value != null:
					if property.type == TYPE_STRING and typeof(value) == TYPE_STRING:
						dll_module_info.set(key, value)
					elif property.type == TYPE_ARRAY and typeof(value) == TYPE_ARRAY:
						var array:Array[String]
						array.assign(value)
						dll_module_info.set(key, array)

		if dll_module_info.moduleId != null and not dll_module_info.moduleId.is_empty():
			dll_module_info.modId = mod_info.id
			dll_modules[dll_module_info.moduleId] = dll_module_info

	return dll_modules




func lookup_zips(path : String) -> Array[String]:
	var zip_paths: Array[String] = []

	if DirAccess.dir_exists_absolute(path):
		var dir = DirAccess.get_files_at(path)
		for file in dir:
			if file.get_extension() == "zip":
				zip_paths.append(path.path_join(file))
	return zip_paths

func log_message(message: String):
	log_history.append(message)
	print(str("[GodotMonoModLoader]: ", message))
