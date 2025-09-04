using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SaveLoadPanel : MonoBehaviour
{
    public enum Mode { Save, Load }

    [Header("Root/Modal")]
    public GameObject root;            // 패널 루트
    public ConfirmDialog confirm;      // 확인창

    [Header("Header")]
    public TMP_Text title;
    public Toggle tabManual;           // Manual 탭 토글
    public Toggle tabAuto;             // Auto 탭 토글 (로드 전용)

    [Header("List")]
    public ScrollRect scroll;
    public RectTransform content;      // Grid/Vertical Layout Group
    public SaveSlotView slotPrefab;
    public int manualSlots = 20;

    [Header("Thumbnail")]
    public Vector2Int thumbSize = new Vector2Int(512, 288); // 16:9

    Mode mode = Mode.Save;
    bool viewingManual = true;

    SaveSlotView[] pool;

    void Awake()
    {
        if (!root) root = gameObject;
        root.SetActive(false);

        if (tabManual) tabManual.onValueChanged.AddListener(OnTabManual);
        if (tabAuto) tabAuto.onValueChanged.AddListener(OnTabAuto);
    }

    public void Open(Mode m)
    {
        mode = m;
        viewingManual = true; // 기본 Manual
        if (title) title.text = mode == Mode.Save ? "Save" : "Load";
        root.SetActive(true);
        UiModalGate.Push(Close);
        InputRouter.Instance?.SuppressAdvance(0.12f);

        if (tabManual) tabManual.SetIsOnWithoutNotify(true);
        if (tabAuto) tabAuto.SetIsOnWithoutNotify(mode == Mode.Load); // 로드시만 자동 탭 가능

        BuildPool();
        Rebuild();
    }

    public void Close()
    {
        root.SetActive(false);
        UiModalGate.Pop();
        InputRouter.Instance?.SuppressAdvance(0.12f);
    }

    void OnTabManual(bool on)
    {
        if (!on) return;
        viewingManual = true;
        Rebuild();
    }

    void OnTabAuto(bool on)
    {
        if (!on) return;
        if (mode != Mode.Load)
        {
            // Save 모드에선 오토탭 의미없음 → 다시 Manual
            if (tabManual) tabManual.isOn = true;
            return;
        }
        viewingManual = false;
        Rebuild();
    }

    void BuildPool()
    {
        if (pool != null && pool.Length > 0) return;
        int count = viewingManual ? manualSlots : Mathf.Max(1, SaveLoadManager.Instance?.maxAutosaves ?? 5);
        pool = new SaveSlotView[count];
        for (int i = 0; i < count; i++)
        {
            var v = Instantiate(slotPrefab, content);
            v.gameObject.SetActive(true);
            pool[i] = v;
        }
    }

    void Rebuild()
    {
        var mgr = SaveLoadManager.Instance;
        if (mgr == null) return;

        int count = viewingManual ? manualSlots : Mathf.Max(1, mgr.maxAutosaves);
        if (pool == null || pool.Length != count)
        {
            // 재생성(슬롯 수 변경 시)
            foreach (var p in pool ?? System.Array.Empty<SaveSlotView>()) if (p) Destroy(p.gameObject);
            pool = new SaveSlotView[count];
            for (int i = 0; i < count; i++)
                pool[i] = Instantiate(slotPrefab, content);
        }

        for (int i = 0; i < count; i++)
        {
            SaveLoadManager.SaveData meta;
            bool ok = mgr.TryGetSlotInfo(viewingManual, i, out meta);
            Texture2D thumb = LoadThumb(i, viewingManual);

            var v = pool[i];
            v.Bind(i, viewingManual, ok ? meta : (SaveLoadManager.SaveData?)null, thumb, OnClickSlot);
        }

        // 스크롤 최상단
        if (scroll) scroll.verticalNormalizedPosition = 1f;
    }

    void OnClickSlot(SaveSlotView v)
    {
        var mgr = SaveLoadManager.Instance; if (mgr == null) return;

        if (mode == Mode.Save)
        {
            if (!viewingManual) return; // 오토 탭은 저장 대상 아님
            if (v.hasData)
            {
                confirm.Open($"Overwrite {v.slotIndex:00} ?", () => DoSave(v.slotIndex));
            }
            else
            {
                DoSave(v.slotIndex);
            }
        }
        else // Load
        {
            if (!v.hasData) return;
            if (viewingManual)
            {
                confirm.Open($"Load slot {v.slotIndex:00} ?", () =>
                {
                    if (mgr.LoadManual(v.slotIndex)) Close();
                });
            }
            else // auto
            {
                confirm.Open($"Load autosave {v.slotIndex:00} ?", () =>
                {
                    if (mgr.LoadAutosaveSlot(v.slotIndex)) Close();
                });
            }
        }
    }

    void DoSave(int slot)
    {
        var mgr = SaveLoadManager.Instance; if (mgr == null) return;
        bool ok = mgr.SaveManual(slot);
        if (ok)
        {
            // 썸네일 저장
            SaveThumb(slot, true);
            // 리스트 갱신
            Rebuild();
        }
    }

    // --- 썸네일 입출력 ---

    string ThumbPath(int slot, bool manual)
    {
        var mgr = SaveLoadManager.Instance;
        string prefix = manual ? mgr.manualPrefix : mgr.autosavePrefix;
        string json = Path.Combine(Application.persistentDataPath, $"{prefix}{slot}.json");
        return Path.ChangeExtension(json, ".png");
    }

    Texture2D LoadThumb(int slot, bool manual)
    {
        string path = ThumbPath(slot, manual);
        if (!File.Exists(path)) return null;
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            tex.LoadImage(bytes, markNonReadable: false);
            return tex;
        }
        catch { return null; }
    }

    // 현재 프레임 화면을 축소 저장(512x288)
    async void SaveThumb(int slot, bool manual)
    {
        string path = ThumbPath(slot, manual);
        StartCoroutine(CoCapture(path));
    }

    System.Collections.IEnumerator CoCapture(string path)
    {
        yield return new WaitForEndOfFrame();
        // 1) 화면 캡처
        var src = ScreenCapture.CaptureScreenshotAsTexture();
        // 2) 다운스케일
        int tw = thumbSize.x, th = thumbSize.y;
        var rt = RenderTexture.GetTemporary(tw, th, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(src, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(tw, th, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        Destroy(src);

        // 3) PNG 저장
        try
        {
            var bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Save thumb failed: {e.Message}");
        }
        finally
        {
            Destroy(tex);
        }
    }
}
