

namespace JsonArchitect;

public class StorageManager : IStorageManager
{
    private static Dictionary<string, object> _localSettings = new();

    public void Set(string key, object value)
    {
        _localSettings[key] = value;
    }

    public object? Get(string key)
    {
        return _localSettings.GetValueOrDefault(key);
    }

    public Dictionary<string, object> GetAll()
    {
        return _localSettings;
    }

    public void SetAll(Dictionary<string, object> items)
    {
        _localSettings = items;
        
    }
}