using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Crosshair : MonoBehaviour
{
    [SerializeField] private float dotSize = 6f;
    [SerializeField] private Color dotColor = Color.white;
    [SerializeField] private Color outlineColor = new Color(0f, 0f, 0f, 0.9f);
    [SerializeField] private float outlineExtra = 2f;

    [Header("Rocket launcher")]
    [SerializeField] private RocketLauncher rocketLauncher;
    [SerializeField] private float ringOuterSize = 22f;
    [SerializeField] private Color loadingRingColor = new Color(1f, 0.92f, 0.15f, 0.95f);
    [SerializeField] private Color fullReloadRingColor = new Color(1f, 0.2f, 0.18f, 0.95f);
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private string ammoTextFormat = "{0} / {1}";
    [SerializeField] private string reloadStatusText = "...";

    [Header("Sprint & stamina (horizontal line)")]
    [SerializeField] private PlayerMotor playerMotor;
    [Tooltip("Extra width while sprinting (multiplies stamina-based length).")]
    [SerializeField] private float sprintDotWidthMul = 2.25f;
    [Tooltip("Line length at 0 stamina (fraction of base width).")]
    [SerializeField] private float minStaminaLineWidthFrac = 0.28f;
    [Tooltip("Seconds to ease width changes (SmoothDamp).")]
    [SerializeField] private float dotWidthSmoothTime = 0.16f;

    Image loadingFillImage;
    Image reloadFillImage;
    RectTransform outlineRt;
    RectTransform dotRt;
    float baseOutlineW;
    float baseOutlineH;
    float baseDotW;
    float baseDotH;
    float dotWidthScale = 1f;
    float dotWidthScaleVel;

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
        Sprite ring = CreateRingSprite();

        loadingFillImage = CreateRing(root.transform, ring, ringOuterSize, loadingRingColor, "LoadingRing");
        reloadFillImage = CreateRing(root.transform, ring, ringOuterSize, fullReloadRingColor, "ReloadRing");
        loadingFillImage.transform.SetAsFirstSibling();

        Image outlineImg = CreateDot(root.transform, pixel, outlineColor, dotSize + outlineExtra, "Outline");
        Image dotImg = CreateDot(root.transform, pixel, dotColor, dotSize, "Dot");

        outlineRt = outlineImg.rectTransform;
        dotRt = dotImg.rectTransform;
        baseOutlineW = outlineRt.sizeDelta.x;
        baseOutlineH = outlineRt.sizeDelta.y;
        baseDotW = dotRt.sizeDelta.x;
        baseDotH = dotRt.sizeDelta.y;
    }

    void Start()
    {
        if (rocketLauncher == null)
            rocketLauncher = GetComponentInChildren<RocketLauncher>(true);
        if (rocketLauncher == null)
            rocketLauncher = FindFirstObjectByType<RocketLauncher>();
        if (playerMotor == null)
            playerMotor = GetComponent<PlayerMotor>();
    }

    void Update()
    {
        if (playerMotor != null)
        {
            float staminaLine = Mathf.Lerp(minStaminaLineWidthFrac, 1f, playerMotor.StaminaNormalized);
            float sprintBoost = playerMotor.IsSprintingBoostActive ? sprintDotWidthMul : 1f;
            float targetScale = staminaLine * sprintBoost;
            float smooth = Mathf.Max(0.01f, dotWidthSmoothTime);
            dotWidthScale = Mathf.SmoothDamp(dotWidthScale, targetScale, ref dotWidthScaleVel, smooth);

            outlineRt.sizeDelta = new Vector2(baseOutlineW * dotWidthScale, baseOutlineH);
            dotRt.sizeDelta = new Vector2(baseDotW * dotWidthScale, baseDotH);
        }

        if (rocketLauncher != null)
        {
            if (ammoText != null)
            {
                if (rocketLauncher.IsFullReloading)
                    ammoText.text = string.IsNullOrEmpty(reloadStatusText) ? "..." : reloadStatusText;
                else
                {
                    string fmt = string.IsNullOrEmpty(ammoTextFormat) ? "{0} / {1}" : ammoTextFormat;
                    ammoText.text = string.Format(fmt, rocketLauncher.CurrentAmmo, rocketLauncher.MaxAmmo);
                }
            }

            if (loadingFillImage != null && reloadFillImage != null)
            {
                if (rocketLauncher.IsFullReloading)
                {
                    loadingFillImage.enabled = false;
                    reloadFillImage.enabled = true;
                    reloadFillImage.fillAmount = rocketLauncher.FullReloadProgress01;
                }
                else if (rocketLauncher.IsLoadingCycle)
                {
                    reloadFillImage.enabled = false;
                    loadingFillImage.enabled = true;
                    loadingFillImage.fillAmount = rocketLauncher.LoadingProgress01;
                }
                else
                {
                    loadingFillImage.enabled = false;
                    reloadFillImage.enabled = false;
                }
            }
        }
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

    static Image CreateRing(Transform parent, Sprite ringSprite, float outerSize, Color color, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.sprite = ringSprite;
        img.color = color;
        img.raycastTarget = false;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillOrigin = (int)Image.Origin360.Top;
        img.fillClockwise = true;
        img.fillAmount = 1f;

        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(outerSize, outerSize);

        return img;
    }

    private static Image CreateDot(Transform parent, Sprite sprite, Color color, float size, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;

        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);

        return img;
    }

    static Sprite CreateRingSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float cx = size * 0.5f;
        float cy = size * 0.5f;
        float rOuter = size * 0.48f;
        float rInner = size * 0.32f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx, dy = y - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = d >= rInner && d <= rOuter ? 1f : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
