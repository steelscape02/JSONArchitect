namespace JsonArchitect;

public interface IStorageManager
{
    void Set(string key, object value);
    object? Get(string key);
    Dictionary<string, object> GetAll();
    void SetAll(Dictionary<string, object> items);
}