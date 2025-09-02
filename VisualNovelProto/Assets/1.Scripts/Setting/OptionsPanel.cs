using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DefaultExecutionOrder(+1)]
public sealed class OptionsPanel : MonoBehaviour
{
    [Header("Panel Root")]
    public GameObject root;          // �г� ��Ʈ(SetActive on/off)

    [Header("Managers")]
    public SettingsManager settings;  // ����θ� �ڵ� Ž��
    public ResolutionManager resMgr;  // ����θ� �ڵ� Ž��
    public AudioManager audioMgr;     // ����θ� �ڵ� Ž��
    public DataManager dataMgr;       // ��� ��ε�(����)

    [Header("Display")]
    public TMP_Dropdown ddResolution;     // 0:720p,1:1080p,2:1440p
    public TMP_Dropdown ddScreenMode;     // 0:Windowed,1:Borderless,2:Exclusive

    [Header("Audio")]
    public Slider slMaster;
    public Slider slBgm;
    public Slider slSfx;
    public Slider slVoice;                // ���̽� ä�� ���� (AudioManager�� ���� �ʿ�)

    [Header("Gameplay / Input")]
    public Slider slMouseSens;            // 0.1~5.0 ��
    public TMP_Dropdown ddTypingSpeed;    // 0 Off, 1 Slow, 2 Normal, 3 Fast
    public Slider slPunctDelay;           // 0.00~0.08 ����

    [Header("Performance")]
    public TMP_Dropdown ddTargetFps;      // 0=Platform(0), 1=30, 2=60, 3=120, 4=144, 5=165, 6=240
    public TMP_Dropdown ddVSync;          // 0 Off, 1 Every V-Blank, 2 Every 2 V-Blank

    [Header("Graphics")]
    public TMP_Dropdown ddTexture;        // 0 Full, 1 Half, 2 Quarter, 3 Eighth
    public TMP_Dropdown ddAA;             // 0 Off, 1=2x, 2=4x, 3=8x

    [Header("Localization / Font")]
    public TMP_Dropdown ddLanguage;       // 0=ko,1=en,2=ja �� �ʿ� ��ϴ��
    public TMP_Dropdown ddFont;           // ����(������ ����α�)

    bool syncing;

    void Awake()
    {
        if (!root) root = gameObject;
        if (!settings) settings = FindObjectOfType<SettingsManager>();
        if (!resMgr) resMgr = ResolutionManager.Instance;
        if (!audioMgr) audioMgr = AudioManager.Instance;
        if (!dataMgr) dataMgr = FindObjectOfType<DataManager>();

        // ��Ӵٿ� �ɼ��� ����ִٸ� �ڵ�� �⺻ �׸��� ��Ƶ� ��(���ϸ� �ּ� ����)
        // InitDropdownsIfEmpty();
    }

    // === ����/�ݱ� (���) ===
    public void Open()
    {
        if (root) root.SetActive(true);
        // ESC�� ������ �ݹ� ���
        UiModalGate.Push(Close);
        RefreshFromSettings();
    }

    public void Close()
    {
        if (root) root.SetActive(false);
        UiModalGate.Pop();
        // �ʿ�� Save �ѹ� ��
        // settings?.Save();
    }

    // === UI -> Settings === (�ν����Ϳ��� OnValueChanged�� �����ص� �ǰ�, �Ʒ� �޼��带 ��ư/�����̴��� ���� ���ε��ص� ��)
    public void OnResolutionChanged(int idx)
    {
        if (!EnsureManagers()) return;
        settings.data.resolutionPreset = Mathf.Clamp(idx, 0, 2);
        settings.Save(); settings.ApplyDisplay();
    }

    public void OnScreenModeChanged(int idx)
    {
        if (!EnsureManagers()) return;
        settings.data.fullscreenMode = (FullScreenMode)Mathf.Clamp(idx, 0, 3);
        settings.Save(); settings.ApplyDisplay();
    }

    public void OnMasterVolume(float v)
    {
        if (!EnsureManagers()) return;
        settings.data.masterVolume = Mathf.Clamp01(v);
        settings.Save();
        // ����
        audioMgr?.SetMasterVolume(settings.data.masterVolume);
    }

