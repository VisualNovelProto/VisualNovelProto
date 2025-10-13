using UnityEngine;

public sealed class StoryPlaybackController : MonoBehaviour
{
    [Header("References (optional)")]
    public DialogueRunner runner;
    public DialogueUI ui;
    public AutoAdvanceManager autoAdvance;
    public LogViewerList logViewer;

    [Header("Feature Toggles")]
    public bool enableFastForward = true;
    public bool enableSkip = true;
    public bool enableAutoToggle = true;
    public bool enableLogToggle = true;
    public bool disableAutoWhenManual = true;

    [Header("Fast Forward")]
    [Tooltip("Interval between automatic steps while fast forward is held (0 = every frame).")]
    public float fastForwardStepInterval = 0.05f;
    public bool fastForwardStopsAtChoices = true;

    [Header("Skip")]
    [Tooltip("Interval between automatic steps while skip is held (0 = every frame).")]
    public float skipStepInterval = 0f;
    public bool skipStopsAtChoices = true;

    bool fastForwarding;
    bool skipping;
    float fastForwardTimer;
    float skipTimer;

    void Awake()
    {
        runner = runner ?? FindObjectOfType<DialogueRunner>();
        ui = ui ?? FindObjectOfType<DialogueUI>();
        autoAdvance = autoAdvance ?? FindObjectOfType<AutoAdvanceManager>();
        logViewer = logViewer ?? FindObjectOfType<LogViewerList>();
    }

    void Update()
    {
        if (skipping)
            TickSkip();
        if (fastForwarding)
            TickFastForward();
    }

    bool CanAdvance()
    {
        if (PauseMenu.IsPaused)
            return false;
        if (TransitionManager.IsPlaying)
            return false;
        if (UiModalGate.IsOpen)
            return false;
        return true;
    }

    void TickSkip()
    {
        if (!enableSkip || runner == null)
            return;
        if (!CanAdvance())
            return;

        if (ui != null)
        {
            if (skipStopsAtChoices && ui.IsAwaitingChoicePublic)
            {
                EndSkip();
                return;
            }

            if (ui.OnAdvanceInput())
                return;
        }

        if (skipStepInterval > 0f)
        {
            skipTimer -= Time.unscaledDeltaTime;
            if (skipTimer > 0f)
                return;
            skipTimer = skipStepInterval;
        }

        runner.Step();
    }

    void TickFastForward()
    {
        if (!enableFastForward || runner == null)
            return;
        if (!CanAdvance())
            return;

        if (ui != null)
        {
            if (fastForwardStopsAtChoices && ui.IsAwaitingChoicePublic)
            {
                EndFastForward();
                return;
            }

            if (ui.OnAdvanceInput())
                return;
        }

        if (fastForwardStepInterval > 0f)
        {
            fastForwardTimer -= Time.unscaledDeltaTime;
            if (fastForwardTimer > 0f)
                return;
            fastForwardTimer = fastForwardStepInterval;
        }

        runner.Step();
    }

    public void BeginFastForward()
    {
        if (!enableFastForward)
            return;

        fastForwarding = true;
        fastForwardTimer = 0f;
        if (disableAutoWhenManual)
            autoAdvance?.SetAuto(false);
    }

    public void EndFastForward()
    {
        fastForwarding = false;
    }

    public void BeginSkip()
    {
        if (!enableSkip)
            return;

        skipping = true;
        skipTimer = 0f;
        if (disableAutoWhenManual)
            autoAdvance?.SetAuto(false);
    }

    public void EndSkip()
    {
        skipping = false;
    }

    public void CancelManualPlayback()
    {
        EndSkip();
        EndFastForward();
    }

    public void ToggleAutoMode()
    {
        if (!enableAutoToggle || autoAdvance == null)
            return;
        autoAdvance.ToggleAuto();
    }

    public void SetAutoMode(bool on)
    {
        if (!enableAutoToggle || autoAdvance == null)
            return;
        autoAdvance.SetAuto(on);
    }

    public void ToggleLogWindow()
    {
        if (!enableLogToggle || logViewer == null)
            return;

        if (logViewer.IsOpen)
            logViewer.Close();
        else
            logViewer.Open();
    }

    public void OpenLogWindow()
    {
        if (!enableLogToggle || logViewer == null)
            return;
        logViewer.Open();
    }

    public void CloseLogWindow()
    {
        if (!enableLogToggle || logViewer == null)
            return;
        logViewer.Close();
    }
}
