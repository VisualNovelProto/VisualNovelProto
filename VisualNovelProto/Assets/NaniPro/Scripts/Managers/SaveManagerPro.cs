
using System.Collections.Generic;
using UnityEngine;

namespace NaniPro.Managers
{
    [System.Serializable] public class SaveState
    {
        public string scriptName;
        public string label;
        public int index;
        public Dictionary<string, object> variables;

        public string backgroundPath;
        public List<CharacterSnapshot> actors = new List<CharacterSnapshot>();
    }
    [System.Serializable] public class CharacterSnapshot
    {
        public string name;
        public string appearance;
        public string anchor;
    }

    public class SaveManagerPro : MonoBehaviour
    {
        public void Save(int slot, SaveState state)
        {
            var json = JsonUtility.ToJson(new Wrapper{ state = state });
            PlayerPrefs.SetString($"NaniPro_Save_{slot}", json);
            PlayerPrefs.Save();
        }
        public SaveState Load(int slot)
        {
            var json = PlayerPrefs.GetString($"NaniPro_Save_{slot}", null);
            if (string.IsNullOrEmpty(json)) return null;
            var w = JsonUtility.FromJson<Wrapper>(json);
            return w.state;
        }

        [System.Serializable] private class Wrapper { public SaveState state; }
    }
}