    public void OnBgmVolume(float v) { settings?.OnChangeBgmVolume(v); }
    public void OnSfxVolume(float v) { settings?.OnChangeSfxVolume(v); }
    public void OnVoiceVolume(float v)
    {
        if (!EnsureManagers()) return;
        settings.data.voiceVolume = Mathf.Clamp01(v);
        settings.Save();
        audioMgr?.SetVoiceMasterVolume(settings.data.voiceVolume); // AudioManager�� ���� �߰� �ʿ�
    }

    public void OnMouseSensitivity(float v)
    {
        if (!EnsureManagers()) return;
        settings.data.mouseSensitivity = Mathf.Clamp(v, 0.05f, 10f);
        settings.Save();
        // ���� ������ �÷��̾� ��Ʈ�ѿ��� settings.data �о� ���
    }

    public void OnTypingSpeed(int idx) { settings?.OnChangeTypingSpeed(idx); }
    public void OnPunctuationDelay(float v) { settings?.OnChangePunctDelay(v); }

    public void OnTargetFps(int idx)
    {
        if (!EnsureManagers()) return;
        // ����ǥ: 0=�÷���(0), 1=30, 2=60, 3=120, 4=144, 5=165, 6=240
        int[] table = { 0, 30, 60, 120, 144, 165, 240 };
        int fps = table[Mathf.Clamp(idx, 0, table.Length - 1)];
        settings.data.targetFps = fps;
        settings.Save(); settings.ApplyDisplay();
    }

    public void OnVSync(int idx)
    {
        if (!EnsureManagers()) return;
        int[] table = { 0, 1, 2 };
        settings.data.vSyncCount = table[Mathf.Clamp(idx, 0, table.Length - 1)];
        settings.Save(); settings.ApplyDisplay();
    }

    public void OnTextureQuality(int idx)
    {
        if (!EnsureManagers()) return;
        // Unity: masterTextureLimit = 0(Full),1(Half),2(Quarter),3(Eighth)
        settings.data.textureQuality = Mathf.Clamp(idx, 0, 3);
        settings.Save();
        QualitySettings.masterTextureLimit = settings.data.textureQuality;
    }

    public void OnAntiAliasing(int idx)
    {
        if (!EnsureManagers()) return;
        // 0 Off, 1=2x, 2=4x, 3=8x
        int[] table = { 0, 2, 4, 8 };
        int aa = table[Mathf.Clamp(idx, 0, table.Length - 1)];
        settings.data.antiAliasing = aa;
        settings.Save();
        QualitySettings.antiAliasing = aa;
    }

    public void OnLanguage(int idx)
    {
        if (!EnsureManagers()) return;
        // ����: 0=ko,1=en,2=ja  (��Ӵٿ� �׸� ������ �°�)
        string[] table = { "ko", "en", "ja" };
        settings.data.languageCode = table[Mathf.Clamp(idx, 0, table.Length - 1)];
        settings.Save();

        // ��Ÿ�� ���ε�(����): CSV ��ü/�ؽ�Ʈ ����
        dataMgr?.LoadIfNeeded();         // ĳ�� ����
        // dataMgr?.ReloadLanguage(settings.data.languageCode); // ���������� ���
        // UI �ؽ�Ʈ �������� ������ �ִٸ� ȣ��
    }

    public void OnFont(int idx)
    {
        if (!EnsureManagers()) return;
        settings.data.fontIndex = Mathf.Max(0, idx);
        settings.Save();

        //������ ���� ������, ���Ŀ� FontManager�� �ִٸ� ���� ����
        //���� UI �ؽ�Ʈ�� �ٲٰ� ������ ���� ��ũ��Ʈ �ϳ�(FontBindable)�� �ٿ� OnFontChanged�� �����ϰ� �ؼ� font = fa; �ϸ� ��.
        //FontManager.Instance?.ApplyByIndex(settings.data.fontIndex);
    }

