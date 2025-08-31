using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-10000)]
public sealed class GameRoot : MonoBehaviour
{
    public static GameRoot Instance { get; private set; }

    [Header("Managers (optional wiring)")]
    public SettingsManager settings;
    public AudioManager audioManager;
    public SaveLoadManager saveLoad;
    public DataManager dataManager;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 보장(없으면 AddComponent)
        settings = settings ?? GetComponent<SettingsManager>() ?? gameObject.AddComponent<SettingsManager>();
        audioManager = audioManager ?? GetComponent<AudioManager>() ?? gameObject.AddComponent<AudioManager>();
        saveLoad = saveLoad ?? GetComponent<SaveLoadManager>() ?? gameObject.AddComponent<SaveLoadManager>();
        dataManager = dataManager ?? GetComponent<DataManager>() ?? gameObject.AddComponent<DataManager>();

        // 상호 참조(필요한 것만)
        if (saveLoad.ui == null) saveLoad.ui = FindObjectOfType<DialogueUI>();
        if (saveLoad.runner == null) saveLoad.runner = FindObjectOfType<DialogueRunner>();

        // 설정 로드 & 적용
        settings.Load();     // 파일 없으면 기본값 생성
        settings.ApplyAll(); // 오디오/타이핑/해상도 즉시 반영
    }
    void Start()
    {
        settings.Load();
        settings.ApplyAll();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬 바뀔 때 인게임 오브젝트를 새로 찾고 필요한 참조 주입
        if (saveLoad.ui == null) saveLoad.ui = FindObjectOfType<DialogueUI>();
        if (saveLoad.runner == null) saveLoad.runner = FindObjectOfType<DialogueRunner>();

        // 새 씬에서 옵션 즉시 재적용(특히 해상도/캔버스 스케일, 타이핑 등)
        settings.ApplyAll();
    }
}
