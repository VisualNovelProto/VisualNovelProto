using UnityEngine;
using UnityEngine.UI;

public sealed class LogViewerList : MonoBehaviour
{
    [Header("UI")]
    public GameObject panel;            // 전체 패널 on/off
    public ScrollRect scrollRect;       // Vertical only
    public Slider slider;               // 옆 슬라이더
    public RectTransform content;       // ScrollRect의 Content
    public LogItemView itemPrefab;      // 프리팹

    [Header("Options")]
    public int windowSize = 50;         // 화면에 보일 라인 수
    public bool showNodeId = false;     // [123] 표시
    public bool newestAtBottom = true;  // 최신 로그를 아래에 배치
    public float itemSpacing = 12f;     // VerticalLayoutGroup와 일치시키기

    LogItemView[] pool;
    int lastLogCount = -1;
    bool syncing;

    void Awake()
    {
        if (panel) panel.SetActive(false);
        if (!content) content = scrollRect ? scrollRect.content : null;

        // 풀 미리 생성
        int n = Mathf.Max(8, windowSize);
        pool = new LogItemView[n];
        for (int i = 0; i < n; i++)
        {
            var v = Instantiate(itemPrefab, content);
            v.gameObject.SetActive(true);
            pool[i] = v;
        }

        // 이벤트 연결
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
        // 첫 클릭 잔상 방지
        InputRouter.Instance?.SuppressAdvance(0.05f);
    }

    public void Close()
    {
        panel.SetActive(false);
        UiModalGate.Pop();
        // 선택 초기화(포커스 잔상 제거)
        if (UnityEngine.EventSystems.EventSystem.current)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        // (선택) CanvasGroup을 썼다면 반드시 blocksRaycasts=false로 복귀
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg) cg.blocksRaycasts = false;
        // 첫 클릭 잔상 방지
        InputRouter.Instance?.SuppressAdvance(0.05f);
    }

    void Update()
    {
        // 로그가 새로 생겼으면 자동 갱신(슬라이더가 최신쪽에 가까우면 밑으로 유지)
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

        // 값 유지: 전체 길이가 바뀌어도 현재 상대 위치는 유지
        // (여기선 0=최신,1=오래된으로 정의)
        slider.SetValueWithoutNotify(slider.value);
    }

    void OnSliderChanged(float value)
    {
        if (syncing) return;
        Rebuild();
        // 슬라이더 → 스크롤Rect 동기화(대략)
        syncing = true;
        if (scrollRect) scrollRect.verticalNormalizedPosition = newestAtBottom ? (1f - value) : value;
        syncing = false;
    }

    void OnScrollChanged(Vector2 _)
    {
        if (syncing || !scrollRect || !slider) return;
        // 스크롤 → 슬라이더
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

    // --- 핵심: 현재 슬라이더 값 기준으로 "윈도우"를 계산해 풀에 바인딩 ---
    void Rebuild()
    {
        var lm = ChatLogManager.Instance;
        if (lm == null || pool == null || pool.Length == 0) return;

        int total = lm.Count;
        int win = Mathf.Min(windowSize, total);
        if (win <= 0) { ClearPool(); return; }

        // 슬라이더 0=최신, 1=오래된
        float t = slider ? slider.value : 0f;
        int firstIndexFromOldest; // 오래된 기준 시작 인덱스(0 = 가장 오래된)

        // 전체(total) 중 win개를 보여줄 것이므로 스크롤 가능한 범위는 (total - win)
        int scrollRange = Mathf.Max(0, total - win);
        int offset = Mathf.RoundToInt(t * scrollRange); // 오래된 쪽으로 offset
        firstIndexFromOldest = (total - win) - offset;  // 최신 기준을 아래로 보낼수록 first가 커짐
        firstIndexFromOldest = Mathf.Clamp(firstIndexFromOldest, 0, Mathf.Max(0, total - win));

        // outBuf 없이 바로 한 줄씩 요청하려면 ChatLogManager에 인덱스 접근자를 추가해도 좋음.
        // 여기서는 CopyLatest를 두 번 활용하는 간단한 방식:
        // 1) 전체 latest=total 를 tmp에 복사 → 2) 거기서 슬라이싱
        // (성능 충분. total<=capacity(예:256) 수준)
        var tmp = new ChatLogManager.LogEntry[total];
        int nTot = lm.CopyLatest(tmp, total); // 오래된→최신 순서

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

        // Content 높이 보정(VerticalLayoutGroup + ContentSizeFitter 쓰면 자동)
        // 여기선 레이아웃 컴포넌트 사용을 권장.
    }

    void ClearPool()
    {
        for (int i = 0; i < pool.Length; i++)
            if (pool[i] && pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
    }
}
