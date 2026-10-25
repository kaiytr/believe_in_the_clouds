using System.Collections.Generic;
using UnityEngine;

// 상점 화면 모듈.
//
// - 상점 상호작용이 발생하면 사진과 같은 3열(주어/조건/결과) × 4행 패널을 연다.
// - 각 열에서 카드 하나씩 고르는 강제 조합이 아니다. 한 장만 골라도 구매된다.
// - 구매한 조각(주어/조건/결과)은 교리로 바로 활성화되지 않고
//   DoctrineInventory(가상 인벤토리)에 쌓인다.
// - 실제 교리 조합/장착은 나중에 재단(Altar)에서 한다.
// - 상점 규칙(선택/장바구니)만 담당하고, UI 그리기는 ShopPanelView가 한다.
public class ShopScreen : MonoBehaviour
{
    public static ShopScreen Instance { get; private set; }

    private ShopPanelView panel;
    private ShopInteractable openShop;

    // 열 제한 없이 여러 장을 고를 수 있다 (장바구니).
    private readonly HashSet<int> selectedSubjects = new HashSet<int>();
    private readonly HashSet<int> selectedConditions = new HashSet<int>();
    private readonly HashSet<int> selectedResults = new HashSet<int>();

    public bool IsOpen => panel != null && panel.IsOpen;
    public ShopInteractable OpenShop => openShop;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBoot()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject go = new GameObject("[ShopScreen]");
        DontDestroyOnLoad(go);
        go.AddComponent<ShopScreen>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsurePanel();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        InteractableBehaviour.Interacted += HandleInteracted;
        UiModal.CloseRequested += HandleCloseRequested;
    }

    private void OnDisable()
    {
        InteractableBehaviour.Interacted -= HandleInteracted;
        UiModal.CloseRequested -= HandleCloseRequested;
    }

    private void EnsurePanel()
    {
        if (panel != null)
        {
            return;
        }

        panel = ShopPanelView.BuildRuntime();
        panel.CardClicked += HandleCardClicked;
        panel.BuyClicked += HandleBuyClicked;
        DontDestroyOnLoad(panel.gameObject);

        // uGUI 버튼은 EventSystem이 없으면 클릭을 못 받는다.
        UiEventSystemBoot.Ensure();
    }

    private void HandleInteracted(InteractableBehaviour target, GameObject interactor)
    {
        if (target is not ShopInteractable shop)
        {
            return;
        }

        if (openShop == shop && IsOpen)
        {
            Close();
            return;
        }

        Open(shop, interactor);
    }

    private void HandleCloseRequested()
    {
        if (IsOpen)
        {
            Close();
        }
    }

    public void Open(ShopInteractable shop, GameObject interactor)
    {
        if (shop == null)
        {
            return;
        }

        EnsurePanel();
        openShop = shop;

        // 씬에 EventSystem이 없으면 클릭이 전달되지 않는다.
        UiEventSystemBoot.Ensure();

        ClearSelection();
        panel.Open(shop.ShopName);
        UiModal.Push(this);
        Debug.Log($"[상점] {shop.ShopName} 열림 ({interactor?.name})");
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        panel.Close();
        UiModal.Pop(this);

        ShopInteractable closed = openShop;
        openShop = null;
        Debug.Log($"[상점] {closed?.ShopName} 닫힘");
    }

    private void ClearSelection()
    {
        selectedSubjects.Clear();
        selectedConditions.Clear();
        selectedResults.Clear();
        panel.ClearSelection();
        RefreshSummary();
    }

    // 각 열 제한 없이 여러 장을 고를 수 있다. 같은 카드를 다시 누르면 해제.
    // 한 장만 골라도 구매된다 (예: 주어 '일반 적' 하나만).
    private void HandleCardClicked(ShopColumn column, int index)
    {
        HashSet<int> bucket = Bucket(column);
        if (bucket.Contains(index))
        {
            bucket.Remove(index);
        }
        else
        {
            bucket.Add(index);
        }

        Debug.Log($"[상점] 카드 클릭: {column} #{index} → 선택 {DescribeSelection()}");

        SyncSelection(column);
        RefreshSummary();
    }

    // 구매: 고른 조각들을 교리로 활성화하지 않고 인벤토리에 쌓는다.
    // 재화 시스템이 아직 없으므로 지금은 "구매 = 즉시 획득"이다.
    // 한 장만 골라도 구매된다.
    private void HandleBuyClicked()
    {
        DoctrineInventory inventory = DoctrineInventory.Instance;
        if (inventory == null)
        {
            panel.SetFooter("인벤토리를 찾을 수 없습니다.");
            return;
        }

        int bought = 0;
        bought += BuyBucket(inventory, ShopColumn.Subject, selectedSubjects);
        bought += BuyBucket(inventory, ShopColumn.Condition, selectedConditions);
        bought += BuyBucket(inventory, ShopColumn.Result, selectedResults);

        if (bought == 0)
        {
            panel.SetFooter("카드를 한 장 이상 고르세요.");
            panel.SetBuyEnabled(false);
            return;
        }

        selectedSubjects.Clear();
        selectedConditions.Clear();
        selectedResults.Clear();
        panel.ClearSelection();
        RefreshSummary();

        panel.SetFooter($"{inventory.Describe()} · 재단에서 조합하세요.");
        panel.SetBuyEnabled(false);
        Debug.Log($"[상점] 조각 구매 {bought}장 → {inventory.Describe()}");
    }

    private int BuyBucket(DoctrineInventory inventory, ShopColumn column, HashSet<int> bucket)
    {
        if (bucket.Count == 0)
        {
            return 0;
        }

        var entries = ShopCatalog.GetColumn(column);
        int bought = 0;
        foreach (int index in bucket)
        {
            if (index < 0 || index >= entries.Count)
            {
                continue;
            }

            inventory.AddEntry(entries[index]);
            bought++;
        }

        return bought;
    }

    private void RefreshSummary()
    {
        SyncAllSelections();

        int total = selectedSubjects.Count + selectedConditions.Count + selectedResults.Count;
        if (total == 0)
        {
            panel.SetSelectionSummary(null, null, null);
            panel.SetFooter("카드를 한 장 이상 고르세요.");
            panel.SetBuyEnabled(false);
            return;
        }

        panel.SetSelectionSummary(
            JoinLabels(ShopColumn.Subject, selectedSubjects),
            JoinLabels(ShopColumn.Condition, selectedConditions),
            JoinLabels(ShopColumn.Result, selectedResults));

        panel.SetFooter($"선택 {total}장  ·  구매를 누르면 인벤토리에 들어갑니다.");
        panel.SetBuyEnabled(true);
    }

    private void SyncAllSelections()
    {
        ApplyBucket(ShopColumn.Subject, selectedSubjects);
        ApplyBucket(ShopColumn.Condition, selectedConditions);
        ApplyBucket(ShopColumn.Result, selectedResults);
    }

    private void SyncSelection(ShopColumn column)
    {
        panel.ClearSelection();
        ApplyBucket(ShopColumn.Subject, selectedSubjects);
        ApplyBucket(ShopColumn.Condition, selectedConditions);
        ApplyBucket(ShopColumn.Result, selectedResults);
    }

    private void ApplyBucket(ShopColumn column, HashSet<int> bucket)
    {
        var entries = ShopCatalog.GetColumn(column);
        foreach (int index in bucket)
        {
            if (index >= 0 && index < entries.Count)
            {
                panel.AddSelection(column, index);
            }
        }
    }
    private HashSet<int> Bucket(ShopColumn column)
    {
        switch (column)
        {
            case ShopColumn.Subject: return selectedSubjects;
            case ShopColumn.Condition: return selectedConditions;
            default: return selectedResults;
        }
    }

    private static string JoinLabels(ShopColumn column, HashSet<int> bucket)
    {
        if (bucket.Count == 0)
        {
            return null;
        }

        var entries = ShopCatalog.GetColumn(column);
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (int index in bucket)
        {
            if (index < 0 || index >= entries.Count)
            {
                continue;
            }

            if (sb.Length > 0)
            {
                sb.Append(", ");
            }

            sb.Append(entries[index].Label);
        }

        return sb.Length > 0 ? sb.ToString() : null;
    }

    private string DescribeSelection()
    {
        return $"주어=[{JoinLabels(ShopColumn.Subject, selectedSubjects) ?? "-"}], " +
               $"조건=[{JoinLabels(ShopColumn.Condition, selectedConditions) ?? "-"}], " +
               $"결과=[{JoinLabels(ShopColumn.Result, selectedResults) ?? "-"}]";
    }
}
