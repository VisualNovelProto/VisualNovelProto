using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

public sealed class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance { get; private set; }

    [Header("Bindings")]
    public DialogueRunner runner;
    public DialogueUI ui; // glossary/characters ���ٿ�

    [Header("Settings")]
    public string filePrefix = "save_";
    public int maxFlagsToScan = 1024; // FlagSet �����*64�� �°� �����ְ�
    public bool useUnscaledTime = true;

    // ���� ����(���� ���� �ּ�ȭ)
    readonly List<int> tmpFlags = new List<int>(2048);
    readonly List<int> tmpGlossary = new List<int>(1024);
    readonly List<int> tmpCharacters = new List<int>(1024);

    [Serializable]
    public struct SaveData
    {
        public int version;
        public string timestamp;

        public int nodeId;

        public int[] flags;        // ���� �÷��� id ���
        public int[] glossary;     // ������ �۷μ��� id ���
        public int[] characters;   // ������ ĳ���� id ���
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

        // 1) ���� ��� id
        int nodeId = GetCurrentNodeIdSafe();

        // 2) �÷��� ���� (0..maxFlagsToScan-1)
        for (int i = 0; i < maxFlagsToScan; i++)
            if (runner.HasFlag(i)) tmpFlags.Add(i);

        // 3) �۷μ���/ĳ���� ���� �׸� ����
        var gdb = ui.glossary;
        for (int i = 0; i < gdb.present.Length; i++)
            if (gdb.present[i] && gdb.owned.Has(i)) tmpGlossary.Add(i);

        var cdb = ui.characters;
        for (int i = 0; i < cdb.present.Length; i++)
            if (cdb.present[i] && cdb.owned.Has(i)) tmpCharacters.Add(i);

        // 4) JSON ����ȭ
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

            // 1) ���� �ʱ�ȭ(����)
            if (clearBeforeApply)
            {
                runner.ClearAllFlags();
                ui.glossary.owned.Clear();
                ui.characters.owned.Clear();
            }

            // 2) ������ ����
            if (data.flags != null)
                for (int i = 0; i < data.flags.Length; i++) runner.SetFlag(data.flags[i]);

            if (data.glossary != null)
                for (int i = 0; i < data.glossary.Length; i++) ui.glossary.owned.Set(data.glossary[i]);

            if (data.characters != null)
                for (int i = 0; i < data.characters.Length; i++) ui.characters.owned.Set(data.characters[i]);

            // 3) ��� ����(����)
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

    // ���� ����� (Runner�� �Ʒ� 4�� public �޼��尡 �ʿ��ؿ�)
    int GetCurrentNodeIdSafe() => runner != null ? runner.GetCurrentNodeId() : -1;
}
