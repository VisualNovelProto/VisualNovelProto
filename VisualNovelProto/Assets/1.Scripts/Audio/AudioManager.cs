using System;
using UnityEngine;

[DefaultExecutionOrder(-1)]
public sealed class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Banks")]
    public AudioBank bgmBank;
    public AudioBank sfxBank;
    public AudioBank voiceBank;

    [Header("BGM")]
    public int bgmChannels = 2;
    public float bgmDefaultFade = 0.8f;
    public string resourcesFolderBgm = "Audio/BGM";

    [Header("SFX")]
    public int sfxVoices = 16;
    public float sfxDefaultVolume = 1f;
    public string resourcesFolderSfx = "Audio/SFX";

    [Header("Voice")]
    public bool voicePlaybackEnabled = true;
    public string resourcesFolderVoice = "Audio/Voice";
    public float voiceDefaultVolume = 1f;

    AudioSource[] bgmSources;
    float[] bgmBaseVolumes;
    int currentBgmIndex;
    int fadeTargetIndex = -1;
    float bgmFadeTimer;
    float bgmFadeDuration;
    bool bgmFading;

    AudioSource[] sfxSources;
    int sfxCursor;

    AudioSource voiceSource;
    float voiceBaseVolume = 1f;
    string currentVoiceKey;

    float masterVolume = 1f;
    float bgmMasterVolume = 1f;
    float sfxMasterVolume = 1f;
    float voiceMasterVolume = 1f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        InitialiseBgmSources();
        InitialiseSfxSources();
        InitialiseVoiceSource();
    }

    void InitialiseBgmSources()
    {
        bgmChannels = Mathf.Max(2, bgmChannels);
        bgmSources = new AudioSource[bgmChannels];
        bgmBaseVolumes = new float[bgmChannels];
        for (int i = 0; i < bgmChannels; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;
            src.volume = 0f;
            bgmSources[i] = src;
            bgmBaseVolumes[i] = 1f;
        }
    }

    void InitialiseSfxSources()
    {
        sfxVoices = Mathf.Max(1, sfxVoices);
        sfxSources = new AudioSource[sfxVoices];
        for (int i = 0; i < sfxVoices; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            src.volume = 0f;
            sfxSources[i] = src;
        }
    }

    void InitialiseVoiceSource()
    {
        voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;
        voiceSource.volume = 0f;
    }

    void Update()
    {
        UpdateBgmFade();
    }

    void UpdateBgmFade()
    {
        if (!bgmFading || bgmSources == null || bgmSources.Length == 0)
            return;

        bgmFadeTimer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(bgmFadeTimer / Mathf.Max(0.0001f, bgmFadeDuration));

        int front = currentBgmIndex;
        var frontSrc = bgmSources[front];
        float frontVol = bgmBaseVolumes[front] * bgmMasterVolume * masterVolume;

        if (fadeTargetIndex >= 0)
        {
            var backSrc = bgmSources[fadeTargetIndex];
            float backVol = bgmBaseVolumes[fadeTargetIndex] * bgmMasterVolume * masterVolume;

            frontSrc.volume = frontVol * (1f - t);
            backSrc.volume = backVol * t;

            if (t >= 1f)
            {
                frontSrc.Stop();
                frontSrc.volume = 0f;
                currentBgmIndex = fadeTargetIndex;
                fadeTargetIndex = -1;
                bgmFading = false;
                ApplyBgmVolumes();
            }
        }
        else
        {
            frontSrc.volume = frontVol * (1f - t);
            if (t >= 1f)
            {
                frontSrc.Stop();
                frontSrc.volume = 0f;
                bgmFading = false;
            }
        }
    }

    void ApplyBgmVolumes()
    {
        if (bgmSources == null)
            return;

        for (int i = 0; i < bgmSources.Length; i++)
        {
            var src = bgmSources[i];
            if (!src)
                continue;

            if (i == currentBgmIndex && src.isPlaying)
            {
                src.volume = bgmBaseVolumes[i] * bgmMasterVolume * masterVolume;
            }
            else if (i != currentBgmIndex)
            {
                src.volume = 0f;
            }
        }
    }

    public void PlayBgm(string key, float fade = -1f)
    {
        if (string.IsNullOrEmpty(key))
        {
            StopBgm(fade < 0f ? bgmDefaultFade : fade);
            return;
        }

        if (!TryLoadBgm(key, out var clip, out var baseVolume))
        {
            StopBgm(fade < 0f ? bgmDefaultFade : fade);
            return;
        }

        StartCrossfadeTo(clip, baseVolume, fade < 0f ? bgmDefaultFade : fade);
    }

    public void StopBgm(float fade = -1f)
    {
        if (bgmSources == null || bgmSources.Length == 0)
            return;

        float duration = fade < 0f ? bgmDefaultFade : fade;
        var current = bgmSources[currentBgmIndex];
        if (current == null || !current.isPlaying)
        {
            for (int i = 0; i < bgmSources.Length; i++)
                if (bgmSources[i])
                    bgmSources[i].Stop();
            bgmFading = false;
            fadeTargetIndex = -1;
            return;
        }

        if (duration <= 0f)
        {
            for (int i = 0; i < bgmSources.Length; i++)
            {
                if (!bgmSources[i])
                    continue;
                bgmSources[i].Stop();
                bgmSources[i].volume = 0f;
            }
            bgmFading = false;
            fadeTargetIndex = -1;
            return;
        }

        fadeTargetIndex = -1;
        bgmFadeTimer = 0f;
        bgmFadeDuration = duration;
        bgmFading = true;
    }

    void StartCrossfadeTo(AudioClip clip, float baseVolume, float fadeDuration)
    {
        if (bgmSources == null || bgmSources.Length == 0)
            return;

        int nextIndex = (currentBgmIndex + 1) % bgmSources.Length;
        var next = bgmSources[nextIndex];
        if (!next)
            return;

        next.Stop();
        next.clip = clip;
        next.loop = true;
        bgmBaseVolumes[nextIndex] = Mathf.Clamp01(baseVolume <= 0f ? 1f : baseVolume);

        if (fadeDuration <= 0f)
        {
            for (int i = 0; i < bgmSources.Length; i++)
            {
                if (!bgmSources[i])
                    continue;
                if (i == nextIndex)
                {
                    bgmSources[i].volume = bgmBaseVolumes[i] * bgmMasterVolume * masterVolume;
                    bgmSources[i].Play();
                }
                else
                {
                    bgmSources[i].Stop();
                    bgmSources[i].volume = 0f;
                }
            }
            currentBgmIndex = nextIndex;
            bgmFading = false;
            fadeTargetIndex = -1;
            return;
        }

        next.volume = 0f;
        next.Play();
        fadeTargetIndex = nextIndex;
        bgmFadeTimer = 0f;
        bgmFadeDuration = Mathf.Max(0.0001f, fadeDuration);
        bgmFading = true;
    }

    bool TryLoadBgm(string key, out AudioClip clip, out float volume)
    {
        if (bgmBank != null && bgmBank.TryGet(key, out clip, out volume))
            return true;

        string path = string.IsNullOrEmpty(resourcesFolderBgm) ? key : ($"{resourcesFolderBgm}/{key}");
        clip = Resources.Load<AudioClip>(path);
        volume = 1f;
        return clip != null;
    }

    public void SetLobbyBgm()
    {
        PlayBgm("LobbyBgm");
    }

    public void PlaySfx(string key, float volumeScale = 1f)
    {
        if (string.IsNullOrEmpty(key) || sfxSources == null || sfxSources.Length == 0)
            return;

        if (sfxBank != null && sfxBank.TryGet(key, out var clip, out var bankVolume))
        {
            PlaySfxInternal(clip, bankVolume, volumeScale);
            return;
        }

        string path = string.IsNullOrEmpty(resourcesFolderSfx) ? key : ($"{resourcesFolderSfx}/{key}");
        var resClip = Resources.Load<AudioClip>(path);
        PlaySfxInternal(resClip, 1f, volumeScale);
    }

    void PlaySfxInternal(AudioClip clip, float baseVolume, float volumeScale)
    {
        if (clip == null || sfxSources == null || sfxSources.Length == 0)
            return;

        int index = sfxCursor++ % sfxSources.Length;
        var src = sfxSources[index];
        float vol = Mathf.Clamp01((baseVolume <= 0f ? 1f : baseVolume) * volumeScale * sfxDefaultVolume * sfxMasterVolume * masterVolume);
        src.PlayOneShot(clip, vol);
    }

    public bool IsVoicePlaybackAvailable => voicePlaybackEnabled && voiceSource != null;

    public void PlayVoice(string key, float volumeScale = 1f, bool restartIfSame = true)
    {
        if (!voicePlaybackEnabled || voiceSource == null || string.IsNullOrEmpty(key))
            return;

        if (!restartIfSame && voiceSource.isPlaying && string.Equals(currentVoiceKey, key, StringComparison.Ordinal))
            return;

        if (!TryLoadVoice(key, out var clip, out var baseVolume))
            return;

        currentVoiceKey = key;
        voiceBaseVolume = Mathf.Clamp01((baseVolume <= 0f ? voiceDefaultVolume : baseVolume) * volumeScale);
        voiceSource.Stop();
        voiceSource.clip = clip;
        UpdateVoiceVolume();
        voiceSource.Play();
    }

    public void StopVoice()
    {
        if (voiceSource == null)
            return;

        voiceSource.Stop();
        voiceSource.clip = null;
        currentVoiceKey = null;
        voiceBaseVolume = 0f;
    }

    bool TryLoadVoice(string key, out AudioClip clip, out float volume)
    {
        if (voiceBank != null && voiceBank.TryGet(key, out clip, out volume))
            return true;

        string path = string.IsNullOrEmpty(resourcesFolderVoice) ? key : ($"{resourcesFolderVoice}/{key}");
        clip = Resources.Load<AudioClip>(path);
        volume = voiceDefaultVolume;
        return clip != null;
    }

    void UpdateVoiceVolume()
    {
        if (voiceSource == null)
            return;
        if (!voicePlaybackEnabled)
        {
            StopVoice();
            return;
        }

        voiceSource.volume = Mathf.Clamp01(voiceBaseVolume * voiceMasterVolume * masterVolume);
    }

    public void SetMasterVolume(float v)
    {
        masterVolume = Mathf.Clamp01(v);
        ApplyBgmVolumes();
        UpdateVoiceVolume();
    }

    public void SetBgmMasterVolume(float v)
    {
        bgmMasterVolume = Mathf.Clamp01(v);
        ApplyBgmVolumes();
    }

    public void SetSfxMasterVolume(float v)
    {
        sfxMasterVolume = Mathf.Clamp01(v);
    }

    public void SetVoiceMasterVolume(float v)
    {
        voiceMasterVolume = Mathf.Clamp01(v);
        UpdateVoiceVolume();
    }
}
