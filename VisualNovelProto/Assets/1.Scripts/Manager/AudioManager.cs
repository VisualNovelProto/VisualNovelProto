using System;
using UnityEngine;

[DefaultExecutionOrder(-1)]
public sealed class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Banks")]
    public AudioBank bgmBank;
    public AudioBank sfxBank;

    [Header("BGM")]
    public int bgmChannels = 2;          // �������̵�� 2ä��
    public float bgmDefaultFade = 0.8f;

    [Header("SFX")]
    public int sfxVoices = 16;           // ���� ���� ��
    public float sfxDefaultVolume = 1f;

    [Header("Fallback")]
    public string resourcesFolderBgm = "Audio/BGM";
    public string resourcesFolderSfx = "Audio/SFX";

    AudioSource[] bgm;
    int bgmFront = 0; // ���� �鸮�� ä�� �ε���
    float bgmFadeT, bgmFadeDur;
    bool bgmFading;

    //����
    float bgmMaster = 1f;
    float sfxMaster = 1f;

    AudioSource[] sfx; int sfxCursor;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // BGM �ҽ�
        bgm = new AudioSource[Mathf.Max(2, bgmChannels)];
        for (int i = 0; i < bgm.Length; i++)
        {
            var a = gameObject.AddComponent<AudioSource>();
            a.playOnAwake = false; a.loop = true; a.spatialBlend = 0f;
            a.volume = 0f;
            bgm[i] = a;
        }

        // SFX Ǯ
        sfx = new AudioSource[Mathf.Max(1, sfxVoices)];
        for (int i = 0; i < sfx.Length; i++)
        {
            var a = gameObject.AddComponent<AudioSource>();
            a.playOnAwake = false; a.loop = false; a.spatialBlend = 0f;
            a.volume = sfxDefaultVolume;
            sfx[i] = a;
        }
    }

    void Update()
    {
        if (bgmFading)
        {
            bgmFadeT += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(bgmFadeT / Mathf.Max(0.0001f, bgmFadeDur));
            float a = 1f - k; float b = k;

            int back = 1 - bgmFront;
            bgm[bgmFront].volume = a * bgmMaster;
            bgm[back].volume = b * bgmMaster;

            if (k >= 1f)
            {
                bgm[bgmFront].Stop(); bgmFront = back; bgmFading = false;
            }
        }
    }

    // -------- BGM --------
    public void PlayBgm(string key, float fade = -1f)
    {
        if (string.IsNullOrEmpty(key)) { StopBgm(fade < 0 ? bgmDefaultFade : fade); return; }

        AudioClip clip; float vol;
        if (bgmBank != null && bgmBank.TryGet(key, out clip, out vol))
        {
            CrossfadeTo(clip, vol, fade < 0 ? bgmDefaultFade : fade);
            return;
        }

        // Resources ����
        var res = string.IsNullOrEmpty(resourcesFolderBgm) ? key : (resourcesFolderBgm + "/" + key);
        clip = Resources.Load<AudioClip>(res);
        CrossfadeTo(clip, 1f, fade < 0 ? bgmDefaultFade : fade);
    }

    public void StopBgm(float fade = -1f)
    {
        float f = (fade < 0f ? bgmDefaultFade : fade);
        if (f <= 0f) { for (int i = 0; i < bgm.Length; i++) { bgm[i].Stop(); bgm[i].volume = 0f; } bgmFading = false; return; }
        int back = 1 - bgmFront;
        bgm[back].Stop(); bgm[back].clip = null; bgm[back].volume = 0f;
        bgmFadeT = 0f; bgmFadeDur = f; bgmFading = true;
    }

    void CrossfadeTo(AudioClip clip, float vol, float fade)
    {
        if (clip == null) { StopBgm(fade); return; }
        int back = 1 - bgmFront;
        bgm[back].clip = clip;
        bgm[back].volume = 0f;
        bgm[back].Play();
        bgm[back].loop = true;
        // Ÿ�� ������ clip ���� ������ �ƴ϶� AudioSource�� �����ϴ� ���̸� �Ʒ� �������� ��ü ����
        // ���⼭�� ������ 1.0���� �ΰ�, ������ �ͽ��� Bank.volume���� ����
        bgm[back].volume = 0f;

        // ���̵� ����
        bgmFadeT = 0f; bgmFadeDur = (fade < 0f ? bgmDefaultFade : fade); bgmFading = true;

        // front�� 1.0 �� 0.0, back�� 0.0 �� 1.0����
        // ���� ������ Mixer���� �����ϴ� ���� ����
    }

    // -------- SFX --------
    public void PlaySfx(string key, float volumeScale = 1f)
    {
        AudioClip clip; float vol;
        if (sfxBank != null && sfxBank.TryGet(key, out clip, out vol))
        {
            PlaySfxInternal(clip, vol * volumeScale);
            return;
        }

        // Resources ����
        var res = string.IsNullOrEmpty(resourcesFolderSfx) ? key : (resourcesFolderSfx + "/" + key);
        clip = Resources.Load<AudioClip>(res);
        PlaySfxInternal(clip, volumeScale);
    }

    void PlaySfxInternal(AudioClip clip, float volume)
    {
        if (clip == null) return;
        int i = (sfxCursor++) % sfx.Length;
        var a = sfx[i];
        a.clip = clip;
        a.volume = Mathf.Clamp01(volume * sfxDefaultVolume * sfxMaster);
        a.PlayOneShot(clip, a.volume);
    }
    //�Ŵ��� ���� ���� �Լ�
    public void SetBgmMasterVolume(float v)
    {
        bgmMaster = Mathf.Clamp01(v);

        // ��� �ݿ� (���̵� ���� �ƴϾ ���� ä�ο� �����ֱ�)
        if (bgm != null)
        {
            for (int i = 0; i < bgm.Length; i++)
            {
                if (bgm[i] == null) continue;
                if (bgm[i].isPlaying)
                    bgm[i].volume = (i == bgmFront ? 1f : 0f) * bgmMaster;
                else
                    bgm[i].volume = 0f;
            }
        }
    }

    public void SetSfxMasterVolume(float v)
    {
        sfxMaster = Mathf.Clamp01(v);
    }
}
