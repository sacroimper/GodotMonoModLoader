using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GodotMonoModLoader;

[Serializable]
public class JTokenDictionaryModConfig : Dictionary<string, JToken>, IModConfig
{
    
    /**
     * Used to generate a new instance with the default configuration.
     * In this case, an empty dictionary of KTokens.
     */
    public static IModConfig Default()
    {
        return new JTokenDictionaryModConfig();
    }
    
    /**
     * <summary>Gets the value from mod options, using <c>constant</c> if not exists, and stores it into <c>constant</c> again.<br/>
     * Uses the constant name if no key is supplied.<br/>
     * Perfect to keep all your constants organized in one class as static.<br/></summary>
     * 
     * <example><c>SetKeepDefault(ref Constants.OPTION)</c> This would be read/stored using the key "Constants.OPTION" in the JSON, and the value from the JSON would be stored in <c>Constants.OPTION</c></example>
     * 
     */
    public T SetAndGetWithDefault<T>(ref T constant, [CallerArgumentExpression(nameof(constant))] string key = "undefined")
        where T : notnull
    {
        return constant = GetOrSetDefault(key, constant);
    }
    
    public T? Get<T>(string key)
    {
        return TryGetValue(key, out T? value) ? value : default;
    }
    
    public T GetOrSetDefault<T>(string key, T defaultValue)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(defaultValue);

        if (TryGetValue(key, out T? value))
        {
            return value!;
        }
        
        this[key] = JToken.FromObject(defaultValue);
        return defaultValue;
    }

    public bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T output)
    {
        if (!base.TryGetValue(key, out JToken? value))
        {
            output = default;
            return false;
        }

        output = value switch
        {
            T existingToken => existingToken,
            _ => value.ToObject<T>()
        };
        return output != null;
    }

    public void Add<T>(string key, T value)
    {
        base.Add(key, JToken.FromObject(value switch
        {
            JToken existingToken => existingToken,
            null => JValue.CreateNull(),
            _ => JToken.FromObject(value)
        }));
    }
    
    

    public override string ToString()
    {
        return JsonConvert.SerializeObject(this, Formatting.Indented);
    }
}