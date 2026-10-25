using UnityEngine;

/// <summary>Keep each test encounter dormant until the player reaches its section.</summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class StageEnemyActivation : MonoBehaviour
{
    [SerializeField] private GameObject[] enemies;
    private bool activated;

    private void Awake() => GetComponent<BoxCollider2D>().isTrigger = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (activated || !other.transform.root.CompareTag("Player")) return;
        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (player == null || player.IsDead) return;
        activated = true;
        foreach (GameObject enemy in enemies)
            if (enemy != null) enemy.SetActive(true);
    }
}
