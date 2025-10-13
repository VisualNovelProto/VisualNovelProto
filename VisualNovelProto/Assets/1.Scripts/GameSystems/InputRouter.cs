using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public sealed class InputRouter : MonoBehaviour
{
    public static InputRouter Instance { get; private set; }

    [Header("References")]
    public PlayerInput playerInput;
    public DialogueUI ui;
    public DialogueRunner runner;
    public PauseMenu pauseMenu;
    public AutoAdvanceManager autoAdvance;
    public StoryPlaybackController playback;
    public LogViewerList logViewer;

    InputAction advance;
    InputAction backPause;
    InputAction skip;
    InputAction fastForward;
    InputAction toggleAuto;
    InputAction toggleLog;

    bool advanceRequested;
    float suppressUntil;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        playerInput = playerInput ? playerInput : GetComponent<PlayerInput>();
        ui = ui ? ui : FindObjectOfType<DialogueUI>();
        runner = runner ? runner : FindObjectOfType<DialogueRunner>();
        pauseMenu = pauseMenu ? pauseMenu : FindObjectOfType<PauseMenu>();
        autoAdvance = autoAdvance ? autoAdvance : FindObjectOfType<AutoAdvanceManager>();
        playback = playback ? playback : FindObjectOfType<StoryPlaybackController>();
        if (!logViewer)
        {
            if (playback != null && playback.enableLogToggle && playback.logViewer != null)
                logViewer = playback.logViewer;
            if (!logViewer)
                logViewer = FindObjectOfType<LogViewerList>();
        }
    }

    void OnEnable()
    {
        var actions = playerInput ? playerInput.actions : null;
        if (actions == null)
            return;

        advance = actions.FindAction("Advance", true);
        backPause = actions.FindAction("BackPause", true);
        skip = actions.FindAction("Skip", false);
        fastForward = actions.FindAction("FastForward", false);
        toggleAuto = actions.FindAction("ToggleAuto", false);
        toggleLog = actions.FindAction("ToggleLog", false);

        advance.performed += OnAdvance;
        backPause.performed += OnBackPause;
        if (skip != null)
        {
            skip.started += OnSkipStarted;
            skip.canceled += OnSkipCanceled;
        }
        if (fastForward != null)
        {
            fastForward.started += OnFastForwardStarted;
            fastForward.canceled += OnFastForwardCanceled;
        }
        if (toggleAuto != null)
            toggleAuto.performed += OnToggleAuto;
        if (toggleLog != null)
            toggleLog.performed += OnToggleLog;

        actions.FindActionMap("UI", true).Enable();
        actions.FindActionMap("Story", true).Enable();
    }

    void OnDisable()
    {
        if (advance != null) advance.performed -= OnAdvance;
        if (backPause != null) backPause.performed -= OnBackPause;
        if (skip != null)
        {
            skip.started -= OnSkipStarted;
            skip.canceled -= OnSkipCanceled;
        }
        if (fastForward != null)
        {
            fastForward.started -= OnFastForwardStarted;
            fastForward.canceled -= OnFastForwardCanceled;
        }
        if (toggleAuto != null) toggleAuto.performed -= OnToggleAuto;
        if (toggleLog != null) toggleLog.performed -= OnToggleLog;
    }

    void Update()
    {
        if (!advanceRequested)
            return;

        if (Time.unscaledTime < suppressUntil)
        {
            advanceRequested = false;
            return;
        }

        advanceRequested = false;

        if (PauseMenu.IsPaused || TransitionManager.IsPlaying || UiModalGate.IsOpen)
            return;

        if (IsPointerBlockingAdvance())
            return;

        if (ui != null)
        {
            ui.OnClickContinue();
            return;
        }

        runner?.Step();
    }

    bool IsPointerBlockingAdvance()
    {
        var es = EventSystem.current;
        if (es == null)
            return false;

        if (es.IsPointerOverGameObject())
            return true;

        var selected = es.currentSelectedGameObject;
        return selected != null && selected.activeInHierarchy;
    }

    public void SuppressAdvance(float seconds = 0.05f)
    {
        suppressUntil = Mathf.Max(suppressUntil, Time.unscaledTime + Mathf.Max(0f, seconds));
    }

    void OnAdvance(InputAction.CallbackContext context)
    {
        if (PauseMenu.IsPaused || TransitionManager.IsPlaying || UiModalGate.IsOpen)
            return;

        playback?.CancelManualPlayback();
        advanceRequested = true;

        var es = EventSystem.current;
        if (es != null && es.currentSelectedGameObject != null)
            return;

        if (ui != null)
        {
            ui.OnClickContinue();
            return;
        }

        runner?.Step();
    }

    void OnBackPause(InputAction.CallbackContext context)
    {
        if (UiModalGate.TryCloseTop())
            return;

        pauseMenu?.Toggle();
    }

    void OnSkipStarted(InputAction.CallbackContext context)
    {
        playback?.BeginSkip();
    }

    void OnSkipCanceled(InputAction.CallbackContext context)
    {
        playback?.EndSkip();
    }

    void OnFastForwardStarted(InputAction.CallbackContext context)
    {
        playback?.BeginFastForward();
    }

    void OnFastForwardCanceled(InputAction.CallbackContext context)
    {
        playback?.EndFastForward();
    }

    void OnToggleAuto(InputAction.CallbackContext context)
    {
        if (playback != null && playback.enableAutoToggle)
            playback.ToggleAutoMode();
        else
            autoAdvance?.ToggleAuto();
    }

    void OnToggleLog(InputAction.CallbackContext context)
    {
        if (playback != null && playback.enableLogToggle)
        {
            playback.ToggleLogWindow();
            return;
        }

        if (logViewer == null)
            logViewer = FindObjectOfType<LogViewerList>();
        if (logViewer == null)
            return;

        if (logViewer.IsOpen)
            logViewer.Close();
        else
            logViewer.Open();
    }
}
