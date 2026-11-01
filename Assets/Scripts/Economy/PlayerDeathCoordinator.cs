using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerDeathCoordinator : MonoBehaviour
{
    private string lifeId = Guid.NewGuid().ToString("N");
    private IEquipmentAdapter equipment;
    private string persistentLifeId;
    public string DeathId => "life:" + lifeId + "/death";
    public CurrencyResult LastResult { get; private set; }

    public void SetEquipmentAdapter(IEquipmentAdapter adapter) { equipment = adapter; }
    public void BindRunLife(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A saved life ID is required.", nameof(id));
        persistentLifeId = id;
        lifeId = id;
    }

    public void BeginLife()
    {
        if (Currency.Service != null && !Currency.Service.BeginLife()) return;
        lifeId = persistentLifeId ?? Guid.NewGuid().ToString("N");
        LastResult = null;
    }

    public void CaptureDeath(Action processExistingDeathEffects)
    {
        if (Currency.Service == null) { processExistingDeathEffects(); return; }
        equipment = equipment ?? new PlayerEquipmentAdapter(gameObject);
        LastResult = Currency.Service.TryHandleDeath(DeathId, equipment, processExistingDeathEffects);
    }
}
