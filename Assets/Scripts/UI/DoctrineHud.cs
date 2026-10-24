using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 참고 목업의 하단 플레이어 HUD.
// [원형 플레이어 초상] [교리 슬롯들] [HP 바] [오른쪽 재화 표시]
// UI는 상태를 읽어 표시만 하며, 교리/체력 규칙을 변경하지 않는다.
[DisallowMultipleComponent]
public class DoctrineHud : MonoBehaviour
{
    [Header("하단 HUD")]
    [SerializeField] private float bandHeight = 220f;
    [SerializeField] private Color bandColor = new Color(0.76f, 0.76f, 0.76f, 0.94f);

    [Header("플레이어 초상")]
    [SerializeField] private Vector2 portraitPosition = new Vector2(48f, 22f);
    [SerializeField] private float portraitDiameter = 176f;
    [Tooltip("비워두면 씬에서 PlayerHealth가 붙은 오브젝트를 자동으로 찾는다.")]
    [SerializeField] private PlayerHealth player;

    [Header("교리 슬롯")]
    [SerializeField] private Vector2 doctrinePosition = new Vector2(238f, -40f);
    [SerializeField] private Vector2 slotSize = new Vector2(88f, 108f);
    [SerializeField] private float slotSpacing = 7f;
    [Tooltip("활성 교리가 없을 때 확인용 교리 에셋을 표시한다.")]
    [SerializeField] private List<DoctrineDefinition> previewDoctrines = new List<DoctrineDefinition>();
    [SerializeField] private DoctrineSlotView slotPrefab;

    [Header("HP 바")]
    [SerializeField] private Vector2 hpPosition = new Vector2(238f, 22f);
    [SerializeField] private Vector2 hpSize = new Vector2(540f, 42f);
    [SerializeField] private Color hpBackgroundColor = new Color(0.96f, 0.96f, 0.96f, 1f);
    [SerializeField] private Color hpFillColor = new Color(0.96f, 0.48f, 0.50f, 1f);

    [Header("재화 표시")]
    [SerializeField] private Vector2 currencyMargin = new Vector2(34f, 22f);
    [SerializeField] private Vector2 currencySize = new Vector2(230f, 44f);
    [SerializeField] private string currencyLabel = "재화";

    private readonly List<DoctrineSlotView> slots = new List<DoctrineSlotView>();
    private DoctrineManager manager;
    private Canvas canvas;
    private RectTransform doctrineRow;
    private RectTransform bandRect;
    private Image portraitImage;
    private Image hpFill;
    private Text portraitFallback;
    private bool built;

    private void Start()
    {
        BuildIfNeeded();
        ResolvePlayer();
        TryBindManager();
        RebuildDoctrineSlots();
        RefreshPlayerHud();
    }

    private void OnEnable()
    {
        TryBindManager();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void Update()
    {
        if (!built)
            BuildIfNeeded();

        if (player == null)
            ResolvePlayer();

        if (manager == null)
        {
            TryBindManager();
            if (manager != null)
                RebuildDoctrineSlots();
        }

        RefreshPlayerHud();
    }

    private void ResolvePlayer()
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerHealth>();

        if (player == null)
            return;

        SpriteRenderer renderer = player.GetComponentInChildren<SpriteRenderer>();
        if (portraitImage != null && renderer != null && renderer.sprite != null)
        {
            portraitImage.sprite = renderer.sprite;
            portraitImage.color = Color.white;
            if (portraitFallback != null)
                portraitFallback.gameObject.SetActive(false);
        }
    }

    private void RefreshPlayerHud()
    {
        if (player == null)
            return;

        if (hpFill != null)
        {
            hpFill.fillAmount = player.MaxHp > 0f
                ? Mathf.Clamp01(player.CurrentHp / player.MaxHp)
                : 0f;
        }

        // SpriteRenderer may be assigned after this HUD starts.
        if (portraitImage != null && portraitImage.sprite == null)
            ResolvePlayer();
    }

    private void TryBindManager()
    {
        if (manager != null)
            return;

        manager = DoctrineManager.Instance;
        if (manager == null)
            return;

        manager.Activated += HandleActivated;
        manager.Deactivated += HandleDeactivated;
        manager.Triggered += HandleTriggered;
        manager.Cleared += HandleCleared;
    }

