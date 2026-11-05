using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class CutsceneTrigger : MonoBehaviour
{
    [SerializeField] private CutsceneRunner runner;
    [SerializeField] private CutsceneDefinition definition;
    [SerializeField] private bool suppressBoundEnemyActors;
    [SerializeField] private bool requireFullPlayerBodyInside = true;
    [SerializeField, Min(0f)] private float boundsInset = 0.05f;

    private Collider2D triggerCollider;
    private bool consumed;

    public bool HasRun => consumed;
    public CutsceneDefinition Definition => definition;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
        if (runner == null)
            runner = GetComponentInParent<CutsceneRunner>();
    }

    public void Configure(CutsceneRunner sequenceRunner, CutsceneDefinition sequence,
        bool suppressEnemies = false, bool checkFullBody = true)
    {
        runner = sequenceRunner;
        definition = sequence;
        suppressBoundEnemyActors = suppressEnemies;
        requireFullPlayerBodyInside = checkFullBody;
    }

    private void OnTriggerEnter2D(Collider2D other) => TryStart(other);
    private void OnTriggerStay2D(Collider2D other) => TryStart(other);

    private void TryStart(Collider2D other)
    {
        if (consumed || runner == null || definition == null ||
            !runner.isActiveAndEnabled || runner.IsRunning || other == null)
            return;

        PlayerHealth candidate = other.GetComponentInParent<PlayerHealth>();
        if (candidate == null || candidate.IsDead || !candidate.CompareTag("Player") ||
            candidate.BodyCollider != other)
            return;

        if (requireFullPlayerBodyInside && !IsBodyFullyInside(candidate.BodyCollider))
            return;

        if (runner.Play(definition, candidate, suppressBoundEnemyActors))
            consumed = true;
    }

    private bool IsBodyFullyInside(Collider2D playerBody)
    {
        if (triggerCollider == null || playerBody == null)
            return false;

        Bounds zone = triggerCollider.bounds;
        Bounds body = playerBody.bounds;
        return body.min.x >= zone.min.x + boundsInset &&
               body.max.x <= zone.max.x - boundsInset &&
               body.min.y >= zone.min.y + boundsInset &&
               body.max.y <= zone.max.y - boundsInset;
    }
}
