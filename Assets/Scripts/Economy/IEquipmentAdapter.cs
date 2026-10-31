using System.Collections.Generic;
using UnityEngine;

public interface IEquipmentAdapter
{
    // Owned pieces include equipment references already. Never return them twice.
    IReadOnlyList<PieceCount> UnequipAllAndGetOwnedPieces(IReadOnlyList<PieceCount> ownedPieces);
}

public class PlayerEquipmentAdapter : IEquipmentAdapter
{
    protected readonly GameObject player;
    public PlayerEquipmentAdapter(GameObject player) { this.player = player; }
    public virtual IReadOnlyList<PieceCount> UnequipAllAndGetOwnedPieces(IReadOnlyList<PieceCount> ownedPieces)
    {
        if (player != null)
        {
            DoctrineManager.Instance?.ClearForOwner(player);
            player.GetComponent<DoctrineStats>()?.ClearAllBuffs();
        }
        return EconomyLedger.CopyPieces(ownedPieces);
    }
}

// Test-only equipment state references pieces that remain in the one ledger.
// This is deliberately not a second inventory of completed doctrines.
public sealed class TestEquipmentAdapter : PlayerEquipmentAdapter
{
    private readonly List<Doctrine> equipped = new List<Doctrine>();
    public int EquippedCount => equipped.Count;
    public TestEquipmentAdapter(GameObject player) : base(player) { }

    public bool TryEquip(Doctrine doctrine, EconomyService service)
    {
        if (!service.IsReady || player == null || DoctrineManager.Instance == null || doctrine == null) return false;
        string[] ids = { ShopCatalog.IdFor(doctrine.Subject), ShopCatalog.IdFor(doctrine.Condition), ShopCatalog.IdFor(doctrine.Result) };
        foreach (string id in ids)
        {
            int used = 0;
            foreach (Doctrine item in equipped)
                if (ShopCatalog.IdFor(item.Subject) == id || ShopCatalog.IdFor(item.Condition) == id || ShopCatalog.IdFor(item.Result) == id) used++;
            if (id == null || service.CountPiece(id) <= used) return false;
        }
        if (!DoctrineManager.Instance.TryActivate(doctrine, player, out _)) return false;
        equipped.Add(doctrine.Clone());
        return true;
    }

    public override IReadOnlyList<PieceCount> UnequipAllAndGetOwnedPieces(IReadOnlyList<PieceCount> ownedPieces)
    {
        var pieces = base.UnequipAllAndGetOwnedPieces(ownedPieces);
        equipped.Clear();
        return pieces;
    }
}
