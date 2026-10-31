using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerDeathCoordinator : MonoBehaviour
{
    private string lifeId = Guid.NewGuid().ToString("N");
    private IEquipmentAdapter equipment;
    public string DeathId => "life:" + lifeId + "/death";
    public CurrencyResult LastResult { get; private set; }

    public void SetEquipmentAdapter(IEquipmentAdapter adapter) { equipment = adapter; }

    public void BeginLife()
    {
        if (Currency.Service != null && !Currency.Service.BeginLife()) return;
        lifeId = Guid.NewGuid().ToString("N");
        LastResult = null;
    }

    public void CaptureDeath(Action processExistingDeathEffects)
    {
        if (Currency.Service == null) { processExistingDeathEffects(); return; }
        equipment = equipment ?? new PlayerEquipmentAdapter(gameObject);
        LastResult = Currency.Service.TryHandleDeath(DeathId, equipment, processExistingDeathEffects);
    }
}
