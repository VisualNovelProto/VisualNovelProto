using UnityEngine;

public sealed class AutoAdvanceManager : MonoBehaviour
{
    [Header("Refs (����θ� �ڵ� Ž��)")]
    public DialogueRunner runner;
    public DialogueUI ui;

    [Header("Timing")]
    public bool useUnscaledTime = false;
    public float baseDelay = 0.4f;     // �⺻ ���
    public float perChar = 0.03f;      // ���ڴ� ����
    public float minDelay = 0.3f;      // ����
    public float maxDelay = 4.0f;      // ����

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

        // �۷ι� ����Ʈ(���� ���� ��Ȳ) üũ
        if (PauseMenu.IsPaused || TransitionManager.IsPlaying || UiModalGate.IsOpen) return; // 

        bool typing = ui.IsTypingPublic;
        bool awaitingChoice = ui.IsAwaitingChoicePublic;

        // ������ �߸� ���(����ڰ� ���� ����)
        if (awaitingChoice) { timer = 0f; return; }

        // Ÿ���� �� �Ϸ�� �Ѿ "������"���� Ÿ�̸� ����
        if (prevTyping && !typing)
        {
            int len = ui.CurrentBodyLengthPublic;
            float wait = baseDelay + perChar * Mathf.Clamp(len, 0, 500);
            timer = Mathf.Clamp(wait, minDelay, maxDelay);
        }
        prevTyping = typing;

        // ���� Ÿ���� ���̸� Ÿ�̸� ����
        if (typing) { timer = 0f; return; }

        // Ÿ�̸� ���� & Step
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (timer > 0f)
        {
            timer -= dt;
            if (timer > 0f) return;
        }

        // ��������
        runner.Step(); // ���� ���� �Լ� �״�� ���. :contentReference[oaicite:7]{index=7}
        timer = 0f;
    }
}
