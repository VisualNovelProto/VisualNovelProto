using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DialogueUI : MonoBehaviour
{
    // ── Actor 상태 캐시 ──
    struct ActorState
    {
        public bool active;
        public string key;
        public char pos;     // 'L','C','R'
        public bool flipX;
        public int z;
        public Sprite sprite;
        public Vector2 anchoredPos;
        public string inFx;
        public float inTime;
    }
    [Serializable]
    public struct SpriteBinding
    {
        public string key;
        public Sprite sprite;
    }
    //게터
    public bool IsTypingPublic => isTyping;
    public bool IsAwaitingChoicePublic => awaitingChoice;
    public int CurrentBodyLengthPublic => bodyText ? bodyText.textInfo.characterCount : 0;

    [Header("Auto Unlock")]
    public bool autoUnlockGlossaryOnAppear = true;
    public bool autoUnlockCharacterOnAppear = true;

    [Header("Text Elements")]
    public TextMeshProUGUI speakerText;
    public TextMeshProUGUI bodyText;

    [Header("Text Style Targets (Optional)")]
    public Image speakerPanel;
    public Image bodyPanel;
    public TMP_FontAsset defaultSpeakerFont;
    public TMP_FontAsset defaultBodyFont;
    public EmotionStyleLibrary emotionStyles;

    [Header("Stage Interaction (Optional)")]
    public CharacterInteractionLibrary interactionLibrary;

    [Header("Actor Stage (1~3 pre-made)")]
    public RectTransform leftAnchor;
    public RectTransform centerAnchor;
    public RectTransform rightAnchor;

    public Image cgImage;

    public Image leftImage;
    public Image centerImage;
    public Image rightImage;

    [Tooltip("입장 연출 기본 시간(초)")]
    public float actorDefaultInTime = 0.25f;

    // 내부용
    readonly List<Coroutine> actorCo = new List<Coroutine>(3);

    [Header("Audio (optional hook only)")]
    public AudioSource bgmSource;
    public AudioSource sfxSource;

    [Header("Choice Buttons (Fixed Pool)")]
    public Button[] choiceButtons = new Button[4];
    public TextMeshProUGUI[] choiceLabels = new TextMeshProUGUI[4];

    [Header("Click To Continue")]
    public Button continueWholeScreenButton;
    public CtcIndicator ctcIndicator;

    [Header("QTE Prompt (Optional)")]
    public QtePrompt qtePrompt;

    [Header("Sprite Bindings (Optional)")]
    public SpriteBinding[] portraitBindings;
    public SpriteBinding[] cgBindings;

    [Header("Databases & Viewers")]
    public GlossaryDatabase glossary;
    public GlossaryViewer glossaryViewer;
    public CharacterDatabase characters;
    public CharacterViewer characterViewer;

    [Header("Link Overlays (pre-made)")]
    public LinkButtonOverlay speakerOverlay;  // 스피커 위 투명 버튼 풀
    public LinkButtonOverlay bodyOverlay;     // 본문 위 투명 버튼 풀

    DialogueRunner runner;
    int currentChoiceCount;
    bool awaitingChoice;
    bool currentLineHasVisibleCharacters;

    readonly Coroutine[] actorSlotCo = new Coroutine[3];
    readonly Coroutine[] interactionCo = new Coroutine[3];

    struct QteSpec
    {
        public bool active;
        public float timeout;
        public int defaultIndex;
        public float pulsePeriod;
        public float pulseStrength;
    }

    QteSpec pendingQte;

    Color defaultSpeakerColor;
    Color defaultBodyColor;
    Color defaultSpeakerPanelColor;
    Color defaultBodyPanelColor;
    Sprite defaultSpeakerSprite;
    Sprite defaultBodySprite;
    TMP_FontAsset cachedSpeakerFont;
    TMP_FontAsset cachedBodyFont;

    void CacheDefaultTextStyles()
    {
        if (speakerText != null)
        {
            defaultSpeakerColor = speakerText.color;
            cachedSpeakerFont = speakerText.font;
            if (defaultSpeakerFont == null) defaultSpeakerFont = speakerText.font;
        }
        if (bodyText != null)
        {
            defaultBodyColor = bodyText.color;
            cachedBodyFont = bodyText.font;
            if (defaultBodyFont == null) defaultBodyFont = bodyText.font;
        }
        if (speakerPanel != null)
        {
            defaultSpeakerSprite = speakerPanel.sprite;
            defaultSpeakerPanelColor = speakerPanel.color;
        }
        if (bodyPanel != null)
        {
            defaultBodySprite = bodyPanel.sprite;
            defaultBodyPanelColor = bodyPanel.color;
        }
    }

    void RestoreDefaultTextStyles()
    {
        if (speakerText != null)
        {
            speakerText.color = defaultSpeakerColor;
            speakerText.font = defaultSpeakerFont != null ? defaultSpeakerFont : cachedSpeakerFont;
        }
        if (bodyText != null)
        {
            bodyText.color = defaultBodyColor;
            bodyText.font = defaultBodyFont != null ? defaultBodyFont : cachedBodyFont;
        }
        if (speakerPanel != null)
        {
            speakerPanel.sprite = defaultSpeakerSprite;
            speakerPanel.color = defaultSpeakerPanelColor == default ? speakerPanel.color : defaultSpeakerPanelColor;
        }
        if (bodyPanel != null)
        {
            bodyPanel.sprite = defaultBodySprite;
            bodyPanel.color = defaultBodyPanelColor == default ? bodyPanel.color : defaultBodyPanelColor;
        }
    }

    void ApplyEmotionStyle(string key)
    {
        if (emotionStyles == null || string.IsNullOrWhiteSpace(key) || !emotionStyles.TryGetStyle(key, out var style))
        {
            RestoreDefaultTextStyles();
            return;
        }

        if (bodyText != null)
        {
            bodyText.color = style.overrideBodyTextColor ? style.bodyTextColor : defaultBodyColor;
            TMP_FontAsset font = style.overrideBodyFont && style.bodyFont != null ? style.bodyFont : (defaultBodyFont != null ? defaultBodyFont : cachedBodyFont);
            if (font != null) bodyText.font = font;
        }
        if (speakerText != null)
        {
            speakerText.color = style.overrideSpeakerTextColor ? style.speakerTextColor : defaultSpeakerColor;
            TMP_FontAsset font = style.overrideSpeakerFont && style.speakerFont != null ? style.speakerFont : (defaultSpeakerFont != null ? defaultSpeakerFont : cachedSpeakerFont);
            if (font != null) speakerText.font = font;
        }
        if (bodyPanel != null)
        {
            if (style.overrideBodyPanelSprite) bodyPanel.sprite = style.bodyPanelSprite ?? defaultBodySprite;
            if (style.overrideBodyPanelColor) bodyPanel.color = style.bodyPanelColor;
            else bodyPanel.color = defaultBodyPanelColor == default ? bodyPanel.color : defaultBodyPanelColor;
        }
        if (speakerPanel != null)
        {
            if (style.overrideSpeakerPanelSprite) speakerPanel.sprite = style.speakerPanelSprite ?? defaultSpeakerSprite;
            if (style.overrideSpeakerPanelColor) speakerPanel.color = style.speakerPanelColor;
            else speakerPanel.color = defaultSpeakerPanelColor == default ? speakerPanel.color : defaultSpeakerPanelColor;
        }
    }

    string ExtractEmotionToken(ref string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        string working = text.TrimStart();

        if (working.StartsWith("[[", StringComparison.Ordinal))
        {
            int end = working.IndexOf("]]", StringComparison.Ordinal);
            if (end > 0)
            {
                string inner = working.Substring(2, end - 2);
                int eq = inner.IndexOf('=');
                if (inner.StartsWith("emotion", StringComparison.OrdinalIgnoreCase) && eq > 0)
                {
                    string key = inner.Substring(eq + 1).Trim();
                    text = working.Substring(end + 2).TrimStart();
                    return key;
                }
            }
        }

        if (working.StartsWith("<emotion", StringComparison.OrdinalIgnoreCase))
        {
            int gt = working.IndexOf('>');
            if (gt > 0)
            {
                string inner = working.Substring(1, gt - 1);
                int eq = inner.IndexOf('=');
                if (eq > 0)
                {
                    string key = inner.Substring(eq + 1).Trim();
                    text = working.Substring(gt + 1).TrimStart();
                    return key;
                }
            }
        }

        return null;
    }

    QteSpec ParseQteSpec(string policy)
    {
        QteSpec spec = default;
        if (string.IsNullOrWhiteSpace(policy)) return spec;

        string trimmed = policy.Trim();
        if (!trimmed.StartsWith("qte", StringComparison.OrdinalIgnoreCase))
            return spec;

        spec.active = true;
        spec.timeout = 5f;
        spec.defaultIndex = 0;
        spec.pulsePeriod = 1f;
        spec.pulseStrength = 0.2f;

        int op = trimmed.IndexOf('(');
        if (op >= 0)
        {
            int cp = trimmed.LastIndexOf(')');
            if (cp > op)
            {
                string args = trimmed.Substring(op + 1, cp - op - 1);
                var tokens = args.Split(',');
                for (int i = 0; i < tokens.Length; i++)
                {
                    string token = tokens[i].Trim();
                    if (token.StartsWith("timeout=", StringComparison.OrdinalIgnoreCase) && float.TryParse(token.Substring(8), out float timeout)) spec.timeout = Mathf.Max(0.1f, timeout);
                    else if (token.StartsWith("default=", StringComparison.OrdinalIgnoreCase) && int.TryParse(token.Substring(8), out int def)) spec.defaultIndex = def;
                    else if (token.StartsWith("pulsePeriod=", StringComparison.OrdinalIgnoreCase) && float.TryParse(token.Substring(12), out float period)) spec.pulsePeriod = Mathf.Max(0.01f, period);
                    else if (token.StartsWith("pulseStrength=", StringComparison.OrdinalIgnoreCase) && float.TryParse(token.Substring(14), out float strength)) spec.pulseStrength = Mathf.Max(0f, strength);
                }
            }
        }

        return spec;
    }

    void OnQteTimeout(int choiceCount)
    {
        if (!pendingQte.active)
            return;

        if (qtePrompt != null)
            qtePrompt.Cancel();

        int index = pendingQte.defaultIndex;
        if (choiceCount <= 0)
        {
            pendingQte = default;
            return;
        }

        if (index < 0 || index >= choiceCount)
            index = Mathf.Clamp(index, 0, choiceCount - 1);

        pendingQte = default;
        OnClickChoice(index);
    }
    static readonly char[] TrimWeird = { '\uFEFF', '\u200B', '\u200E', '\u200F', '\u00A0', ' ', '\t', '\r', '\n' };
    static string Clean(string s) => string.IsNullOrEmpty(s) ? s : s.Trim(TrimWeird);

    [Header("Typing")]
    public bool typingUseUnscaledTime = false;  // 일시정지 무시하고 진행할지
    public bool punctuationExtraDelay = true;   // 문장부호 추가 딜레이
    public float punctuationDelay = 0.12f;      // . , ! ? … 뒤에 추가 지연

    Coroutine typingCo;
    bool isTyping;
    string currentFullText = "";
    int currentVisible;                 // 현재 표시 글자수(가시 문자 기준)
    TypingSpeed currentSpeed;

    ActorState[] curActor = new ActorState[3]; // L=0, C=1, R=2
    string lastCgKey;                          // CG 키 캐시

    void Awake()
    {
        currentSpeed = TypingConfig.Load();
        // 테스트 중이라면 임시로 강제 속도 지정 가능
        // ApplyTypingSpeed(TypingSpeed.Fast);
        CacheDefaultTextStyles();
    }

    public void Bind(DialogueRunner attachedRunner)
    {
        runner = attachedRunner;

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int idx = i;
            if (choiceButtons[i] == null) continue;
            choiceButtons[i].onClick.RemoveAllListeners();
            choiceButtons[i].onClick.AddListener(() => OnClickChoice(idx));
        }

        if (continueWholeScreenButton != null)
        {
            continueWholeScreenButton.onClick.RemoveAllListeners();
            continueWholeScreenButton.onClick.AddListener(OnClickContinue);
        }

        if (ctcIndicator != null)
        {
            ctcIndicator.SetInlineSource(bodyText);
            ctcIndicator.OnLineStarted();
            ctcIndicator.OnLineContentUpdated(false);
        }

        // 링크 오버레이 핸들러 연결
        if (speakerOverlay != null)
        {
            speakerOverlay.onClickLink = HandleLink;
            speakerOverlay.Bind(speakerText, HandleLink);
        }
        if (bodyOverlay != null)
        {
            bodyOverlay.onClickLink = HandleLink;
            bodyOverlay.Bind(bodyText, HandleLink);
        }

        HideAllChoices();
    }

    public void ShowNode(DialogueNode node, DialogueDatabase db)
    {
        awaitingChoice = false;
        currentLineHasVisibleCharacters = false;

        pendingQte = ParseQteSpec(node.advancePolicy);
        if (!pendingQte.active && qtePrompt != null)
            qtePrompt.HideImmediate();

        if (ctcIndicator != null)
        {
            ctcIndicator.SetInlineSource(bodyText);
            ctcIndicator.OnLineStarted();
        }

        string speakerValue = node.speaker ?? string.Empty;
        string bodyValue = node.text ?? string.Empty;
        string emotionKey = ExtractEmotionToken(ref speakerValue);
        string bodyEmotion = ExtractEmotionToken(ref bodyValue);
        if (string.IsNullOrEmpty(emotionKey)) emotionKey = bodyEmotion;
        ApplyEmotionStyle(emotionKey);

        // 1) 스피커: 나레이션/속마음이면 이름칸 비움
        if (speakerText != null)
        {
            bool hideSpeaker = IsNarrative(node.rowType, speakerValue);
            if (hideSpeaker)
                speakerText.text = string.Empty;
            else
            {
                string spk = speakerValue ?? string.Empty;
                if (characters != null) spk = CharacterHighlighter.InjectLinks(spk, characters);
                speakerText.text = spk;
            }
            speakerText.ForceMeshUpdate();
            if (speakerOverlay != null) { speakerOverlay.Rebuild(); speakerOverlay.SetVisibleCharacterCount(int.MaxValue); }
            if (autoUnlockCharacterOnAppear) AutoUnlockFromTMP(speakerText);
            if(autoUnlockGlossaryOnAppear) AutoUnlockFromTMP(bodyText);
        }

        // 2) 본문: 링크 하이라이트 삽입 → 타이핑 준비
        string shown = bodyValue ?? string.Empty;
        if (glossary != null) shown = GlossaryHighlighter.InjectLinks(shown, glossary);
        if (characters != null) shown = CharacterHighlighter.InjectLinks(shown, characters);

        SetBodyTextForTyping(shown);   //원문(node.text)로 덮어쓰지 않음
        //로그 입력
        StartCoroutine(CoPushLog(node, speakerText != null ? speakerText.text : speakerValue, bodyValue, node.voice));
        //if (ChatLogManager.Instance != null)
        //{
        //    int nid = node.nodeId;
        //    string spk = speakerText ? speakerText.text : (node.speaker ?? string.Empty);
        //    string body = bodyText ? bodyText.text : (node.text ?? string.Empty);
        //    ChatLogManager.Instance.Push(nid, spk, body);
        //}
        // 링크 히트박스 "미리" 생성
        if (bodyOverlay != null) { bodyOverlay.Rebuild(); bodyOverlay.SetVisibleCharacterCount(0); }

        // 3) 타이핑 시작 (Off면 즉시 완료)
        BeginTyping();

        // 4) 이미지
        UpdateActors(node.actors);
        if (cgImage != null)
        {
            string cgKey = node.cg ?? string.Empty;
            if (!string.Equals(lastCgKey, cgKey, StringComparison.Ordinal))
            {
                cgImage.sprite = string.IsNullOrEmpty(cgKey) ? null : FindSprite(cgBindings, cgKey);
                lastCgKey = cgKey;
            }
        }
        //소리(BGM,SFX)
        if (!string.IsNullOrEmpty(node.bgm) && AudioManager.Instance != null)
            AudioManager.Instance.PlayBgm(node.bgm);

        if (!string.IsNullOrEmpty(node.sfx) && AudioManager.Instance != null)
            AudioManager.Instance.PlaySfx(node.sfx);

        HandleVoicePlayback(node.voice);

        HideAllChoices();
        ShowContinueHint(true);
    }

