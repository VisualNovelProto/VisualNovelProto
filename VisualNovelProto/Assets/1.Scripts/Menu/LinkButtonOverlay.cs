using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// TextMeshProUGUI 안의 <link> 태그 영역을 스캔해서
/// 미리 준비된 투명 버튼 풀을 글자 위에 배치한다.
/// - Rebuild(): 전체 문장 기준으로 히트박스(줄 단위)를 미리 만든다.
/// - SetVisibleCharacterCount(v): 타이핑 진행도(v)에 맞춰 히트박스 on/off.
/// </summary>
public sealed class LinkButtonOverlay : MonoBehaviour
{
    [Header("Target")]
    public TextMeshProUGUI targetText;    // 링크를 가진 TMP 텍스트
    public RectTransform overlayRect;     // 버튼을 얹을 레이어

    [Header("Fixed Button Pool (pre-made)")]
    public Button[] buttonPool = new Button[2]; // 런타임 생성 X, 에디터에서 충분히 크게 셋업

    // 클릭 콜백(예: "0", "g:12", "c:3" 등 linkID)
    public Action<string> onClickLink;

    // 내부 캐시(풀 크기 최대 256 가정)
    readonly string[] idByIndex = new string[256];
    readonly int[] firstCharByIndex = new int[256];   // 세그먼트 시작 문자 인덱스(표시 gating용)
    readonly int[] lastCharByIndex = new int[256];    // 필요시 사용
    int usedCount;

    Canvas cachedCanvas;
    Camera uiCam;

    void Awake()
    {
        if (overlayRect == null) overlayRect = GetComponent<RectTransform>();
        cachedCanvas = overlayRect != null ? overlayRect.GetComponentInParent<Canvas>() : null;
        uiCam = cachedCanvas != null && cachedCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? cachedCanvas.worldCamera : null;

        // 버튼 핸들러 세팅 & 초기 비활성
        for (int i = 0; i < buttonPool.Length; i++)
        {
            int captured = i;
            var btn = buttonPool[i];
            if (btn == null) continue;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                string id = idByIndex[captured];
                if (!string.IsNullOrEmpty(id) && onClickLink != null) onClickLink(id);
            });
            btn.gameObject.SetActive(false);
        }
    }

    public void Bind(TextMeshProUGUI text, Action<string> clickHandler)
    {
        targetText = text;
        onClickLink = clickHandler;
    }

    /// <summary>
    /// 텍스트가 바뀐 직후 호출: 링크 위치를 스캔하고 버튼을 배치(줄 단위로 분할).
    /// 이 함수는 버튼을 "만들기만" 하고, 활성/비활성은 SetVisibleCharacterCount로 제어한다.
    /// </summary>
    public void Rebuild()
    {
        if (targetText == null || overlayRect == null || buttonPool == null) return;

        targetText.ForceMeshUpdate(); // 전체 레이아웃 계산(줄바꿈 확정)

        usedCount = 0;
        int linkCount = targetText.textInfo.linkCount;
        if (linkCount <= 0)
        {
            HideAll();
            return;
        }

        // 모든 링크에 대해, 줄이 바뀔 때마다 하나의 버튼 세그먼트 생성
        for (int li = 0; li < linkCount; li++)
        {
            var link = targetText.textInfo.linkInfo[li];
            string linkId = link.GetLinkID();

            int first = link.linkTextfirstCharacterIndex;
            int last = first + link.linkTextLength - 1;

            int curLine = -1;
            bool has = false;
            Vector2 min = Vector2.zero, max = Vector2.zero;
            int segStart = first;

            for (int ci = first; ci <= last; ci++)
            {
                var ch = targetText.textInfo.characterInfo[ci];
                if (!ch.isVisible) continue;

                int line = ch.lineNumber;

                // 문자 사각형(텍스트 로컬 → 월드 → 오버레이 로컬)
                Vector3 blW = targetText.rectTransform.TransformPoint(ch.bottomLeft);
                Vector3 trW = targetText.rectTransform.TransformPoint(ch.topRight);

                Vector2 bl, tr;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    overlayRect,
                    RectTransformUtility.WorldToScreenPoint(uiCam, blW), uiCam, out bl);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    overlayRect,
                    RectTransformUtility.WorldToScreenPoint(uiCam, trW), uiCam, out tr);

                if (line != curLine)
                {
                    // 이전 줄 세그먼트를 확정
                    if (has) Flush(linkId, min, max, segStart, ci - 1);

                    curLine = line;
                    has = true;
                    segStart = ci;

                    min = new Vector2(Mathf.Min(bl.x, tr.x), Mathf.Min(bl.y, tr.y));
                    max = new Vector2(Mathf.Max(bl.x, tr.x), Mathf.Max(bl.y, tr.y));
                }
                else
                {
                    if (bl.x < min.x) min.x = bl.x;
                    if (bl.y < min.y) min.y = bl.y;
                    if (tr.x > max.x) max.x = tr.x;
                    if (tr.y > max.y) max.y = tr.y;
                }
            }

            if (has) Flush(linkId, min, max, segStart, last);
            if (usedCount >= buttonPool.Length) break; // 풀 한도
        }

        // 남은 버튼은 꺼두기
        for (int i = usedCount; i < buttonPool.Length; i++)
            if (buttonPool[i] != null) buttonPool[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// 타이핑 진행도(가시 문자 수)에 맞춰 세그먼트 버튼 on/off.
    /// visible >= 세그먼트 firstChar+1 이 되는 순간부터 버튼 활성화.
    /// </summary>
    public void SetVisibleCharacterCount(int visible)
    {
        for (int i = 0; i < usedCount; i++)
        {
            var btn = buttonPool[i];
            if (btn == null) continue;

            bool on = (visible > firstCharByIndex[i]);
            if (btn.gameObject.activeSelf != on)
                btn.gameObject.SetActive(on);
        }
    }

    void Flush(string linkId, Vector2 min, Vector2 max, int segFirstChar, int segLastChar)
    {
        if (usedCount >= buttonPool.Length) return;

        var btn = buttonPool[usedCount];
        if (btn == null) return;

        var rt = btn.transform as RectTransform;
        if (rt == null) return;

        // 위치/사이즈 적용 (오버레이 로컬 좌표 기준)
        Vector2 size = max - min;
        rt.anchoredPosition = (min + max) * 0.5f;
        rt.sizeDelta = size;

        idByIndex[usedCount] = linkId;
        firstCharByIndex[usedCount] = segFirstChar;
        lastCharByIndex[usedCount] = segLastChar;

        // Rebuild 단계에선 "비활성"로 두고, SetVisibleCharacterCount에서 켠다.
        btn.gameObject.SetActive(false);
        usedCount++;
    }

    void HideAll()
    {
        for (int i = 0; i < buttonPool.Length; i++)
            if (buttonPool[i] != null) buttonPool[i].gameObject.SetActive(false);
    }
}
