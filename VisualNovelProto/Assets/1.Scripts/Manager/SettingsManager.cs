using System;
using System.IO;
using UnityEngine;

public sealed class SettingsManager : MonoBehaviour
{
    [Serializable]
    public struct SettingsData
    {
        public float bgmVolume;       // 0..1
        public float sfxVolume;       // 0..1
        public TypingSpeed typing;    // Off/Slow/Normal/Fast
        public float punctuationDelay; // �߰� ������(��): 0.00~0.08 ����

        public int width;             // �ػ�
        public int height;
        public FullScreenMode fullscreenMode; // Exclusive/FullscreenWindow/MaximizedWindow/Windowed
        public int targetFps;         // 0=�÷��� �⺻
        public int vSyncCount;        // 0/1/2 ...

        public int resolutionPreset;                 // 0=720, 1=1080, 2=1440
    }

    public enum TypingSpeed { Off = 0, Slow = 1, Normal = 2, Fast = 3 }

    public SettingsData data;

    string filePath;

    void Reset()
    {
        // �ո��� �⺻��
        data.bgmVolume = 0.8f;
        data.sfxVolume = 1.0f;
        data.typing = TypingSpeed.Normal;
        data.punctuationDelay = 0.04f;

        data.width = Screen.currentResolution.width;
        data.height = Screen.currentResolution.height;
        data.fullscreenMode = FullScreenMode.FullScreenWindow;
        data.targetFps = 0;
        data.vSyncCount = 1;

        data.resolutionPreset = 1; // 1080p �⺻
    }

    void Awake()
    {
        filePath = Path.Combine(Application.persistentDataPath, "settings.json");
    }

    // ---------- Persist ----------
    public void Load()
    {
        EnsureFilePath();
        try
        {
            if (!File.Exists(filePath)) { Reset(); Save(); return; }
            var json = File.ReadAllText(filePath);
            data = JsonUtility.FromJson<SettingsData>(json);
        }
        catch
        {
            Reset(); Save();
        }
    }

    public void Save()
    {
        EnsureFilePath();
        try
        {
            var json = JsonUtility.ToJson(data, prettyPrint: false);
            File.WriteAllText(filePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"Settings save error: {e.Message}");
        }
    }

    void EnsureFilePath()
    {
        if (string.IsNullOrEmpty(filePath))
            filePath = Path.Combine(Application.persistentDataPath, "settings.json");
    }

    // ---------- Apply ----------
    public void ApplyAll()
    {
        ApplyAudio();
        ApplyTyping();
        ApplyDisplay();
    }

    public void ApplyAudio()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetBgmMasterVolume(Mathf.Clamp01(data.bgmVolume));
            AudioManager.Instance.SetSfxMasterVolume(Mathf.Clamp01(data.sfxVolume));
        }
    }

    public void ApplyTyping()
    {
        // TypingConfig ����: ���� ������ ���� �޼��尡 �ִٸ� ȣ��
        // ���ٸ� DialogueUI�� SettingsManager�� ��ȸ�ϵ��� �����ص� ��.
        TypingConfig.Apply(
            enabled: data.typing != TypingSpeed.Off,
            cps: data.typing == TypingSpeed.Fast ? 120f :
                 data.typing == TypingSpeed.Slow ? 30f : 60f,
            punctuationExtraDelay: Mathf.Clamp(data.punctuationDelay, 0f, 0.2f)
        );
    }

    public void ApplyDisplay()
    {
        // vSync/targetFPS�� �������
        QualitySettings.vSyncCount = Mathf.Max(0, data.vSyncCount);
        Application.targetFrameRate = data.targetFps;

        // ������ ���� (ResolutionManager ����)
        var rm = ResolutionManager.Instance;
        if (rm != null)
        {
            var preset = (ResolutionManager.Preset)Mathf.Clamp(data.resolutionPreset, 0, 2);
            rm.Apply(preset, data.fullscreenMode);
        }
        else
        {
            // ����: ���� ���� (���� ����� ��������)
            Vector2Int wh = Vector2Int.zero;
            switch (Mathf.Clamp(data.resolutionPreset, 0, 2))
            {
                case 0: wh = new Vector2Int(1280, 720); break;
                case 1: wh = new Vector2Int(1920, 1080); break;
                default: wh = new Vector2Int(2560, 1440); break;
            }
#if UNITY_2021_3_OR_NEWER
            Screen.SetResolution(wh.x, wh.y, data.fullscreenMode, Screen.currentResolution.refreshRate);
#else
        Screen.SetResolution(wh.x, wh.y, data.fullscreenMode);
#endif
        }
    }

    // ---------- UI handlers (�޴� �����) ----------
    public void OnChangeBgmVolume(float v) { data.bgmVolume = v; Save(); ApplyAudio(); }
    public void OnChangeSfxVolume(float v) { data.sfxVolume = v; Save(); ApplyAudio(); }
    public void OnChangeTypingSpeed(int idx) { data.typing = (TypingSpeed)Mathf.Clamp(idx, 0, 3); Save(); ApplyTyping(); }
    public void OnChangePunctDelay(float v) { data.punctuationDelay = v; Save(); ApplyTyping(); }
    public void OnChangeResolution(int w, int h) { data.width = w; data.height = h; Save(); ApplyDisplay(); }
    public void OnChangeFullscreen(int mode) { data.fullscreenMode = (FullScreenMode)mode; Save(); ApplyDisplay(); }
    public void OnChangeVSync(int v) { data.vSyncCount = Mathf.Max(0, v); Save(); ApplyDisplay(); }
    public void OnChangeTargetFps(int fps) { data.targetFps = Mathf.Max(0, fps); Save(); ApplyDisplay(); }
    public void OnChangeResolutionPreset(int idx) { data.resolutionPreset = Mathf.Clamp(idx, 0, 2); Save(); ApplyDisplay(); }
    public void OnChangeFullscreenMode(int modeIdx)
    {
        // 0=Windowed, 1=Borderless(FullScreenWindow), 2=Exclusive(�����쿡����)
        data.fullscreenMode = (FullScreenMode)modeIdx;
        Save(); ApplyDisplay();
    }
}