#region 캐릭터 이미지 관련
    struct ActorCmd
    {
        public string key;
        public char pos;
        public Vector2 xy;
        public string inFx;
        public bool flipX;
        public int z;
        public float time;
        public string outFx;
        public float outTime;
        public bool waitForOut;
        public bool crossFade;
        public float pulseDuration;
        public float pulseStrength;
        public float pulseFrequency;
        public string pose;
        public char poseWith;
    }

    void ClearActors()
    {
        StopAllActorCoroutines();
        if (leftImage) { leftImage.gameObject.SetActive(false); ResetActorVisual(leftImage); }
        if (centerImage) { centerImage.gameObject.SetActive(false); ResetActorVisual(centerImage); }
        if (rightImage) { rightImage.gameObject.SetActive(false); ResetActorVisual(rightImage); }
        for (int i = 0; i < curActor.Length; i++) curActor[i] = default;
    }

    void StopAllActorCoroutines()
    {
        for (int i = 0; i < actorSlotCo.Length; i++)
        {
            if (actorSlotCo[i] != null) StopCoroutine(actorSlotCo[i]);
            actorSlotCo[i] = null;
        }
        for (int i = 0; i < interactionCo.Length; i++)
        {
            if (interactionCo[i] != null) StopCoroutine(interactionCo[i]);
            interactionCo[i] = null;
        }
    }

    void ResetActorVisual(Image img)
    {
        if (!img) return;
        var c = img.color; c.a = 1f; img.color = c;
        var rt = img.rectTransform;
        rt.localScale = Vector3.one;
        rt.anchoredPosition = Vector2.zero;
        rt.localEulerAngles = Vector3.zero;
    }

    Image PickSlot(char pos)
    {
        switch (char.ToUpperInvariant(pos))
        {
            case 'L': return leftImage;
            case 'C': return centerImage;
            case 'R': return rightImage;
            default: return centerImage;
        }
    }

    RectTransform PickAnchor(char pos)
    {
        switch (char.ToUpperInvariant(pos))
        {
            case 'L': return leftAnchor;
            case 'C': return centerAnchor;
            case 'R': return rightAnchor;
            default: return centerAnchor;
        }
    }

    int SlotIndex(char pos)
    {
        switch (char.ToUpperInvariant(pos))
        {
            case 'L': return 0;
            case 'C': return 1;
            case 'R': return 2;
            default: return 1;
        }
    }

    static char NormalizeSlot(char pos)
    {
        char u = char.ToUpperInvariant(pos);
        if (u == 'L' || u == 'C' || u == 'R') return u;
        return 'C';
    }

    void PrepareSlot(Image slot, RectTransform anchor, ref ActorCmd cmd)
    {
        if (!slot || !anchor) return;
        var rt = slot.rectTransform;
        rt.SetParent(anchor, false);
        rt.SetSiblingIndex(Mathf.Clamp(cmd.z, 0, 10));
        float sign = cmd.flipX ? -1f : 1f;
        rt.localScale = new Vector3(sign, 1f, 1f);
        if (char.ToUpperInvariant(cmd.pos) == 'X')
            rt.anchoredPosition = cmd.xy;
        else
            rt.anchoredPosition = Vector2.zero;
    }

    void StartActorRoutine(int slotIndex, Image slot, ActorCmd cmd, Sprite nextSprite)
    {
        if (slot == null) return;
        if (actorSlotCo[slotIndex] != null)
        {
            StopCoroutine(actorSlotCo[slotIndex]);
            actorSlotCo[slotIndex] = null;
        }
        actorSlotCo[slotIndex] = StartCoroutine(CoApplyActor(slotIndex, slot, cmd, nextSprite));
    }

    IEnumerator CoApplyActor(int slotIndex, Image slot, ActorCmd cmd, Sprite newSprite)
    {
        ActorState prev = curActor[slotIndex];
        bool hadPrev = prev.active && slot.sprite != null;
        bool removing = string.IsNullOrEmpty(cmd.key) || newSprite == null;
        bool keyChanged = !string.IsNullOrEmpty(cmd.key) && !string.Equals(prev.key ?? string.Empty, cmd.key ?? string.Empty, StringComparison.Ordinal);

        if (hadPrev && (removing || keyChanged || cmd.waitForOut))
        {
            if (cmd.crossFade && !removing && keyChanged && newSprite != null)
            {
                yield return CoCrossFade(slot, prev, cmd, newSprite);
                UpdateActorState(slotIndex, cmd, newSprite);
                if (cmd.pulseDuration > 0f)
                    yield return CoLightPulse(slot, cmd);
                if (!string.IsNullOrEmpty(cmd.pose))
                    TriggerInteraction(slotIndex, SlotIndex(cmd.poseWith == '\0' ? cmd.pos : cmd.poseWith), cmd.pose);
                actorSlotCo[slotIndex] = null;
                yield break;
            }

            string outFx = !string.IsNullOrEmpty(cmd.outFx) ? cmd.outFx : prev.inFx;
            if (string.IsNullOrEmpty(outFx)) outFx = "fade";
            float outTime = cmd.outTime > 0f ? cmd.outTime : (prev.inTime > 0f ? prev.inTime : actorDefaultInTime);
            yield return CoPlayOut(slot, prev, outFx, outTime);
        }

        if (removing)
        {
            slot.gameObject.SetActive(false);
            ResetActorVisual(slot);
            curActor[slotIndex] = default;
            actorSlotCo[slotIndex] = null;
            yield break;
        }

        if (!hadPrev || keyChanged)
        {
            slot.sprite = newSprite;
            slot.gameObject.SetActive(true);
            yield return CoPlayIn(slot, cmd);
        }
        else
        {
            slot.gameObject.SetActive(true);
        }

        UpdateActorState(slotIndex, cmd, newSprite);

        if (cmd.pulseDuration > 0f)
            yield return CoLightPulse(slot, cmd);

        if (!string.IsNullOrEmpty(cmd.pose))
        {
            int targetIndex = SlotIndex(cmd.poseWith == '\0' ? cmd.pos : cmd.poseWith);
            if (targetIndex != slotIndex)
                TriggerInteraction(slotIndex, targetIndex, cmd.pose);
        }

        actorSlotCo[slotIndex] = null;
    }

    void UpdateActorState(int slotIndex, in ActorCmd cmd, Sprite sprite)
    {
        ref ActorState st = ref curActor[slotIndex];
        st.active = sprite != null;
        st.key = cmd.key;
        st.pos = cmd.pos;
        st.flipX = cmd.flipX;
        st.z = cmd.z;
        st.sprite = sprite;
        st.anchoredPos = char.ToUpperInvariant(cmd.pos) == 'X' ? cmd.xy : Vector2.zero;
        st.inFx = cmd.inFx;
        st.inTime = cmd.time > 0f ? cmd.time : actorDefaultInTime;
    }

    IEnumerator CoPlayIn(Image img, ActorCmd cmd)
    {
        string effect = string.IsNullOrEmpty(cmd.inFx) ? "fade" : cmd.inFx.ToLowerInvariant();
        float dur = cmd.time > 0f ? cmd.time : actorDefaultInTime;
        var rt = img.rectTransform;
        Vector2 basePos = char.ToUpperInvariant(cmd.pos) == 'X' ? cmd.xy : Vector2.zero;
        Vector3 baseScale = new Vector3(cmd.flipX ? -1f : 1f, 1f, 1f);
        Vector2 startPos = basePos;
        Vector3 startScale = baseScale;

        Color col = img.color;
        if (effect == "fade")
        {
            col.a = 0f; img.color = col;
        }
        else if (effect == "pop" || effect == "scale")
        {
            col.a = 0f; img.color = col;
            startScale = baseScale * 1.2f;
            rt.localScale = startScale;
        }
        else if (effect == "slide")
        {
            col.a = 1f; img.color = col;
            Vector2 dir = Vector2.down;
            switch (char.ToUpperInvariant(cmd.pos))
            {
                case 'L': dir = Vector2.left; break;
                case 'R': dir = Vector2.right; break;
                default: dir = Vector2.down; break;
            }
            startPos = basePos - dir * 220f;
            rt.anchoredPosition = startPos;
        }

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / Mathf.Max(0.0001f, dur));
            float eased = Ease01(a);

            if (effect == "fade")
            {
                col.a = Mathf.Lerp(0f, 1f, eased);
                img.color = col;
            }
            else if (effect == "pop" || effect == "scale")
            {
                col.a = Mathf.Lerp(0f, 1f, eased);
                img.color = col;
                rt.localScale = Vector3.Lerp(startScale, baseScale, eased);
            }
            else if (effect == "slide")
            {
                rt.anchoredPosition = Vector2.Lerp(startPos, basePos, eased);
            }

            yield return null;
        }

        col.a = 1f; img.color = col;
        rt.localScale = baseScale;
        rt.anchoredPosition = basePos;
    }

    IEnumerator CoPlayOut(Image img, ActorState prev, string effect, float dur)
    {
        string fx = string.IsNullOrEmpty(effect) ? "fade" : effect.ToLowerInvariant();
        dur = Mathf.Max(0.0001f, dur);
        var rt = img.rectTransform;
        Vector2 basePos = prev.anchoredPos;
        Vector3 baseScale = new Vector3(prev.flipX ? -1f : 1f, 1f, 1f);
        Vector2 targetPos = basePos;
        Vector3 targetScale = baseScale;

        if (fx == "slide")
        {
            Vector2 dir = Vector2.down;
            switch (char.ToUpperInvariant(prev.pos))
            {
                case 'L': dir = Vector2.left; break;
                case 'R': dir = Vector2.right; break;
                default: dir = Vector2.down; break;
            }
            targetPos = basePos + dir * 220f;
        }
        else if (fx == "pop" || fx == "shrink")
        {
            targetScale = baseScale * 0.8f;
        }

        Color col = img.color;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / dur);
            float eased = Ease01(a);

            if (fx == "slide")
            {
                rt.anchoredPosition = Vector2.Lerp(basePos, targetPos, eased);
                col.a = Mathf.Lerp(col.a, 0f, eased);
            }
            else if (fx == "pop" || fx == "shrink")
            {
                rt.localScale = Vector3.Lerp(baseScale, targetScale, eased);
                col.a = Mathf.Lerp(1f, 0f, eased);
            }
            else
            {
                col.a = Mathf.Lerp(1f, 0f, eased);
            }

            img.color = col;
            yield return null;
        }

        col.a = 0f; img.color = col;
        rt.localScale = baseScale;
        rt.anchoredPosition = basePos;
    }

    IEnumerator CoCrossFade(Image slot, ActorState prev, ActorCmd cmd, Sprite newSprite)
    {
        RectTransform rt = slot.rectTransform;
        GameObject tempGo = new GameObject(slot.name + "_swap", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var tempRt = tempGo.GetComponent<RectTransform>();
        tempRt.SetParent(rt.parent, false);
        tempRt.anchorMin = rt.anchorMin;
        tempRt.anchorMax = rt.anchorMax;
        tempRt.pivot = rt.pivot;
        tempRt.sizeDelta = rt.sizeDelta;
        tempRt.anchoredPosition = rt.anchoredPosition;
        tempRt.localScale = rt.localScale;

        var tempImg = tempGo.GetComponent<Image>();
        tempImg.sprite = slot.sprite;
        tempImg.color = slot.color;
        tempImg.preserveAspect = slot.preserveAspect;
        tempImg.raycastTarget = false;
        tempImg.material = slot.material;

        slot.sprite = newSprite;
        slot.gameObject.SetActive(true);

        float inTime = cmd.time > 0f ? cmd.time : actorDefaultInTime;
        float outTime = cmd.outTime > 0f ? cmd.outTime : (prev.inTime > 0f ? prev.inTime : actorDefaultInTime);
        float total = Mathf.Max(Mathf.Max(inTime, outTime), 0.0001f);

        Color newCol = slot.color; newCol.a = 0f; slot.color = newCol;
        Color oldCol = tempImg.color;

        float t = 0f;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;
            float inNorm = Mathf.Clamp01(t / inTime);
            float outNorm = Mathf.Clamp01(t / outTime);
            float easedIn = Ease01(inNorm);
            float easedOut = Ease01(outNorm);

            newCol.a = Mathf.Lerp(0f, 1f, easedIn);
            slot.color = newCol;

            oldCol.a = Mathf.Lerp(1f, 0f, easedOut);
            tempImg.color = oldCol;

            yield return null;
        }

        newCol.a = 1f; slot.color = newCol;
        Destroy(tempGo);
    }

    IEnumerator CoLightPulse(Image img, ActorCmd cmd)
    {
        float dur = Mathf.Max(0f, cmd.pulseDuration);
        if (dur <= 0f) yield break;

        float strength = cmd.pulseStrength > 0f ? cmd.pulseStrength : 0.25f;
        float freq = cmd.pulseFrequency > 0f ? cmd.pulseFrequency : 2f;
        Color baseColor = img.color;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float phase = Mathf.Sin(t * freq * Mathf.PI * 2f) * 0.5f + 0.5f;
            float k = Mathf.Lerp(1f, 1f + strength, phase);
            float r = Mathf.Clamp01(baseColor.r * k);
            float g = Mathf.Clamp01(baseColor.g * k);
            float b = Mathf.Clamp01(baseColor.b * k);
            img.color = new Color(r, g, b, baseColor.a);
            yield return null;
        }
        img.color = baseColor;
    }

    void TriggerInteraction(int initiatorIndex, int targetIndex, string poseKey)
    {
        if (interactionLibrary == null || string.IsNullOrEmpty(poseKey)) return;
        if ((uint)initiatorIndex >= 3 || (uint)targetIndex >= 3) return;

        if (!interactionLibrary.TryGetPose(poseKey, out var pose))
            return;

        Image initiator = initiatorIndex == 0 ? leftImage : (initiatorIndex == 1 ? centerImage : rightImage);
        Image target = targetIndex == 0 ? leftImage : (targetIndex == 1 ? centerImage : rightImage);
        if (!initiator || !target) return;
        if (!curActor[initiatorIndex].active || !curActor[targetIndex].active) return;

        if (interactionCo[initiatorIndex] != null) StopCoroutine(interactionCo[initiatorIndex]);
        if (interactionCo[targetIndex] != null) StopCoroutine(interactionCo[targetIndex]);

        var co = StartCoroutine(CoPlayInteraction(initiatorIndex, targetIndex, initiator, target, pose));
        interactionCo[initiatorIndex] = co;
        interactionCo[targetIndex] = co;
    }

    IEnumerator CoPlayInteraction(int initiatorIndex, int targetIndex, Image initiator, Image target, CharacterInteractionLibrary.PoseDefinition pose)
    {
        float duration = pose.duration > 0f ? pose.duration : 0.6f;
        RectTransform initRt = initiator.rectTransform;
        RectTransform targetRt = target.rectTransform;

        Vector2 initBasePos = initRt.anchoredPosition;
        Vector2 targetBasePos = targetRt.anchoredPosition;
        Vector3 initBaseScale = initRt.localScale;
        Vector3 targetBaseScale = targetRt.localScale;
        float initBaseRot = initRt.localEulerAngles.z;
        float targetBaseRot = targetRt.localEulerAngles.z;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(t / duration);
            float weight = interactionLibrary.EvaluatePoseProgress(pose, normalized);

            ApplyPoseFrame(initRt, initBasePos, initBaseScale, initBaseRot, pose.initiator, weight);
            ApplyPoseFrame(targetRt, targetBasePos, targetBaseScale, targetBaseRot, pose.target, weight);

            yield return null;
        }

        ApplyPoseFrame(initRt, initBasePos, initBaseScale, initBaseRot, pose.initiator, 0f);
        ApplyPoseFrame(targetRt, targetBasePos, targetBaseScale, targetBaseRot, pose.target, 0f);

        interactionCo[initiatorIndex] = null;
        interactionCo[targetIndex] = null;
    }

    void ApplyPoseFrame(RectTransform rt, Vector2 basePos, Vector3 baseScale, float baseRot, CharacterInteractionLibrary.PoseFrame frame, float weight)
    {
        if (rt == null) return;
        Vector2 targetPos = basePos + frame.positionOffset * weight;
        float scaleMul = Mathf.Lerp(1f, frame.scaleMultiplier <= 0f ? 1f : frame.scaleMultiplier, weight);
        float targetRot = baseRot + frame.rotationOffset * weight;
        rt.anchoredPosition = targetPos;
        rt.localScale = new Vector3(baseScale.x * scaleMul, baseScale.y * scaleMul, baseScale.z);
        rt.localEulerAngles = new Vector3(0f, 0f, targetRot);
    }

    static float Ease01(float x)
    {
        return x * x * (3f - 2f * x);
    }

    void HandleVoicePlayback(string voiceKey)
    {
        var audio = AudioManager.Instance;
        if (audio == null) return;
        if (!audio.IsVoicePlaybackAvailable)
        {
            audio.StopVoice();
            return;
        }
        if (string.IsNullOrEmpty(voiceKey))
        {
            audio.StopVoice();
            return;
        }
        audio.PlayVoice(voiceKey);
    }

    IEnumerator CoPushLog(DialogueNode node, string displaySpeaker, string displayBody, string voiceKey)
    {
        yield return null;
        ChatLogManager.Instance?.Push(node.nodeId, displaySpeaker, displayBody, voiceKey);
    }

    void UpdateActors(string spec)
    {
        ActorCmd[] cmds = new ActorCmd[3];
        int cmdCount = ParseActorsToArray(spec, cmds);
        bool[] touched = new bool[3];

        for (int i = 0; i < cmdCount; i++)
        {
            ref ActorCmd cmd = ref cmds[i];
            int si = SlotIndex(cmd.pos);
            touched[si] = true;

            Image slot = PickSlot(cmd.pos);
            RectTransform anchor = PickAnchor(cmd.pos);
            if (!slot || !anchor) continue;

            PrepareSlot(slot, anchor, ref cmd);
            Sprite spr = string.IsNullOrEmpty(cmd.key) ? null : FindSprite(portraitBindings, cmd.key);
            StartActorRoutine(si, slot, cmd, spr);
        }

        for (int si = 0; si < 3; si++)
        {
            if (touched[si]) continue;
            if (!curActor[si].active) continue;
            Image slot = si == 0 ? leftImage : (si == 1 ? centerImage : rightImage);
            if (!slot) continue;

            var cmd = new ActorCmd
            {
                key = null,
                pos = curActor[si].pos,
                xy = curActor[si].anchoredPos,
                outFx = string.IsNullOrEmpty(curActor[si].inFx) ? "fade" : curActor[si].inFx,
                outTime = curActor[si].inTime > 0f ? curActor[si].inTime : actorDefaultInTime,
                time = actorDefaultInTime
            };
            StartActorRoutine(si, slot, cmd, null);
        }
    }

    int ParseActorsToArray(string spec, ActorCmd[] outBuf)
    {
        int count = 0;
        spec = Clean(spec);
        if (string.IsNullOrWhiteSpace(spec)) return 0;

        var entries = spec.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        for (int ei = 0; ei < entries.Length && count < outBuf.Length; ei++)
        {
            string raw = Clean(entries[ei]);
            if (raw.Length == 0) continue;

            string key = raw;
            char pos = 'C';
            Vector2 xy = Vector2.zero;
            string opts = null;

            int at = raw.IndexOf('@');
            int op = raw.IndexOf('(');
            if (at >= 0) key = raw.Substring(0, at).Trim();
            if (op >= 0)
            {
                int cp = raw.LastIndexOf(')');
                if (cp > op) opts = raw.Substring(op + 1, cp - op - 1);
            }

            if (at >= 0)
            {
                string p = (op > at ? raw.Substring(at + 1, op - at - 1) : raw.Substring(at + 1)).Trim();
                if (p.Equals("L", StringComparison.OrdinalIgnoreCase)) pos = 'L';
                else if (p.Equals("C", StringComparison.OrdinalIgnoreCase)) pos = 'C';
                else if (p.Equals("R", StringComparison.OrdinalIgnoreCase)) pos = 'R';
                else
                {
                    var xyTok = p.Split(',');
                    if (xyTok.Length == 2 && float.TryParse(xyTok[0], out float px) && float.TryParse(xyTok[1], out float py))
                    {
                        pos = 'X';
                        xy = new Vector2(px, py);
                    }
                }
            }

            var cmd = new ActorCmd
            {
                key = key,
                pos = pos,
                xy = xy,
                inFx = "fade",
                flipX = false,
                z = 0,
                time = actorDefaultInTime,
                outFx = string.Empty,
                outTime = 0f,
                waitForOut = false,
                crossFade = false,
                pulseDuration = 0f,
                pulseStrength = 0f,
                pulseFrequency = 0f,
                pose = null,
                poseWith = '\0'
            };

            if (!string.IsNullOrEmpty(opts))
            {
                var kvs = opts.Split(',');
                for (int i = 0; i < kvs.Length; i++)
                {
                    string token = kvs[i].Trim();
                    if (token.StartsWith("in=", StringComparison.OrdinalIgnoreCase)) cmd.inFx = token.Substring(3).Trim();
                    else if (token.StartsWith("z=", StringComparison.OrdinalIgnoreCase) && int.TryParse(token.Substring(2), out int z)) cmd.z = z;
                    else if ((token.StartsWith("t=", StringComparison.OrdinalIgnoreCase) || token.StartsWith("time=", StringComparison.OrdinalIgnoreCase)) && float.TryParse(token.Substring(token.IndexOf('=') + 1), out float tt)) cmd.time = Mathf.Max(0.01f, tt);
                    else if (token.Equals("flipX", StringComparison.OrdinalIgnoreCase)) cmd.flipX = true;
                    else if (token.StartsWith("out=", StringComparison.OrdinalIgnoreCase)) cmd.outFx = token.Substring(4).Trim();
                    else if ((token.StartsWith("outT=", StringComparison.OrdinalIgnoreCase) || token.StartsWith("outTime=", StringComparison.OrdinalIgnoreCase)) && float.TryParse(token.Substring(token.IndexOf('=') + 1), out float ot)) cmd.outTime = Mathf.Max(0.01f, ot);
                    else if (token.StartsWith("swap=", StringComparison.OrdinalIgnoreCase))
                    {
                        string mode = token.Substring(5).Trim();
                        if (mode.Equals("wait", StringComparison.OrdinalIgnoreCase)) cmd.waitForOut = true;
                        else if (mode.Equals("cross", StringComparison.OrdinalIgnoreCase)) cmd.crossFade = true;
                    }
                    else if (token.Equals("wait", StringComparison.OrdinalIgnoreCase) || token.Equals("waitout", StringComparison.OrdinalIgnoreCase)) cmd.waitForOut = true;
                    else if (token.StartsWith("pulse=", StringComparison.OrdinalIgnoreCase) && float.TryParse(token.Substring(6), out float pd)) cmd.pulseDuration = Mathf.Max(0f, pd);
                    else if (token.StartsWith("pulseAmp=", StringComparison.OrdinalIgnoreCase) && float.TryParse(token.Substring(9), out float pa)) cmd.pulseStrength = Mathf.Max(0f, pa);
                    else if (token.StartsWith("pulseFreq=", StringComparison.OrdinalIgnoreCase) && float.TryParse(token.Substring(10), out float pf)) cmd.pulseFrequency = Mathf.Max(0f, pf);
                    else if (token.StartsWith("pose=", StringComparison.OrdinalIgnoreCase))
                    {
                        string val = token.Substring(5).Trim();
                        int colon = val.IndexOf(':');
                        if (colon >= 0)
                        {
                            cmd.pose = Clean(val.Substring(0, colon));
                            if (colon + 1 < val.Length) cmd.poseWith = NormalizeSlot(val[colon + 1]);
                        }
                        else cmd.pose = Clean(val);
                    }
                    else if (token.StartsWith("with=", StringComparison.OrdinalIgnoreCase))
                    {
                        string val = token.Substring(5).Trim();
                        if (!string.IsNullOrEmpty(val)) cmd.poseWith = NormalizeSlot(val[0]);
                    }
                }
            }

            if (cmd.crossFade) cmd.waitForOut = true;

            outBuf[count++] = cmd;
        }
        return count;
    }

