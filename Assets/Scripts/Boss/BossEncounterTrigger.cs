using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class BossEncounterTrigger : MonoBehaviour
{
    [SerializeField] private BossCombatController boss;
    private bool triggered;

    private void Awake()
    {
        BoxCollider2D trigger = GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        if (boss == null)
            boss = FindFirstObjectByType<BossCombatController>(FindObjectsInactive.Include);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered || boss == null || other.transform.root.CompareTag("Player") == false)
            return;

        triggered = true;
        if (!boss.gameObject.activeSelf)
            boss.gameObject.SetActive(true);
        boss.BeginEncounter();
    }
}
