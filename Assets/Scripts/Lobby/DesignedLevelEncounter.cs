using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(950)]
public sealed class DesignedLevelEncounter : MonoBehaviour
{
    [SerializeField] private PlayerHealth player;
    [SerializeField] private EnemyUnit[] enemies;
    [SerializeField] private Camera view;
    [SerializeField] private LobbyCameraFollow exploration;
    [SerializeField] private BoxCollider2D entranceSeal, farSeal;
    [SerializeField] private float arenaMinY = -5f, arenaMaxY = 7f;
    [SerializeField, Range(0f, 1f)] private float arenaActivationProgress = .5f;
    [SerializeField] private BossArenaCamera arenaCamera;
    [SerializeField] private bool revealEnemiesFromGround;
    private bool locked, cleared;
    private bool revealing;
    private float explorationSize;
    private readonly List<EnemyArenaEmergence> emergence = new List<EnemyArenaEmergence>();

    private bool HasArena => entranceSeal != null && farSeal != null;

    private void Awake()
    {
        foreach (EnemyUnit enemy in enemies)
            if (enemy != null) enemy.gameObject.SetActive(false);
        explorationSize = view != null ? view.orthographicSize : 5f;
        if (HasArena)
        {
            entranceSeal.gameObject.SetActive(false);
            farSeal.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        LobbyFlowRuntime flow = LobbyFlowRuntime.Instance;
        if (flow?.Scene == null || flow.Scene.gameObject.scene != gameObject.scene) return;
        cleared = flow.State?.stage?.completed == true;
        if (!cleared && flow.State?.hasPosition == true && ReachedActivationPoint())
            BeginArena();
    }

    private void LateUpdate()
    {
        if (cleared || player == null || player.IsDead) return;
        if (enemies.All(enemy => enemy == null || enemy.IsDead))
        {
            cleared = true;
            if (locked) SetLocked(false);
            return;
        }

        if (HasArena && !locked)
        {
            if (!ReachedActivationPoint()) return;
            BeginArena();
        }

        if (!revealing)
        {
            foreach (EnemyUnit enemy in enemies)
            {
                if (enemy != null && !enemy.gameObject.activeSelf &&
                    (locked || Mathf.Abs(enemy.transform.position.x - player.transform.position.x) < 11f))
                    enemy.gameObject.SetActive(true);
            }
        }

        if (locked && arenaCamera == null && view != null)
        {
            float left = entranceSeal.transform.position.x;
            float right = farSeal.transform.position.x;
            view.orthographicSize = Mathf.Max(explorationSize, Mathf.Max(
                (right - left + 1.5f) / (2f * view.aspect), (arenaMaxY - arenaMinY) * .5f));
            view.transform.position = new Vector3((left + right) * .5f, (arenaMinY + arenaMaxY) * .5f, -10f);
        }
    }

    private bool ReachedActivationPoint()
    {
        if (!HasArena || player == null || player.IsDead) return false;
        float left = Mathf.Min(entranceSeal.transform.position.x, farSeal.transform.position.x);
        float right = Mathf.Max(entranceSeal.transform.position.x, farSeal.transform.position.x);
        Bounds body = player.BodyCollider != null ? player.BodyCollider.bounds
            : new Bounds(player.transform.position, Vector3.one);
        float activationX = Mathf.Lerp(left, right, arenaActivationProgress);
        return body.min.x > left && body.max.x < right && body.center.x >= activationX &&
            body.max.y >= arenaMinY && body.min.y <= arenaMaxY;
    }

    private void BeginArena()
    {
        if (locked || cleared) return;
        SetLocked(true);
        if (revealEnemiesFromGround) StartCoroutine(RevealEnemies());
    }

    private IEnumerator RevealEnemies()
    {
        revealing = true;
        try
        {
            foreach (EnemyUnit enemy in enemies)
                if (enemy != null && !enemy.IsDead)
                    emergence.Add(EnemyArenaEmergence.Begin(enemy));
            while (emergence.Any(presentation => presentation.IsPlaying))
            {
                if (player == null || player.IsDead) yield break;
                yield return null;
            }
        }
        finally
        {
            FinishEmergence();
        }
    }

    private void FinishEmergence()
    {
        foreach (EnemyArenaEmergence presentation in emergence) presentation.Dispose();
        emergence.Clear();
        revealing = false;
    }

    private void SetLocked(bool value)
    {
        locked = value;
        if (entranceSeal != null) entranceSeal.gameObject.SetActive(value);
        if (farSeal != null) farSeal.gameObject.SetActive(value);
        if (arenaCamera != null)
        {
            if (value)
            {
                float left = Mathf.Min(entranceSeal.transform.position.x, farSeal.transform.position.x);
                float right = Mathf.Max(entranceSeal.transform.position.x, farSeal.transform.position.x);
                arenaCamera.ConfigureWorldBounds(view, new Bounds(
                    new Vector3((left + right) * .5f, (arenaMinY + arenaMaxY) * .5f, 0f),
                    new Vector3(right - left, arenaMaxY - arenaMinY, 1f)));
                arenaCamera.Enter(player.transform);
            }
            else arenaCamera.Exit();
            return;
        }
        if (exploration != null) exploration.enabled = !value;
        if (!value && view != null)
        {
            view.orthographicSize = explorationSize;
            Vector3 position = view.transform.position;
            position.x = player.transform.position.x;
            view.transform.position = position;
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        FinishEmergence();
        if (locked) SetLocked(false);
    }
}
