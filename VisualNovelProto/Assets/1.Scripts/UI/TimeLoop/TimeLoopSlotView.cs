using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Single entry inside the wrist watch loop list.
/// </summary>
public sealed class TimeLoopSlotView : MonoBehaviour
{
    public TMP_Text timeLabel;
    public TMP_Text branchLabel;
    public TMP_Text detailLabel;
    public Button button;
    public GameObject lockedGroup;
    public GameObject currentIndicator;

    TimeLoopManager _manager;
    TimeLoopSlot _slot;
    int _slotIndex = -1;

    public void Configure(TimeLoopManager manager, int slotIndex, TimeLoopSlot slot)
    {
        _manager = manager;
        _slotIndex = slotIndex;
        _slot = slot;

        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
            button.onClick.AddListener(OnClick);
        }

        Refresh();
    }

    void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClick);
    }

    void OnClick()
    {
        if (_manager != null)
            _manager.TryLoopToSlot(_slotIndex);
    }

    public void Refresh()
    {
        if (timeLabel != null)
            timeLabel.text = _slot != null ? _slot.GetDisplayLabel() : "--:--";

        TimeLoopSlot resolvedSlot;
        TimeLoopSlotBranch branch;
        bool hasBranch = _manager != null && _manager.TryGetResolvedBranch(_slotIndex, out resolvedSlot, out branch);

        if (resolvedSlot != null)
            _slot = resolvedSlot;

        if (branchLabel != null)
        {
            if (hasBranch && branch != null)
            {
                string label = branch.branchName;
                if (string.IsNullOrEmpty(label))
                    label = branch.description;
                if (string.IsNullOrEmpty(label))
                    label = _manager != null ? _manager.BuildRequirementSummary(branch) : branch.BuildRequirementSummary();
                branchLabel.text = label;
            }
            else
            {
                var lockedBranch = _manager?.GetNextLockedBranch(_slotIndex);
                if (lockedBranch != null)
                {
                    if (!string.IsNullOrEmpty(lockedBranch.branchName))
                        branchLabel.text = lockedBranch.branchName;
                    else
                        branchLabel.text = _manager != null ? _manager.BuildRequirementSummary(lockedBranch) : lockedBranch.BuildRequirementSummary();
                }
                else
                {
                    branchLabel.text = string.Empty;
                }
            }
        }

        if (detailLabel != null)
        {
            if (hasBranch && branch != null)
            {
                detailLabel.text = _manager != null ? _manager.BuildRequirementSummary(branch) : branch.BuildRequirementSummary();
            }
            else
            {
                var lockedBranch = _manager?.GetNextLockedBranch(_slotIndex);
                if (lockedBranch != null)
                {
                    string summary = _manager != null ? _manager.BuildMissingRequirementSummary(lockedBranch) : string.Empty;
                    if (string.IsNullOrEmpty(summary))
                        summary = _manager != null ? _manager.BuildRequirementSummary(lockedBranch) : lockedBranch.BuildRequirementSummary();
                    detailLabel.text = summary;
                }
                else
                {
                    detailLabel.text = string.Empty;
                }
            }
        }

        bool isCurrent = _manager != null && _manager.CurrentSlotIndex == _slotIndex;
        if (currentIndicator != null)
            currentIndicator.SetActive(isCurrent);

        bool interactable = hasBranch && _manager != null && _manager.CanLoopNow;
        if (button != null)
            button.interactable = interactable;

        if (lockedGroup != null)
            lockedGroup.SetActive(!hasBranch);
    }
}
