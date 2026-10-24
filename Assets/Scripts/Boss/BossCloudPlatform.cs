using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class BossCloudPlatform : MonoBehaviour
{
    private BossCombatController owner;
    private bool isWhiteCloud;
    private bool reported;

    public void Initialize(BossCombatController boss, bool whiteCloud)
    {
        owner = boss;
        isWhiteCloud = whiteCloud;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isWhiteCloud)
            Report(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isWhiteCloud)
            Report(collision.gameObject);
    }

    private void Report(GameObject other)
    {
        if (reported)
            return;
        reported = true;
        owner?.OnCloudInteraction(isWhiteCloud, other);
    }
}
