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

    /// <summary>이 칸이 표시하는 교리. 빈 칸이면 null.</summary>
    public Doctrine Doctrine => doctrine;

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
            background.color = filled ? UiTheme.SlotFill : UiTheme.SlotFillEmpty;
            baseBackground = background.color;
        }

        if (label != null)
        {
            // OS 폰트는 런타임에 만들어져 프리팹에 저장되지 않는다.
            // 프리팹 인스턴스에서도 한글이 보이도록 매번 지정한다.
            label.font = UiTheme.GetFont();
            label.text = string.IsNullOrEmpty(displayName) ? string.Empty : displayName;
            label.color = filled ? UiTheme.TextDark : UiTheme.TextMuted;
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
        return slot;
    }
}
