// Assets/1.Scripts/UI/PanelAnimator.cs
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class PanelAnimator : MonoBehaviour
{
    public enum Mode
    {
        Fade,           // 알파만
        FadePop,        // 알파 + 스케일
        FadeSlide,      // 알파 + 슬라이드
        FadeSlidePop    // 알파 + 슬라이드 + 스케일
    }
    public enum SlideDir { Up, Down, Left, Right }

    [Header("Preset")]
    public Mode mode = Mode.FadePop;
    [Range(0.05f, 1.0f)] public float openDuration = 0.25f;
    [Range(0.05f, 1.0f)] public float closeDuration = 0.22f;
    public AnimationCurve openCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve closeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Pop / Slide")]
    [Range(0.5f, 1.0f)] public float popStartScale = 0.95f; // Open 시작 스케일
    public SlideDir slideFrom = SlideDir.Down;
    [Range(8, 400)] public float slidePixels = 96f;         // Open 시작 오프셋(px)

    [Header("Behaviour")]
    public bool startHidden = true;             // 처음 비활성 상태처럼 보이기
    public bool ignoreCancelDuringOpen = true;  // 오픈 도중 ESC/Cancel은 무시
    public bool blockRaycastDuringAnim = true;  // 애니 중엔 입력 흡수

    CanvasGroup cg;
    RectTransform rt;
    Vector2 basePos;
    Vector3 baseScale;
    Coroutine co;
    bool opening, closing;

    public bool IsAnimating => opening || closing;
    public float EffectiveOpenDuration => openDuration;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();

        basePos = rt.anchoredPosition;
        baseScale = Vector3.one;

        if (startHidden)
        {
            // 닫힌 상태로 초기화(패널이 Active라도 숨김)
            SetClosedVisualsImmediate();
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }

        // 씬 전환 시 남는 코루틴/상태 정리
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnDisable()
    {
        // 비활성화 시 레이캐스트 막지 않도록 보장
        if (cg)
        {
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }
        if (co != null) { StopCoroutine(co); co = null; opening = closing = false; }
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        // 씬 바뀌면 애니 중단 & 스냅 클로즈(DDOL 패널 보호)
        if (!gameObject.activeInHierarchy) return;
        if (IsAnimating) { if (co != null) StopCoroutine(co); opening = closing = false; }
        if (startHidden) SetClosedVisualsImmediate();
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    // ----- 외부에서 호출 -----
    public void PlayOpen()
    {
        if (!gameObject.activeInHierarchy) return;
        if (co != null) StopCoroutine(co);

        // 입력 잔상 억제
        TrySuppressInput(openDuration + 0.05f);

        co = StartCoroutine(CoOpen());
    }
    public void PlayOpen(GameObject go)
    {
        if (!go.activeInHierarchy) return;
        if (co != null) StopCoroutine(co);
        // 입력 잔상 억제
        TrySuppressInput(openDuration + 0.05f);

        co = StartCoroutine(CoOpen());
    }
    public IEnumerator PlayClose()
    {
        if (!gameObject.activeInHierarchy)
            yield break;

        if (co != null) StopCoroutine(co);
        co = StartCoroutine(CoClose());
        while (closing) yield return null;
    }
    public IEnumerator PlayClose(GameObject go)
    {
        if (!go.activeInHierarchy)
            yield break;

        if (co != null) StopCoroutine(co);
        co = StartCoroutine(CoClose());
        while (closing) yield return null;
    }

    // ----- 내부 -----
    IEnumerator CoOpen()
    {
        opening = true; closing = false;

        // 시작 상태
        float t = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = blockRaycastDuringAnim;

        Vector2 startPos = basePos;
        if (mode == Mode.FadeSlide || mode == Mode.FadeSlidePop)
        {
            var off = GetSlideOffset(slideFrom, slidePixels);
            rt.anchoredPosition = basePos + off;
            startPos = rt.anchoredPosition;
        }
        else rt.anchoredPosition = basePos;

        Vector3 startScale = Vector3.one;
        if (mode == Mode.FadePop || mode == Mode.FadeSlidePop)
        {
            startScale = Vector3.one * popStartScale;
            rt.localScale = startScale;
        }
        else rt.localScale = Vector3.one;

        cg.alpha = 0f;

        // 트윈
        while (t < openDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / openDuration);
            float e = openCurve.Evaluate(k);

            cg.alpha = e;

            if (mode == Mode.FadeSlide || mode == Mode.FadeSlidePop)
                rt.anchoredPosition = Vector2.LerpUnclamped(startPos, basePos, e);

            if (mode == Mode.FadePop || mode == Mode.FadeSlidePop)
                rt.localScale = Vector3.LerpUnclamped(startScale, Vector3.one, e);

            yield return null;
        }

        // 종료 상태
        cg.alpha = 1f;
        rt.anchoredPosition = basePos;
        rt.localScale = Vector3.one;

        cg.interactable = true;
        cg.blocksRaycasts = true;

        opening = false;
        co = null;

        // 포커스 잔상 제거(선택)
        EventSystem.current?.SetSelectedGameObject(null);
    }

    IEnumerator CoClose()
    {
        opening = false; closing = true;

        cg.interactable = false;
        cg.blocksRaycasts = blockRaycastDuringAnim;

        float t = 0f;
        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos = basePos;
        Vector3 startScale = rt.localScale;
        Vector3 endScale = Vector3.one;

        if (mode == Mode.FadeSlide || mode == Mode.FadeSlidePop)
        {
            endPos = basePos + GetSlideOffset(slideFrom, slidePixels);
        }
        if (mode == Mode.FadePop || mode == Mode.FadeSlidePop)
        {
            endScale = Vector3.one * 0.96f; // 살짝 줄이며 닫기
        }

        float startAlpha = cg.alpha;

        while (t < closeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / closeDuration);
            float e = closeCurve.Evaluate(k);

            cg.alpha = Mathf.LerpUnclamped(startAlpha, 0f, e);
            rt.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, e);
            rt.localScale = Vector3.LerpUnclamped(startScale, endScale, e);
            yield return null;
        }

        SetClosedVisualsImmediate(); // 최종 스냅
        closing = false;
        co = null;

        // 포커스 잔상 제거
        EventSystem.current?.SetSelectedGameObject(null);
    }

    void SetClosedVisualsImmediate()
    {
        cg.alpha = 0f;
        rt.anchoredPosition = basePos + ((mode == Mode.FadeSlide || mode == Mode.FadeSlidePop) ? GetSlideOffset(slideFrom, slidePixels) : Vector2.zero);
        rt.localScale = (mode == Mode.FadePop || mode == Mode.FadeSlidePop) ? Vector3.one * popStartScale : Vector3.one;
        cg.blocksRaycasts = false;
        cg.interactable = false;
    }

    Vector2 GetSlideOffset(SlideDir dir, float px)
    {
        switch (dir)
        {
            case SlideDir.Up: return new Vector2(0, px);
            case SlideDir.Down: return new Vector2(0, -px);
            case SlideDir.Left: return new Vector2(-px, 0);
            case SlideDir.Right: return new Vector2(px, 0);
        }
        return Vector2.zero;
    }

    void TrySuppressInput(float sec)
    {
        // 프로젝트에 InputRouter가 있을 때만
        var type = System.Type.GetType("InputRouter");
        if (type != null)
        {
            var instProp = type.GetProperty("Instance");
            var inst = instProp != null ? instProp.GetValue(null, null) : null;
            if (inst != null)
            {
                var mi = type.GetMethod("SuppressAdvance");
                if (mi != null) mi.Invoke(inst, new object[] { sec });
            }
        }
    }
}
