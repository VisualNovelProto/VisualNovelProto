using UnityEngine;

public sealed class AutoAdvanceManager : MonoBehaviour
{
    [Header("Refs (비워두면 자동 탐색)")]
    public DialogueRunner runner;
    public DialogueUI ui;

    [Header("Timing")]
    public bool useUnscaledTime = false;
    public float baseDelay = 0.4f;     // 기본 대기
    public float perChar = 0.03f;      // 글자당 가중
    public float minDelay = 0.3f;      // 하한
    public float maxDelay = 4.0f;      // 상한

    [Header("State")]
    public bool autoEnabled;

    float timer;
    bool prevTyping;

    void Awake()
    {
        if (runner == null) runner = FindObjectOfType<DialogueRunner>();
        if (ui == null) ui = FindObjectOfType<DialogueUI>();
    }

    public void ToggleAuto() => SetAuto(!autoEnabled);

    public void SetAuto(bool on)
    {
        autoEnabled = on;
        timer = 0f;
        prevTyping = ui ? ui.IsTypingPublic : false;
    }

    void Update()
    {
        if (!autoEnabled || runner == null || ui == null) return;

        // 글로벌 게이트(진행 금지 상황) 체크
        if (PauseMenu.IsPaused || TransitionManager.IsPlaying || UiModalGate.IsOpen) return; // 

        bool typing = ui.IsTypingPublic;
        bool awaitingChoice = ui.IsAwaitingChoicePublic;

        // 선택지 뜨면 대기(사용자가 직접 선택)
        if (awaitingChoice) { timer = 0f; return; }

        // 타이핑 → 완료로 넘어간 "변곡점"에서 타이머 세팅
        if (prevTyping && !typing)
        {
            int len = ui.CurrentBodyLengthPublic;
            float wait = baseDelay + perChar * Mathf.Clamp(len, 0, 500);
            timer = Mathf.Clamp(wait, minDelay, maxDelay);
        }
        prevTyping = typing;

        // 아직 타이핑 중이면 타이머 리셋
        if (typing) { timer = 0f; return; }

        // 타이머 감소 & Step
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (timer > 0f)
        {
            timer -= dt;
            if (timer > 0f) return;
        }

        // 다음으로
        runner.Step(); // 기존 진행 함수 그대로 사용. :contentReference[oaicite:7]{index=7}
        timer = 0f;
    }
}
