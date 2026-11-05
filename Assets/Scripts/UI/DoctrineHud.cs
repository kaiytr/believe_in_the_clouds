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

    [Tooltip("비워두면 씬에서 PlayerHealth가 붙은 오브젝트를 자동으로 찾는다.")]
    [SerializeField] private PlayerHealth player;

    [Header("교리 슬롯")]
    [SerializeField] private Vector2 doctrinePosition = new Vector2(252f, 6f);
    [SerializeField] private Vector2 slotSize = new Vector2(118f, 130f);
    [SerializeField] private float slotSpacing = 20f;
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
    private Text currencyText;
    private RectTransform currencyTarget;
    private float currencyFlashUntil;
    public static RectTransform CurrencyTarget => instance != null ? instance.currencyTarget : null;

    private readonly List<DoctrineSlotView> slots = new List<DoctrineSlotView>();
    private DoctrineManager manager;
    private PlayerHealth resolvedPlayer;
    private Canvas canvas;
    private RectTransform doctrineRow;
    private RectTransform bandRect;
    private Image hpFill;
    private bool built;

    [Header("교리 효과 툴팁 (좌측 상단)")]
    [SerializeField] private float tooltipSeconds = 3f;
    [SerializeField] private Vector2 tooltipMargin = new Vector2(0f, -30f);
    [Tooltip("Doctrine_BG 아트의 원본 크기(913x186). 스킨을 못 찾으면 이 크기의 단색 배경으로 대체한다.")]
    [SerializeField] private Vector2 tooltipSize = new Vector2(913f, 186f);
    [SerializeField] private Color tooltipBackground = new Color(0.95f, 0.86f, 0.73f, 1f);
    [SerializeField] private Color tooltipTextColor = Color.black;

    private GameObject tooltipObject;
    private Text tooltipTitle;
    private Text tooltipBody;
    private Coroutine tooltipRoutine;

    // 툴팁 배경 아트(Assets/Resources/UI/Doctrine_BG). Import 설정과 무관하게 Sprite/Texture2D 둘 다 시도한다.
    private static Sprite tooltipSprite;

    private static Sprite LoadTooltipSprite()
    {
        if (tooltipSprite != null)
        {
            return tooltipSprite;
        }

        tooltipSprite = Resources.Load<Sprite>("UI/Doctrine_BG");
        if (tooltipSprite == null)
        {
            Texture2D texture = Resources.Load<Texture2D>("UI/Doctrine_BG");
            if (texture != null)
            {
                tooltipSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                tooltipSprite.name = "Doctrine_BG";
            }
        }

        return tooltipSprite;
    }

    private static DoctrineHud instance;

    // 씬 전환 뒤 PlayerHealth 참조를 다시 찾는 간격.
    private float nextPlayerSearchTime;

    [Header("확인용 (자동 표시)")]
    [Tooltip("활성 교리도, 연결된 확인용 목록도 없을 때 내장된 예시 3개를 보여준다. 끄면 비어 있을 때 아무 칸도 그리지 않는다.")]
    [SerializeField] private bool useBuiltInPreview = false;

    private void Awake()
    {
        // 자동 부팅으로 만들어진 HUD와 씬에 직접 둔 HUD가 겹치면 하나만 남긴다.
        // (도메인 리로드를 끈 상태에서는 파괴된 객체가 static에 남을 수 있어 null 검사를 먼저 한다.)
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        // New profiles must display their actual empty equipment state.
        useBuiltInPreview = false;
        previewDoctrines.Clear();
        DontDestroyOnLoad(gameObject);
    }

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
        Currency.Changed += HandleCurrencyChanged;
        TryBindManager();
        UiEventSystemBoot.Ensure();
    }

    private void OnDisable()
    {
        Currency.Changed -= HandleCurrencyChanged;
        Unbind();
    }

    private void OnDestroy()
    {
        Currency.Changed -= HandleCurrencyChanged;
        if (instance == this) instance = null;
        Unbind();
    }

    private void Update()
    {
        if (!built)
            BuildIfNeeded();

        // 씬이 바뀌면 플레이어/초상/매니저 참조가 끊긴다. 0.5초 간격으로 다시 찾는다.
        if (Time.unscaledTime >= nextPlayerSearchTime && player == null)
        {
            nextPlayerSearchTime = Time.unscaledTime + 0.5f;
            ResolvePlayer();
        }

        if (manager == null)
        {
            TryBindManager();
            if (manager != null)
                RebuildDoctrineSlots();
        }

        RefreshPlayerHud();
        RefreshCurrency();
    }

    private void HandleCurrencyChanged(CurrencyChange change)
    {
        if (change.ActualDelta > 0) currencyFlashUntil = Time.unscaledTime + .3f;
        RefreshCurrency();
    }

    private void RefreshCurrency()
    {
        if (currencyText == null) return;
        PositionCurrencyBadge();
        currencyText.text = Currency.IsReady
            ? (Currency.Balance < 100 ? Currency.Balance.ToString("D2") : Currency.Balance.ToString("N0"))
            : "--";
        currencyText.color = Time.unscaledTime < currencyFlashUntil
            ? new Color(1f, .72f, .22f)
            : new Color(0.30f, 0.22f, 0.15f);
    }

    private void ResolvePlayer()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerHealth>();
        }

        if (player == null)
            return;

        // 플레이어를 늦게 찾은 경우(씬 전환 등) 오너 기준 표시가 달라질 수 있으니 슬롯을 다시 그린다.
        if (resolvedPlayer != player)
        {
            resolvedPlayer = player;
            RebuildDoctrineSlots();
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
    }

    private void PositionCurrencyBadge()
    {
        if (currencyTarget == null)
            return;

        currencyTarget.anchorMin = currencyTarget.anchorMax = new Vector2(1f, 0f);
        currencyTarget.pivot = new Vector2(1f, 0f);
        currencyTarget.anchoredPosition = new Vector2(-12f, 42f);
        currencyTarget.localScale = Vector3.one;
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
            Doctrine doctrine = doctrines[i];
            DoctrineSlotView slot = CreateSlot();
            if (slot == null)
                continue;

            // 목업의 "교리1, 교리2..." 이름과 규칙 요약을 함께 표시한다.
            string label = $"교리{i + 1}\n{DoctrineText.ShortName(doctrine)}";
            slot.Bind(doctrine, label, true);
            slot.Clicked -= HandleSlotClicked;
            slot.Clicked += HandleSlotClicked;
            slots.Add(slot);
        }

        LayoutSlots();
    }

    // 슬롯을 누르면 좌측 상단에 효과 텍스트를 3초간 보여준다.
    private void HandleSlotClicked(DoctrineSlotView slot)
    {
        if (slot == null || slot.Doctrine == null)
        {
            ShowTooltip("빈 교리", "이 슬롯에는 장착된 교리가 없습니다.", 0);
            return;
        }

        int index = slots.IndexOf(slot);
        string title = index >= 0 ? $"교리{index + 1} · {DoctrineText.ShortName(slot.Doctrine)}" : DoctrineText.ShortName(slot.Doctrine);
        ShowTooltip(title, DoctrineText.Describe(slot.Doctrine), index);
    }

    private void ShowTooltip(string title, string body, int slotIndex)
    {
        BuildTooltipIfNeeded();
        UiEventSystemBoot.Ensure();

        if (tooltipObject != null)
        {
            tooltipObject.SetActive(true);
            CanvasGroup group = tooltipObject.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
            }
        }

        if (tooltipTitle != null)
        {
            tooltipTitle.font = UiTheme.GetFont();
            tooltipTitle.text = title ?? string.Empty;
        }

        if (tooltipBody != null)
        {
            tooltipBody.font = UiTheme.GetFont();
            tooltipBody.text = body ?? string.Empty;
        }

        Debug.Log($"[교리툴팁] 슬롯 {slotIndex + 1} 선택 → {title} / {body}");

        if (tooltipRoutine != null)
        {
            StopCoroutine(tooltipRoutine);
        }

        tooltipRoutine = StartCoroutine(HideTooltipAfterDelay(Mathf.Max(0.5f, tooltipSeconds)));
    }

    private System.Collections.IEnumerator HideTooltipAfterDelay(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);

        // 자연스럽게 사라지도록 마지막 0.5초는 페이드한다.
        const float fadeSeconds = 0.5f;
        CanvasGroup group = tooltipObject != null ? tooltipObject.GetComponent<CanvasGroup>() : null;
        if (group != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01(1f - elapsed / fadeSeconds);
                yield return null;
            }
        }

        if (tooltipObject != null)
        {
            tooltipObject.SetActive(false);
            if (group != null)
            {
                group.alpha = 1f;
            }
        }

        tooltipRoutine = null;
    }

    private void BuildTooltipIfNeeded()
    {
        if (!built)
        {
            BuildIfNeeded();
        }

        if (tooltipObject != null || canvas == null)
        {
            return;
        }

        GameObject root = new GameObject("DoctrineTooltip", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(canvas.transform, false);
        Canvas tooltipCanvas = root.AddComponent<Canvas>();
        tooltipCanvas.overrideSorting = true;
        tooltipCanvas.sortingOrder = 160; // Above HUD/test panels/coin flights, below shop and foundation modals.
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = tooltipMargin;
        rect.sizeDelta = tooltipSize;

        GameObject background = UiTheme.CreateImage("TooltipBackground", root.transform, tooltipBackground);
        UiTheme.Stretch(background.GetComponent<RectTransform>());
        Image backgroundImage = background.GetComponent<Image>();
        backgroundImage.raycastTarget = false;

        Sprite art = LoadTooltipSprite();
        if (art != null)
        {
            backgroundImage.sprite = art;
            backgroundImage.color = Color.white;
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.preserveAspect = false;
            rect.sizeDelta = new Vector2(art.rect.width, art.rect.height);
        }

        GameObject texts = new GameObject("TooltipTexts", typeof(RectTransform));
        texts.transform.SetParent(root.transform, false);
        RectTransform textsRect = texts.GetComponent<RectTransform>();
        textsRect.anchorMin = Vector2.zero;
        textsRect.anchorMax = Vector2.one;
        // 아트 안쪽 베이지 판(위 18px·아래 18px는 투명 여백)에 맞춘 여백.
        textsRect.offsetMin = new Vector2(52f, 40f);
        textsRect.offsetMax = new Vector2(-56f, -34f);

        // 제목/본문을 왼쪽 정렬한 채 두루마리 안쪽에서 세로 중앙에 묶는다.
        VerticalLayoutGroup layout = texts.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        tooltipTitle = UiTheme.CreateText("TooltipTitle", texts.transform, string.Empty,
            24, tooltipTextColor, TextAnchor.UpperLeft);
        tooltipTitle.horizontalOverflow = HorizontalWrapMode.Overflow;

        tooltipBody = UiTheme.CreateText("TooltipBody", texts.transform, string.Empty,
            20, tooltipTextColor, TextAnchor.UpperLeft);
        tooltipBody.horizontalOverflow = HorizontalWrapMode.Wrap;

        root.SetActive(false);
        tooltipObject = root;
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
                DoctrineManager.ActiveDoctrine entry = active[i];
                if (entry == null || entry.Doctrine == null) continue;

                // 다른 존재(적 등)에 묶인 교리는 플레이어 HUD에 표시하지 않는다.
                // 오너가 없는 교리는 재단에서 조합한 플레이어 교리이므로 주어와 무관하게 모두 표시한다.
                if (entry.Owner != null && (player == null || entry.Owner != player.gameObject)) continue;

                result.Add(entry.Doctrine);
            }
            return result;
        }

        for (int i = 0; i < previewDoctrines.Count; i++)
        {
            if (previewDoctrines[i] != null)
                result.Add(previewDoctrines[i].ToRuntime());
        }

        // 인스펙터에 아무것도 연결하지 않아도 빈 칸이 보이지 않도록
        // 내장된 예시 3개를 쓴다. (표 기준: 회복/피해증가/폭발)
        if (result.Count == 0 && useBuiltInPreview)
        {
            result.Add(new Doctrine(SubjectType.Player, ConditionType.OnHit, ResultType.Heal));
            result.Add(new Doctrine(SubjectType.Player, ConditionType.OnAttack, ResultType.IncreaseAttack));
            result.Add(new Doctrine(SubjectType.Enemy, ConditionType.OnDeath, ResultType.Explode));
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
        UiEventSystemBoot.Ensure();
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
            // Canvas를 별도 오브젝트로 만들면 씬 전환 시 HUD만 남고 캔버스가 사라진다.
            // DontDestroyOnLoad 대상인 자기 자신에 붙여 함께 유지되게 한다.
            canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        // Authored HUD prefabs already have a Canvas and need the same click routing as generated HUDs.
        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        ClearGeneratedHud();

        // 씬에 저장된 HUD의 스케일이 0이면 아무것도 보이지 않으므로 바로잡는다.
        if (transform.localScale == Vector3.zero)
            transform.localScale = Vector3.one;

        BuildBottomBand();
        BuildHudArtwork();
        BuildHpBar();
        BuildDoctrineRow();
        BuildCurrencyBadge();
    }

    private void ClearGeneratedHud()
    {
        for (int i = canvas.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = canvas.transform.GetChild(i);
            if (child.name != "PlayerHudBand" && child.name != "PlayerStrengthBackground"
                && child.name != "PlayerPortrait" && child.name != "DoctrineRow")
                continue;

            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    private void BuildBottomBand()
    {
        GameObject band = new GameObject("PlayerHudBand", typeof(RectTransform));
        band.transform.SetParent(canvas.transform, false);
        bandRect = band.GetComponent<RectTransform>();
        bandRect.anchorMin = new Vector2(0f, 0f);
        bandRect.anchorMax = new Vector2(1f, 0f);
        bandRect.pivot = new Vector2(0.5f, 0f);
        bandRect.anchoredPosition = Vector2.zero;
        bandRect.sizeDelta = new Vector2(0f, bandHeight);
    }

    private void BuildHudArtwork()
    {
        Image background = CreatePixelArtImage(
            "PlayerStrengthBackground",
            HudArtworkResources.Load("PlayerStrengthBackground"),
            canvas.transform);
        if (background != null)
            background.transform.SetAsFirstSibling();

        hpFill = CreatePixelArtImage(
            "PlayerStrengthGauge",
            HudArtworkResources.Load("PlayerStrengthGauge"),
            bandRect);

        Image portrait = CreatePixelArtImage(
            "PlayerPortrait",
            HudArtworkResources.Load("PlayerPortrait"),
            canvas.transform);
        if (portrait != null)
            portrait.transform.SetAsLastSibling();
    }

    private Image CreatePixelArtImage(string name, HudArtworkSprite artwork, Transform parent)
    {
        if (!artwork.IsValid)
            return null;

        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(artwork.PixelBounds.x, artwork.PixelBounds.y);
        rect.sizeDelta = new Vector2(artwork.PixelBounds.width, artwork.PixelBounds.height);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = artwork.Sprite;
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private void BuildDoctrineRow()
    {
        GameObject row = new GameObject("DoctrineRow", typeof(RectTransform));
        // 체력바 배경(PlayerStrengthBackground)보다 먼저 그려지도록 캔버스 맨 뒤에 둔다.
        // 밴드 안에 두면 밴드가 배경보다 위에 있어 두루마리가 체력바를 덮는다.
        row.transform.SetParent(canvas.transform, false);
        doctrineRow = row.GetComponent<RectTransform>();
        doctrineRow.anchorMin = new Vector2(0f, 0f);
        doctrineRow.anchorMax = new Vector2(0f, 0f);
        doctrineRow.pivot = new Vector2(0f, 1f);
        // 기존에는 밴드 위쪽 모서리(anchor/pivot 모두 위)에서 y만큼 위로 올린 위치였다.
        // 캔버스 좌하단 기준으로는 밴드 높이에 y를 더하면 같은 화면 위치가 된다.
        doctrineRow.anchoredPosition = new Vector2(doctrinePosition.x, bandHeight + doctrinePosition.y);
        doctrineRow.sizeDelta = new Vector2(slotSize.x, slotSize.y);

        // 체력바 뒤로 보낸다. 이후에 그려지는 배경/게이지가 두루마리 아래쪽을 덮는다.
        row.transform.SetAsFirstSibling();
    }

    private void BuildHpBar()
    {
        if (hpFill == null)
            return;

        hpFill.type = Image.Type.Filled;
        hpFill.fillMethod = Image.FillMethod.Horizontal;
        hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        hpFill.fillAmount = 1f;
    }

    private void BuildCurrencyBadge()
    {
        Image badge = CreatePixelArtImage(
            "CurrencyBadge",
            HudArtworkResources.Load("PlayerCoinBadge"),
            bandRect);
        if (badge == null)
            return;

        currencyTarget = badge.rectTransform;
        PositionCurrencyBadge();
        currencyText = UiTheme.CreateText("CurrencyLabel", badge.transform, "00", 42,
            new Color(0.30f, 0.22f, 0.15f), TextAnchor.MiddleCenter);
        RectTransform textRect = currencyText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(44f, 0f);
        textRect.offsetMax = new Vector2(-28f, 0f);
    }
}
