
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace NaniPro.Managers
{
    public class TextPrinterManagerPro : MonoBehaviour
    {
        public Text textLabel;
        public float typeSpeed = 40f; // chars per second
        public bool auto = false;
        public float autoWait = 0.8f; // seconds after line ends
        public bool skip = false;

        public System.Collections.Generic.List<string> backlog = new System.Collections.Generic.List<string>();

        public IEnumerator PrintLine(string author, string rawText, System.Func<string,string> variableResolver)
        {
            string text = ResolveVariables(rawText, variableResolver);
            string composed = string.IsNullOrEmpty(author) ? text : $"<b>{author}</b>\n{text}";
            backlog.Add(composed);
            if (textLabel == null)
            {
                Debug.Log("[NaniPro] " + composed);
                yield break;
            }

            if (skip || typeSpeed <= 0f)
            {
                textLabel.text = composed;
            }
            else
            {
                textLabel.text = "";
                float charsPerSec = Mathf.Max(1f, typeSpeed);
                float t = 0f;
                int shown = 0;
                while (shown < composed.Length)
                {
                    t += Time.deltaTime * charsPerSec;
                    int target = Mathf.Clamp(Mathf.FloorToInt(t), 0, composed.Length);
                    if (target != shown)
                    {
                        textLabel.text = composed.Substring(0, target);
                        shown = target;
                    }
                    yield return null;
                }
            }

            if (auto)
            {
                float t = 0f;
                while (t < autoWait) { t += Time.deltaTime; yield return null; }
            }
            else
            {
                while (!Input.GetMouseButtonDown(0) && !Input.GetKeyDown(KeyCode.Space))
                    yield return null;
            }
        }

        private string ResolveVariables(string text, System.Func<string,string> resolver)
        {
            return Regex.Replace(text, @"\{([A-Za-z_][A-Za-z0-9_]*)\}", m =>
            {
                var key = m.Groups[1].Value;
                return resolver != null ? resolver(key) ?? "" : "";
            });
        }
    }
}
