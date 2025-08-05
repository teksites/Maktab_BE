using System.Collections.Concurrent;

namespace Users.Utils
{
    public interface IAppState
    {
        void Set(string key, object value);
        object Get(string key);
        T Get<T>(string key);
        Dictionary<string, object> GetAll();
    }
}
