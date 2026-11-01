using UnityEngine;

// Kept as the prefab component to preserve existing asset GUIDs and references.
// Clouds are visual markers; the lightning owns the shelter and strike geometry.
[RequireComponent(typeof(BoxCollider2D))]
public sealed class BossCloudPlatform : MonoBehaviour
{
    public bool IsWhite { get; private set; }
    public void Initialize(BossCombatController boss, bool whiteCloud, AttackGeometry shape)
    {
        IsWhite = whiteCloud;
        transform.position = shape.Center;
        transform.localScale = new Vector3(shape.Size.x, shape.Size.y, 1f);
        gameObject.layer = 0;
        var surface = GetComponent<BoxCollider2D>();
        surface.usedByEffector = false;
        surface.isTrigger = true;
    }
}
