using UnityEngine;
using UnityEngine.UI;

public class MapView : MonoBehaviour
{
    [SerializeField] private Image backgroundImage;   // UI Image
    [SerializeField] private SpriteTable spriteTable; // ScriptableObject

    void Awake()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
        if (spriteTable != null)
            spriteTable.Build();
    }

    public void Show(string imageKey)
    {
        if (string.IsNullOrEmpty(imageKey) || backgroundImage == null) return;

        if (spriteTable != null && spriteTable.TryGet(imageKey, out var spr))
        {
            backgroundImage.sprite = spr;
        }
        else
        {
            Debug.LogWarning($"MapView: SpriteTable¿¡ '{imageKey}' ¾øÀ½");
        }
    }
}
