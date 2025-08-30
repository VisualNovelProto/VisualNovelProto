using System;
using UnityEngine;
/// <summary>
/// �ػ� �����ϴ� �Ŵ����Դϴ�.
/// </summary>
public sealed class ResolutionManager : MonoBehaviour
{
    public static ResolutionManager Instance { get; private set; }

    public enum Preset { P720 = 0, P1080 = 1, P1440 = 2 }

    public struct ApplyResult
    {
        public int width, height;
        public FullScreenMode mode;
        public int refreshRate;
    }

    public event Action<ApplyResult> OnResolutionApplied;

    // 16:9 ������
    static readonly Vector2Int[] kPresets = {
        new Vector2Int(1280, 720),
        new Vector2Int(1920,1080),
        new Vector2Int(2560,1440),
    };

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public Vector2Int GetSize(Preset p) => kPresets[(int)p];

    public ApplyResult Apply(Preset preset, FullScreenMode mode)
    {
        var wh = GetSize(preset);

        // ����� ���� �ػ� �� ���� ����� �� ���� (�ֻ��� ����)
        var res = ChooseClosest(wh.x, wh.y);
        int rr = res.refreshRate;

#if UNITY_2021_3_OR_NEWER
        Screen.SetResolution(res.width, res.height, mode, rr);
#else
        Screen.SetResolution(res.width, res.height, mode);
#endif
        var result = new ApplyResult { width = res.width, height = res.height, mode = mode, refreshRate = rr };
        OnResolutionApplied?.Invoke(result);
        return result;
    }

    // ���� ����Ϳ��� ���� ����� �ػ� ã��
    Resolution ChooseClosest(int w, int h)
    {
        var list = Screen.resolutions;
        if (list == null || list.Length == 0)
        {
            return new Resolution { width = w, height = h, refreshRate = Screen.currentResolution.refreshRate };
        }

        int bestIdx = 0;
        long bestDiff = long.MaxValue;
        for (int i = 0; i < list.Length; i++)
        {
            long dx = list[i].width - w;
            long dy = list[i].height - h;
            long diff = dx * dx + dy * dy;
            if (diff < bestDiff) { bestDiff = diff; bestIdx = i; }
        }
        return list[bestIdx];
    }
}
