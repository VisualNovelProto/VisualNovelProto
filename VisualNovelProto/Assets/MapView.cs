using UnityEngine;
using UnityEngine.UI;

public class MapView : MonoBehaviour
{
    [SerializeField] private Image backgroundImage;   // UI Image 컴포넌트
    [SerializeField] private SpriteTable spriteTable; // ScriptableObject 에셋

    void Awake()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (spriteTable != null)
            spriteTable.Build();
    }

    public void Show(string imageKey)
    {
        if (string.IsNullOrEmpty(imageKey))
        {
            Debug.LogWarning("MapView: imageKey 비어있음");
            return;
        }
        if (backgroundImage == null)
        {
            Debug.LogError("MapView: backgroundImage 미연결");
            return;
        }

        if (spriteTable != null && spriteTable.TryGet(imageKey, out var spr))
            backgroundImage.sprite = spr;
        else
            Debug.LogWarning($"MapView: SpriteTable에 '{imageKey}' 매핑 없음");
    }
}
