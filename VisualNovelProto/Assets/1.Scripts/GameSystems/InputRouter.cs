using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public sealed class InputRouter : MonoBehaviour

{
    public static InputRouter Instance { get; private set; }


    public PlayerInput playerInput;   // GameRoot에 같이 붙이기
    public DialogueUI ui;             // 비워두면 자동 탐색
    public DialogueRunner runner;     // 비워두면 자동 탐색
    public PauseMenu pauseMenu;       // 비워두면 자동 탐색

    InputAction advance;
    InputAction backPause;

    //버그 수정 관련 변수들
    bool _advanceRequested;
    float _suppressUntil; // 억제 마감 시각(unscaled)
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

        // UI 맵은 EventSystem이 사용. 우리는 Story 맵에서만 훅.
        advance = actions.FindAction("Advance", true);
        backPause = actions.FindAction("BackPause", true);

        advance.performed += OnAdvance;
        backPause.performed += OnBackPause;

        // 두 맵 모두 활성화(또는 상황에 따라 Story만 on/off)
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
            // 포인터 UI 위면 진행 금지
            if (es.IsPointerOverGameObject()) return;

            // 포커스 잔상: 비활성/파괴 객체는 무시하고, 활성인 경우만 진짜 포커스로 간주
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
        // 1) 글로벌 게이트
        // 모달/일시정지/트랜지션 중엔 즉시 무시
        if (PauseMenu.IsPaused || TransitionManager.IsPlaying || UiModalGate.IsOpen) return;

        _advanceRequested = true;
        // 2) UI가 클릭/포커스를 점유 중이면 무시
        var es = EventSystem.current;
        if (es != null)
        {
            // 마우스로 UI를 누른 프레임이면 true
            //if (es.IsPointerOverGameObject()) return;

            // 키보드 네비게이션으로 버튼/필드에 포커스가 있는 경우
            if (es.currentSelectedGameObject != null) return;
        }

        // 3) 실제 진행
        if (ui != null) { ui.OnClickContinue(); return; }
        if (runner != null) runner.Step();
    }

    void OnBackPause(InputAction.CallbackContext _)
    {
        // 1) 떠있는 모달이 있으면 그걸 먼저 닫는다(로그 포함)
        if (UiModalGate.TryCloseTop())
            return;

        // 2) 모달이 없다면 일시정지 토글
        if (pauseMenu != null)
            pauseMenu.Toggle();

    }
}
