using System.Collections.Generic;
using UnityEngine;

// Keeps the scene's exploration camera controls, and temporarily takes over the output camera for an arena.
[DefaultExecutionOrder(1000)]
public sealed class BossArenaCamera : MonoBehaviour
{
    [SerializeField] private Camera view;
    [SerializeField] private BoxCollider2D cameraBounds;
    [SerializeField] private Behaviour[] explorationControllers = new Behaviour[0];
    [SerializeField, Min(.1f)] private float combatSize = 8f;
    [SerializeField, Min(.1f)] private float maximumCombatSize = 10.5f;
    [SerializeField, Min(0f)] private float playerMargin = 1.5f;
    [SerializeField, Min(.01f)] private float blendDuration = .6f;
    [SerializeField] private Vector3 explorationOffset = new Vector3(1.5f, 2.2f, -10f);
    [SerializeField] private bool explorationFixedY;
    [SerializeField] private bool clampExplorationX;
    [SerializeField] private Vector2 explorationXLimits = new Vector2(-18f, 18f);

    private readonly List<Behaviour> suspended = new List<Behaviour>();
    private Transform player;
    private bool controlling, fighting;
    private float explorationSize, explorationY, returnElapsed;
    private Vector3 returnPosition;
    private Quaternion explorationRotation;
    private float returnSize;

    public void Enter(Transform actor)
    {
        if (view == null) view = Camera.main;
        if (view == null || cameraBounds == null || actor == null) return;
        player = actor;
        if (!controlling)
        {
            explorationSize = view.orthographicSize;
            explorationY = view.transform.position.y;
            explorationRotation = view.transform.rotation;
            suspended.Clear();
            foreach (Behaviour controller in explorationControllers) Suspend(controller);
            // Prefabs cannot serialize references to a scene's camera controls.
            foreach (MonoBehaviour controller in view.GetComponents<MonoBehaviour>())
                if (controller != null && (controller is LobbyCameraFollow || controller.GetType().FullName == "Unity.Cinemachine.CinemachineBrain"))
                    Suspend(controller);
            foreach (MonoBehaviour controller in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (controller != null && controller.gameObject.scene == view.gameObject.scene &&
                    controller.GetType().FullName == "Unity.Cinemachine.CinemachineCamera")
                    Suspend(controller);
        }
        controlling = fighting = true;
    }

    private void Suspend(Behaviour controller)
    {
        if (controller == null || controller == this || !controller.enabled || suspended.Contains(controller)) return;
        suspended.Add(controller);
        controller.enabled = false;
    }

    public void Exit()
    {
        if (!controlling || !fighting || view == null) return;
        fighting = false;
        returnElapsed = 0f;
        returnPosition = view.transform.position;
        returnSize = view.orthographicSize;
    }

    private void LateUpdate()
    {
        if (!controlling || view == null) return;
        if (fighting && player != null && cameraBounds != null)
        {
            Bounds bounds = cameraBounds.bounds;
            float size = Mathf.Clamp(Mathf.Max(combatSize, Mathf.Abs(player.position.y - bounds.center.y) + playerMargin),
                combatSize, Mathf.Max(combatSize, maximumCombatSize));
            float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 5f / blendDuration);
            view.orthographicSize = Mathf.Lerp(view.orthographicSize, size, blend);
            float x = ConfineX(player.position.x, bounds.min.x, bounds.max.x, view.orthographicSize * view.aspect);
            Vector3 position = new Vector3(x, bounds.center.y, view.transform.position.z);
            view.transform.position = Vector3.Lerp(view.transform.position, position, blend);
            view.transform.rotation = Quaternion.Slerp(view.transform.rotation, Quaternion.identity, blend);
        }
        else if (!fighting)
        {
            returnElapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(returnElapsed / blendDuration));
            Vector3 position = player != null ? player.position + explorationOffset : returnPosition;
            if (explorationFixedY) position.y = explorationY;
            if (clampExplorationX)
                position.x = ConfineX(position.x, explorationXLimits.x, explorationXLimits.y, explorationSize * view.aspect);
            view.transform.SetPositionAndRotation(Vector3.Lerp(returnPosition, position, t),
                Quaternion.Slerp(view.transform.rotation, explorationRotation, t));
            view.orthographicSize = Mathf.Lerp(returnSize, explorationSize, t);
            if (t >= 1f) RestoreExploration();
        }
    }

    private static float ConfineX(float x, float min, float max, float halfWidth) =>
        min + halfWidth > max - halfWidth ? (min + max) * .5f : Mathf.Clamp(x, min + halfWidth, max - halfWidth);

    private void RestoreExploration()
    {
        // Restore virtual cameras before their output brain so tracking starts from the new player position.
        for (int i = suspended.Count - 1; i >= 0; i--)
            if (suspended[i] != null) suspended[i].enabled = true;
        suspended.Clear();
        controlling = fighting = false;
    }

    private void OnDisable()
    {
        if (controlling && view != null) view.orthographicSize = explorationSize;
        RestoreExploration();
    }
}
