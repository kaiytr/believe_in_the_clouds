using UnityEngine;
using UnityEngine.UI;

// uGUI 공통 스타일/헬퍼.
// 기존 BeliefView와 같은 방식으로 런타임에 OS 한글 폰트를 찾아 쓰므로
// 폰트 에셋을 만들 필요가 없다.
public static class UiTheme
{
    // ---- 색상 팔레트 (참고 목업의 하단 HUD 톤) ----
    public static readonly Color SlotFill = new Color(0.94f, 0.66f, 0.66f, 1f);
    public static readonly Color SlotFillEmpty = new Color(0.85f, 0.85f, 0.85f, 1f);
    public static readonly Color SlotHover = new Color(0.97f, 0.76f, 0.76f, 1f);
    public static readonly Color TextDark = new Color(0.10f, 0.11f, 0.13f, 1f);
    public static readonly Color TextMuted = new Color(0.40f, 0.42f, 0.45f, 1f);
    public static readonly Color BandBackground = new Color(0.72f, 0.72f, 0.72f, 0.85f);

    // 주어(Subject)별 강조색
    public static readonly Color SubjectPlayer = new Color(0.78f, 0.36f, 0.36f, 1f);
    public static readonly Color SubjectEnemy = new Color(0.85f, 0.60f, 0.30f, 1f);
    public static readonly Color SubjectBoss = new Color(0.52f, 0.30f, 0.68f, 1f);
    public static readonly Color SubjectAll = new Color(0.45f, 0.45f, 0.45f, 1f);

    private const int FontSize = 18;

    private static Font cachedFont;
    private static Sprite cachedWhiteSprite;

    public static Sprite WhiteSprite
    {
        get
        {
            if (cachedWhiteSprite == null)
            {
                cachedWhiteSprite = Sprite.Create(
                    Texture2D.whiteTexture,
                    new Rect(0f, 0f, 1f, 1f),
                    new Vector2(0.5f, 0.5f));
            }
            return cachedWhiteSprite;
        }
    }

    private static Sprite cachedCircleSprite;

    // Portrait 배경과 Mask 양쪽에 재사용하는 알파 원형 스프라이트.
    public static Sprite CircleSprite
    {
        get
        {
            if (cachedCircleSprite != null)
                return cachedCircleSprite;

            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "RuntimeCircleSprite";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color32[] pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01(radius + 0.75f - distance);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            cachedCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
            cachedCircleSprite.name = "RuntimeCircleSprite";
            return cachedCircleSprite;
        }
    }

    // 한글 폰트. OS에 있는 폰트를 우선 사용하고, 없으면 내장 폰트로 떨어진다.
    public static Font GetFont()
    {
        if (cachedFont != null)
            return cachedFont;

        string[] installedFonts = Font.GetOSInstalledFontNames();
        string[] preferredFonts = { "Malgun Gothic", "맑은 고딕", "Noto Sans CJK KR", "Arial" };
        foreach (string preferredFont in preferredFonts)
        {
            if (System.Array.IndexOf(installedFonts, preferredFont) >= 0)
            {
                Font dynamicFont = Font.CreateDynamicFontFromOSFont(preferredFont, FontSize);
                if (dynamicFont != null)
                {
                    cachedFont = dynamicFont;
                    return cachedFont;
                }
            }
        }

        cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (cachedFont == null)
            cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return cachedFont;
    }

    public static Color GetSubjectColor(SubjectType subject)
    {
        switch (subject)
        {
            case SubjectType.Player: return SubjectPlayer;
            case SubjectType.Enemy: return SubjectEnemy;
            case SubjectType.Boss: return SubjectBoss;
            default: return SubjectAll;
        }
    }

    public static GameObject CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = WhiteSprite;
        image.color = color;
        image.raycastTarget = false;
        return go;
    }

    public static Text CreateText(string objectName, Transform parent, string content,
        int fontSize, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = GetFont();
        text.fontSize = fontSize;
        text.text = content;
        text.color = color;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    public static void Stretch(RectTransform rect, float padding = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }
}
