using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public sealed class InputRouter : MonoBehaviour

{
    public static InputRouter Instance { get; private set; }


    public PlayerInput playerInput;   // GameRoot�� ���� ���̱�
    public DialogueUI ui;             // ����θ� �ڵ� Ž��
    public DialogueRunner runner;     // ����θ� �ڵ� Ž��
    public PauseMenu pauseMenu;       // ����θ� �ڵ� Ž��

    InputAction advance;
    InputAction backPause;

    //���� ���� ���� ������
    bool _advanceRequested;
    float _suppressUntil; // ���� ���� �ð�(unscaled)
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        if (!playerInput) playerInput = GetComponent<PlayerInput>();
        if (!ui) ui = FindObjectOfType<DialogueUI>();
        if (!runner) runner = FindObjectOfType<DialogueRunner>();
        if (!pauseMenu) pauseMenu = FindObjectOfType<PauseMenu>();
    }

    void OnEnable()
    {
        var actions = playerInput ? playerInput.actions : null;
        if (actions == null) return;

        // UI ���� EventSystem�� ���. �츮�� Story �ʿ����� ��.
        advance = actions.FindAction("Advance", true);
        backPause = actions.FindAction("BackPause", true);

        advance.performed += OnAdvance;
        backPause.performed += OnBackPause;

        // �� �� ��� Ȱ��ȭ(�Ǵ� ��Ȳ�� ���� Story�� on/off)
        actions.FindActionMap("UI", true).Enable();
        actions.FindActionMap("Story", true).Enable();
    }
    void Update()
    {
        if (!_advanceRequested) return;
        if (Time.unscaledTime < _suppressUntil) { _advanceRequested = false; return; }
        _advanceRequested = false;

        if (PauseMenu.IsPaused || TransitionManager.IsPlaying || UiModalGate.IsOpen) return;

        var es = EventSystem.current;
        if (es != null)
        {
            // ������ UI ���� ���� ����
            if (es.IsPointerOverGameObject()) return;

            // ��Ŀ�� �ܻ�: ��Ȱ��/�ı� ��ü�� �����ϰ�, Ȱ���� ��츸 ��¥ ��Ŀ���� ����
            var sel = es.currentSelectedGameObject;
            if (sel != null && sel && sel.activeInHierarchy) return;
        }

        if (ui != null) { ui.OnClickContinue(); return; }
        if (runner != null) runner.Step();
    }
    void OnDisable()
    {
        if (advance != null) advance.performed -= OnAdvance;
        if (backPause != null) backPause.performed -= OnBackPause;
    }

    public void SuppressAdvance(float sec = 0.05f)
    {
        _suppressUntil = Mathf.Max(_suppressUntil, Time.unscaledTime + Mathf.Max(0f, sec));
    }

    void OnAdvance(InputAction.CallbackContext _)
    {
        // 1) �۷ι� ����Ʈ
        // ���/�Ͻ�����/Ʈ������ �߿� ��� ����
        if (PauseMenu.IsPaused || TransitionManager.IsPlaying || UiModalGate.IsOpen) return;

        _advanceRequested = true;
        // 2) UI�� Ŭ��/��Ŀ���� ���� ���̸� ����
        var es = EventSystem.current;
        if (es != null)
        {
            // ���콺�� UI�� ���� �������̸� true
            //if (es.IsPointerOverGameObject()) return;

            // Ű���� �׺���̼����� ��ư/�ʵ忡 ��Ŀ���� �ִ� ���
            if (es.currentSelectedGameObject != null) return;
        }

        // 3) ���� ����
        if (ui != null) { ui.OnClickContinue(); return; }
        if (runner != null) runner.Step();
    }

    void OnBackPause(InputAction.CallbackContext _)
    {
        // 1) ���ִ� ����� ������ �װ� ���� �ݴ´�(�α� ����)
        if (UiModalGate.TryCloseTop())
            return;

        // 2) ����� ���ٸ� �Ͻ����� ���
        if (pauseMenu != null)
            pauseMenu.Toggle();

    }
}
