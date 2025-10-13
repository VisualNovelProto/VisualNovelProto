using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class LogViewerList : MonoBehaviour
{
    [Header("UI")]
    public GameObject panel;
    public ScrollRect scrollRect;
    public Slider slider;
    public RectTransform content;
    public LogItemView itemPrefab;

    [Header("Voice Preview (Optional)")]
    public bool enableVoiceButton = true;
    public bool stopVoiceOnClose = true;

    [Header("Options")]
    [Min(1)] public int windowSize = 50;
    public bool showNodeId = false;
    public bool newestAtBottom = true;

    LogItemView[] pool;
    ChatLogManager.LogEntry[] temp;
    bool syncingScroll;
    bool opened;
    int lastKnownCount = -1;

    public bool IsOpen => panel != null && panel.activeInHierarchy;

    void Awake()
    {
        if (!content && scrollRect)
            content = scrollRect.content;

        if (panel)
            panel.SetActive(false);

        AllocatePool();
        HookEvents();
    }

    void AllocatePool()
    {
        if (!itemPrefab || content == null)
            return;

        int n = Mathf.Max(1, windowSize);
        pool = new LogItemView[n];
        for (int i = 0; i < n; i++)
        {
            var view = Instantiate(itemPrefab, content);
            view.gameObject.SetActive(false);
            pool[i] = view;
        }
    }

    void HookEvents()
    {
        if (slider)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.onValueChanged.AddListener(OnSliderChanged);
        }

        if (scrollRect)
            scrollRect.onValueChanged.AddListener(OnScrollChanged);
    }

    void OnDestroy()
    {
        if (slider)
            slider.onValueChanged.RemoveListener(OnSliderChanged);
        if (scrollRect)
            scrollRect.onValueChanged.RemoveListener(OnScrollChanged);
    }

    public void Open()
    {
        if (!panel)
            return;

        if (!opened)
        {
            UiModalGate.Push(Close);
            opened = true;
        }

        panel.SetActive(true);
        SyncSliderRange();
        Rebuild();
        if (newestAtBottom)
            SnapToLatest();
        else
            SnapToOldest();

        InputRouter.Instance?.SuppressAdvance(0.05f);
    }

    public void Close()
    {
        if (!panel)
            return;

        if (stopVoiceOnClose)
            AudioManager.Instance?.StopVoice();

        panel.SetActive(false);
        if (opened)
        {
            UiModalGate.Pop();
            opened = false;
        }

        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null)
            es.SetSelectedGameObject(null);

        InputRouter.Instance?.SuppressAdvance(0.05f);
    }

    void Update()
    {
        var log = ChatLogManager.Instance;
        if (log == null)
        {
            if (lastKnownCount != 0)
            {
                lastKnownCount = 0;
                if (IsOpen)
                    ClearPool();
            }
            return;
        }

        if (log.Count != lastKnownCount)
        {
            bool stickToEdge = false;
            if (IsOpen)
                stickToEdge = newestAtBottom ? IsAtBottom() : IsAtTop();

            lastKnownCount = log.Count;
            SyncSliderRange();

            if (IsOpen)
            {
                Rebuild();
                if (stickToEdge)
                {
                    if (newestAtBottom)
                        SnapToLatest();
                    else
                        SnapToOldest();
                }
            }
        }
    }

    void SyncSliderRange()
    {
        if (!slider)
            return;

        slider.SetValueWithoutNotify(Mathf.Clamp01(slider.value));
    }

    void OnSliderChanged(float value)
    {
        if (syncingScroll)
            return;

        if (IsOpen)
            Rebuild();

        if (scrollRect)
        {
            syncingScroll = true;
            float v = newestAtBottom ? (1f - value) : value;
            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(v);
            syncingScroll = false;
        }
    }

    void OnScrollChanged(Vector2 value)
    {
        if (syncingScroll || slider == null)
            return;

        syncingScroll = true;
        float v = newestAtBottom ? (1f - value.y) : value.y;
        slider.SetValueWithoutNotify(Mathf.Clamp01(v));
        syncingScroll = false;
    }

    bool IsAtBottom() => scrollRect && scrollRect.verticalNormalizedPosition <= 0.02f;
    bool IsAtTop() => scrollRect && scrollRect.verticalNormalizedPosition >= 0.98f;

    void SnapToLatest()
    {
        if (!scrollRect)
            return;

        scrollRect.verticalNormalizedPosition = newestAtBottom ? 0f : 1f;
        if (slider)
            slider.SetValueWithoutNotify(newestAtBottom ? 0f : 1f);
    }

    void SnapToOldest()
    {
        if (!scrollRect)
            return;

        scrollRect.verticalNormalizedPosition = newestAtBottom ? 1f : 0f;
        if (slider)
            slider.SetValueWithoutNotify(newestAtBottom ? 1f : 0f);
    }

    void Rebuild()
    {
        var log = ChatLogManager.Instance;
        if (log == null || pool == null || pool.Length == 0)
            return;

        int total = log.Count;
        if (total <= 0)
        {
            ClearPool();
            return;
        }

        EnsureTempBuffer(total);
        int copied = log.CopyLatest(temp, total);
        if (copied <= 0)
        {
            ClearPool();
            return;
        }

        int window = Mathf.Min(windowSize, copied);
        if (window <= 0)
        {
            ClearPool();
            return;
        }

        int startIndex = CalculateWindowStart(copied, window);
        int zebra = 0;
        for (int i = 0; i < pool.Length; i++)
        {
            var view = pool[i];
            if (!view)
                continue;

            if (i < window)
            {
                int src = startIndex + i;
                if (src < 0 || src >= copied)
                {
                    view.gameObject.SetActive(false);
                    continue;
                }

                var entry = temp[src];
                bool showVoice = ShouldShowVoiceButton(entry);
                Action onVoice = showVoice ? () => PreviewVoice(entry.voiceKey) : null;
                view.Bind(entry, showNodeId, zebra++, showVoice, onVoice);
                if (!view.gameObject.activeSelf)
                    view.gameObject.SetActive(true);
            }
            else
            {
                if (view.gameObject.activeSelf)
                    view.gameObject.SetActive(false);
            }
        }
    }

    void EnsureTempBuffer(int total)
    {
        if (temp != null && temp.Length >= total)
            return;
        int size = Mathf.NextPowerOfTwo(Mathf.Max(8, total));
        temp = new ChatLogManager.LogEntry[size];
    }

    int CalculateWindowStart(int total, int window)
    {
        if (slider == null)
        {
            return newestAtBottom ? total - window : 0;
        }

        float t = Mathf.Clamp01(slider.value);
        int scrollRange = Mathf.Max(0, total - window);
        if (newestAtBottom)
        {
            int start = Mathf.RoundToInt((1f - t) * scrollRange);
            return Mathf.Clamp(start, 0, Mathf.Max(0, total - window));
        }
        else
        {
            int start = Mathf.RoundToInt(t * scrollRange);
            return Mathf.Clamp(start, 0, Mathf.Max(0, total - window));
        }
    }

    void ClearPool()
    {
        if (pool == null)
            return;
        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i] && pool[i].gameObject.activeSelf)
                pool[i].gameObject.SetActive(false);
        }
    }

    bool ShouldShowVoiceButton(in ChatLogManager.LogEntry entry)
    {
        if (!enableVoiceButton)
            return false;
        if (!entry.HasVoice)
            return false;
        var audio = AudioManager.Instance;
        if (audio == null || !audio.IsVoicePlaybackAvailable)
            return false;
        return true;
    }

    void PreviewVoice(string voiceKey)
    {
        if (string.IsNullOrEmpty(voiceKey))
            return;
        var audio = AudioManager.Instance;
        if (audio == null)
            return;
        audio.PlayVoice(voiceKey);
    }
}
