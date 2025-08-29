
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NaniPro.Managers
{
    public enum AnchorPos { left, center, right }

    public class CharacterActorPro
    {
        public string name;
        public string appearance;
        public GameObject root;
        public Image image;
    }

    public class CharacterManagerPro : MonoBehaviour
    {
        public Transform stageRoot;
        public RectTransform leftAnchor, centerAnchor, rightAnchor;

        private readonly Dictionary<string, CharacterActorPro> _actors = new Dictionary<string, CharacterActorPro>();

        public IEnumerator Show(string name, AnchorPos at, string appearance = null, float fade = 0f)
        {
            Sprite sprite = null;
            if (!string.IsNullOrEmpty(appearance))
                sprite = Resources.Load<Sprite>("Characters/" + appearance);
            if (sprite == null) sprite = Resources.Load<Sprite>("Characters/" + name);

            if (!_actors.TryGetValue(name, out var actor))
            {
                actor = new CharacterActorPro { name = name };
                actor.root = new GameObject($"Char_{name}", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                actor.root.transform.SetParent(AnchorFor(at), false);
                actor.image = actor.root.GetComponent<Image>();
                actor.image.preserveAspect = true;
                _actors[name] = actor;
            }
            if (sprite != null) actor.image.sprite = sprite;
            actor.appearance = appearance ?? name;

            // Move to anchor
            actor.root.transform.SetParent(AnchorFor(at), false);

            if (fade > 0f)
            {
                var cg = actor.root.GetComponent<CanvasGroup>();
                cg.alpha = 0f;
                float t = 0f;
                while (t < fade)
                {
                    t += Time.deltaTime;
                    cg.alpha = Mathf.Clamp01(t / fade);
                    yield return null;
                }
                cg.alpha = 1f;
            }
            else actor.root.SetActive(true);
        }

        public IEnumerator Hide(string name, float fade = 0f)
        {
            if (_actors.TryGetValue(name, out var actor))
            {
                if (fade > 0f)
                {
                    var cg = actor.root.GetComponent<CanvasGroup>();
                    float t = 0f;
                    float start = cg.alpha;
                    while (t < fade)
                    {
                        t += Time.deltaTime;
                        cg.alpha = Mathf.Lerp(start, 0f, t/fade);
                        yield return null;
                    }
                    cg.alpha = 0f;
                }
                actor.root.SetActive(false);
            }
            yield return null;
        }

        public IEnumerator Move(string name, AnchorPos to, float time = 0.3f)
        {
            if (!_actors.TryGetValue(name, out var actor)) yield break;
            var dstParent = AnchorFor(to);
            // smooth reparent via world position
            var start = actor.root.transform.position;
            var end = dstParent.position;
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                actor.root.transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(t/time));
                yield return null;
            }
            actor.root.transform.SetParent(dstParent, false);
        }

        public Transform AnchorFor(AnchorPos at)
        {
            switch (at)
            {
                case AnchorPos.left: return leftAnchor ? leftAnchor : transform;
                case AnchorPos.right: return rightAnchor ? rightAnchor : transform;
                default: return centerAnchor ? centerAnchor : transform;
            }
        }

        // Expose snapshot for save/load
        public List<(string name, string appearance, string anchor)> Snapshot()
        {
            var list = new List<(string,string,string)>();
            foreach (var kv in _actors)
            {
                var a = kv.Value;
                if (!a.root.activeSelf) continue;
                string anchor = "center";
                if (a.root.transform.parent == leftAnchor) anchor = "left";
                else if (a.root.transform.parent == rightAnchor) anchor = "right";
                list.Add((a.name, a.appearance, anchor));
            }
            return list;
        }
    }
}
