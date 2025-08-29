
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NaniPro.Managers
{
    public class BackgroundManagerPro : MonoBehaviour
    {
        public Image backgroundImage;
        private string _currentPath;

        public IEnumerator SetBackground(string path, float fade = 0f)
        {
            var sprite = Resources.Load<Sprite>("Backgrounds/" + path);
            if (sprite == null)
            {
                Debug.LogWarning("[NaniPro] Background not found: " + path);
                yield break;
            }

            if (backgroundImage == null)
            {
                var go = new GameObject("Background", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var r = go.GetComponent<RectTransform>();
                r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
                r.offsetMin = r.offsetMax = Vector2.zero;
                backgroundImage = go.GetComponent<Image>();
                backgroundImage.color = Color.black;
            }

            if (fade > 0f)
            {
                // Crossfade
                var old = new GameObject("BG_FadeOld", typeof(RectTransform), typeof(Image));
                old.transform.SetParent(backgroundImage.transform.parent, false);
                var r = old.GetComponent<RectTransform>();
                r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
                r.offsetMin = r.offsetMax = Vector2.zero;
                var img = old.GetComponent<Image>();
                img.sprite = backgroundImage.sprite;
                img.color = backgroundImage.color;

                backgroundImage.sprite = sprite;
                backgroundImage.color = new Color(1,1,1,0);

                float t = 0f;
                while (t < fade)
                {
                    t += Time.deltaTime;
                    float a = Mathf.Clamp01(t / fade);
                    backgroundImage.color = new Color(1,1,1,a);
                    img.color = new Color(1,1,1,1-a);
                    yield return null;
                }
                Destroy(old);
                backgroundImage.color = Color.white;
            }
            else
            {
                backgroundImage.sprite = sprite;
                backgroundImage.color = Color.white;
            }

            _currentPath = path;
        }

        public string CurrentPath => _currentPath;
    }
}
