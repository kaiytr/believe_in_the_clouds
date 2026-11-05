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
    private bool locked, cleared;
    private float explorationSize;

    private void Awake()
    {
        foreach (EnemyUnit enemy in enemies) enemy.gameObject.SetActive(false);
        explorationSize = view.orthographicSize;
    }

    private void Start()
    {
        LobbyFlowRuntime flow = LobbyFlowRuntime.Instance;
        if (flow?.Scene == null || flow.Scene.gameObject.scene != gameObject.scene) return;
        cleared = flow.State?.stage?.completed == true;
        if (cleared || entranceSeal == null || flow.State?.hasPosition != true) return;

        float x = player.transform.position.x;
        if (x > entranceSeal.transform.position.x && x < farSeal.transform.position.x)
            SetLocked(true);
    }

    private void LateUpdate()
    {
        if (cleared || player.IsDead) return;
        if (enemies.All(enemy => enemy == null || enemy.IsDead))
        {
            cleared = true;
            if (locked) SetLocked(false);
            return;
        }

        if (entranceSeal != null && !locked)
        {
            float x = player.transform.position.x;
            if (x < entranceSeal.transform.position.x + 1f || x > farSeal.transform.position.x - 1f) return;
            SetLocked(true);
        }

        foreach (EnemyUnit enemy in enemies)
        {
            if (enemy != null && !enemy.gameObject.activeSelf &&
                (locked || Mathf.Abs(enemy.transform.position.x - player.transform.position.x) < 11f))
                enemy.gameObject.SetActive(true);
        }

        if (locked)
        {
            float left = entranceSeal.transform.position.x;
            float right = farSeal.transform.position.x;
            view.orthographicSize = Mathf.Max(explorationSize, Mathf.Max(
                (right - left + 1.5f) / (2f * view.aspect), (arenaMaxY - arenaMinY) * .5f));
            view.transform.position = new Vector3((left + right) * .5f, (arenaMinY + arenaMaxY) * .5f, -10f);
        }
    }

    private void SetLocked(bool value)
    {
        locked = value;
        entranceSeal.gameObject.SetActive(value);
        farSeal.gameObject.SetActive(value);
        exploration.enabled = !value;
        if (!value)
        {
            view.orthographicSize = explorationSize;
            Vector3 position = view.transform.position;
            position.x = player.transform.position.x;
            view.transform.position = position;
        }
    }
}
