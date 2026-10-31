using System;
using System.Collections.Generic;
using UnityEngine;

// Compatibility facade: the economy ledger is the sole owner of all pieces.
public class DoctrineInventory : MonoBehaviour
{
    public static DoctrineInventory Instance { get; private set; }
    public event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBoot()
    {
        if (Instance == null) new GameObject("[DoctrineInventory]").AddComponent<DoctrineInventory>();
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    private void OnEnable() { Currency.Changed += HandleChanged; }
    private void OnDisable() { Currency.Changed -= HandleChanged; }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    private void HandleChanged(CurrencyChange change) { Changed?.Invoke(); }

    public CurrencyResult TryAddEntry(ShopEntry entry, string reason, string transactionId = null) =>
        Currency.Service?.TryGrantPiece(entry?.Id, 1, reason, transactionId) ?? CurrencyResult.Failed(EconomyError.NotReady);
    private void Add(string id)
    {
        if (id == null) return;
        var result = Currency.Service?.TryGrantPiece(id, 1, "inventory.legacy-grant");
        if (result == null || !result.Success) Debug.LogWarning($"[Inventory] Grant failed: {result?.Error ?? EconomyError.NotReady}");
    }
    public void AddSubject(SubjectType subject) => Add(ShopCatalog.IdFor(subject));
    public void AddCondition(ConditionType condition) => Add(ShopCatalog.IdFor(condition));
    public void AddResult(ResultType result) => Add(ShopCatalog.IdFor(result));
    public void AddEntry(ShopEntry entry) { if (entry != null) Add(entry.Id); }
    public int CountSubject(SubjectType subject) => Currency.Service?.CountPiece(ShopCatalog.IdFor(subject)) ?? 0;
    public int CountCondition(ConditionType condition) => Currency.Service?.CountPiece(ShopCatalog.IdFor(condition)) ?? 0;
    public int CountResult(ResultType result) => Currency.Service?.CountPiece(ShopCatalog.IdFor(result)) ?? 0;
    public int TotalCount => Currency.Service == null ? 0 : EconomyLedger.Total(Currency.Service.Snapshot().pieces);

    public string Describe()
    {
        int s = 0, c = 0, r = 0;
        IReadOnlyList<PieceCount> pieces = Currency.Service?.Snapshot().pieces;
        if (pieces != null)
            foreach (PieceCount piece in pieces)
            {
                if (!ShopCatalog.TryGet(piece.id, out ShopEntry entry)) continue;
                if (entry.Column == ShopColumn.Subject) s += piece.count;
                else if (entry.Column == ShopColumn.Condition) c += piece.count;
                else r += piece.count;
            }
        return $"인벤토리 — 주어 {s} · 조건 {c} · 결과 {r} (총 {s + c + r})";
    }
}
