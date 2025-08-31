using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class Letterboxer : MonoBehaviour
{
    public float targetAspect = 16f / 9f;
    Camera cam;

    void Awake() { cam = GetComponent<Camera>(); Apply(); }
    void OnEnable() { Apply(); }

    // ResolutionManager�� �����ϸ� �� ��Ȯ: rm.OnResolutionApplied += _ => Apply();
    void Update()
    {
        // ������ ũ�Ⱑ �ٲ� �� �����Ƿ� ������ ����
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
            // �¿� �ʷ��ڽ�
            float w = targetAspect / screenAspect;
            float x = (1f - w) * 0.5f;
            cam.rect = new Rect(x, 0, w, 1);
        }
        else
        {
            // ���� ���͹ڽ�
            float h = screenAspect / targetAspect;
            float y = (1f - h) * 0.5f;
            cam.rect = new Rect(0, y, 1, h);
        }
    }
}
