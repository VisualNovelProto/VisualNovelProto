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
        public float punctuationDelay; // 추가 딜레이(초): 0.00~0.08 권장

        public int width;             // 해상도
        public int height;
        public FullScreenMode fullscreenMode; // Exclusive/FullscreenWindow/MaximizedWindow/Windowed
        public int targetFps;         // 0=플랫폼 기본
        public int vSyncCount;        // 0/1/2 ...
    }

    public enum TypingSpeed { Off = 0, Slow = 1, Normal = 2, Fast = 3 }

    public SettingsData data;

    string filePath;

    void Reset()
    {
        // 합리적 기본값
        data.bgmVolume = 0.8f;
        data.sfxVolume = 1.0f;
        data.typing = TypingSpeed.Normal;
        data.punctuationDelay = 0.04f;

        data.width = Screen.currentResolution.width;
        data.height = Screen.currentResolution.height;
        data.fullscreenMode = FullScreenMode.FullScreenWindow;
        data.targetFps = 0;
        data.vSyncCount = 1;
    }

    void Awake()
    {
        filePath = Path.Combine(Application.persistentDataPath, "settings.json");
    }

    // ---------- Persist ----------
    public void Load()
    {
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
        // TypingConfig 가정: 정적 프로필 적용 메서드가 있다면 호출
        // 없다면 DialogueUI가 SettingsManager를 조회하도록 구성해도 됨.
        TypingConfig.Apply(
            enabled: data.typing != TypingSpeed.Off,
            cps: data.typing == TypingSpeed.Fast ? 120f :
                 data.typing == TypingSpeed.Slow ? 30f : 60f,
            punctuationExtraDelay: Mathf.Clamp(data.punctuationDelay, 0f, 0.2f)
        );
    }

    public void ApplyDisplay()
    {
        QualitySettings.vSyncCount = Mathf.Max(0, data.vSyncCount);
        Application.targetFrameRate = data.targetFps;
        // 해상도/창모드
        var w = Mathf.Max(640, data.width);
        var h = Mathf.Max(360, data.height);
        Screen.SetResolution(w, h, data.fullscreenMode, Screen.currentResolution.refreshRate);
    }

    // ---------- UI handlers (메뉴 연결용) ----------
    public void OnChangeBgmVolume(float v) { data.bgmVolume = v; Save(); ApplyAudio(); }
    public void OnChangeSfxVolume(float v) { data.sfxVolume = v; Save(); ApplyAudio(); }
    public void OnChangeTypingSpeed(int idx) { data.typing = (TypingSpeed)Mathf.Clamp(idx, 0, 3); Save(); ApplyTyping(); }
    public void OnChangePunctDelay(float v) { data.punctuationDelay = v; Save(); ApplyTyping(); }
    public void OnChangeResolution(int w, int h) { data.width = w; data.height = h; Save(); ApplyDisplay(); }
    public void OnChangeFullscreen(int mode) { data.fullscreenMode = (FullScreenMode)mode; Save(); ApplyDisplay(); }
    public void OnChangeVSync(int v) { data.vSyncCount = Mathf.Max(0, v); Save(); ApplyDisplay(); }
    public void OnChangeTargetFps(int fps) { data.targetFps = Mathf.Max(0, fps); Save(); ApplyDisplay(); }
}
