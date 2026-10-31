using System;
using UnityEngine;

// Main-thread facade for all coin sources/sinks. Views do not own balances.
public static class Currency
{
    public static EconomyService Service { get; private set; }
    public static int Balance => Service?.Balance ?? 0;
    public static int PendingBalance => Service?.PendingBalance ?? 0;
    public static bool IsReady => Service != null && Service.IsReady;
    public static event Action<CurrencyChange> Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Service = null; Changed = null; }

    public static void Bind(EconomyService service)
    {
        if (Service != null) Service.Changed -= Forward;
        Service = service;
        if (Service != null) Service.Changed += Forward;
        Forward(new CurrencyChange(Balance, Balance, PendingBalance, PendingBalance, "profile.bound", null));
    }

    private static void Forward(CurrencyChange change)
    {
        if (Changed == null) return;
        foreach (Action<CurrencyChange> listener in Changed.GetInvocationList())
        {
            try { listener(change); }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }

    public static bool CanAfford(int amount) => Service != null && Service.CanAfford(amount);
    public static CurrencyResult TryGrant(int amount, string reason, string transactionId = null) =>
        Service?.TryGrant(amount, reason, transactionId) ?? CurrencyResult.Failed(EconomyError.NotReady);
    public static CurrencyResult TrySpend(int amount, string reason, string transactionId = null) =>
        Service?.TrySpend(amount, reason, transactionId) ?? CurrencyResult.Failed(EconomyError.NotReady);
    public static CurrencyResult TryGrantWithEffect(int amount, Vector3 position, string reason, string transactionId = null, Camera sourceCamera = null) =>
        Service?.TryGrantWithEffect(amount, position, reason, transactionId, sourceCamera) ?? CurrencyResult.Failed(EconomyError.NotReady);
}
