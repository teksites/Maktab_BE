using System.Collections.Concurrent;

namespace Users.Utils.Implementation
{
    public class AppState : IAppState
    {
        private readonly ConcurrentDictionary<string, object> _cache = new()
        {
            ["autoTransfer"] = false,
            ["transferFee"] = 0.0

        };

        public void Set(string key, object value)
        {
            _cache[key] = value;
        }

        public object Get(string key)
        {
            _cache.TryGetValue(key, out var value);
            return value;
        }

        public T Get<T>(string key)
        {
            if (_cache.TryGetValue(key, out var value) && value is T typed)
                return typed;

            return default;
        }

        public Dictionary<string, object> GetAll()
        {
            return new Dictionary<string, object>(_cache);
        }
    }
}
