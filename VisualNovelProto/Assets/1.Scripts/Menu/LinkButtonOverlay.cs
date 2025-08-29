using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// TextMeshProUGUI ���� <link> �±� ������ ��ĵ�ؼ�
/// �̸� �غ�� ���� ��ư Ǯ�� ���� ���� ��ġ�Ѵ�.
/// - Rebuild(): ��ü ���� �������� ��Ʈ�ڽ�(�� ����)�� �̸� �����.
/// - SetVisibleCharacterCount(v): Ÿ���� ���൵(v)�� ���� ��Ʈ�ڽ� on/off.
/// </summary>
public sealed class LinkButtonOverlay : MonoBehaviour
{
    [Header("Target")]
    public TextMeshProUGUI targetText;    // ��ũ�� ���� TMP �ؽ�Ʈ
    public RectTransform overlayRect;     // ��ư�� ���� ���̾�

    [Header("Fixed Button Pool (pre-made)")]
    public Button[] buttonPool = new Button[2]; // ��Ÿ�� ���� X, �����Ϳ��� ����� ũ�� �¾�

    // Ŭ�� �ݹ�(��: "0", "g:12", "c:3" �� linkID)
    public Action<string> onClickLink;

    // ���� ĳ��(Ǯ ũ�� �ִ� 256 ����)
    readonly string[] idByIndex = new string[256];
    readonly int[] firstCharByIndex = new int[256];   // ���׸�Ʈ ���� ���� �ε���(ǥ�� gating��)
    readonly int[] lastCharByIndex = new int[256];    // �ʿ�� ���
    int usedCount;

    Canvas cachedCanvas;
    Camera uiCam;

    void Awake()
    {
        if (overlayRect == null) overlayRect = GetComponent<RectTransform>();
        cachedCanvas = overlayRect != null ? overlayRect.GetComponentInParent<Canvas>() : null;
        uiCam = cachedCanvas != null && cachedCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? cachedCanvas.worldCamera : null;

        // ��ư �ڵ鷯 ���� & �ʱ� ��Ȱ��
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
    /// �ؽ�Ʈ�� �ٲ� ���� ȣ��: ��ũ ��ġ�� ��ĵ�ϰ� ��ư�� ��ġ(�� ������ ����).
    /// �� �Լ��� ��ư�� "����⸸" �ϰ�, Ȱ��/��Ȱ���� SetVisibleCharacterCount�� �����Ѵ�.
    /// </summary>
    public void Rebuild()
    {
        if (targetText == null || overlayRect == null || buttonPool == null) return;

        targetText.ForceMeshUpdate(); // ��ü ���̾ƿ� ���(�ٹٲ� Ȯ��)

        usedCount = 0;
        int linkCount = targetText.textInfo.linkCount;
        if (linkCount <= 0)
        {
            HideAll();
            return;
        }

        // ��� ��ũ�� ����, ���� �ٲ� ������ �ϳ��� ��ư ���׸�Ʈ ����
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

                // ���� �簢��(�ؽ�Ʈ ���� �� ���� �� �������� ����)
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
                    // ���� �� ���׸�Ʈ�� Ȯ��
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
            if (usedCount >= buttonPool.Length) break; // Ǯ �ѵ�
        }

        // ���� ��ư�� ���α�
        for (int i = usedCount; i < buttonPool.Length; i++)
            if (buttonPool[i] != null) buttonPool[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Ÿ���� ���൵(���� ���� ��)�� ���� ���׸�Ʈ ��ư on/off.
    /// visible >= ���׸�Ʈ firstChar+1 �� �Ǵ� �������� ��ư Ȱ��ȭ.
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

        // ��ġ/������ ���� (�������� ���� ��ǥ ����)
        Vector2 size = max - min;
        rt.anchoredPosition = (min + max) * 0.5f;
        rt.sizeDelta = size;

        idByIndex[usedCount] = linkId;
        firstCharByIndex[usedCount] = segFirstChar;
        lastCharByIndex[usedCount] = segLastChar;

        // Rebuild �ܰ迡�� "��Ȱ��"�� �ΰ�, SetVisibleCharacterCount���� �Ҵ�.
        btn.gameObject.SetActive(false);
        usedCount++;
    }

    void HideAll()
    {
        for (int i = 0; i < buttonPool.Length; i++)
            if (buttonPool[i] != null) buttonPool[i].gameObject.SetActive(false);
    }
}
