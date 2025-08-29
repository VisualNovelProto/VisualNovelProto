
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NaniPro.Managers
{
    public class ChoiceHandlerManagerPro : MonoBehaviour
    {
        public Transform container;
        public GameObject buttonPrefab;
        public int LastSelectedIndex { get; private set; } = -1;

        public IEnumerator ShowChoices(List<string> labels)
        {
            LastSelectedIndex = -1;
            if (container != null && buttonPrefab != null)
            {
                for (int i = container.childCount - 1; i >= 0; --i)
                    Destroy(container.GetChild(i).gameObject);

                for (int i = 0; i < labels.Count; i++)
                {
                    var go = GameObject.Instantiate(buttonPrefab, container);
                    var btn = go.GetComponent<Button>();
                    var txt = go.GetComponentInChildren<Text>();
                    if (txt != null) txt.text = labels[i];
                    int idx = i;
                    btn.onClick.AddListener(() => { LastSelectedIndex = idx; });
                }

                while (LastSelectedIndex < 0) yield return null;

                for (int i = container.childCount - 1; i >= 0; --i)
                    Destroy(container.GetChild(i).gameObject);
            }
            else
            {
                Debug.LogWarning("[NaniPro] Choice UI missing, auto-picking first.");
                LastSelectedIndex = 0;
                yield return null;
            }
        }
    }
}
