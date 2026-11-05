using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 교리 슬롯 1칸.
// 참고 목업처럼 정사각형에 가까운 작은 타일이고, 칸 안에 교리 이름이 들어간다.
// 발동 시 타일이 밝게 번쩍인다.
public class DoctrineSlotView : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Image triggerFlash;
    [SerializeField] private Text label;

    private Doctrine doctrine;
    private Coroutine flashRoutine;
    private Color baseBackground;
    private Button button;

    // 교리 목록 두루마리 아트 (Design/Doctrine/교리 1.png → Resources/UI/Doctrine/DoctrineSlot).
    // Import 설정과 무관하게 Sprite/Texture2D 둘 다 시도한다.
    private static Sprite slotSprite;
    private static bool slotSpriteLoaded;

    private static Sprite LoadSlotSprite()
    {
        if (slotSpriteLoaded)
        {
            return slotSprite;
        }

        slotSpriteLoaded = true;
        slotSprite = Resources.Load<Sprite>("UI/Doctrine/DoctrineSlot");
        if (slotSprite == null)
        {
            Texture2D texture = Resources.Load<Texture2D>("UI/Doctrine/DoctrineSlot");
            if (texture != null)
            {
                slotSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                slotSprite.name = "DoctrineSlot";
            }
        }

        return slotSprite;
    }

    /// <summary>이 칸이 표시하는 교리. 빈 칸이면 null.</summary>
    public Doctrine Doctrine => doctrine;

    /// <summary>슬롯 타일을 눌렀을 때. HUD 툴팁 표시용.</summary>
    public event Action<DoctrineSlotView> Clicked;

    /// <summary>
    /// 칸을 초기화한다.
    /// displayName은 칸 안에 표시할 짧은 이름, filled는 장착된 교리인지 여부.
    /// </summary>
    public void Bind(Doctrine target, string displayName, bool filled)
    {
        doctrine = target;
        ResolveReferences();

        if (background != null)
        {
            Sprite art = LoadSlotSprite();
            if (art != null)
            {
                background.sprite = art;
                background.type = Image.Type.Simple;
                background.preserveAspect = false;
                // 목업처럼 칸 3개가 모두 같은 두루마리로 보이게 한다 (빈 칸은 글자만 없다).
                background.color = Color.white;
            }
            else
            {
                background.color = filled ? UiTheme.SlotFill : UiTheme.SlotFillEmpty;
            }

            baseBackground = background.color;
        }

        if (label != null)
        {
            // 두루마리 아트를 쓰면 칸 안 글자는 표시하지 않는다 (목업처럼 그림만).
            // 툴팁은 클릭 시 그대로 뜬다.
            bool hasArt = LoadSlotSprite() != null;
            label.font = UiTheme.GetFont();
            label.text = hasArt || string.IsNullOrEmpty(displayName) ? string.Empty : displayName;
            label.color = filled ? UiTheme.TextDark : UiTheme.TextMuted;
            label.gameObject.SetActive(!hasArt && !string.IsNullOrEmpty(displayName));

            // 두루마리 아트는 위쪽에 감긴 막대가 있어 글자를 그 아래 양피지 안쪽에 둔다.
            if (hasArt && label.rectTransform != null)
            {
                label.fontSize = 16;
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(8f, 10f);
                labelRect.offsetMax = new Vector2(-8f, -28f);
            }
        }

        if (triggerFlash != null)
        {
            Color c = triggerFlash.color;
            c.a = 0f;
            triggerFlash.color = c;
            triggerFlash.raycastTarget = false;
        }

        // 이전 반짝임이 남아 있으면 정리한다.
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        EnsureClickable();
    }

    // 하단 HUD 슬롯에 클릭이 붙어 있지 않아 툴팁이 뜨지 않았다.
    // 배경 이미지를 버튼 타깃으로 삼아 클릭을 받는다.
    private void EnsureClickable()
    {
        ResolveReferences();

        if (button == null)
        {
            button = GetComponent<Button>();
            if (button == null)
            {
                button = gameObject.AddComponent<Button>();
            }
        }

        if (button != null)
        {
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.RemoveListener(HandleButtonClicked);
            button.onClick.AddListener(HandleButtonClicked);
            button.interactable = true;
            if (background != null)
            {
                button.targetGraphic = background;
                background.raycastTarget = true;
            }
        }
    }

    private void HandleButtonClicked()
    {
        Clicked?.Invoke(this);
    }

    private void ResolveReferences()
    {
        if (background == null)
            background = FindImage("Background");
        if (triggerFlash == null)
            triggerFlash = FindImage("TriggerFlash");
        if (label == null)
        {
            Transform child = transform.Find("Label");
            if (child != null)
                label = child.GetComponent<Text>();
        }
    }

    private Image FindImage(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    /// <summary>발동 순간 반짝임.</summary>
    public void PlayTriggerFlash(float duration = 0.35f)
    {
        if (triggerFlash == null)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(duration));
    }

    private IEnumerator FlashRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));

            // 처음엔 강하게, 끝으로 갈수록 사라진다.
            Color flash = Color.white;
            flash.a = (1f - t) * 0.75f;
            triggerFlash.color = flash;

            yield return null;
        }

        Color end = triggerFlash.color;
        end.a = 0f;
        triggerFlash.color = end;
        flashRoutine = null;
    }

    private void OnDisable()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }
        if (triggerFlash != null)
        {
            Color c = triggerFlash.color;
            c.a = 0f;
            triggerFlash.color = c;
        }
    }

    /// <summary>
    /// 슬롯 타일 계층을 만든다.
    /// HUD의 런타임 생성과 에디터 프리팹 생성이 같은 코드를 쓰도록 여기 한 곳에 둔다.
    /// </summary>
    public static DoctrineSlotView BuildRuntime(Transform parent)
    {
        GameObject slotObject = new GameObject("DoctrineSlot", typeof(RectTransform));
        slotObject.transform.SetParent(parent, false);

        GameObject bgObject = UiTheme.CreateImage("Background", slotObject.transform, UiTheme.SlotFill);
        UiTheme.Stretch(bgObject.GetComponent<RectTransform>());

        Text label = UiTheme.CreateText("Label", slotObject.transform, string.Empty,
            14, UiTheme.TextDark, TextAnchor.MiddleCenter);
        UiTheme.Stretch(label.rectTransform, 3f);

        // 반짝임은 맨 위에 덮는다. 레이아웃에는 영향이 없다.
        GameObject flashObject = UiTheme.CreateImage("TriggerFlash", slotObject.transform,
            new Color(1f, 1f, 1f, 0f));
        UiTheme.Stretch(flashObject.GetComponent<RectTransform>());

        DoctrineSlotView slot = slotObject.AddComponent<DoctrineSlotView>();
        slot.background = bgObject.GetComponent<Image>();
        slot.label = label;
        slot.triggerFlash = flashObject.GetComponent<Image>();
        slot.EnsureClickable();
        return slot;
    }
}
