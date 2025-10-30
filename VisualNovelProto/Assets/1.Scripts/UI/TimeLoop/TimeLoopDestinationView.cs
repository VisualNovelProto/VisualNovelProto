using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Single entry inside the wrist watch destination list.
/// </summary>
public sealed class TimeLoopDestinationView : MonoBehaviour
{
    //public TMP_Text timeLabel;
    //public TMP_Text branchLabel;
    //public TMP_Text detailLabel;
    //public Button button;
    //public GameObject lockedGroup;
    //public GameObject currentIndicator;

    //TimeLoopManager _manager;
    //TimeLoopManager.Destination _destination;
    //int _destinationIndex = -1;

    //public void Configure(TimeLoopManager manager, int destinationIndex, TimeLoopManager.Destination destination)
    //{
    //    _manager = manager;
    //    _destinationIndex = destinationIndex;
    //    _destination = destination;

    //    if (button != null)
    //    {
    //        button.onClick.RemoveListener(OnClick);
    //        button.onClick.AddListener(OnClick);
    //    }

    //    Refresh();
    //}

    //void OnDestroy()
    //{
    //    if (button != null)
    //        button.onClick.RemoveListener(OnClick);
    //}

    //void OnClick()
    //{
    //    if (_manager != null)
    //        _manager.TryLoopToDestination(_destinationIndex);
    //}

    //public void Refresh()
    //{
    //    if (_manager != null && _manager.TryGetDestination(_destinationIndex, out var resolved))
    //        _destination = resolved;

    //    if (timeLabel != null)
    //    {
    //        string label = _destination.GetDisplayLabel();
    //        timeLabel.text = string.IsNullOrEmpty(label) ? "--" : label;
    //    }

    //    if (branchLabel != null)
    //        branchLabel.text = _destination.GetBranchLabel();

    //    if (detailLabel != null)
    //        detailLabel.text = _destination.GetDetailLabel();

    //    bool isCurrent = _manager != null && _manager.CurrentDestinationIndex == _destinationIndex;
    //    if (currentIndicator != null)
    //        currentIndicator.SetActive(isCurrent);

    //    bool interactable = _manager != null && _manager.CanLoopNow && !string.IsNullOrEmpty(_destination.indexKey);
    //    if (button != null)
    //        button.interactable = interactable;

    //    if (lockedGroup != null)
    //        lockedGroup.SetActive(false);
    //}
    //}
}
