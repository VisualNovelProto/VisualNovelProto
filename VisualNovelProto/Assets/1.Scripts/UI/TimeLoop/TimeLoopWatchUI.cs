using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Visual wrist watch widget placed in the top-left corner. Shows the current time slot and exposes
/// buttons for jumping to other slots in the loop.
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
    public GameObject slotListPanel;
    public RectTransform slotListRoot;
    public TimeLoopSlotView slotViewPrefab;

    [Header("Behaviour")]
    public bool openListOnStart;

    readonly List<TimeLoopSlotView> _slotViews = new List<TimeLoopSlotView>();
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
            RebuildSlots();
        }
        else
        {
            ClearSlots();
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

        var currentSlot = manager.CurrentSlot;
        if (currentTimeLabel != null)
            currentTimeLabel.text = currentSlot != null ? currentSlot.GetDisplayLabel() : "--:--";

        if (currentBranchLabel != null)
        {
            if (manager.CurrentBranch != null)
            {
                string branchName = manager.CurrentBranch.branchName;
                if (string.IsNullOrEmpty(branchName))
                    branchName = manager.CurrentBranch.description;
                if (string.IsNullOrEmpty(branchName))
                    branchName = manager.CurrentBranch.BuildRequirementSummary();
                currentBranchLabel.text = branchName;
            }
            else
            {
                currentBranchLabel.text = "";
            }
        }

        if (loopCountLabel != null)
            loopCountLabel.text = manager.LoopCount.ToString();

        EnsureSlotViews();
        for (int i = 0; i < _slotViews.Count; i++)
            _slotViews[i].Refresh();

        if (slotListPanel != null)
            slotListPanel.SetActive(_isOpen);
    }

    void EnsureSlotViews()
    {
        if (manager == null || manager.schedule == null || slotListRoot == null || slotViewPrefab == null)
            return;

        if (_slotViews.Count == manager.schedule.SlotCount)
            return;

        RebuildSlots();
    }

    void RebuildSlots()
    {
        ClearSlots();

        if (manager == null || manager.schedule == null || slotListRoot == null || slotViewPrefab == null)
            return;

        int count = manager.schedule.SlotCount;
        for (int i = 0; i < count; i++)
        {
            var slot = manager.schedule.GetSlotOrDefault(i);
            if (slot == null)
                continue;

            var view = Instantiate(slotViewPrefab, slotListRoot);
            view.gameObject.SetActive(true);
            view.Configure(manager, i, slot);
            _slotViews.Add(view);
        }
    }

    void ClearSlots()
    {
        for (int i = 0; i < _slotViews.Count; i++)
        {
            if (_slotViews[i] != null)
                Destroy(_slotViews[i].gameObject);
        }
        _slotViews.Clear();
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
        if (slotListPanel != null)
            slotListPanel.SetActive(true);
    }

    public void CloseList()
    {
        _isOpen = false;
        if (slotListPanel != null)
            slotListPanel.SetActive(false);
    }
}
