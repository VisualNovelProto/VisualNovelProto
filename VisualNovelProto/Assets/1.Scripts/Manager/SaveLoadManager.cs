using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

public sealed class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance { get; private set; }

    [Header("Bindings")]
    public DialogueRunner runner;
    public DialogueUI ui; // glossary/characters 접근용

    [Header("Settings")]
    public string filePrefix = "save_";
    public int maxFlagsToScan = 1024; // FlagSet 워드수*64에 맞게 여유있게
    public bool useUnscaledTime = true;

    // 재사용 버퍼(동적 생성 최소화)
    readonly List<int> tmpFlags = new List<int>(2048);
    readonly List<int> tmpGlossary = new List<int>(1024);
    readonly List<int> tmpCharacters = new List<int>(1024);

    [Serializable]
    public struct SaveData
    {
        public int version;
        public string timestamp;

        public int nodeId;

        public int[] flags;        // 켜진 플래그 id 목록
        public int[] glossary;     // 소유한 글로서리 id 목록
        public int[] characters;   // 소유한 캐릭터 id 목록
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    string PathOf(int slot) => Path.Combine(Application.persistentDataPath, $"{filePrefix}{slot}.json");

    // -------- SAVE --------
    public bool Save(int slot)
    {
        if (runner == null || ui == null) { Debug.LogWarning("Save: runner/ui not set."); return false; }
        if (ui.glossary == null || ui.characters == null) { Debug.LogWarning("Save: DB not bound."); return false; }

        tmpFlags.Clear(); tmpGlossary.Clear(); tmpCharacters.Clear();

        // 1) 현재 노드 id
        int nodeId = GetCurrentNodeIdSafe();

        // 2) 플래그 수집 (0..maxFlagsToScan-1)
        for (int i = 0; i < maxFlagsToScan; i++)
            if (runner.HasFlag(i)) tmpFlags.Add(i);

        // 3) 글로서리/캐릭터 소유 항목 수집
        var gdb = ui.glossary;
        for (int i = 0; i < gdb.present.Length; i++)
            if (gdb.present[i] && gdb.owned.Has(i)) tmpGlossary.Add(i);

        var cdb = ui.characters;
        for (int i = 0; i < cdb.present.Length; i++)
            if (cdb.present[i] && cdb.owned.Has(i)) tmpCharacters.Add(i);

        // 4) JSON 직렬화
        var data = new SaveData
        {
            version = 1,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            nodeId = nodeId,
            flags = tmpFlags.ToArray(),
            glossary = tmpGlossary.ToArray(),
            characters = tmpCharacters.ToArray()
        };

        try
        {
            var json = JsonUtility.ToJson(data, prettyPrint: false);
            File.WriteAllText(PathOf(slot), json);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Save error: {e.Message}");
            return false;
        }
    }

    // -------- LOAD --------
    public bool Load(int slot, bool jumpToNode = true, bool clearBeforeApply = true)
    {
        if (runner == null || ui == null) { Debug.LogWarning("Load: runner/ui not set."); return false; }

        string path = PathOf(slot);
        if (!File.Exists(path)) { Debug.LogWarning($"Load: file not found: {path}"); return false; }

        try
        {
            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<SaveData>(json);

            // 1) 상태 초기화(선택)
            if (clearBeforeApply)
            {
                runner.ClearAllFlags();
                ui.glossary.owned.Clear();
                ui.characters.owned.Clear();
            }

            // 2) 데이터 적용
            if (data.flags != null)
                for (int i = 0; i < data.flags.Length; i++) runner.SetFlag(data.flags[i]);

            if (data.glossary != null)
                for (int i = 0; i < data.glossary.Length; i++) ui.glossary.owned.Set(data.glossary[i]);

            if (data.characters != null)
                for (int i = 0; i < data.characters.Length; i++) ui.characters.owned.Set(data.characters[i]);

            // 3) 노드 점프(선택)
            if (jumpToNode && data.nodeId >= 0)
                runner.JumpToNode(data.nodeId);

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Load error: {e.Message}");
            return false;
        }
    }

    // 안전 어댑터 (Runner에 아래 4개 public 메서드가 필요해요)
    int GetCurrentNodeIdSafe() => runner != null ? runner.GetCurrentNodeId() : -1;
}
