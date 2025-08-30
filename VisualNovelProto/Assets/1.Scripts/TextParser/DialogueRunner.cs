using System;
using UnityEngine;

public sealed class DialogueRunner : MonoBehaviour
{
    [Header("Debug / Fallback")]
    public bool autoNextById = true;

    public TextAsset csv;
    public int startNodeId = 0;

    public DialogueUI ui; // 에디터에서 할당

    DialogueDatabase db;
    FlagSet flags;

    const int StackMax = 1024;
    int[] stack = new int[StackMax];
    int sp = 0;

    DialogueNode current;
    bool hasCurrent;

    // DialogueRunner.cs 내부 어딘가에 추가 (public)
    public int GetCurrentNodeId() => hasCurrent ? current.nodeId : -1;

    public bool HasFlag(int id) => flags.Has(id);
    public void SetFlag(int id) => flags.Set(id);
    public void ClearAllFlags() => flags.Clear();

    // 저장된 노드로 바로 이동 (UI 업데이트 포함)
    public void JumpToNode(int nodeId)
    {
        if (nodeId < 0) return;
        EnterNode(nodeId);
    }

    void Awake()
    {
        db = ScriptableObject.CreateInstance<DialogueDatabase>();
        db.LoadFromCsvText(csv.text);

        flags.Clear();

        if (ui != null) ui.Bind(this);

        StoryFlags.Bind(id => flags.Has(id));
        Push(startNodeId);
        Step();
    }

    void Push(int nodeId)
    {
        if (sp >= StackMax) throw new Exception("Dialogue stack overflow");
        stack[sp++] = nodeId;
    }

    int Pop()
    {
        if (sp <= 0) return -1;
        return stack[--sp];
    }

    bool EnterNode(int nodeId)
    {
        if (!db.TryGetNodeById(nodeId, out current, out _)) return false;

        if (!flags.HasAll(db.flagsPool, current.flagsReqOffset, current.flagsReqCount))
            return false;

        flags.SetAll(db.flagsPool, current.flagsSetOffset, current.flagsSetCount);

        hasCurrent = true;

        // ? 트랜지션 훅: 노드 진입 직후 재생(입력은 TransitionManager에서 막음)
        if (!string.IsNullOrEmpty(current.transition))
            TransitionManager.Play(current.transition);

        if (ui != null)
            ui.ShowNode(current, db);
        else
            Debug.Log($"[{current.nodeId}] {current.speaker}: {current.text}");

        return true;
    }

    public void Step()
    {
        // 일시정지/트랜지션/패널열림 중에는 진행 금지
        if (PauseMenu.IsPaused || TransitionManager.IsPlaying || UiModalGate.IsOpen) return;

        // 스택에 예약된 노드가 있으면 우선 진입
        while (sp > 0)
        {
            int nid = Pop();
            if (EnterNode(nid)) return;
        }

        if (hasCurrent)
        {
            // 선택지 표시
            if (current.HasChoices)
            {
                var choices = db.GetChoicesOf(ref current);
                if (ui != null)
                {
                    ui.ShowChoices(choices);
                }
                else
                {
                    for (int i = 0; i < choices.Length; i++)
                        Debug.Log($"  ({i}) {choices[i].label} -> {choices[i].gotoNodeId}");
                    Debug.Log("Call Choose(index) to continue.");
                }
                return;
            }

            // 다음 노드로 단순 이동
            if (current.nextNodeId >= 0)
            {
                EnterNode(current.nextNodeId);
                return;
            }

            // 종료
            hasCurrent = false;
            if (ui == null) Debug.Log("End of script.");
        }
        else
        {
            if (ui == null) Debug.Log("No more nodes.");
        }
    }
    public void Choose(int index)
    {
        if (PauseMenu.IsPaused || TransitionManager.IsPlaying || UiModalGate.IsOpen) return;
        if (!hasCurrent || !current.HasChoices) return;

        var span = db.GetChoicesOf(ref current);
        if ((uint)index >= (uint)span.Length) return;

        ref readonly Choice ch = ref span[index];

        flags.SetAll(db.flagsPool, ch.setOffset, ch.setCount);

        if (ch.gotoNodeId >= 0)
        {
            Push(ch.gotoNodeId);
            Step();
        }
    }
}