    private void Unbind()
    {
        if (manager == null)
            return;

        manager.Activated -= HandleActivated;
        manager.Deactivated -= HandleDeactivated;
        manager.Triggered -= HandleTriggered;
        manager.Cleared -= HandleCleared;
        manager = null;
    }

    private void HandleActivated(DoctrineManager.ActiveDoctrine entry)
    {
        RebuildDoctrineSlots();
        int index = FindSlotIndex(entry.Doctrine);
        if (index >= 0 && index < slots.Count)
            slots[index].PlayTriggerFlash(0.45f);
    }

    private void HandleDeactivated(DoctrineManager.ActiveDoctrine entry)
    {
        RebuildDoctrineSlots();
    }

    private void HandleCleared()
    {
        RebuildDoctrineSlots();
    }

    private void HandleTriggered(DoctrineManager.ActiveDoctrine entry, GameObject target)
    {
        int index = FindSlotIndex(entry.Doctrine);
        if (index >= 0 && index < slots.Count)
            slots[index].PlayTriggerFlash();
    }

    private int FindSlotIndex(Doctrine doctrine)
    {
        if (doctrine == null)
            return -1;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && slots[i].Doctrine != null &&
                slots[i].Doctrine.HasSameRule(doctrine))
                return i;
        }
        return -1;
    }

    private void RebuildDoctrineSlots()
    {
        if (!built)
            BuildIfNeeded();

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null)
                continue;
            slots[i].gameObject.SetActive(false);
            Destroy(slots[i].gameObject);
        }
        slots.Clear();

        if (doctrineRow == null)
            return;

        List<Doctrine> doctrines = CollectDoctrines();
        for (int i = 0; i < doctrines.Count; i++)
        {
            DoctrineSlotView slot = CreateSlot();
            if (slot == null)
                continue;

            // 목업의 "교리1, 교리2..." 이름과 규칙 요약을 함께 표시한다.
            string label = $"교리{i + 1}\n{DoctrineText.ShortName(doctrines[i])}";
            slot.Bind(doctrines[i], label, true);
            slots.Add(slot);
        }

        LayoutSlots();
    }

    private List<Doctrine> CollectDoctrines()
    {
        List<Doctrine> result = new List<Doctrine>();
        IReadOnlyList<DoctrineManager.ActiveDoctrine> active =
            manager != null ? manager.ActiveDoctrines : null;

        if (active != null && active.Count > 0)
        {
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i] != null && active[i].Doctrine != null)
                    result.Add(active[i].Doctrine);
            }
            return result;
        }

        for (int i = 0; i < previewDoctrines.Count; i++)
        {
            if (previewDoctrines[i] != null)
                result.Add(previewDoctrines[i].ToRuntime());
        }

        return result;
    }

    private DoctrineSlotView CreateSlot()
    {
        DoctrineSlotView slot = slotPrefab != null
            ? Instantiate(slotPrefab, doctrineRow, false)
            : DoctrineSlotView.BuildRuntime(doctrineRow);
        slot.name = "DoctrineSlot";
        slot.gameObject.SetActive(true);
        return slot;
    }

    private void LayoutSlots()
    {
        float step = slotSize.x + slotSpacing;
        for (int i = 0; i < slots.Count; i++)
        {
            RectTransform rect = slots[i].GetComponent<RectTransform>();
            rect.SetParent(doctrineRow, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = slotSize;
            rect.anchoredPosition = new Vector2(i * step, 0f);
        }

        float width = slots.Count == 0 ? 0f : slots.Count * slotSize.x + (slots.Count - 1) * slotSpacing;
        doctrineRow.sizeDelta = new Vector2(width, slotSize.y);
    }

    private void BuildIfNeeded()
    {
        if (built)
            return;
        built = true;

        canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("PlayerHudCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        BuildBottomBand();
        BuildPortrait();
        BuildDoctrineRow();
        BuildHpBar();
        BuildCurrencyBadge();
    }

    private void BuildBottomBand()
    {
        GameObject band = UiTheme.CreateImage("PlayerHudBand", canvas.transform, bandColor);
        bandRect = band.GetComponent<RectTransform>();
        bandRect.anchorMin = new Vector2(0f, 0f);
        bandRect.anchorMax = new Vector2(1f, 0f);
        bandRect.pivot = new Vector2(0.5f, 0f);
        bandRect.anchoredPosition = Vector2.zero;
        bandRect.sizeDelta = new Vector2(0f, bandHeight);
    }

    private void BuildPortrait()
    {
        GameObject avatar = UiTheme.CreateImage("PlayerPortraitCircle", bandRect, UiTheme.SubjectPlayer);
        RectTransform avatarRect = avatar.GetComponent<RectTransform>();
        avatarRect.anchorMin = new Vector2(0f, 0f);
        avatarRect.anchorMax = new Vector2(0f, 0f);
        avatarRect.pivot = new Vector2(0f, 0f);
        avatarRect.anchoredPosition = portraitPosition;
        avatarRect.sizeDelta = new Vector2(portraitDiameter, portraitDiameter);
        avatar.GetComponent<Image>().sprite = UiTheme.CircleSprite;

        GameObject maskObject = new GameObject("PortraitMask", typeof(RectTransform), typeof(Image), typeof(Mask));
        maskObject.transform.SetParent(avatar.transform, false);
        RectTransform maskRect = maskObject.GetComponent<RectTransform>();
        UiTheme.Stretch(maskRect, 8f);
        Image maskImage = maskObject.GetComponent<Image>();
        maskImage.sprite = UiTheme.CircleSprite;
        maskImage.color = Color.white;
        maskObject.GetComponent<Mask>().showMaskGraphic = false;

        GameObject portrait = new GameObject("PortraitSprite", typeof(RectTransform), typeof(Image));
        portrait.transform.SetParent(maskObject.transform, false);
        RectTransform portraitRect = portrait.GetComponent<RectTransform>();
        UiTheme.Stretch(portraitRect);
        portraitImage = portrait.GetComponent<Image>();
        portraitImage.preserveAspect = true;
        portraitImage.color = Color.white;

        portraitFallback = UiTheme.CreateText("PlayerFallback", avatar.transform, "플레이어", 16,
            UiTheme.TextDark, TextAnchor.MiddleCenter);
        UiTheme.Stretch(portraitFallback.rectTransform, 8f);
    }

    private void BuildDoctrineRow()
    {
        GameObject row = new GameObject("DoctrineRow", typeof(RectTransform));
        row.transform.SetParent(bandRect, false);
        doctrineRow = row.GetComponent<RectTransform>();
        doctrineRow.anchorMin = new Vector2(0f, 1f);
        doctrineRow.anchorMax = new Vector2(0f, 1f);
        doctrineRow.pivot = new Vector2(0f, 1f);
        doctrineRow.anchoredPosition = doctrinePosition;
        doctrineRow.sizeDelta = new Vector2(slotSize.x, slotSize.y);
    }

    private void BuildHpBar()
    {
        GameObject background = UiTheme.CreateImage("HpBarBackground", bandRect, hpBackgroundColor);
        RectTransform rect = background.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = hpPosition;
        rect.sizeDelta = hpSize;

        GameObject fill = UiTheme.CreateImage("HpBarFill", background.transform, hpFillColor);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        hpFill = fill.GetComponent<Image>();
        hpFill.type = Image.Type.Filled;
        hpFill.fillMethod = Image.FillMethod.Horizontal;
        hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        hpFill.fillAmount = 1f;
    }

    private void BuildCurrencyBadge()
    {
        GameObject badge = UiTheme.CreateImage("CurrencyBadge", bandRect,
            new Color(0.57f, 0.57f, 0.57f, 1f));
        RectTransform rect = badge.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-currencyMargin.x, currencyMargin.y);
        rect.sizeDelta = currencySize;

        UiTheme.CreateText("CurrencyLabel", badge.transform, currencyLabel, 18,
            Color.white, TextAnchor.MiddleCenter);
        RectTransform textRect = badge.transform.Find("CurrencyLabel").GetComponent<RectTransform>();
        UiTheme.Stretch(textRect);
    }
}
