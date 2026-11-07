using System;
using System.Collections.Generic;
using UnityEngine;

// Briefly pauses the existing enemy while ground dust marks its arena entrance.
public sealed class EnemyArenaEmergence : IDisposable
{
    private readonly List<Behaviour> behaviours = new List<Behaviour>();
    private readonly List<Collider2D> colliders = new List<Collider2D>();
    private readonly List<Rigidbody2D> bodies = new List<Rigidbody2D>();
    private LobbyBranchRevealEffect effect;
    private bool restored;

    public bool IsPlaying => effect != null && effect.IsPlaying;

    public static EnemyArenaEmergence Begin(EnemyUnit enemy)
    {
        var presentation = new EnemyArenaEmergence();
        try
        {
            presentation.Prepare(enemy);
            return presentation;
        }
        catch
        {
            presentation.Dispose();
            throw;
        }
    }

    private void Prepare(EnemyUnit enemy)
    {
        if (enemy == null) return;

        foreach (MonoBehaviour behaviour in enemy.GetComponentsInChildren<MonoBehaviour>(true))
            Pause(behaviour);
        foreach (Animator animator in enemy.GetComponentsInChildren<Animator>(true))
            Pause(animator);
        foreach (Collider2D collider in enemy.GetComponentsInChildren<Collider2D>(true))
        {
            if (!collider.enabled) continue;
            colliders.Add(collider);
            collider.enabled = false;
        }
        foreach (Rigidbody2D body in enemy.GetComponentsInChildren<Rigidbody2D>(true))
        {
            if (!body.simulated) continue;
            bodies.Add(body);
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }

        enemy.gameObject.SetActive(true);
        SpriteRenderer primary = null;
        float largestArea = 0f;
        foreach (SpriteRenderer renderer in enemy.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.sprite == null) continue;
            float area = renderer.bounds.size.x * renderer.bounds.size.y;
            if (primary == null || area > largestArea)
            {
                primary = renderer;
                largestArea = area;
            }
        }
        if (primary == null) return;

        effect = enemy.gameObject.AddComponent<LobbyBranchRevealEffect>();
        effect.PlayGroundDust(primary);
    }

    private void Pause(Behaviour behaviour)
    {
        if (behaviour == null || !behaviour.enabled) return;
        behaviours.Add(behaviour);
        behaviour.enabled = false;
    }

    public void Complete()
    {
        if (restored) return;
        restored = true;
        foreach (Collider2D collider in colliders)
            if (collider != null) collider.enabled = true;
        foreach (Rigidbody2D body in bodies)
            if (body != null) body.simulated = true;
        foreach (Behaviour behaviour in behaviours)
            if (behaviour != null) behaviour.enabled = true;
    }

    public void Dispose()
    {
        if (effect != null && effect.IsPlaying)
        {
            effect.enabled = false;
            UnityEngine.Object.Destroy(effect);
        }
        Complete();
    }
}
