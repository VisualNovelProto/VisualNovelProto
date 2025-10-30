using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Visual wrist watch widget placed in the top-left corner. Shows the current destination and exposes
/// buttons for jumping to other destinations in the loop.
/// </summary>
public sealed class TimeLoopWatchUI : MonoBehaviour
{
    [Header("References")]
    public TimeLoopManager manager;
    public GameObject root;
    public TMP_Text currentTimeLabel;
    public TMP_Text currentBranchLabel;
    public TMP_Text loopCountLabel;
    public Button toggleButton;
    public GameObject destinationListPanel;
    public RectTransform destinationListRoot;
    public TimeLoopDestinationView destinationViewPrefab;

    [Header("Behaviour")]
    public bool openListOnStart;

    readonly List<TimeLoopDestinationView> _destinationViews = new List<TimeLoopDestinationView>();
    bool _isOpen;

    void Awake()
    {
        if (toggleButton != null)
            toggleButton.onClick.AddListener(ToggleList);
    }

    void OnEnable()
    {
        var targetManager = manager != null ? manager : FindObjectOfType<TimeLoopManager>();
        manager = null; // ensure Bind always re-subscribes
        Bind(targetManager);
        if (openListOnStart)
            OpenList();
        else
            CloseList();
        Refresh();
    }

    void OnDisable()
    {
        if (manager != null)
            manager.StateChanged -= HandleManagerStateChanged;
    }

    public void Bind(TimeLoopManager newManager)
    {
        if (manager != null)
            manager.StateChanged -= HandleManagerStateChanged;

        manager = newManager;

        if (manager != null)
        {
            manager.StateChanged += HandleManagerStateChanged;
            RebuildDestinations();
        }
        else
        {
            ClearDestinations();
        }

        Refresh();
    }

    void HandleManagerStateChanged()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (root != null)
            root.SetActive(manager != null);

        if (manager == null)
            return;

        if (currentTimeLabel != null)
        {
            string label = manager.CurrentDestinationLabel;
            currentTimeLabel.text = string.IsNullOrEmpty(label) ? "--" : label;
        }

        if (currentBranchLabel != null)
        {
            if (manager.TryGetDestination(manager.CurrentDestinationIndex, out var current))
                currentBranchLabel.text = current.GetBranchLabel();
            else
                currentBranchLabel.text = manager.CurrentDestinationKey ?? string.Empty;
        }

        if (loopCountLabel != null)
            loopCountLabel.text = manager.LoopCount.ToString();

        EnsureDestinationViews();
        for (int i = 0; i < _destinationViews.Count; i++)
            _destinationViews[i].Refresh();

        if (destinationListPanel != null)
            destinationListPanel.SetActive(_isOpen);
    }

    void EnsureDestinationViews()
    {
        if (manager == null || destinationListRoot == null || destinationViewPrefab == null)
            return;

        if (_destinationViews.Count == manager.DestinationCount)
            return;

        RebuildDestinations();
    }

    void RebuildDestinations()
    {
        ClearDestinations();

        if (manager == null || destinationListRoot == null || destinationViewPrefab == null)
            return;

        int count = manager.DestinationCount;
        for (int i = 0; i < count; i++)
        {
            if (!manager.TryGetDestination(i, out var destination))
                continue;

            var view = Instantiate(destinationViewPrefab, destinationListRoot);
            view.gameObject.SetActive(true);
            view.Configure(manager, i, destination);
            _destinationViews.Add(view);
        }
    }

    void ClearDestinations()
    {
        for (int i = 0; i < _destinationViews.Count; i++)
        {
            if (_destinationViews[i] != null)
                Destroy(_destinationViews[i].gameObject);
        }
        _destinationViews.Clear();
    }

    void ToggleList()
    {
        if (_isOpen)
            CloseList();
        else
            OpenList();
    }

    public void OpenList()
    {
        _isOpen = true;
        if (destinationListPanel != null)
            destinationListPanel.SetActive(true);
    }

    public void CloseList()
    {
        _isOpen = false;
        if (destinationListPanel != null)
            destinationListPanel.SetActive(false);
    }
}