    // === Settings -> UI ===
    public void RefreshFromSettings()
    {
        if (!EnsureManagers()) return;
        syncing = true;

        // Display
        ddResolution?.SetValueWithoutNotify(Mathf.Clamp(settings.data.resolutionPreset, 0, 2));
        ddScreenMode?.SetValueWithoutNotify((int)settings.data.fullscreenMode);

        // Audio
        slMaster?.SetValueWithoutNotify(settings.data.masterVolume);
        slBgm?.SetValueWithoutNotify(settings.data.bgmVolume);
        slSfx?.SetValueWithoutNotify(settings.data.sfxVolume);
        if (slVoice) slVoice.SetValueWithoutNotify(settings.data.voiceVolume);

        // Gameplay / Input
        slMouseSens?.SetValueWithoutNotify(settings.data.mouseSensitivity);
        ddTypingSpeed?.SetValueWithoutNotify((int)settings.data.typing);
        slPunctDelay?.SetValueWithoutNotify(settings.data.punctuationDelay);

        // Performance
        ddVSync?.SetValueWithoutNotify(Mathf.Clamp(settings.data.vSyncCount, 0, 2));
        ddTargetFps?.SetValueWithoutNotify(FpsToIndex(settings.data.targetFps));

        // Graphics
        ddTexture?.SetValueWithoutNotify(Mathf.Clamp(settings.data.textureQuality, 0, 3));
        ddAA?.SetValueWithoutNotify(AAToIndex(settings.data.antiAliasing));

        // Localization / Font
        ddLanguage?.SetValueWithoutNotify(LangToIndex(settings.data.languageCode));
        if (ddFont) ddFont.SetValueWithoutNotify(Mathf.Max(0, settings.data.fontIndex));

        syncing = false;
    }

    // Helpers
    int FpsToIndex(int fps)
    {
        switch (fps) { case 30: return 1; case 60: return 2; case 120: return 3; case 144: return 4; case 165: return 5; case 240: return 6; default: return 0; }
    }
    int AAToIndex(int aa)
    { // 0,2,4,8 �� 0,1,2,3
        if (aa <= 0) return 0; if (aa == 2) return 1; if (aa == 4) return 2; return 3;
    }
    int LangToIndex(string code)
    {
        if (string.IsNullOrEmpty(code)) return 0;
        code = code.ToLowerInvariant();
        if (code.StartsWith("en")) return 1;
        if (code.StartsWith("ja")) return 2;
        return 0; // ko default
    }

    bool EnsureManagers()
    {
        if (!settings) settings = FindObjectOfType<SettingsManager>();
        if (!resMgr) resMgr = ResolutionManager.Instance;
        if (!audioMgr) audioMgr = AudioManager.Instance;
        return settings != null;
    }

    // (����) �ɼ� �׸��� ��� ������ �⺻ �׸� ����
    void InitDropdownsIfEmpty()
    {
        if (ddResolution && ddResolution.options.Count == 0)
            ddResolution.AddOptions(new System.Collections.Generic.List<string> { "1280��720", "1920��1080", "2560��1440" });
        if (ddScreenMode && ddScreenMode.options.Count == 0)
            ddScreenMode.AddOptions(new System.Collections.Generic.List<string> { "Windowed", "Borderless", "Exclusive" });
        if (ddTypingSpeed && ddTypingSpeed.options.Count == 0)
            ddTypingSpeed.AddOptions(new System.Collections.Generic.List<string> { "Off", "Slow", "Normal", "Fast" });
        if (ddTargetFps && ddTargetFps.options.Count == 0)
            ddTargetFps.AddOptions(new System.Collections.Generic.List<string> { "Platform", "30", "60", "120", "144", "165", "240" });
        if (ddVSync && ddVSync.options.Count == 0)
            ddVSync.AddOptions(new System.Collections.Generic.List<string> { "Off", "Every V-Blank", "Every 2 V-Blank" });
        if (ddTexture && ddTexture.options.Count == 0)
            ddTexture.AddOptions(new System.Collections.Generic.List<string> { "Full", "Half", "Quarter", "Eighth" });
        if (ddAA && ddAA.options.Count == 0)
            ddAA.AddOptions(new System.Collections.Generic.List<string> { "Off", "2x", "4x", "8x" });
        if (ddLanguage && ddLanguage.options.Count == 0)
            ddLanguage.AddOptions(new System.Collections.Generic.List<string> { "�ѱ���", "English", "������" });
    }
}
