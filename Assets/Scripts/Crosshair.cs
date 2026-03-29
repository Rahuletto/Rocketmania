using UnityEngine;
using UnityEngine.UI;

public class Crosshair : MonoBehaviour
{
    [SerializeField] private float dotSize = 5f;
    [SerializeField] private Color dotColor = Color.white;
    [SerializeField] private Color outlineColor = new Color(0f, 0f, 0f, 0.9f);
    [SerializeField] private float outlineExtra = 2f;

    private void Awake()
    {
        var root = new GameObject("CrosshairCanvas");
        root.transform.SetParent(transform, false);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        root.AddComponent<GraphicRaycaster>();

        Sprite pixel = CreatePixelSprite();

        CreateDot(root.transform, pixel, outlineColor, dotSize + outlineExtra, "Outline");
        CreateDot(root.transform, pixel, dotColor, dotSize, "Dot");
    }

    private static Sprite CreatePixelSprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Point;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void CreateDot(Transform parent, Sprite sprite, Color color, float size, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;

        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
    }
}
