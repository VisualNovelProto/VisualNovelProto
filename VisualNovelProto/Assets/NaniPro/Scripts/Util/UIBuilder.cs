using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NaniPro.Util
{
    public static class UIBuilder
    {
        public static void EnsureUI(NaniPro.Core.EnginePro engine)
        {
            if (GameObject.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                var inputSystemType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (inputSystemType != null) es.AddComponent(inputSystemType);
                else es.AddComponent<StandaloneInputModule>();
            }
            bool needPrinter = engine.textPrinter.textLabel == null;
            bool needChoices = engine.choiceHandler.container == null || engine.choiceHandler.buttonPrefab == null;
            bool needBG = engine.background.backgroundImage == null;

            if (!needPrinter && !needChoices && !needBG) return;

            var canvasGO = new GameObject("NaniProCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Background
            if (needBG)
            {
                var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
                bgGO.transform.SetParent(canvasGO.transform, false);
                var rect = bgGO.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                engine.background.backgroundImage = bgGO.GetComponent<Image>();
                engine.background.backgroundImage.color = Color.black;
            }

            // Dialogue panel
            var panelGO = new GameObject("DialoguePanel", typeof(RectTransform), typeof(Image));
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.05f, 0.05f);
            panelRect.anchorMax = new Vector2(0.95f, 0.25f);
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            panelGO.GetComponent<Image>().color = new Color(0, 0, 0, 0.45f);

            // Text
            var textGO = new GameObject("DialogueText", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(panelGO.transform, false);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.02f, 0.1f);
            textRect.anchorMax = new Vector2(0.98f, 0.9f);
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            var text = textGO.GetComponent<Text>();
            text.supportRichText = true;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 28;
            text.alignment = TextAnchor.UpperLeft;
            engine.textPrinter.textLabel = text;

            // Choices
            var choicesPanel = new GameObject("ChoicesPanel", typeof(RectTransform), typeof(VerticalLayoutGroup));
            choicesPanel.transform.SetParent(canvasGO.transform, false);
            var cRect = choicesPanel.GetComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0.7f, 0.3f);
            cRect.anchorMax = new Vector2(0.95f, 0.8f);
            cRect.offsetMin = cRect.offsetMax = Vector2.zero;
            var vlg = choicesPanel.GetComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = vlg.childControlWidth = true;
            vlg.childForceExpandHeight = vlg.childForceExpandWidth = true;
            vlg.spacing = 8;
            engine.choiceHandler.container = choicesPanel.transform;

            var btnPrefab = new GameObject("ChoiceButtonPrefab", typeof(RectTransform), typeof(Button), typeof(Image));
            var btnTxtGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            btnTxtGO.transform.SetParent(btnPrefab.transform, false);
            var btnTxt = btnTxtGO.GetComponent<Text>();
            btnTxt.font = text.font; btnTxt.fontSize = 24; btnTxt.alignment = TextAnchor.MiddleCenter;
            var btnTxtRect = btnTxtGO.GetComponent<RectTransform>();
            btnTxtRect.anchorMin = Vector2.zero; btnTxtRect.anchorMax = Vector2.one;
            btnTxtRect.offsetMin = btnTxtRect.offsetMax = Vector2.zero;
            engine.choiceHandler.buttonPrefab = btnPrefab;

            // Character anchors
            engine.characters.stageRoot = canvasGO.transform;
            engine.characters.leftAnchor = MakeAnchor(canvasGO.transform, "PosLeft", 0.1f, 0.3f);
            engine.characters.centerAnchor = MakeAnchor(canvasGO.transform, "PosCenter", 0.4f, 0.6f);
            engine.characters.rightAnchor = MakeAnchor(canvasGO.transform, "PosRight", 0.7f, 0.9f);
        }

            static RectTransform MakeAnchor(Transform parent, string name, float minX, float maxX)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var r = go.GetComponent<RectTransform>();
                r.anchorMin = new Vector2(minX, 0.1f);
                r.anchorMax = new Vector2(maxX, 0.8f);
                r.offsetMin = r.offsetMax = Vector2.zero;
                return r;
            }
    }
}
