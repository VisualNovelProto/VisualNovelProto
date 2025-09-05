using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// DDOL(전역) 세이브/로드 + 오토세이브 매니저
/// - Manual 슬롯 저장/로드
/// - Autosave(주기/챕터 종료)
/// - 슬롯 메타(타임스탬프, 플레이타임, 씬/챕터/노드) 조회
/// - 씬 재진입 시 DialogueRunner/DialogueUI 재바인딩
/// </summary>
public sealed class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance { get; private set; }

    [Header("Bindings (씬 의존)")]
    public DialogueRunner runner; // 씬 로드마다 허브로 주입
    public DialogueUI ui;         // glossary/characters 접근용(씬 로컬)

    [Header("File Settings")]
    public string manualPrefix = "save_";
    public string autosavePrefix = "auto_";
    public int maxAutosaves = 5;

    [Header("Autosave")]
    public bool enableIntervalAutosave = true;
    public float autosaveIntervalSec = 180f; // 3분
    public bool enableChapterEndAutosave = true;

    [Header("Scan Limits")]
    public int maxFlagsToScan = 2048; // 팀 규칙: 넉넉히, 풀 재사용

    [Header("Time Source")]
    public bool useUnscaledTime = true;
    [Header("Log Snapshot")]
    public int logTailCount = 40; // 세이브에 함께 저장할 백로그 줄 수 (권장 30~50)

    ChatLogManager.LogEntry[] _tmpLogBuf; // 동적할당 최소화용 임시 버퍼

    // --- 재사용 버퍼 (동적 생성 최소화) ---
    readonly List<int> tmpFlags = new List<int>(4096);
    readonly List<int> tmpGlossary = new List<int>(1024);
    readonly List<int> tmpCharacters = new List<int>(1024);

    // --- 내부 상태 ---
    float _timer;                      // 오토세이브 타이머
    int _lastSavedNodeId = -1;       // 동일 노드 반복 저장 방지
    int _lastObservedNodeId = -1;    // 진행 감지
    float _playtimeSec;                // 누적 플레이타임(세션 내)
    DateTime _sessionStart;

    [System.Serializable]
    public struct LogLine
    {
        public int nodeId;          // 선택(언어 바뀌면 재생성용)
        public string speaker;      // 화면에 보인 최종 문자열(링크/색상 포함 OK)
        public string body;         // 화면에 보인 최종 문자열
    }


    // --- 저장 데이터 포맷 ---
    [Serializable]
    public struct SaveData
    {
        public int version;               // 포맷 버전 (증분)
        public string timestamp;          // "yyyy-MM-dd HH:mm:ss"
        public string sceneName;          // 씬 이름
        public int nodeId;                // 진행 지점
        public int chapterId;             // (없으면 -1) 챕터 식별자
        public float playtimeSec;         // 누적 플레이타임(선택)
        public string saveType;           // "Manual" / "Auto" / "ChapterEnd"

        public int[] flags;               // 켜진 플래그 id
        public int[] glossary;            // 소유 글로서리 id
        public int[] characters;          // 소유 캐릭터 id
        public LogLine[] logTail;
    }

    // === 수명주기 ===
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        _sessionStart = DateTime.UtcNow;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        // 씬 허브에서 의존 레퍼런스 주입
        var hub = FindObjectOfType<SceneRefHub>();
        if (hub != null)
        {
            runner = hub.dialogueRunner;
            ui = hub.dialogueUI;
        }

        // 씬 바뀌면 오토세이브 타이머/진행 감지 초기화
        _timer = 0f;
        _lastObservedNodeId = SafeGetNodeId();
    }

    void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _playtimeSec += dt;

        // 진행 감지
        int nid = SafeGetNodeId();
        if (nid >= 0 && nid != _lastObservedNodeId)
        {
            _lastObservedNodeId = nid;
            _timer = 0f; // 진행이 있으면 타이머 리셋(짧은 시간 내 중복 저장 방지)
        }

        if (!enableIntervalAutosave) return;

        // 일정 시간 저장 안 했을 때 오토세이브
        _timer += dt;
        if (_timer >= autosaveIntervalSec)
        {
            _timer = 0f;
            TryAutosave("Interval");
        }
    }

    // === 외부에서 호출할 API ===

    /// <summary>메뉴/버튼에서 호출: 수동 저장</summary>
    public bool SaveManual(int slot)
    {
        return SaveInternal(slot, isAuto: false, tag: "Manual");
    }

    /// <summary>오토세이브(챕터 종료 시 등). 실패해도 게임 진행은 계속.</summary>
    public void SaveAutosave(string tag = "Auto")
    {
        TryAutosave(tag);
    }

    /// <summary>챕터 종료 시 호출(스토리 매니저/러너 이벤트에서)</summary>
    public void NotifyChapterEnd(int chapterId = -1)
    {
        if (!enableChapterEndAutosave) return;
        TryAutosave("ChapterEnd", chapterId);
    }

    public bool LoadManual(int slot, bool jumpToNode = true, bool clearBeforeApply = true)
    {
        string path = PathOf(manualPrefix, slot);
        return LoadFromPath(path, jumpToNode, clearBeforeApply);
    }
    public bool LoadAutosaveSlot(int slot, bool jumpToNode = true, bool clearBeforeApply = true)
    {
        string path = Path.Combine(Application.persistentDataPath, $"{autosavePrefix}{slot}.json");
        return LoadFromPath(path, jumpToNode, clearBeforeApply);
    }

    public bool LoadAutosaveLatest(bool jumpToNode = true, bool clearBeforeApply = true)
    {
        // 최신 오토세이브 파일(수정시각 기준) 찾아 로드
        var files = Directory.GetFiles(Application.persistentDataPath, autosavePrefix + "*.json");
        string best = null; DateTime bestTime = DateTime.MinValue;
        for (int i = 0; i < files.Length; i++)
        {
            var t = File.GetLastWriteTimeUtc(files[i]);
            if (t > bestTime) { bestTime = t; best = files[i]; }
        }
        if (string.IsNullOrEmpty(best)) return false;
        return LoadFromPath(best, jumpToNode, clearBeforeApply);
    }

    /// <summary>슬롯 메타 정보 단순 조회(UI 목록용)</summary>
    public bool TryGetSlotInfo(bool manual, int slot, out SaveData meta)
    {
        string path = PathOf(manual ? manualPrefix : autosavePrefix, slot);
        if (!File.Exists(path)) { meta = default; return false; }
        try
        {
            var json = File.ReadAllText(path);
            meta = JsonUtility.FromJson<SaveData>(json);
            return true;
        }
        catch { meta = default; return false; }
    }

    // === 내부 구현 ===

    string PathOf(string prefix, int slot)
    {
        return Path.Combine(Application.persistentDataPath, $"{prefix}{slot}.json");
    }

    bool SaveInternal(int slot, bool isAuto, string tag, int chapterOverride = -1)
    {
        if (runner == null || ui == null)
        {
            Debug.LogWarning("Save: runner/ui not set.");
            return false;
        }
        if (ui.glossary == null || ui.characters == null)
        {
            Debug.LogWarning("Save: DB not bound.");
            return false;
        }

        tmpFlags.Clear(); tmpGlossary.Clear(); tmpCharacters.Clear();

        // 1) 현재 노드
        int nodeId = SafeGetNodeId();
        if (nodeId < 0) nodeId = -1;

        // 동일 노드 반복 저장 방지 (오토세이브 폭주 방지)
        if (isAuto && nodeId >= 0 && nodeId == _lastSavedNodeId)
        {
            // 진행이 없으면 저장 패스
            return false;
        }

        // 2) 플래그 수집
        for (int i = 0; i < maxFlagsToScan; i++)
            if (runner.HasFlag(i)) tmpFlags.Add(i);

        // 3) 글로서리/캐릭터
        var gdb = ui.glossary;
        if (gdb != null && gdb.present != null)
        {
            for (int i = 0; i < gdb.present.Length; i++)
                if (gdb.present[i] && gdb.owned.Has(i)) tmpGlossary.Add(i);
        }
        var cdb = ui.characters;
        if (cdb != null && cdb.present != null)
        {
            for (int i = 0; i < cdb.present.Length; i++)
                if (cdb.present[i] && cdb.owned.Has(i)) tmpCharacters.Add(i);
        }

        // 4) 메타
        var data = new SaveData
        {
            version = 2,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            sceneName = SceneManager.GetActiveScene().name,
            nodeId = nodeId,
            chapterId = chapterOverride >= 0 ? chapterOverride : SafeGetChapterId(),
            playtimeSec = _playtimeSec,
            saveType = tag ?? (isAuto ? "Auto" : "Manual"),
            flags = tmpFlags.ToArray(),
            glossary = tmpGlossary.ToArray(),
            characters = tmpCharacters.ToArray()
        };

        // 4-1) 백로그 스냅샷 저장
        var lm = ChatLogManager.Instance;
        if (lm != null && logTailCount > 0)
        {
            int want = Mathf.Min(logTailCount, lm.Count);
            if (want > 0)
            {
                // 임시 버퍼 크기 확보
                if (_tmpLogBuf == null || _tmpLogBuf.Length < want)
                    _tmpLogBuf = new ChatLogManager.LogEntry[want];

                int got = lm.CopyLatest(_tmpLogBuf, want); // 오래된→최신 순서로 채워짐
                if (got > 0)
                {
                    data.logTail = new LogLine[got];
                    for (int i = 0; i < got; i++)
                    {
                        data.logTail[i] = new LogLine
                        {
                            nodeId = _tmpLogBuf[i].nodeId,
                            speaker = _tmpLogBuf[i].speaker,
                            body = _tmpLogBuf[i].bodyRich
                        };
                    }
                }
            }
        }

        // 5) 파일 기록
        try
        {
            string path = PathOf(isAuto ? autosavePrefix : manualPrefix, slot);
            var json = JsonUtility.ToJson(data, prettyPrint: false);
            File.WriteAllText(path, json);

            _lastSavedNodeId = nodeId;
            // 오토세이브는 회전(로테이션) 관리
            if (isAuto) RotateAutosaves();

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Save error: {e.Message}");
            return false;
        }
    }

    void TryAutosave(string tag, int chapterOverride = -1)
    {
        // 슬롯 회전: auto_0 ~ auto_{maxAutosaves-1}
        // 가장 오래된 파일을 덮어쓰는 방식
        int slot = FindOldestAutosaveSlot();
        SaveInternal(slot, isAuto: true, tag: tag, chapterOverride: chapterOverride);
    }

    int FindOldestAutosaveSlot()
    {
        DateTime oldest = DateTime.MaxValue; int oldestIdx = 0;
        for (int i = 0; i < Mathf.Max(1, maxAutosaves); i++)
        {
            string path = PathOf(autosavePrefix, i);
            var t = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            if (t < oldest) { oldest = t; oldestIdx = i; }
        }
        return oldestIdx;
    }

    void RotateAutosaves()
    {
        // 여기선 별도 삭제/이동 없이 FindOldestAutosaveSlot로 회전 관리
        // (필요하면 용량 관리/최대 보관 일수 추가)
    }

    bool LoadFromPath(string path, bool jumpToNode, bool clearBeforeApply)
    {
        if (runner == null || ui == null) { Debug.LogWarning("Load: runner/ui not set."); return false; }
        if (!File.Exists(path)) { Debug.LogWarning($"Load: file not found: {path}"); return false; }

        try
        {
            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<SaveData>(json);

            // 1) 초기화
            if (clearBeforeApply)
            {
                runner.ClearAllFlags();
                if (ui.glossary != null) ui.glossary.owned.Clear();
                if (ui.characters != null) ui.characters.owned.Clear();
            }

            // 2) 적용
            if (data.flags != null)
                for (int i = 0; i < data.flags.Length; i++) runner.SetFlag(data.flags[i]);

            if (data.glossary != null && ui.glossary != null)
                for (int i = 0; i < data.glossary.Length; i++) ui.glossary.owned.Set(data.glossary[i]);

            if (data.characters != null && ui.characters != null)
                for (int i = 0; i < data.characters.Length; i++) ui.characters.owned.Set(data.characters[i]);

            _playtimeSec = data.playtimeSec > 0 ? data.playtimeSec : _playtimeSec;
            // 2-1) 백로그 복원 (점프 전에 처리)
            if (clearBeforeApply)
            {
                ChatLogManager.Instance?.Clear();
            }
            if (data.logTail != null && data.logTail.Length > 0)
            {
                // 점프할 노드가 스냅샷의 '마지막 줄'과 같다면 마지막 줄은 제외(점프 직후 중복 방지)
                int tailLen = data.logTail.Length;
                int end = tailLen;
                if (jumpToNode && data.nodeId >= 0 && tailLen > 0)
                {
                    var last = data.logTail[tailLen - 1];
                    if (last.nodeId == data.nodeId) end = tailLen - 1;
                }

                var lm = ChatLogManager.Instance;
                if (lm != null)
                {
                    for (int i = 0; i < end; i++)
                    {
                        var l = data.logTail[i];
                        lm.Push(l.nodeId, l.speaker, l.body);
                    }
                }
            }
            // 3) 점프
            if (jumpToNode && data.nodeId >= 0)
            {
                runner.JumpToNode(data.nodeId);
                _lastObservedNodeId = data.nodeId;
                _lastSavedNodeId = data.nodeId;
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Load error: {e.Message}");
            return false;
        }
    }

    // === 안전 어댑터 ===
    int SafeGetNodeId() => runner != null ? runner.GetCurrentNodeId() : -1;

    // 챕터 ID 제공자가 없으면 -1 (필요 시 StoryGameManager 등에서 주입/바인딩)
    int SafeGetChapterId()
    {
        // 1) 러너가 챕터 제공 인터페이스를 구현했다면 사용
        // 2) 아니면 StoryGameManager 등에서 주입된 현재 챕터 값을 참조하도록 확장 가능
        // 여기서는 기본 -1
        return -1;
    }
}
