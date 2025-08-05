using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Data
{
    public static class SqlBuilderUtils
    {
        public static string ByteArrayToString(IReadOnlyCollection<byte> ba)
        {
            var hex = new StringBuilder(ba.Count * 2);
            foreach (var b in ba)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString();
        }
        public static string GetCommandTextWithParameters(string query, IDictionary<string, object> parameters)
        {
            var result = query;
            foreach (var parameter in parameters)
            {
                var replacement = parameter.Value switch
                {
                    null => "NULL",
                    string str => $"'{str}'",
                    byte[] bytes => $"UNHEX('{ByteArrayToString(bytes)}')",
                    Guid guid => $"UNHEX('{ByteArrayToString(guid.ToByteArray())}')",
                    DateTime dateTime => $"'{dateTime: yyyy-MM-dd HH:mm:ss}'",
                    _ => $"{parameter.Value}"
                };
                result = result.Replace(parameter.Key, replacement);
            }
            return result;
        }
        public static ReadOnlyDictionary<string, T> BuildInClauseWithParameters<T>(IEnumerable<T> values, string parameterPrefix = "@value")
        {
            var parameters = new Dictionary<string, T>();
            if (values == null)
            {
                return new ReadOnlyDictionary<string, T>(parameters);
            }
            var i = 1;
            foreach (var value in values)
            {
                if (value == null)
                {
                    continue;
                }
                var paramName = parameterPrefix + i;
                if (!parameters.ContainsKey(paramName))
                {
                    i++;
                }
                parameters.Add(paramName, value);
            }
            return new ReadOnlyDictionary<string, T>(parameters);
        }
    }
}