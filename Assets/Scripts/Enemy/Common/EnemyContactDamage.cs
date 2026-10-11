using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnemyContactDamage : MonoBehaviour
{
    [SerializeField]
    private float damage = 5f;

    private bool suppressed;

    public float Damage => damage;

    private void Awake()
    {
        Collider2D col =
            GetComponent<Collider2D>();

        if (!col.isTrigger)
        {
            Debug.LogWarning(
                $"{name}: ContactDamage Collider를 Trigger로 자동 변경합니다."
            );

            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(
        Collider2D other
    )
    {
        TryDamagePlayer(other);
    }

    private void OnTriggerStay2D(
        Collider2D other
    )
    {
        /*
         * Player가 적 내부에 계속 서 있어도
         * PlayerHealth의 무적시간이 끝난 후
         * 다시 피해를 받을 수 있게 한다.
         */
        TryDamagePlayer(other);
    }

    private void TryDamagePlayer(
        Collider2D other
    )
    {
        if (suppressed)
            return;

        Transform playerRoot =
            GetPlayerRoot(other);

        if (playerRoot == null)
            return;

        IDamageable damageable =
            playerRoot.GetComponent<IDamageable>();

        if (damageable == null)
        {
            damageable =
                playerRoot
                    .GetComponentInChildren<IDamageable>();
        }

        damageable?.TakeDamage(damage);
    }

    private Transform GetPlayerRoot(
        Collider2D other
    )
    {
        if (other.CompareTag("Player"))
            return other.transform;

        if (other.attachedRigidbody != null &&
            other.attachedRigidbody.CompareTag("Player"))
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

    public void SetSuppressed(bool value)
    {
        suppressed = value;
    }
}