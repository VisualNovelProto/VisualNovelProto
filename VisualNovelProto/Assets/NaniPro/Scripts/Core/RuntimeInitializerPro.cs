using System.Collections;
using UnityEngine;

namespace NaniPro.Core
{
    public class RuntimeInitializerPro : MonoBehaviour
    {
        public string language = "ko";
        public string scriptName = "ProDemo";
        public string startLabel = "Start";

        private IEnumerator Start()
        {
            var engine = GetComponent<EnginePro>();
            if (engine == null) engine = gameObject.AddComponent<EnginePro>();
            NaniPro.Util.UIBuilder.EnsureUI(engine);

            var path = $"Scripts/{language}/{scriptName}";
            var textAsset = Resources.Load<TextAsset>(path);
            if (textAsset == null)
            {
                Debug.LogError("[NaniPro] Script not found: " + path);
                yield break;
            }

            var script = NaniPro.Scripting.ScriptPro.Parse(textAsset.text);
            yield return engine.scriptPlayer.Play(script, startLabel);
        }
    }
}
