using Atomcraft;

namespace GodotMonoModLoader.Atomcraft.UI;

public abstract partial class CustomWindow : UIWindow
{
    private static int _nextId = Enum.GetValues<WindowId>().Max(id => Convert.ToInt32(id)) + 1;

    public override WindowId ID { get; } = (WindowId) _nextId++;

    /**
     * <summary>This makes the window available in the current game scene. It should be created hidden by default.<br/><br/>
     * The window can be opened when needed using <c>Gameplay.SetCurrentWindowId(yourCustomWindow.ID)</c> or <c>yourCustomWindow.Open()</c></summary> 
     */
    public void AddToGameplay()
    {
        Gameplay.Windows.Add(this);
        Gameplay.Instance.AddChild(this);
    }

    public void Open()
    {
        Gameplay.SetCurrentWindowId(ID);
    }

}