using Godot;

namespace GodotMonoModLoader;

public partial class ModLoaderReport : CanvasLayer
{
    private Dictionary<string, ModInfo> _mods = null!;
    private ModLoaderLogger _logger = null!;

    private RichTextLabel _summary = null!;
    private TabContainer _tabs = null!;
    private RichTextLabel _errors = null!;
    private RichTextLabel _logLabel = null!;
    private Button _continueButton = null!;
    private ItemList _modList = null!;
    private RichTextLabel _modDetails = null!;

    public ModLoaderReport Init(Dictionary<string, ModInfo> mods, ModLoaderLogger logger)
    {
        _mods = mods;
        _logger = logger;
        _logger.LogAdded += OnLogAdded;
        return this;
    }

    public override void _Ready()
    {
        _summary = GetNode<RichTextLabel>("%Summary");
        _tabs = GetNode<TabContainer>("%Tabs");
        _errors = GetNode<RichTextLabel>("%ErrorsText");
        _logLabel = GetNode<RichTextLabel>("%LogText");
        _continueButton = GetNode<Button>("%ContinueButton");
        _modList = GetNode<ItemList>("%ModList");
        _modDetails = GetNode<RichTextLabel>("%ModDetails");

        _continueButton.Pressed += QueueFree;

        //StyleBoxFlat style = (StyleBoxFlat)_logLabel.GetThemeStylebox("normal");

        // style.ContentMarginLeft = 20;
        // style.ContentMarginTop = 20;
        // style.ContentMarginRight = 20;
        // style.ContentMarginBottom = 20;

        _tabs.SetTabTitle(0, "            Errors             ");
        _tabs.SetTabTitle(1, "             Mods              ");
        _tabs.SetTabTitle(2, "           View Log            ");

        // _errors.AddThemeStyleboxOverride("normal", style);
        // _logLabel.AddThemeStyleboxOverride("normal", style);

        // StyleBoxEmpty styleEmpty = new();
        // _summary.AddThemeStyleboxOverride("focus", styleEmpty);
        // _errors.AddThemeStyleboxOverride("focus", styleEmpty);
        // _modDetails.AddThemeStyleboxOverride("focus", styleEmpty);
        // _modList.AddThemeStyleboxOverride("focus", styleEmpty);
        // _logLabel.AddThemeStyleboxOverride("focus", styleEmpty);

        Populate();
    }
    
    public override void _ExitTree()
    {
        _logger.LogAdded -= OnLogAdded;
    }
    
    

    private void OnLogAdded(string message)
    {
        _logLabel.AppendText(message + "\n");
    }

    private void Populate()
    {
        _logLabel.Clear();

        foreach (string line in _logger.History)
        {
            _logLabel.AppendText(line + "\n");
        }

        _summary.Clear();
        _errors.Clear();

        int modsTotal = 0;
        int modsLoaded = 0;
        int modsPartial = 0;
        int failed = 0;
        int optional = 0;
        int modulesTotal = 0;
        int modulesLoaded = 0;

        foreach (ModInfo mod in _mods.Values)
        {
            if (mod.Path == "bundled")
            {
                continue;
            }

            modsTotal += 1;
            bool moduleLoaded = false;
            bool moduleError = false;

            foreach (ModuleInfo module in mod.Modules.Values)
            {
                modulesTotal += 1;

                switch (module.State)
                {
                    case ModuleState.Loaded:
                        modulesLoaded += 1;
                        moduleLoaded = true;
                        break;

                    case ModuleState.PartialError:
                        failed += 1;

                        _errors.AppendText($"Mod: {mod.Name} Module: {module.ModuleId}\n");
                        _errors.AppendText($"[color=yellow]This mod has been partially loaded with errors: {module.ErrorMessage}[/color]\n\n");
                        
                        moduleLoaded = true;
                        moduleError = true;
                        break;
                    
                    case ModuleState.Error:
                        failed += 1;

                        _errors.AppendText($"Mod: {mod.Name} Module: {module.ModuleId}\n");
                        _errors.AppendText($"[color=red]Error: {module.ErrorMessage}[/color]\n\n");
                        moduleError = true;
                        break;

                    case ModuleState.Optional:
                        optional += 1;
                        break;
                }
            }

            if (moduleLoaded)
            {
                modsLoaded += 1;
                if (moduleError)
                {
                    modsPartial += 1;
                }
            }
        }

        _summary.AppendText($"Mods Loaded: {modsLoaded} / {modsTotal}\n");

        if (modsPartial != 0)
        {
            _summary.AppendText($"Mods Partially Loaded: {modsPartial} / {modsTotal}\n");
        }

        _summary.AppendText($"Modules Loaded: {modulesLoaded} / {modulesTotal}\n");

        if (failed != 0)
        {
            _tabs.CurrentTab = 0;
            _summary.AppendText($"Modules Failed: {failed}\n");
        }

        if (optional != 0)
        {
            _summary.AppendText($"Optional Modules Skipped: {optional}\n");
        }

        if (failed == 0)
        {
            _errors.AppendText("[color=lime]All mods loaded correctly.[/color]\n");
        }

        _modList.Clear();

        foreach (ModInfo mod in _mods.Values)
        {
            _modList.AddItem(mod.Name);
            _modList.SetItemMetadata(_modList.ItemCount - 1, mod.Id);
        }

        _modList.ItemSelected += idx => OnModSelected((string)_modList.GetItemMetadata((int)idx));

        if (_mods.Count > 0)
        {
            _modList.Select(0);
            OnModSelected((string)_modList.GetItemMetadata(0));
        }
    }

    private void OnModSelected(string modId)
    {
        ModInfo mod = _mods[modId];

        _modDetails.Clear();

        _modDetails.AppendText($"[font_size=36][b]{mod.Name}[/b][/font_size]\n\n");

        _modDetails.AppendText($"[b]Author:[/b] {mod.Author}\n");
        _modDetails.AppendText($"[b]Version:[/b] {mod.Version}\n");
        _modDetails.AppendText($"[b]ID:[/b] {mod.Id}\n\n");

        _modDetails.AppendText("[b]Description[/b]\n");
        _modDetails.AppendText($"{mod.Description}\n\n");

        if (mod.Modules.Count > 0)
        {
            _modDetails.AppendText("[b]Modules[/b]\n");

            foreach (ModuleInfo module in mod.Modules.Values)
            {
                string status;

                switch (module.State)
                {
                    case ModuleState.Loaded:
                        status = "[color=lime]✓ Loaded[/color]";
                        break;

                    case ModuleState.PartialError:
                        status = "[color=yellow]✗ Error[/color]";
                        break;

                    case ModuleState.Error:
                        status = "[color=red]✗ Error[/color]";
                        break;

                    case ModuleState.Optional:
                        status = "[color=grey]○ Optional[/color]";
                        break;

                    case ModuleState.Ready:
                        status = "Ready";
                        break;

                    default:
                        status = "Default";
                        break;
                }

                _modDetails.AppendText($"• {module.ModuleId} — {status}\n");

                if (module.ErrorMessage != null)
                {
                    _modDetails.AppendText($"    [color=red]{module.ErrorMessage}[/color]\n");
                }
            }
        }

        _modDetails.AppendText($"\n[b]Location:[/b] {mod.Path}\n");
    }
}