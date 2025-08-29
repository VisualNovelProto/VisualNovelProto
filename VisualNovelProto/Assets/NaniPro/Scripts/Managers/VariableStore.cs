
using System.Collections.Generic;
using UnityEngine;

namespace NaniPro.Managers
{
    public class VariableStore : MonoBehaviour
    {
        private readonly Dictionary<string, object> _vars = new Dictionary<string, object>();

        public void Set(string key, object value) => _vars[key] = value;
        public object Get(string key) => _vars.TryGetValue(key, out var v) ? v : null;
        public string GetString(string key) => Get(key)?.ToString();
        public double GetNumber(string key) { var v = Get(key); if (v == null) return 0; double d; return double.TryParse(v.ToString(), out d) ? d : 0; }
        public bool GetBool(string key) { var v = Get(key); if (v == null) return false; bool b; if (bool.TryParse(v.ToString(), out b)) return b; double d; if (double.TryParse(v.ToString(), out d)) return d != 0; return !string.IsNullOrEmpty(v.ToString()); }

        public Dictionary<string, object> Snapshot() => new Dictionary<string, object>(_vars);
        public void Restore(Dictionary<string, object> data)
        {
            _vars.Clear();
            if (data == null) return;
            foreach (var kv in data) _vars[kv.Key] = kv.Value;
        }
    }
}
