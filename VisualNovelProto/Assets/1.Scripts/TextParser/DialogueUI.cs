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
    }
    [Serializable]
    public struct SpriteBinding
    {
        public string key;
        public Sprite sprite;
    }

    [Header("Auto Unlock")]
    public bool autoUnlockGlossaryOnAppear = true;
    public bool autoUnlockCharacterOnAppear = true;

    [Header("Text Elements")]
    public TextMeshProUGUI speakerText;
    public TextMeshProUGUI bodyText;

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
    string lastCgKey;                           // CG 키 캐시

    void Awake()
    {
        currentSpeed = TypingConfig.Load();
        // 테스트 중이라면 임시로 강제 속도 지정 가능
        // ApplyTypingSpeed(TypingSpeed.Fast);
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

        // 1) 스피커: 나레이션/속마음이면 이름칸 비움
        if (speakerText != null)
        {
            bool hideSpeaker = IsNarrative(node.rowType, node.speaker);
            if (hideSpeaker)
                speakerText.text = string.Empty;
            else
            {
                string spk = node.speaker ?? string.Empty;
                if (characters != null) spk = CharacterHighlighter.InjectLinks(spk, characters);
                speakerText.text = spk;
            }
            speakerText.ForceMeshUpdate();
            if (speakerOverlay != null) { speakerOverlay.Rebuild(); speakerOverlay.SetVisibleCharacterCount(int.MaxValue); }
            if (autoUnlockCharacterOnAppear) AutoUnlockFromTMP(speakerText);
        }

        // 2) 본문: 링크 하이라이트 삽입 → 타이핑 준비
        string shown = node.text ?? string.Empty;
        if (glossary != null) shown = GlossaryHighlighter.InjectLinks(shown, glossary);
        if (characters != null) shown = CharacterHighlighter.InjectLinks(shown, characters);

        SetBodyTextForTyping(shown);   // ★ 원문(node.text)로 덮어쓰지 않음

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

        HideAllChoices();
        ShowContinueHint(true);
    }
    #region 캐릭터 이미지 관련
    // ────── Actors (생략없음, 기존 동일) ──────
    void ClearActors()
    {
        StopActorCo();
        if (leftImage) { leftImage.gameObject.SetActive(false); ResetAlpha(leftImage); }
        if (centerImage) { centerImage.gameObject.SetActive(false); ResetAlpha(centerImage); }
        if (rightImage) { rightImage.gameObject.SetActive(false); ResetAlpha(rightImage); }
    }
    void StopActorCo()
    {
        for (int i = 0; i < actorCo.Count; i++)
            if (actorCo[i] != null) StopCoroutine(actorCo[i]);
        actorCo.Clear();
    }
    void ResetAlpha(Image img)
    {
        if (!img) return;
        var c = img.color; c.a = 1f; img.color = c;
        img.rectTransform.localScale = Vector3.one;
    }

    struct ActorCmd
    {
        public string key;   // 스프라이트 키
        public char pos;     // 'L','C','R' 또는 'X'(커스텀)
        public Vector2 xy;   // pos=='X'일 때 위치
        public string inFx;  // "fade","slide","pop"
        public bool flipX;
        public int z;
        public float time;
    }

    void ApplyActors(string spec)
    {
        ClearActors();
        if (string.IsNullOrWhiteSpace(spec)) return;

        var cmds = ParseActors(spec);
        for (int i = 0; i < Mathf.Min(3, cmds.Count); i++)
        {
            var cmd = cmds[i];
            var slot = PickSlot(cmd.pos);
            var anchor = PickAnchor(cmd.pos);
            if (!slot || !anchor) continue;

            slot.sprite = FindSprite(portraitBindings, cmd.key);
            slot.gameObject.SetActive(true);
            var rt = slot.rectTransform;
            rt.SetParent(anchor, false);
            if (cmd.pos == 'X') rt.anchoredPosition = cmd.xy;
            rt.SetSiblingIndex(Mathf.Clamp(cmd.z, 0, 10));
            rt.localScale = new Vector3(cmd.flipX ? -1f : 1f, 1f, 1f);

            StartCoroutine(PlayIn(slot, cmd));
        }
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
    List<ActorCmd> ParseActors(string spec)
    {
        spec = Clean(spec);
        var list = new List<ActorCmd>(3);
        if (string.IsNullOrWhiteSpace(spec)) return list;

        var entries = spec.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in entries)
        {
            var s = Clean(raw);
            if (s.Length == 0) continue;

            string key = s;
            key = Clean(key);
            char pos = 'C';
            Vector2 xy = Vector2.zero;
            string opts = null;

            int at = s.IndexOf('@');
            int op = s.IndexOf('(');
            if (at >= 0) key = s.Substring(0, at).Trim();
            if (op >= 0)
            {
                int cp = s.LastIndexOf(')');
                if (cp > op) opts = s.Substring(op + 1, cp - op - 1);
            }

            if (at >= 0)
            {
                string p = (op > at ? s.Substring(at + 1, op - at - 1) : s.Substring(at + 1)).Trim();
                if (p.Equals("L", StringComparison.OrdinalIgnoreCase)) pos = 'L';
                else if (p.Equals("C", StringComparison.OrdinalIgnoreCase)) pos = 'C';
                else if (p.Equals("R", StringComparison.OrdinalIgnoreCase)) pos = 'R';
                else
                {
                    var xyTok = p.Split(',');
                    if (xyTok.Length == 2 &&
                        float.TryParse(xyTok[0], out float px) &&
                        float.TryParse(xyTok[1], out float py))
                    {
                        pos = 'X';
                        xy = new Vector2(px, py);
                    }
                }
            }

            var cmd = new ActorCmd { key = key, pos = pos, xy = xy, inFx = "fade", z = 0, time = actorDefaultInTime };
            if (!string.IsNullOrEmpty(opts))
            {
                var kvs = opts.Split(',');
                foreach (var kv in kvs)
                {
                    var t = kv.Trim();
                    if (t.StartsWith("in=", StringComparison.OrdinalIgnoreCase)) cmd.inFx = t.Substring(3).Trim();
                    else if (t.StartsWith("z=", StringComparison.OrdinalIgnoreCase) && int.TryParse(t.Substring(2), out int z)) cmd.z = z;
                    else if (t.StartsWith("t=", StringComparison.OrdinalIgnoreCase) && float.TryParse(t.Substring(2), out float tt)) cmd.time = Mathf.Max(0.01f, tt);
                    else if (t.Equals("flipX", StringComparison.OrdinalIgnoreCase)) cmd.flipX = true;
                }
            }
            list.Add(cmd);
        }
        return list;
    }
    IEnumerator PlayIn(Image img, ActorCmd cmd)
    {
        float t = 0f;
        var rt = img.rectTransform;

        var col = img.color;
        if (cmd.inFx == "fade") col.a = 0f;
        else if (cmd.inFx == "pop") { col.a = 0f; rt.localScale = rt.localScale * 1.2f; }
        else if (cmd.inFx == "slide")
        {
            col.a = 1f;
            float off = 220f;
            if (cmd.pos == 'L') rt.anchoredPosition += new Vector2(-off, 0f);
            else if (cmd.pos == 'R') rt.anchoredPosition += new Vector2(+off, 0f);
            else rt.anchoredPosition += new Vector2(0f, -off);
        }
        img.color = col;

        while (t < cmd.time)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / cmd.time);

            if (cmd.inFx == "fade")
            {
                col.a = a; img.color = col;
            }
            else if (cmd.inFx == "pop")
            {
                col.a = a; img.color = col;
                rt.localScale = Vector3.Lerp(rt.localScale, Vector3.one * (cmd.flipX ? -1f : 1f), a);
            }
            else if (cmd.inFx == "slide")
            {
                rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, Vector2.zero, a);
            }
            yield return null;
        }

        col.a = 1f; img.color = col;
        rt.localScale = new Vector3(cmd.flipX ? -1f : 1f, 1f, 1f);
        rt.anchoredPosition = Vector2.zero;
    }
    int SlotIndex(char pos)
    {
        switch (char.ToUpperInvariant(pos))
        {
            case 'L': return 0;
            case 'C': return 1;
            case 'R': return 2;
            default: return 1; // 안전: Center
        }
    }

    void UpdateActors(string spec)
    {
        // 1) 새 명령 파싱(최대 3개, 추가 할당 없이)
        ActorCmd[] cmds = new ActorCmd[3];
        int cmdCount = ParseActorsToArray(spec, cmds);

        // 2) 이번 노드에서 사용되는 슬롯 표시
        bool[] desired = new bool[3];

        for (int i = 0; i < cmdCount; i++)
        {
            ref ActorCmd cmd = ref cmds[i];
            int si = SlotIndex(cmd.pos);
            desired[si] = true;

            // 현재 상태
            ref ActorState st = ref curActor[si];

            // 바뀐 게 없다면(키/flipX/z 모두 동일) 건너뜀
            bool sameKey = string.Equals(st.key ?? "", cmd.key ?? "", StringComparison.Ordinal);
            bool sameFlip = st.flipX == cmd.flipX;
            bool sameZ = st.z == cmd.z;
            bool noChange = st.active && sameKey && sameFlip && sameZ;

            if (noChange)
                continue;

            // 스프라이트 찾기(키가 비면 비활성)
            Sprite spr = string.IsNullOrEmpty(cmd.key) ? null : FindSprite(portraitBindings, cmd.key);

            // 슬롯/앵커 선택
            Image slot = PickSlot(cmd.pos);
            RectTransform anchor = PickAnchor(cmd.pos);
            if (!slot || !anchor)
                continue;

            // 부모/정렬/플립/위치 세팅
            var rt = slot.rectTransform;
            rt.SetParent(anchor, false);
            rt.SetSiblingIndex(Mathf.Clamp(cmd.z, 0, 10));
            rt.localScale = new Vector3(cmd.flipX ? -1f : 1f, 1f, 1f);

            // 스프라이트가 바뀌면 교체
            slot.sprite = spr;
            slot.gameObject.SetActive(spr != null);

            // 트랜지션은 "내용이 바뀔 때만" 실행
            StopActorCo(); // 기존 코루틴 정리(해당 슬롯만 정리하고 싶으면 분리해도 됨)
            TransitionManager.PlayActorIn(slot,cmd.pos,string.IsNullOrEmpty(cmd.inFx)?"fade":cmd.inFx,(cmd.time<=0?actorDefaultInTime:cmd.time),cmd.flipX);

            // 상태 갱신
            st.active = spr != null;
            st.key = cmd.key;
            st.pos = cmd.pos;
            st.flipX = cmd.flipX;
            st.z = cmd.z;
            st.sprite = spr;
        }

        // 3) 명령에 없는 슬롯은 숨김(이번 컷씬에서 내려야 하는 배우)
        for (int si = 0; si < 3; si++)
        {
            if (!desired[si] && curActor[si].active)
            {
                Image slot = si == 0 ? leftImage : (si == 1 ? centerImage : rightImage);
                if (slot) slot.gameObject.SetActive(false);
                curActor[si].active = false;
                curActor[si].key = null;
                curActor[si].sprite = null;
            }
        }
    }

    // 기존 ParseActors(list) 대신, 고정 버퍼로 채우는 버전
    int ParseActorsToArray(string spec, ActorCmd[] outBuf)
    {
        // L;C;R 최대 3개 가정
        int count = 0;
        spec = Clean(spec);
        if (string.IsNullOrWhiteSpace(spec)) return 0;

        var entries = spec.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        for (int ei = 0; ei < entries.Length && count < 3; ei++)
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
                    if (xyTok.Length == 2 &&
                        float.TryParse(xyTok[0], out float px) &&
                        float.TryParse(xyTok[1], out float py))
                    {
                        pos = 'X'; // 커스텀 위치는 Center 슬록을 쓰되 좌표만 사용
                        xy = new Vector2(px, py);
                    }
                }
            }

            var cmd = new ActorCmd { key = key, pos = pos, xy = xy, inFx = "fade", z = 0, time = actorDefaultInTime };

            if (!string.IsNullOrEmpty(opts))
            {
                var kvs = opts.Split(',');
                for (int i = 0; i < kvs.Length; i++)
                {
                    string t = kvs[i].Trim();
                    if (t.StartsWith("in=", StringComparison.OrdinalIgnoreCase)) cmd.inFx = t.Substring(3).Trim();
                    else if (t.StartsWith("z=", StringComparison.OrdinalIgnoreCase) && int.TryParse(t.Substring(2), out int z)) cmd.z = z;
                    else if (t.StartsWith("t=", StringComparison.OrdinalIgnoreCase) && float.TryParse(t.Substring(2), out float tt)) cmd.time = Mathf.Max(0.01f, tt);
                    else if (t.Equals("flipX", StringComparison.OrdinalIgnoreCase)) cmd.flipX = true;
                }
            }

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
        if (!bodyText) return;

        bodyText.text = currentFullText;       // 링크/색 포함된 텍스트
        bodyText.ForceMeshUpdate();            // 줄바꿈 확정
        currentVisible = 0;
        bodyText.maxVisibleCharacters = 0;     // 0부터 시작
    }

    void BeginTyping()
    {
        if (typingCo != null) StopCoroutine(typingCo);
        isTyping = false;

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
        if (!label) yield break;

        isTyping = true;

        label.ForceMeshUpdate();
        int totalChars = label.textInfo.characterCount;

        float t = 0f;
        float secPerChar = 1f / Mathf.Max(1f, charsPerSec);
        int targetVisible = 0;

        while (true)
        {
            label.ForceMeshUpdate();
            totalChars = label.textInfo.characterCount;
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

        label.maxVisibleCharacters = int.MaxValue;
        isTyping = false;
        typingCo = null;

        RefreshOverlaysAfterTyping();
    }

    void RefreshOverlaysAfterTyping()
    {
        if (speakerOverlay != null)
        {
            speakerOverlay.Bind(speakerText, HandleLink);
            speakerOverlay.Rebuild();
            speakerOverlay.SetVisibleCharacterCount(int.MaxValue); // ★ 항상 보이게
        }
        if (bodyOverlay != null)
        {
            bodyOverlay.Bind(bodyText, HandleLink);
            bodyOverlay.Rebuild();
            // 현재 보이는 글자 수를 반영(타이핑 끝이면 MaxValue)
            int visible = bodyText ? bodyText.maxVisibleCharacters : int.MaxValue;
            bodyOverlay.SetVisibleCharacterCount(visible);         // ★ 핵심
        }
    }

    public void ShowChoices(ReadOnlySpan<Choice> choices)
    {
        HideAllChoices();
        awaitingChoice = true;

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

        ShowContinueHint(false);
    }

    public void HideAllChoices()
    {
        currentChoiceCount = 0;
        for (int i = 0; i < choiceButtons.Length; i++)
            if (choiceButtons[i] != null)
                choiceButtons[i].gameObject.SetActive(false);
    }

    public void OnClickContinue()
    {
        if (PauseMenu.IsPaused) return;
        if (TransitionManager.IsPlaying) return;
        if (UiModalGate.IsOpen) return;
        if (awaitingChoice) return;
        if (runner != null) runner.Step();
    }

    void OnClickChoice(int index)
    {
        if (PauseMenu.IsPaused) return;
        if (TransitionManager.IsPlaying) return;
        if (UiModalGate.IsOpen) return;
        if (!awaitingChoice) return;
        if (index < 0 || index >= currentChoiceCount) return;

        if (runner != null)
            runner.Choose(index);
    }

    void ShowContinueHint(bool show) { /* TODO */ }

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
                glossary.owned.Set(id);
                if (glossaryViewer != null) glossaryViewer.Open(glossary, id);
            }
        }
        else if (linkId.StartsWith("c:"))
        {
            if (int.TryParse(linkId.Substring(2), out int id) && characters != null && characters.Exists(id))
            {
                characters.owned.Set(id);
                if (characterViewer != null) characterViewer.Open(characters, id);
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
                int v; if (int.TryParse(id.Substring(2), out v) && glossary != null) glossary.owned.Set(v);
            }
            else if (id[0] == 'c' && id[1] == ':' && autoUnlockCharacterOnAppear)
            {
                int v; if (int.TryParse(id.Substring(2), out v) && characters != null) characters.owned.Set(v);
            }
        }
    }
}
