using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class Letterboxer : MonoBehaviour
{
    public float targetAspect = 16f / 9f;
    Camera cam;

    void Awake() { cam = GetComponent<Camera>(); Apply(); }
    void OnEnable() { Apply(); }

    // ResolutionManager와 연결하면 더 정확: rm.OnResolutionApplied += _ => Apply();
    void Update()
    {
        // 윈도우 크기가 바뀔 수 있으므로 가볍게 폴링
        Apply();
    }

    void Apply()
    {
        float screenAspect = (float)Screen.width / Screen.height;
        if (Mathf.Approximately(screenAspect, targetAspect))
        {
            cam.rect = new Rect(0, 0, 1, 1);
            return;
        }

        if (screenAspect > targetAspect)
        {
            // 좌우 필러박스
            float w = targetAspect / screenAspect;
            float x = (1f - w) * 0.5f;
            cam.rect = new Rect(x, 0, w, 1);
        }
        else
        {
            // 상하 레터박스
            float h = screenAspect / targetAspect;
            float y = (1f - h) * 0.5f;
            cam.rect = new Rect(0, y, 1, h);
        }
    }
}
