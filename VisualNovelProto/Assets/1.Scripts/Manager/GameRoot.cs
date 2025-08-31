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

        // ����(������ AddComponent)
        settings = settings ?? GetComponent<SettingsManager>() ?? gameObject.AddComponent<SettingsManager>();
        audioManager = audioManager ?? GetComponent<AudioManager>() ?? gameObject.AddComponent<AudioManager>();
        saveLoad = saveLoad ?? GetComponent<SaveLoadManager>() ?? gameObject.AddComponent<SaveLoadManager>();
        dataManager = dataManager ?? GetComponent<DataManager>() ?? gameObject.AddComponent<DataManager>();

        // ��ȣ ����(�ʿ��� �͸�)
        if (saveLoad.ui == null) saveLoad.ui = FindObjectOfType<DialogueUI>();
        if (saveLoad.runner == null) saveLoad.runner = FindObjectOfType<DialogueRunner>();

        // ���� �ε� & ����
        settings.Load();     // ���� ������ �⺻�� ����
        settings.ApplyAll(); // �����/Ÿ����/�ػ� ��� �ݿ�
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
        // �� �ٲ� �� �ΰ��� ������Ʈ�� ���� ã�� �ʿ��� ���� ����
        if (saveLoad.ui == null) saveLoad.ui = FindObjectOfType<DialogueUI>();
        if (saveLoad.runner == null) saveLoad.runner = FindObjectOfType<DialogueRunner>();

        // �� ������ �ɼ� ��� ������(Ư�� �ػ�/ĵ���� ������, Ÿ���� ��)
        settings.ApplyAll();
    }
}
