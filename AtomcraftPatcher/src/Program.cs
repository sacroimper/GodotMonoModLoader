using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AtomcraftPatcher;

internal static class Program
{
    private const int ExitSuccess = 0;
    private const int ExitFailure = 1;
    private const int ExitUsage = 2;
    private const int ExitTargetNotFound = 3;
    private const int ExitPatchUnusable = 4;
    private const int ExitOldPatch = 5;
    private const int ExitAlreadyPatched = 6;

    private static string _targetPath = Path.Combine(AppContext.BaseDirectory, "data_Atomcraft_windows_x86_64", "Atomcraft.dll");

    private static bool _nonInteractive;
    private static bool _quiet;
    private static bool _failIfPatched;
    private static bool _restoreOnly;

    private static bool IsInteractive =>
        !_nonInteractive && !Console.IsInputRedirected && Environment.UserInteractive;

    static int Main(string[] args)
    {
        
        string typeToBeInjected = "Atomcraft.GodotMonoModLoaderPatch";
        string oldType = "Atomcraft.GodotMonoModLoader";

        if (!TryParseArguments(args, out string? targetArgument, out bool showHelp))
        {
            PrintUsage(Console.Error);
            return Exit(ExitUsage);
        }

        if (showHelp)
        {
            PrintUsage(Console.Out);
            return ExitSuccess;
        }

        if (targetArgument != null)
        {
            _targetPath = Path.GetFullPath(targetArgument);
            if (!File.Exists(_targetPath))
            {
                Error($"File not found: {_targetPath}");
                return Exit(ExitTargetNotFound);
            }
        }
        else
        {
            if (!File.Exists(_targetPath))
            {
                Info($"File not found: {_targetPath}");
                Info("Trying one folder up.");
                _targetPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "data_Atomcraft_windows_x86_64", "Atomcraft.dll"));
                if (!File.Exists(_targetPath))
                {
                    Error($"File not found: {_targetPath}");
                    return Exit(ExitTargetNotFound);
                }
            }
        }

        Info($"File to patch located: {_targetPath}.");

        if (_restoreOnly)
        {
            return Exit(RestoreBackup());
        }

        
        string patcherDirectory = AppContext.BaseDirectory;
        string patchPath = Path.Combine(patcherDirectory, "GodotMonoModLoader", "ModLoaderPatch.dll");
        
        string backupPath = _targetPath + ".backup";
        string tempPath = _targetPath + ".patched";

        if (!File.Exists(patchPath))
        {
            Error($"Patch Not Found: {patchPath}");
            return Exit(ExitPatchUnusable);
        }

        try
        {
            Info("Reading Atomcraft.dll...");

            using DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();
            
            string targetDirectory = Path.GetDirectoryName(_targetPath)!;

            resolver.AddSearchDirectory(targetDirectory);

            ReaderParameters readerParameters = new ReaderParameters
            {
                AssemblyResolver = resolver
            };

            
            using (AssemblyDefinition target = AssemblyDefinition.ReadAssembly(_targetPath, readerParameters))
            using (AssemblyDefinition patch = AssemblyDefinition.ReadAssembly(patchPath, readerParameters)) 
            {

                ModuleDefinition targetModule = target.MainModule;
                ModuleDefinition modModule = patch.MainModule;

                TypeDefinition? sourceType = modModule.GetType(typeToBeInjected);

                if (sourceType == null)
                {
                    Error($"Class not found {typeToBeInjected} in {patchPath}.");
                    return Exit(ExitPatchUnusable);
                }

                TypeDefinition? existingType = targetModule.GetType(typeToBeInjected);

                if (existingType != null)
                {
                    Info($"{typeToBeInjected} class already patched.");
                    target.Dispose(); // Required to be able to restore the target with the backup
                    return Exit(_failIfPatched ? ExitAlreadyPatched : ExitSuccess, true);
                }
                
                TypeDefinition? existingOldType = targetModule.GetType(oldType);
                
                if (existingOldType != null)
                {
                    Error("Old patch detected. Restore the backup or check files integrity from Steam.");
                    target.Dispose(); // Required to be able to restore the target with the backup
                    return Exit(ExitOldPatch, true);
                }

                Info($"Injecting {typeToBeInjected}...");

                Dictionary<TypeDefinition, TypeDefinition> typeMap = new();
                Dictionary<FieldDefinition, FieldDefinition> fieldMap = new();
                Dictionary<MethodDefinition, MethodDefinition> methodMap = new();

                TypeDefinition injectedType = CloneType(sourceType, targetModule, typeMap, fieldMap, methodMap);

                targetModule.Types.Add(injectedType);

                AddTypeToGodotObjectSystem(target, injectedType);

                // foreach (var reference in targetModule.AssemblyReferences)
                // {
                //     Console.WriteLine(
                //         $"{reference.FullName}");
                // }

                Info("Creating backup...");

                File.Copy(_targetPath, backupPath, overwrite: true);
                
                Info($"Backup created: {backupPath}");
                Info("Applying patch...");
                
                target.Write(tempPath);
            }

            File.Move(tempPath, _targetPath, overwrite: true);

            Info();
            Info("Patch applied.");
            
            return Exit(ExitSuccess, true);
        }
        catch (Exception ex)
        {
            Error();
            Error("ERROR:");
            Error(ex.ToString());

            return Exit(ExitFailure);
        }
    }

    static bool TryParseArguments(string[] args, out string? targetArgument, out bool showHelp)
    {
        targetArgument = null;
        showHelp = false;

        bool optionsEnded = false;

        foreach (string arg in args)
        {
            if (!optionsEnded)
            {
                switch (arg)
                {
                    case "--":
                        optionsEnded = true;
                        continue;
                    case "-y":
                    case "--non-interactive":
                        _nonInteractive = true;
                        continue;
                    case "--quiet":
                        _quiet = true;
                        continue;
                    case "--fail-if-patched":
                        _failIfPatched = true;
                        continue;
                    case "--restore":
                        _restoreOnly = true;
                        continue;
                    case "-h":
                    case "--help":
                        showHelp = true;
                        continue;
                }

                if (arg.StartsWith('-') && arg.Length > 1)
                {
                    Error($"Unknown option: {arg}");
                    return false;
                }
            }

            if (targetArgument != null)
            {
                Error($"Unexpected argument: {arg}");
                return false;
            }

            targetArgument = arg;
        }

        return true;
    }

    static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: AtomcraftPatcher [options] [path to Atomcraft.dll]");
        writer.WriteLine();
        writer.WriteLine("Patches Atomcraft.dll to load GodotMonoModLoader. With no path given, the");
        writer.WriteLine("game data folder next to the patcher, then one folder up, is searched.");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  -y, --non-interactive  Never wait for a keypress before exiting.");
        writer.WriteLine("      --restore          Restore the backup instead of patching.");
        writer.WriteLine("      --fail-if-patched  Exit 6 instead of 0 when already patched.");
        writer.WriteLine("      --quiet            Suppress progress output. Errors still go to stderr.");
        writer.WriteLine("  -h, --help             Show this help.");
        writer.WriteLine();
        writer.WriteLine("Exit codes:");
        writer.WriteLine("  0  Patch applied, or already patched");
        writer.WriteLine("  1  Unclassified failure");
        writer.WriteLine("  2  Usage error");
        writer.WriteLine("  3  Atomcraft.dll or backup not found");
        writer.WriteLine("  4  ModLoaderPatch.dll missing or unusable");
        writer.WriteLine("  5  Old patch detected, restore required");
        writer.WriteLine("  6  Already patched, with --fail-if-patched");
    }

    static void Info(string message = "")
    {
        if (!_quiet)
        {
            Console.WriteLine(message);
        }
    }

    static void Error(string message = "")
    {
        Console.Error.WriteLine(message);
    }

    static int Exit(int exitCode, bool restore = false)
    {
        bool canRestore = restore && File.Exists(_targetPath + ".backup");

        if (!IsInteractive)
        {
            return exitCode;
        }

        Console.WriteLine();
        Console.WriteLine(canRestore ? "Press ANY key to EXIT or R to restore the backup" : "Press ANY key to EXIT");
        Console.WriteLine();

        ConsoleKeyInfo key;
        try
        {
            key = Console.ReadKey(true);
        }
        catch (InvalidOperationException)
        {
            // No console input handle after all. Nothing to wait for.
            return exitCode;
        }

        if (canRestore && key.Key == ConsoleKey.R)
        {
            return Exit(RestoreBackup());
        }
        return exitCode;
    }

    static int RestoreBackup()
    {
        string backupPath = _targetPath + ".backup";

        if (!File.Exists(backupPath))
        {
            Error($"Backup not found: {backupPath}");
            return ExitTargetNotFound;
        }

        try
        {
            File.Copy(backupPath, _targetPath, overwrite: true);
        }
        catch (Exception ex)
        {
            Error();
            Error("ERROR:");
            Error(ex.ToString());
            return ExitFailure;
        }

        Info("Backup restored.");
        return ExitSuccess;
    }

    private static void AddTypeToGodotObjectSystem(AssemblyDefinition target, TypeDefinition injectedType)
    {
        var attribute = target.CustomAttributes
            .FirstOrDefault(a =>
                a.AttributeType.FullName ==
                "Godot.AssemblyHasScriptsAttribute");
            
        if (attribute != null) {
                
            var typeArrayArgument =
                attribute.ConstructorArguments[0];

            var existingTypes =
                (CustomAttributeArgument[])typeArrayArgument.Value;
                
            var injectedArgument =
                new CustomAttributeArgument(
                    target.MainModule.ImportReference(typeof(Type)),
                    injectedType
                );

            var newTypes =
                existingTypes
                    .Append(injectedArgument)
                    .ToArray();

            attribute.ConstructorArguments[0] =
                new CustomAttributeArgument(
                    typeArrayArgument.Type,
                    newTypes
                );
        }
    }

    static void DumpReference(object? operand)
    {
        switch (operand)
        {
            case TypeReference type:
                Console.WriteLine(
                    $"TYPE: {type.FullName} | " +
                    $"SCOPE: {type.Scope}");

                if (type.DeclaringType != null)
                {
                    Console.WriteLine(
                        $"  DECLARING: {type.DeclaringType.FullName} | " +
                        $"SCOPE: {type.DeclaringType.Scope}");
                }

                break;

            case MethodReference method:
                Console.WriteLine(
                    $"METHOD: {method.FullName}");

                Console.WriteLine(
                    $"  DECLARING: {method.DeclaringType.FullName}");

                Console.WriteLine(
                    $"  SCOPE: {method.DeclaringType.Scope}");

                break;

            case FieldReference field:
                Console.WriteLine(
                    $"FIELD: {field.FullName}");

                Console.WriteLine(
                    $"  DECLARING: {field.DeclaringType.FullName}");

                Console.WriteLine(
                    $"  SCOPE: {field.DeclaringType.Scope}");

                break;
        }
    }
    
    static void DumpTypeDefinition(TypeDefinition type)
    {
        Console.WriteLine(
            $"Nested: {type.FullName}");
            
        Console.WriteLine(
            $"Base: {type.BaseType?.FullName}");
            
        Console.WriteLine(
            $"Base scope: {type.BaseType?.Scope}");
            
        foreach (var field in type.Fields)
        {
            Console.WriteLine(
                $"Field: {field.FullName}");
            
            Console.WriteLine(
                $"  Type: {field.FieldType.FullName}");
            
            Console.WriteLine(
                $"  Type scope: {field.FieldType.Scope}");
        }
            
        foreach (var method in type.Methods)
        {
            Console.WriteLine(
                $"Method: {method.FullName}");
            
            foreach (var instruction in method.Body.Instructions)
            {
                DumpReference(instruction.Operand);
            }
        }
    }

    static TypeDefinition CloneType(TypeDefinition source, ModuleDefinition targetModule,
        Dictionary<TypeDefinition, TypeDefinition> typeMap,
        Dictionary<FieldDefinition, FieldDefinition> fieldMap,
        Dictionary<MethodDefinition, MethodDefinition> methodMap)
    {
        TypeReference? baseType = source.BaseType != null
            ? targetModule.ImportReference(source.BaseType)
            : null;

        var target = new TypeDefinition(source.Namespace, source.Name, source.Attributes, baseType);

        CopyCustomAttributes(
            source.CustomAttributes,
            target,
            targetModule);
        /*
    foreach (CustomAttribute attribute in source.CustomAttributes)
    {
        var newAttribute = new CustomAttribute(
            targetModule.ImportReference(attribute.Constructor));

        foreach (var argument in attribute.ConstructorArguments)
        {
            newAttribute.ConstructorArguments.Add(
                ImportCustomAttributeArgument(
                    argument,
                    targetModule));
        }

        foreach (var field in attribute.Fields)
        {
            newAttribute.Fields.Add(
                new CustomAttributeNamedArgument(
                    field.Name,
                    ImportCustomAttributeArgument(
                        field.Argument,
                        targetModule)));
        }

        foreach (var property in attribute.Properties)
        {
            newAttribute.Properties.Add(
                new CustomAttributeNamedArgument(
                    property.Name,
                    ImportCustomAttributeArgument(
                        property.Argument,
                        targetModule)));
        }

        target.CustomAttributes.Add(newAttribute);
    }*/
        
        foreach (GenericParameter gp in source.GenericParameters)
        {
            target.GenericParameters.Add(new GenericParameter(gp.Name, target));
        }

        foreach (TypeDefinition nestedType in source.NestedTypes)
        {
            TypeDefinition clonedNestedType =
                CloneType(nestedType, targetModule, typeMap, fieldMap, methodMap);

            clonedNestedType.DeclaringType = target;

            target.NestedTypes.Add(clonedNestedType);
            
            typeMap[nestedType] = clonedNestedType;
        }
        
        foreach (FieldDefinition field in source.Fields)
        {
            var newField = new FieldDefinition(field.Name, field.Attributes, targetModule.ImportReference(field.FieldType));

            target.Fields.Add(newField);
            
            fieldMap[field] = newField;
        }

        foreach (MethodDefinition method in source.Methods)
        {
            var clonedMethod = CloneMethod(method, target, targetModule);
            
            methodMap[method] = clonedMethod;
        }
        
        foreach (MethodDefinition method in source.Methods)
        {
            CloneMethodBody(method, targetModule, typeMap, fieldMap, methodMap);
        }
        
        return target;
    }

    static MethodDefinition CloneMethod(
        MethodDefinition source,
        TypeDefinition targetType,
        ModuleDefinition targetModule)
    {
        var method = new MethodDefinition(
            source.Name,
            source.Attributes,
            targetModule.ImportReference(source.ReturnType));

        CopyCustomAttributes(
            source.CustomAttributes,
            method,
            targetModule);

        // Parameters
        foreach (ParameterDefinition parameter in source.Parameters)
        {
            var newParameter =
                new ParameterDefinition(
                    parameter.Name,
                    parameter.Attributes,
                    targetModule.ImportReference(
                        parameter.ParameterType));

            CopyCustomAttributes(
                parameter.CustomAttributes,
                newParameter,
                targetModule);

            method.Parameters.Add(newParameter);
        }

        targetType.Methods.Add(method);

        return method;
    }

    static void CloneMethodBody(
        MethodDefinition source,
        ModuleDefinition targetModule,
        Dictionary<TypeDefinition, TypeDefinition> typeMap,
        Dictionary<FieldDefinition, FieldDefinition> fieldMap,
        Dictionary<MethodDefinition, MethodDefinition> methodMap)
    {
        if (!source.HasBody)
            return;

        MethodDefinition method = methodMap[source];
        
        method.Body.InitLocals = source.Body.InitLocals;

        // Local variables
        foreach (VariableDefinition variable in source.Body.Variables)
        {
            method.Body.Variables.Add(
                new VariableDefinition(
                    targetModule.ImportReference(
                        variable.VariableType)));
        }

        var processor = method.Body.GetILProcessor();

        // Original instruction -> cloned instruction
        var instructionMap =
            new Dictionary<Instruction, Instruction>();

        // ---------------------------------------------------------
        // PASS 1
        // Create all instructions without resolving their operands.
        // ---------------------------------------------------------

        foreach (Instruction instruction in source.Body.Instructions)
        {
            Instruction cloned;

            switch (instruction.Operand)
            {
                case null:
                    cloned = Instruction.Create(instruction.OpCode);
                    break;

                case Instruction:
                    // Branch target. Resolve it later.
                    // cloned = Instruction.Create(instruction.OpCode, 0);
                    
                    cloned = Instruction.Create(
                        OpCodes.Nop);

                    cloned.OpCode = instruction.OpCode;
                    break;

                case Instruction[]:
                    // Switch target. Resolve it later.
                    cloned = Instruction.Create(
                        instruction.OpCode,
                        Array.Empty<Instruction>());
                    break;

                case MethodReference methodReference:
                    
                    var methodDefinition = methodReference.Resolve();

                    if (methodDefinition != null &&
                        methodMap.TryGetValue(
                            methodDefinition,
                            out var clonedMethod))
                    {
                        cloned = Instruction.Create(
                            instruction.OpCode,
                            clonedMethod);
                    }
                    else
                    {
                        cloned = Instruction.Create(
                            instruction.OpCode,
                            targetModule.ImportReference(methodReference));
                    }
                    
                    break;

                case FieldReference fieldReference:
                    var fieldDefinition = fieldReference.Resolve();

                    if (fieldMap.TryGetValue(fieldDefinition, out var clonedField))
                    {
                        cloned = Instruction.Create(
                            instruction.OpCode,
                            clonedField);
                    }
                    else
                    {
                        cloned = Instruction.Create(
                            instruction.OpCode,
                            targetModule.ImportReference(fieldReference));
                    }
                    
                    break;

                case TypeReference typeReference:
                    var typeDefinition = typeReference.Resolve();

                    if (typeDefinition != null &&
                        typeMap.TryGetValue(
                            typeDefinition,
                            out var clonedType))
                    {
                        cloned = Instruction.Create(
                            instruction.OpCode,
                            clonedType);
                    }
                    else
                    {
                        cloned = Instruction.Create(
                            instruction.OpCode,
                            targetModule.ImportReference(typeReference));
                    }

                    break;

                case ParameterDefinition parameter:
                    cloned = Instruction.Create(
                        instruction.OpCode,
                        method.Parameters[parameter.Index]);

                    break;

                case VariableDefinition variable:
                    cloned = Instruction.Create(
                        instruction.OpCode,
                        method.Body.Variables[variable.Index]);
                    break;

                case string stringValue:
                    cloned = Instruction.Create(
                        instruction.OpCode,
                        stringValue);
                    break;

                case sbyte sbyteValue:
                    cloned = Instruction.Create(
                        instruction.OpCode,
                        sbyteValue);
                    break;

                case byte byteValue:
                    cloned = Instruction.Create(
                        instruction.OpCode,
                        byteValue);
                    break;

                case int intValue:
                    cloned = Instruction.Create(
                        instruction.OpCode,
                        intValue);
                    break;

                case long longValue:
                    cloned = Instruction.Create(
                        instruction.OpCode,
                        longValue);
                    break;

                case float floatValue:
                    cloned = Instruction.Create(
                        instruction.OpCode,
                        floatValue);
                    break;

                case double doubleValue:
                    cloned = Instruction.Create(
                        instruction.OpCode,
                        doubleValue);
                    break;

                default:
                    throw new NotSupportedException(
                        $"Unsupported IL operand type: " +
                        $"{instruction.Operand.GetType().FullName}");
            }

            instructionMap[instruction] = cloned;
            processor.Append(cloned);
        }

        // ---------------------------------------------------------
        // PASS 2
        // Fix branch and switch targets.
        // ---------------------------------------------------------

        foreach (Instruction instruction in source.Body.Instructions)
        {
            Instruction cloned =
                instructionMap[instruction];

            switch (instruction.Operand)
            {
                case Instruction target:
                    cloned.Operand =
                        instructionMap[target];
                    break;

                case Instruction[] targets:
                    var clonedTargets =
                        new Instruction[targets.Length];

                    for (int i = 0; i < targets.Length; i++)
                    {
                        clonedTargets[i] =
                            instructionMap[targets[i]];
                    }

                    cloned.Operand = clonedTargets;
                    break;
            }
        }

        // ---------------------------------------------------------
        // Exception handlers
        // ---------------------------------------------------------

        foreach (ExceptionHandler handler
                 in source.Body.ExceptionHandlers)
        {
            var newHandler =
                new ExceptionHandler(handler.HandlerType);

            if (handler.TryStart != null)
                newHandler.TryStart =
                    instructionMap[handler.TryStart];

            if (handler.TryEnd != null)
                newHandler.TryEnd =
                    instructionMap[handler.TryEnd];

            if (handler.HandlerStart != null)
                newHandler.HandlerStart =
                    instructionMap[handler.HandlerStart];

            if (handler.HandlerEnd != null)
                newHandler.HandlerEnd =
                    instructionMap[handler.HandlerEnd];

            if (handler.FilterStart != null)
                newHandler.FilterStart =
                    instructionMap[handler.FilterStart];

            if (handler.CatchType != null)
            {
                newHandler.CatchType =
                    targetModule.ImportReference(
                        handler.CatchType);
            }

            method.Body.ExceptionHandlers.Add(newHandler);
        }

    }
    
    static void CopyCustomAttributes(
        IEnumerable<CustomAttribute> sourceAttributes,
        ICustomAttributeProvider target,
        ModuleDefinition targetModule)
    {
        foreach (CustomAttribute source in sourceAttributes)
        {
            var attribute = new CustomAttribute(
                targetModule.ImportReference(
                    source.Constructor));

            foreach (CustomAttributeArgument argument
                     in source.ConstructorArguments)
            {
                attribute.ConstructorArguments.Add(
                    ImportCustomAttributeArgument(
                        argument,
                        targetModule));
            }

            foreach (CustomAttributeNamedArgument namedArgument
                     in source.Fields)
            {
                attribute.Fields.Add(
                    new CustomAttributeNamedArgument(
                        namedArgument.Name,
                        ImportCustomAttributeArgument(
                            namedArgument.Argument,
                            targetModule)));
            }

            foreach (CustomAttributeNamedArgument namedArgument
                     in source.Properties)
            {
                attribute.Properties.Add(
                    new CustomAttributeNamedArgument(
                        namedArgument.Name,
                        ImportCustomAttributeArgument(
                            namedArgument.Argument,
                            targetModule)));
            }

            target.CustomAttributes.Add(attribute);
        }
    }
    
    static CustomAttributeArgument ImportCustomAttributeArgument(
        CustomAttributeArgument argument,
        ModuleDefinition targetModule)
    {
        return new CustomAttributeArgument(
            targetModule.ImportReference(argument.Type),
            ImportCustomAttributeValue(
                argument.Value,
                targetModule));
    }

    static object ImportCustomAttributeValue(
        object value,
        ModuleDefinition targetModule)
    {
        if (value is TypeReference type)
            return targetModule.ImportReference(type);

        if (value is CustomAttributeArgument argument)
            return ImportCustomAttributeArgument(
                argument,
                targetModule);

        if (value is CustomAttributeArgument[] arguments)
        {
            var result =
                new CustomAttributeArgument[arguments.Length];

            for (int i = 0; i < arguments.Length; i++)
            {
                result[i] =
                    ImportCustomAttributeArgument(
                        arguments[i],
                        targetModule);
            }

            return result;
        }

        return value;
    }
}