#endregion

    // ===== 타이핑 =====
    public void ApplyTypingSpeed(TypingSpeed s) { currentSpeed = s; TypingConfig.Save(s); }

    void SetBodyTextForTyping(string fullRich)
    {
        currentFullText = fullRich ?? string.Empty;
        if (!bodyText)
        {
            currentLineHasVisibleCharacters = false;
            if (ctcIndicator != null) ctcIndicator.OnLineContentUpdated(false);
            return;
        }

        bodyText.text = currentFullText;   // 링크/색 포함된 텍스트

        //파괴/비활성 가드 + try/catch
        if (!bodyText || !bodyText.gameObject.activeInHierarchy)
        {
            currentLineHasVisibleCharacters = false;
            if (ctcIndicator != null) ctcIndicator.OnLineContentUpdated(false);
            return;
        }
        try { bodyText.ForceMeshUpdate(); }
        catch
        {
            currentLineHasVisibleCharacters = false;
            if (ctcIndicator != null) ctcIndicator.OnLineContentUpdated(false);
            return;
        }

        currentVisible = 0;
        bodyText.maxVisibleCharacters = 0; // 0부터 시작

        currentLineHasVisibleCharacters = HasDisplayableCharacters(bodyText.textInfo);

        if (ctcIndicator != null)
            ctcIndicator.OnLineContentUpdated(currentLineHasVisibleCharacters);
    }

    bool HasDisplayableCharacters(TMP_TextInfo info)
    {
        if (info == null)
            return false;

        for (int i = 0; i < info.characterCount; i++)
        {
            var characterInfo = info.characterInfo[i];

            if (characterInfo.elementType == TMP_TextElementType.Sprite)
                return true;

            if (characterInfo.elementType != TMP_TextElementType.Character)
                continue;

            char ch = characterInfo.character;
            if (ch == 0 || char.IsControl(ch) || char.IsWhiteSpace(ch))
                continue;

            return true;
        }

        return false;
    }

    void BeginTyping()
    {
        if (typingCo != null) StopCoroutine(typingCo);
        isTyping = false;

        if (ctcIndicator != null)
            ctcIndicator.OnTypingStarted();

        float cps = TypingConfig.GetCharsPerSecond(currentSpeed);
        if (float.IsInfinity(cps)) // Off → 전부 표시
        {
            ShowAllText();
            return;
        }
        typingCo = StartCoroutine(CoType(bodyText, cps));
    }

    public void ShowAllText()
    {
        if (!bodyText) return;
        if (typingCo != null) StopCoroutine(typingCo);
        typingCo = null;
        isTyping = false;

        bodyText.maxVisibleCharacters = int.MaxValue;
        RefreshOverlaysAfterTyping();
        if (bodyOverlay != null) bodyOverlay.SetVisibleCharacterCount(int.MaxValue);

        AutoUnlockFromTMP(bodyText);

        NotifyTypingEnded(true);
    }

    void TryNotifyAwaitingInput()
    {
        if (ctcIndicator == null)
            return;
        if (!currentLineHasVisibleCharacters)
            return;
        if (awaitingChoice)
            return;
        //if (PauseMenu.IsPaused || TransitionManager.IsPlaying || UiModalGate.IsOpen)
        //    return;

        ctcIndicator.OnAwaitingInput();
    }

    void NotifyTypingEnded(bool attemptAwait)
    {
        if (ctcIndicator != null)
            ctcIndicator.OnTypingCompleted();
        if (attemptAwait)
            TryNotifyAwaitingInput();
    }

    void AbortTyping()
    {
        isTyping = false;
        typingCo = null;
        NotifyTypingEnded(false);
    }

    // 입력 처리에서 호출: 진행 키/클릭
    public bool OnAdvanceInput()
    {
        if (isTyping)
        {
            ShowAllText();
            return true;
        }
        return false;
    }

    IEnumerator CoType(TMP_Text label, float charsPerSec)
    {
        // 시작 시점 가드
        if (!this || !label) { AbortTyping(); yield break; }
        isTyping = true;

        // 첫 ForceMeshUpdate도 안전하게
        if (!label || !label.gameObject.activeInHierarchy) { AbortTyping(); yield break; }
        try { label.ForceMeshUpdate(); }
        catch { AbortTyping(); yield break; }

        int totalChars = label.textInfo.characterCount;

        float t = 0f;
        float secPerChar = 1f / Mathf.Max(1f, charsPerSec);
        int targetVisible = 0;

        while (true)
        {
            // ★ 루프마다 생존 확인
            if (!this || !label) { AbortTyping(); yield break; }
            if (!label.gameObject.activeInHierarchy) { AbortTyping(); yield break; }

            // 안전한 ForceMeshUpdate
            try
            {
                label.ForceMeshUpdate();
                totalChars = label.textInfo.characterCount;
            }
            catch
            {
                AbortTyping(); yield break;
            }

            if (targetVisible >= totalChars) break;

            float dt = typingUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt;

            if (t >= secPerChar)
            {
                t -= secPerChar;

                targetVisible = Mathf.Clamp(targetVisible + 1, 0, totalChars);
                label.maxVisibleCharacters = targetVisible;

                if (bodyOverlay != null)
                    bodyOverlay.SetVisibleCharacterCount(targetVisible);

                if (punctuationExtraDelay && targetVisible > 0 && targetVisible <= label.text.Length)
                {
                    char ch = label.text[targetVisible - 1];
                    if (ch == '.' || ch == ',' || ch == '!' || ch == '?' || ch == '…' || ch == '，' || ch == '。')
                        t -= punctuationDelay;
                }
            }
            yield return null;
        }

        // 끝맺음
        if (label) label.maxVisibleCharacters = int.MaxValue;
        isTyping = false;
        typingCo = null;
        RefreshOverlaysAfterTyping();
        AutoUnlockFromTMP(bodyText);

        NotifyTypingEnded(true);
    }


    void RefreshOverlaysAfterTyping()
    {
        if (speakerOverlay != null && speakerText && speakerText.gameObject.activeInHierarchy)
        {
            speakerOverlay.Bind(speakerText, HandleLink);
            speakerOverlay.Rebuild();
            speakerOverlay.SetVisibleCharacterCount(int.MaxValue);
        }
        if (bodyOverlay != null && bodyText && bodyText.gameObject.activeInHierarchy)
        {
            bodyOverlay.Bind(bodyText, HandleLink);
            bodyOverlay.Rebuild();
            int visible = bodyText ? bodyText.maxVisibleCharacters : int.MaxValue;
            bodyOverlay.SetVisibleCharacterCount(visible);
        }
    }


    public void ShowChoices(ReadOnlySpan<Choice> choices)
    {
        HideAllChoices();
        awaitingChoice = true;

        if (ctcIndicator != null)
            ctcIndicator.OnChoicesShown();

        int showCount = choices.Length;
        if (showCount > choiceButtons.Length)
        {
            Debug.LogWarning($"Choice pool limit {choiceButtons.Length} exceeded. Extra choices will be ignored.");
            showCount = choiceButtons.Length;
        }

        currentChoiceCount = showCount;

        for (int i = 0; i < showCount; i++)
        {
            if (choiceLabels[i] != null)
                choiceLabels[i].text = choices[i].label ?? string.Empty;

            if (choiceButtons[i] != null)
            {
                choiceButtons[i].gameObject.SetActive(true);
                choiceButtons[i].interactable = true;
            }
        }

        if (pendingQte.active && qtePrompt != null)
        {
            qtePrompt.Show(pendingQte.timeout, pendingQte.pulsePeriod, pendingQte.pulseStrength, () => OnQteTimeout(showCount));
        }
        else if (qtePrompt != null)
        {
            qtePrompt.HideImmediate();
        }

        ShowContinueHint(false);
    }

    public void HideAllChoices()
    {
        currentChoiceCount = 0;
        for (int i = 0; i < choiceButtons.Length; i++)
            if (choiceButtons[i] != null)
                choiceButtons[i].gameObject.SetActive(false);

        if (ctcIndicator != null)
            ctcIndicator.OnChoicesHidden();
        if (qtePrompt != null)
            qtePrompt.HideImmediate();
        pendingQte = default;
    }

    public void OnClickContinue()
    {
        if (PauseMenu.IsPaused) return;
        if (TransitionManager.IsPlaying) return;
        if (UiModalGate.IsOpen) return;
        if (awaitingChoice) return;
        if (OnAdvanceInput()) return;
        if (ctcIndicator != null) ctcIndicator.OnAdvanceConsumed();
        if (runner != null) runner.Step();
    }

    public void OnAutoModeChanged(bool on)
    {
        if (ctcIndicator != null)
            ctcIndicator.OnAutoModeChanged(on);
    }

    void OnClickChoice(int index)
    {
        if (PauseMenu.IsPaused) return;
        if (TransitionManager.IsPlaying) return;
        if (UiModalGate.IsOpen) return;
        if (!awaitingChoice) return;
        if (index < 0 || index >= currentChoiceCount) return;

        if (ctcIndicator != null)
            ctcIndicator.OnAdvanceConsumed();

        if (qtePrompt != null)
            qtePrompt.Cancel();
        pendingQte = default;

        if (runner != null)
            runner.Choose(index);
    }

    void ShowContinueHint(bool show)
    {
        if (continueWholeScreenButton == null)
            return;

        continueWholeScreenButton.interactable = show;

        var graphics = continueWholeScreenButton.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] == null)
                continue;

            graphics[i].raycastTarget = show;
        }

        if (continueWholeScreenButton.gameObject.activeSelf != show)
            continueWholeScreenButton.gameObject.SetActive(show);
    }

    Sprite FindSprite(SpriteBinding[] arr, string key)
    {
        if (arr == null) { return null; }
        key = Clean(key);
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i].sprite != null && string.Equals(arr[i].key, key, StringComparison.Ordinal))
            {
                return arr[i].sprite;
            }
        }
        return null;
    }

    static bool IsNarrative(string rowType, string speaker)
    {
        if (!string.IsNullOrWhiteSpace(rowType))
        {
            string rt = rowType.Trim();
            if (rt.Equals("Narration", StringComparison.OrdinalIgnoreCase) ||
                rt.Equals("Monologue", StringComparison.OrdinalIgnoreCase) ||
                rt.Equals("나레이션") || rt.Equals("속마음"))
                return true;
        }
        if (string.IsNullOrWhiteSpace(speaker)) return true;
        speaker = speaker.Trim();
        if (speaker == "-" || speaker == "_") return true;
        return false;
    }

    // 공통 링크 처리: "g:123" / "c:45"
    void HandleLink(string linkId)
    {
        if (string.IsNullOrEmpty(linkId)) return;

        if (linkId.StartsWith("g:"))
        {
            if (int.TryParse(linkId.Substring(2), out int id) && glossary != null && glossary.Exists(id))
            {
                GlobalCodex.AddGlossary(glossary, id);
                if (glossaryViewer != null) glossaryViewer.Open(glossary, id);
            }
        }
        else if (linkId.StartsWith("c:"))
        {
            if (int.TryParse(linkId.Substring(2), out int id) && characters != null && characters.Exists(id))
            {
                if (characters != null && characters.Exists(id))
                {
                    GlobalCodex.AddCharacter(characters, id);
                    if (characterViewer != null)
                    {
                        characterViewer.Open(characters, id);
                    }
                }
            }
        }
    }
    void AutoUnlockFromTMP(TextMeshProUGUI tmp)
    {
        if (tmp == null) return;
        var ti = tmp.textInfo;
        int count = ti.linkCount;
        for (int i = 0; i < count; i++)
        {
            var link = ti.linkInfo[i];
            string id = link.GetLinkID();  // "g:12" / "c:5"
            if (id.Length < 3) continue;

            if (id[0] == 'g' && id[1] == ':' && autoUnlockGlossaryOnAppear)
            {
                if (int.TryParse(id.Substring(2), out int v))
                    GlobalCodex.AddGlossary(glossary, v);
            }
            else if (id[0] == 'c' && id[1] == ':' && autoUnlockCharacterOnAppear)
            {
                if (int.TryParse(id.Substring(2), out int v))
                    GlobalCodex.AddCharacter(characters, v);
            }
        }
    }
    //버그 터진거 관련 수습...
    void StopTypingRoutine()
    {
        if (typingCo != null)
        {
            StopCoroutine(typingCo);
            typingCo = null;
        }
        isTyping = false;
        // 오버레이/표시 상태를 안전값으로
        try { bodyOverlay?.SetVisibleCharacterCount(int.MaxValue); } catch { }
    }

    // 라이프사이클에서 반드시 정리
    void OnDisable() { StopTypingRoutine(); }
    void OnDestroy() { StopTypingRoutine(); }
}
