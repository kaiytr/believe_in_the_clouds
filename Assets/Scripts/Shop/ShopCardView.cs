using System;
using UnityEngine;
using UnityEngine.UI;

// 상점 카드 1장.
// 사진의 카드 구성과 같다: 왼쪽 회색 블록(아이콘 자리) + 오른쪽 글 영역.
// 선택되면 ① 테두리 두껍게 ② 아이콘 위치에 체크 ③ 배경 붉은 톤
// 세 가지가 동시에 바뀌어 선택 여부가 한눈에 보이게 한다.
public class ShopCardView : MonoBehaviour
{
    private static readonly Color CardNormal = new Color(0.93f, 0.93f, 0.93f, 1f);
    private static readonly Color CardHover = new Color(1f, 1f, 1f, 1f);
    private static readonly Color CardSelected = new Color(0.98f, 0.80f, 0.80f, 1f);
    private static readonly Color CardSelectedHover = new Color(1f, 0.86f, 0.86f, 1f);
    private static readonly Color IconNormal = new Color(0.60f, 0.60f, 0.60f, 1f);
    private static readonly Color IconSelected = new Color(0.78f, 0.36f, 0.36f, 1f);
    private static readonly Color BorderNormal = new Color(0.80f, 0.80f, 0.80f, 1f);
    private static readonly Color BorderSelected = new Color(0.78f, 0.36f, 0.36f, 1f);
    private const float BorderThickness = 3f;

    private Image background;
    private Image icon;
    private Image[] borderStrips;
    private Text checkmark;
    private Text label;
    private Button button;
    private bool selected;

    public ShopEntry Entry { get; private set; }
    public bool IsSelected => selected;

    /// <summary>카드를 눌렀을 때. (이 카드)</summary>
    public event Action<ShopCardView> Clicked;

    public static ShopCardView BuildRuntime(Transform parent, ShopEntry entry)
    {
        GameObject card = UiTheme.CreateImage("Card_" + entry.Label, parent, Color.white);
        Image background = card.GetComponent<Image>();
        background.raycastTarget = true;

        // 선택 테두리: 카드 가장자리에 3px 프레임 4개. 카드 위(마지막 자식)에 그린다.
        GameObject borderRoot = new GameObject("Border", typeof(RectTransform));
        borderRoot.transform.SetParent(card.transform, false);
        UiTheme.Stretch(borderRoot.GetComponent<RectTransform>());

        Image[] borderStrips =
        {
            CreateBorderStrip(borderRoot.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -BorderThickness), Vector2.zero),
            CreateBorderStrip(borderRoot.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, BorderThickness)),
            CreateBorderStrip(borderRoot.transform, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(BorderThickness, 0f)),
            CreateBorderStrip(borderRoot.transform, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-BorderThickness, 0f), Vector2.zero)
        };

        // 사진과 같은 비율: 왼쪽 블록 ≈ 45%, 오른쪽 글 영역 ≈ 55%.
        GameObject inner = new GameObject("Inner", typeof(RectTransform));
        inner.transform.SetParent(card.transform, false);
        UiTheme.Stretch(inner.GetComponent<RectTransform>(), 3f);

        HorizontalLayoutGroup layout = inner.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 8;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.MiddleLeft;

        GameObject iconObject = UiTheme.CreateImage("Icon", inner.transform, IconNormal);
        iconObject.GetComponent<Image>().raycastTarget = false;
        LayoutElement iconLayout = iconObject.AddComponent<LayoutElement>();
        iconLayout.flexibleWidth = 1f;
        iconLayout.minWidth = 40f;

        // 아이콘 위에 얹는 체크 표시. 선택 전에는 숨긴다.
        Text checkmark = UiTheme.CreateText("Check", iconObject.transform, "✓", 30,
            Color.white, TextAnchor.MiddleCenter);
        checkmark.raycastTarget = false;
        UiTheme.Stretch(checkmark.rectTransform);
        checkmark.gameObject.SetActive(false);

        Text label = UiTheme.CreateText("Label", inner.transform, entry.Label, 17,
            UiTheme.TextDark, TextAnchor.MiddleLeft);
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.lineSpacing = 1.05f;
        LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
        labelLayout.flexibleWidth = 1.15f;
        labelLayout.minWidth = 70f;

        Button button = card.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        ShopCardView view = card.AddComponent<ShopCardView>();
        view.Entry = entry;
        view.background = background;
        view.icon = iconObject.GetComponent<Image>();
        view.borderStrips = borderStrips;
        view.checkmark = checkmark;
        view.button = button;
        view.label = label;

        button.onClick.AddListener(() => view.Clicked?.Invoke(view));
        view.ApplyLabel();
        view.SetSelected(false);
        return view;
    }

    public void SetSelected(bool value)
    {
        selected = value;

        ColorBlock colors = button.colors;
        colors.normalColor = value ? CardSelected : CardNormal;
        colors.highlightedColor = value ? CardSelectedHover : CardHover;
        colors.selectedColor = value ? CardSelected : CardNormal;
        colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        colors.disabledColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.05f;
        button.colors = colors;

        if (background != null)
        {
            background.color = Color.white;
        }

        if (borderStrips != null)
        {
            Color borderColor = value ? BorderSelected : BorderNormal;
            for (int i = 0; i < borderStrips.Length; i++)
            {
                if (borderStrips[i] != null)
                {
                    borderStrips[i].color = borderColor;
                }
            }
        }

        if (icon != null)
        {
            icon.color = value ? IconSelected : IconNormal;
        }

        if (checkmark != null)
        {
            checkmark.gameObject.SetActive(value);
        }

        if (label != null)
        {
            label.fontStyle = value ? FontStyle.Bold : FontStyle.Normal;
        }
    }

    private static Image CreateBorderStrip(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject strip = UiTheme.CreateImage(name, parent, BorderNormal);
        strip.GetComponent<Image>().raycastTarget = false;
        RectTransform rect = strip.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return strip.GetComponent<Image>();
    }

    private void ApplyLabel()
    {
        if (label == null || Entry == null)
        {
            return;
        }

        // 큰 글씨 = 이름, 작은 글씨 = 표 문장 조각.
        label.text = string.IsNullOrEmpty(Entry.Detail)
            ? Entry.Label
            : $"{Entry.Label}\n<size=11><color=#5A5C60>{Entry.Detail}</color></size>";
    }
}
