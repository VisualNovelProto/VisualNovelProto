using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class AutoAdvanceToggleUI : MonoBehaviour
{
    [Header("References")]
    public AutoAdvanceManager autoAdvance;
    public Toggle toggle;
    public Button button;
    public GameObject activeIndicator;

    [Header("Events")]
    public UnityEvent<bool> onAutoStateChanged;

    AutoAdvanceManager subscribedManager;

    void Awake()
    {
        autoAdvance = autoAdvance ?? FindObjectOfType<AutoAdvanceManager>();
        if (toggle != null)
            toggle.onValueChanged.AddListener(OnToggleChanged);
        if (button != null)
            button.onClick.AddListener(OnButtonClicked);
    }

    void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void OnDestroy()
    {
        if (toggle != null)
            toggle.onValueChanged.RemoveListener(OnToggleChanged);
        if (button != null)
            button.onClick.RemoveListener(OnButtonClicked);
        Unsubscribe();
    }

    void Subscribe()
    {
        if (autoAdvance == null)
            autoAdvance = FindObjectOfType<AutoAdvanceManager>();
        if (autoAdvance != null && subscribedManager != autoAdvance)
        {
            if (subscribedManager != null)
                subscribedManager.AutoModeChanged -= HandleAutoChanged;
            subscribedManager = autoAdvance;
            subscribedManager.AutoModeChanged += HandleAutoChanged;
        }
    }

    void Unsubscribe()
    {
        if (subscribedManager != null)
            subscribedManager.AutoModeChanged -= HandleAutoChanged;
        subscribedManager = null;
    }

    void HandleAutoChanged(bool on)
    {
        Refresh(on);
    }

    void Refresh()
    {
        bool state = autoAdvance != null && autoAdvance.autoEnabled;
        Refresh(state);
    }

    void Refresh(bool state)
    {
        if (toggle != null)
            toggle.SetIsOnWithoutNotify(state);
        if (activeIndicator != null)
            activeIndicator.SetActive(state);
        onAutoStateChanged?.Invoke(state);
    }

    void OnToggleChanged(bool value)
    {
        if (autoAdvance == null)
        {
            Refresh(value);
            return;
        }

        autoAdvance.SetAuto(value);
    }

    void OnButtonClicked()
    {
        if (autoAdvance == null)
            return;

        autoAdvance.ToggleAuto();
    }
}
