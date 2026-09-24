using Godot;

namespace GodotMonoModLoader;

public partial class ModLoaderLogger : RefCounted
{
    public event Action<string>? LogAdded;
    public List<string> History = [];
    private ulong _startTime;
 
    public ModLoaderLogger()
    {
        _startTime = Time.GetTicksMsec();
    }

    public ModLoaderLogger Init(ulong startTime, List<string> history)
    {
        _startTime = startTime;
        History = history;

        return this;
    }
 
    public void LogSeparator()
    {
        LogMessage(true ,"--------------------------------------------------------------------------------");
    }

    private string AddTime(string message)
    {
        ulong elapsed = Time.GetTicksMsec() - _startTime;
        return message + " Time: " + elapsed + " ms";
    }

    public void LogError(string message)
    {
        LogError(false,  message);
    }
    public void LogError(params string[] message)
    {
        LogError(false,  message);
    }

    public void LogError(bool useTime, params string[] message)
    {
        GD.PrintErr(PrepareMessage(useTime, message));
    }

    public void LogMessage(string message)
    {
        LogMessage(false,  message);
    }
    
    public void LogMessage(params string[] message)
    {
        LogMessage(false,  message);
    }

    public void LogMessage(bool useTime, params string[] message)
    {
        GD.Print(PrepareMessage(useTime, message));
    }

    private string PrepareMessage(bool useTime, params string[] message)
    {
        string fullMessage = string.Join("", message);
        fullMessage = useTime ? AddTime(fullMessage) :  fullMessage;
        History.Add(fullMessage);
        LogAdded?.Invoke(fullMessage);
        return "[GodotMonoModLoader]: " + fullMessage;
    }
}