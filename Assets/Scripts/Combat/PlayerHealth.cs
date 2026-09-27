using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float maxHp = 100f;

    [Header("Invulnerability")]
    [SerializeField] private float invulnerabilityDuration = 1f;
    [SerializeField] private float blinkInterval = 0.1f;

    private float currentHp;

    private bool isInvulnerable;
    private Coroutine invulnerabilityRoutine;

    private SpriteRenderer[] spriteRenderers;
    private bool[] originalRendererStates;

    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public bool IsInvulnerable => isInvulnerable;

    private void Awake()
    {
        currentHp = maxHp;

        spriteRenderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        originalRendererStates =
            new bool[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            originalRendererStates[i] =
                spriteRenderers[i].enabled;
        }
    }

    public void TakeDamage(float damage)
    {
        if (damage <= 0f)
            return;

        if (isInvulnerable)
            return;

        if (currentHp <= 0f)
            return;

        currentHp -= damage;
        currentHp = Mathf.Max(currentHp, 0f);

        Debug.Log(
            $"[Player] Damage: {damage} / HP: {currentHp}/{maxHp}"
        );

        if (currentHp <= 0f)
        {
            Die();
            return;
        }

        StartInvulnerability();
    }

    private void StartInvulnerability()
    {
        // 같은 프레임에 여러 공격이 들어오는 것도 방지하기 위해
        // Coroutine 시작 전에 즉시 true 처리.
        isInvulnerable = true;

        if (invulnerabilityRoutine != null)
        {
            StopCoroutine(invulnerabilityRoutine);
        }

        invulnerabilityRoutine =
            StartCoroutine(
                InvulnerabilityRoutine()
            );
    }

    private IEnumerator InvulnerabilityRoutine()
    {
        float elapsed = 0f;
        bool visible = true;

        while (elapsed < invulnerabilityDuration)
        {
            visible = !visible;

            SetRenderersVisible(visible);

            yield return new WaitForSeconds(
                blinkInterval
            );

            elapsed += blinkInterval;
        }

        RestoreRenderers();

        isInvulnerable = false;
        invulnerabilityRoutine = null;
    }

    private void SetRenderersVisible(bool visible)
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (!originalRendererStates[i])
                continue;

            spriteRenderers[i].enabled = visible;
        }
    }

    private void RestoreRenderers()
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            spriteRenderers[i].enabled =
                originalRendererStates[i];
        }
    }

    private void Die()
    {
        isInvulnerable = true;

        RestoreRenderers();

        Debug.Log("[Player] Dead");

        // 실제 Player 사망 로직은 나중에 연결
    }

    private void OnDisable()
    {
        RestoreRenderers();
    }
}