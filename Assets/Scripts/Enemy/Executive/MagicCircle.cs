using System.Collections.Generic;
using UnityEngine;

public class MagicCircle : MonoBehaviour
{
    private float radius;

    public void Activate(
        float radius,
        float damage,
        float slowMultiplier,
        float slowDuration,
        float visualDuration
    )
    {
        this.radius = radius;

        float diameter =
            radius * 2f;

        transform.localScale =
            new Vector3(
                diameter,
                diameter,
                1f
            );

        ApplyEffect(
            damage,
            slowMultiplier,
            slowDuration
        );

        Destroy(
            gameObject,
            visualDuration
        );
    }

    private void ApplyEffect(
        float damage,
        float slowMultiplier,
        float slowDuration
    )
    {
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                radius
            );

        /*
         * Player가 여러 Collider를 가지고 있어도
         * 한 번만 맞도록 처리.
         */
        HashSet<Transform> processedPlayers =
            new HashSet<Transform>();

        foreach (Collider2D hit in hits)
        {
            Transform playerRoot =
                GetPlayerRoot(hit);

            if (playerRoot == null)
                continue;

            if (!processedPlayers.Add(
                    playerRoot))
            {
                continue;
            }

            IDamageable damageable =
                playerRoot
                    .GetComponent<IDamageable>();

            if (damageable == null)
            {
                damageable =
                    playerRoot
                        .GetComponentInChildren<IDamageable>();
            }

            if (damageable != null)
            {
                damageable.TakeDamage(
                    damage
                );
            }

            ISlowable slowable =
                playerRoot
                    .GetComponent<ISlowable>();

            if (slowable == null)
            {
                slowable =
                    playerRoot
                        .GetComponentInChildren<ISlowable>();
            }

            if (slowable != null)
            {
                slowable.ApplySlow(
                    slowMultiplier,
                    slowDuration
                );
            }
        }
    }

    private Transform GetPlayerRoot(
        Collider2D other
    )
    {
        if (other.CompareTag("Player"))
        {
            return other.transform;
        }

        if (other.attachedRigidbody != null &&
            other.attachedRigidbody.CompareTag(
                "Player"
            ))
        {
            return
                other.attachedRigidbody.transform;
        }

        Transform root =
            other.transform.root;

        if (root != null &&
            root.CompareTag("Player"))
        {
            return root;
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            radius
        );
    }
}