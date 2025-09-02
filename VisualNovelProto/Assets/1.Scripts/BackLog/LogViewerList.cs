using UnityEngine;
using UnityEngine.UI;

public sealed class LogViewerList : MonoBehaviour
{
    [Header("UI")]
    public GameObject panel;            // ��ü �г� on/off
    public ScrollRect scrollRect;       // Vertical only
    public Slider slider;               // �� �����̴�
    public RectTransform content;       // ScrollRect�� Content
    public LogItemView itemPrefab;      // ������

    [Header("Options")]
    public int windowSize = 50;         // ȭ�鿡 ���� ���� ��
    public bool showNodeId = false;     // [123] ǥ��
    public bool newestAtBottom = true;  // �ֽ� �α׸� �Ʒ��� ��ġ
    public float itemSpacing = 12f;     // VerticalLayoutGroup�� ��ġ��Ű��

    LogItemView[] pool;
    int lastLogCount = -1;
    bool syncing;

    void Awake()
    {
        if (panel) panel.SetActive(false);
        if (!content) content = scrollRect ? scrollRect.content : null;

        // Ǯ �̸� ����
        int n = Mathf.Max(8, windowSize);
        pool = new LogItemView[n];
        for (int i = 0; i < n; i++)
        {
            var v = Instantiate(itemPrefab, content);
            v.gameObject.SetActive(true);
            pool[i] = v;
        }

        // �̺�Ʈ ����
        if (slider)
        {
            slider.minValue = 0f; slider.maxValue = 1f; slider.wholeNumbers = false;
            slider.onValueChanged.AddListener(OnSliderChanged);
        }
        if (scrollRect) scrollRect.onValueChanged.AddListener(OnScrollChanged);
    }

    public void Open()
    {
        panel.SetActive(true);
        UiModalGate.Push(Close);
        Rebuild();
        // ù Ŭ�� �ܻ� ����
        InputRouter.Instance?.SuppressAdvance(0.05f);
    }

    public void Close()
    {
        panel.SetActive(false);
        UiModalGate.Pop();
        // ���� �ʱ�ȭ(��Ŀ�� �ܻ� ����)
        if (UnityEngine.EventSystems.EventSystem.current)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        // (����) CanvasGroup�� ��ٸ� �ݵ�� blocksRaycasts=false�� ����
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg) cg.blocksRaycasts = false;
        // ù Ŭ�� �ܻ� ����
        InputRouter.Instance?.SuppressAdvance(0.05f);
    }

    void Update()
    {
        // �αװ� ���� �������� �ڵ� ����(�����̴��� �ֽ��ʿ� ������ ������ ����)
        var lm = ChatLogManager.Instance;
        if (lm == null) return;
        if (lm.Count != lastLogCount)
        {
            bool stickToEnd = newestAtBottom ? IsAtBottom() : IsAtTop();
            SyncSliderRange();
            Rebuild();
            if (newestAtBottom) SnapToLatest();
            else SnapToOldest();
        }
    }

    void SyncSliderRange()
    {
        var lm = ChatLogManager.Instance;
        lastLogCount = lm ? lm.Count : 0;
        if (!slider) return;

        // �� ����: ��ü ���̰� �ٲ� ���� ��� ��ġ�� ����
        // (���⼱ 0=�ֽ�,1=���������� ����)
        slider.SetValueWithoutNotify(slider.value);
    }

    void OnSliderChanged(float value)
    {
        if (syncing) return;
        Rebuild();
        // �����̴� �� ��ũ��Rect ����ȭ(�뷫)
        syncing = true;
        if (scrollRect) scrollRect.verticalNormalizedPosition = newestAtBottom ? (1f - value) : value;
        syncing = false;
    }

    void OnScrollChanged(Vector2 _)
    {
        if (syncing || !scrollRect || !slider) return;
        // ��ũ�� �� �����̴�
        syncing = true;
        float v = scrollRect.verticalNormalizedPosition;
        slider.SetValueWithoutNotify(newestAtBottom ? (1f - v) : v);
        syncing = false;
    }

    bool IsAtBottom() => scrollRect && scrollRect.verticalNormalizedPosition <= 0.02f;
    bool IsAtTop() => scrollRect && scrollRect.verticalNormalizedPosition >= 0.98f;

    void SnapToLatest()
    {
        if (!scrollRect) return;
        scrollRect.verticalNormalizedPosition = newestAtBottom ? 0f : 1f;
        if (slider) slider.SetValueWithoutNotify(newestAtBottom ? 0f : 1f);
    }
    void SnapToOldest()
    {
        if (!scrollRect) return;
        scrollRect.verticalNormalizedPosition = newestAtBottom ? 1f : 0f;
        if (slider) slider.SetValueWithoutNotify(newestAtBottom ? 1f : 0f);
    }

    // --- �ٽ�: ���� �����̴� �� �������� "������"�� ����� Ǯ�� ���ε� ---
    void Rebuild()
    {
        var lm = ChatLogManager.Instance;
        if (lm == null || pool == null || pool.Length == 0) return;

        int total = lm.Count;
        int win = Mathf.Min(windowSize, total);
        if (win <= 0) { ClearPool(); return; }

        // �����̴� 0=�ֽ�, 1=������
        float t = slider ? slider.value : 0f;
        int firstIndexFromOldest; // ������ ���� ���� �ε���(0 = ���� ������)

        // ��ü(total) �� win���� ������ ���̹Ƿ� ��ũ�� ������ ������ (total - win)
        int scrollRange = Mathf.Max(0, total - win);
        int offset = Mathf.RoundToInt(t * scrollRange); // ������ ������ offset
        firstIndexFromOldest = (total - win) - offset;  // �ֽ� ������ �Ʒ��� �������� first�� Ŀ��
        firstIndexFromOldest = Mathf.Clamp(firstIndexFromOldest, 0, Mathf.Max(0, total - win));

        // outBuf ���� �ٷ� �� �پ� ��û�Ϸ��� ChatLogManager�� �ε��� �����ڸ� �߰��ص� ����.
        // ���⼭�� CopyLatest�� �� �� Ȱ���ϴ� ������ ���:
        // 1) ��ü latest=total �� tmp�� ���� �� 2) �ű⼭ �����̽�
        // (���� ���. total<=capacity(��:256) ����)
        var tmp = new ChatLogManager.LogEntry[total];
        int nTot = lm.CopyLatest(tmp, total); // �����ȡ��ֽ� ����

        int zebra = 0;
        for (int i = 0; i < pool.Length; i++)
        {
            var go = pool[i].gameObject;
            if (i < win)
            {
                int src = firstIndexFromOldest + i;
                var e = tmp[src];
                pool[i].Bind(e, showNodeId, zebra++);
                if (!go.activeSelf) go.SetActive(true);
            }
            else
            {
                if (go.activeSelf) go.SetActive(false);
            }
        }

        // Content ���� ����(VerticalLayoutGroup + ContentSizeFitter ���� �ڵ�)
        // ���⼱ ���̾ƿ� ������Ʈ ����� ����.
    }

    void ClearPool()
    {
        for (int i = 0; i < pool.Length; i++)
            if (pool[i] && pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
    }
}
