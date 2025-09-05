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

    [Header("Thumbnail Capture (Camera)")]
    [SerializeField] Canvas visualsCanvas;            // 이 Canvas만 썸네일에 담음
    [SerializeField] Camera visualsCaptureCamera;     // 전용 캡쳐 카메라 (enabled=false 권장)

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
        StartCoroutine(CoDoSave(slot));
    }

    System.Collections.IEnumerator CoDoSave(int slot)
    {
        var mgr = SaveLoadManager.Instance; if (mgr == null) yield break;
        bool ok = mgr.SaveManual(slot);
        if (ok)
        {
            // 캡쳐 완료까지 '반드시' 기다린다
            string path = ThumbPath(slot, true);
            yield return CoCapture(path);
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

    System.Collections.IEnumerator CoCapture(string path)
    {
        if (!visualsCanvas || !visualsCaptureCamera)
        {
            Debug.LogWarning("SaveLoadPanel: visualsCanvas or visualsCaptureCamera is not set. Fallback to full-screen capture.");
            yield return new WaitForEndOfFrame();
            var full = ScreenCapture.CaptureScreenshotAsTexture();
            try { System.IO.File.WriteAllBytes(path, full.EncodeToPNG()); }
            finally { Destroy(full); }
            yield break;
        }

        // --- 1) VisualsCanvas 설정 백업
        var prevMode = visualsCanvas.renderMode;
        var prevWorldCam = visualsCanvas.worldCamera;
        var prevPlane = visualsCanvas.planeDistance;

        // --- 2) 캡쳐용으로 전환 (잠시)
        visualsCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        visualsCanvas.worldCamera = visualsCaptureCamera;
        visualsCanvas.planeDistance = 1f;

        // 캔버스 갱신 반영 대기
        Canvas.ForceUpdateCanvases();
        yield return null;                   // 1프레임
        yield return new WaitForEndOfFrame();// 렌더 직후

        // --- 3) 카메라로 RenderTexture에 그리기
        int tw = thumbSize.x, th = thumbSize.y;      // 기존 필드 사용
        var rt = RenderTexture.GetTemporary(tw, th, 0, RenderTextureFormat.ARGB32);
        var prevTarget = visualsCaptureCamera.targetTexture;
        var prevActive = RenderTexture.active;

        visualsCaptureCamera.targetTexture = rt;
        visualsCaptureCamera.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(tw, th, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
        tex.Apply();

        // --- 4) 저장
        try
        {
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Save thumb failed: {e.Message}");
        }

        // --- 5) 원복/정리
        visualsCaptureCamera.targetTexture = prevTarget;
        RenderTexture.active = prevActive;
        RenderTexture.ReleaseTemporary(rt);
        Destroy(tex);

        visualsCanvas.renderMode = prevMode;
        visualsCanvas.worldCamera = prevWorldCam;
        visualsCanvas.planeDistance = prevPlane;
    }
}
