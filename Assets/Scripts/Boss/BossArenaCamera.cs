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
    [Tooltip("Keeps the combat camera centered on the arena and fits its full width in view.")]
    [SerializeField] private bool fixedCombatView;
    [SerializeField] private float combatVerticalOffset = -1f;
    [SerializeField, Min(0f)] private float stormCloudTopMargin = .7f;
    [Tooltip("Lowers the combat view without moving the storm clouds, so their top edge overlaps the top of the screen (needs to exceed the top margin).")]
    [SerializeField, Min(0f)] private float combatCameraDrop = 1.5f;
    [SerializeField, Min(0f)] private float playerMargin = 1.5f;
    [SerializeField, Min(.01f)] private float blendDuration = .6f;
    [SerializeField] private Vector3 explorationOffset = new Vector3(1.5f, 2.2f, -10f);
    [SerializeField] private bool explorationFixedY;
    [SerializeField] private bool clampExplorationX;
    [SerializeField] private Vector2 explorationXLimits = new Vector2(-18f, 18f);
    [Header("Cutscene focus")]
    [Tooltip("Orthographic size while zoomed in on a speaker, as a fraction of the normal size (smaller = closer).")]
    [SerializeField, Range(.2f, 1f)] private float focusZoom = .55f;
    [Tooltip("Seconds the zoom in, and again the zoom back out, takes.")]
    [SerializeField, Min(.05f)] private float focusSeconds = .6f;

    private readonly List<Behaviour> suspended = new List<Behaviour>();
    private Transform player;
    private bool controlling, fighting;
    private float explorationSize, explorationY, returnElapsed;
    private Vector3 returnPosition;
    private Quaternion explorationRotation;
    private float returnSize;
    private bool endingHold;
    private Vector3 endingPosition;
    private Quaternion endingRotation;
    private float endingSize;
    private Bounds configuredBounds;
    private bool hasConfiguredBounds;

    // Cutscene focus: a zoom on top of whatever pose the camera would have anyway, so zooming back out lands on
    // the camera as it is at that moment (combat view, or the ending hold).
    private Transform focusTarget;
    private float focusHeight, focusProgress, focusDirection;
    private bool focusApplied;
    // Moving the focus from one target to another while zoomed in: the view pans instead of jumping.
    private bool panning;
    private float panProgress;
    private Vector3 panFrom;
    private Vector3 basePosition;
    private float baseSize;

    public float FocusSeconds => focusSeconds;
    public bool CanFocus => controlling && view != null;

    /// <summary>
    /// Starts zooming in on the target (heightOffset above its position). Returns false without a camera.
    /// Already zoomed in on something else: the view stays zoomed and pans to the new target.
    /// </summary>
    public bool FocusOn(Transform target, float heightOffset)
    {
        if (target == null || !CanFocus) return false;
        if (focusTarget != null && focusDirection >= 0f && focusProgress > 0f)
        {
            panFrom = FocusPoint();
            panProgress = 0f;
            panning = true;
        }
        else panning = false;
        focusTarget = target;
        focusHeight = heightOffset;
        focusDirection = 1f;
        return true;
    }

    /// <summary>Starts zooming back out to the normal camera.</summary>
    public void ReleaseFocus()
    {
        if (focusTarget != null) focusDirection = -1f;
    }

    /// <summary>Drops the focus at once (boss disabled, scene torn down).</summary>
    public void ClearFocus()
    {
        if (focusApplied && view != null)
        {
            view.transform.position = basePosition;
            view.orthographicSize = baseSize;
        }
        focusApplied = false;
        focusTarget = null;
        panning = false;
        focusProgress = focusDirection = 0f;
    }

    public void ConfigureWorldBounds(Camera output, Bounds bounds)
    {
        if (output != null) view = output;
        configuredBounds = bounds;
        hasConfiguredBounds = bounds.size.x > 0f && bounds.size.y > 0f;
    }

    private bool TryGetArenaBounds(out Bounds bounds)
    {
        if (hasConfiguredBounds)
        {
            bounds = configuredBounds;
            return true;
        }
        if (cameraBounds != null)
        {
            bounds = cameraBounds.bounds;
            return true;
        }
        bounds = default;
        return false;
    }

    public bool TryGetStormCloudTop(Transform focus, out float topY)
    {
        if (!TryGetArenaBounds(out Bounds bounds))
        {
            topY = 0f;
            return false;
        }

        float centerY = bounds.center.y + combatVerticalOffset;
        float size = fixedCombatView ? GetFixedCombatSize(bounds) : combatSize;
        if (!fixedCombatView && focus != null)
            size = Mathf.Clamp(Mathf.Max(combatSize, Mathf.Abs(focus.position.y - centerY) + playerMargin),
                combatSize, Mathf.Max(combatSize, maximumCombatSize));
        topY = centerY + size - stormCloudTopMargin;
        return true;
    }

    public bool TryGetFixedCombatViewport(out Bounds viewport)
    {
        viewport = default;
        if (!fixedCombatView || !TryGetArenaBounds(out Bounds bounds)) return false;
        Camera output = view != null ? view : Camera.main;
        float aspect = Mathf.Max(.01f, output != null ? output.aspect : 16f / 9f);
        float size = GetFixedCombatSize(bounds);
        viewport = new Bounds(new Vector3(bounds.center.x,
            bounds.center.y + combatVerticalOffset - combatCameraDrop, 0f),
            new Vector3(size * aspect * 2f, size * 2f, 1f));
        return true;
    }

    public void HoldForEnding(Transform actor)
    {
        if (endingHold) return;
        if (view == null) view = Camera.main;
        if (view == null) return;

        if (!controlling && actor != null) Enter(actor);
        endingPosition = view.transform.position;
        endingRotation = view.transform.rotation;
        endingSize = view.orthographicSize;
        endingHold = true;
        controlling = true;
        fighting = false;
    }

    public void Enter(Transform actor)
    {
        if (view == null) view = Camera.main;
        if (view == null || !TryGetArenaBounds(out _) || actor == null) return;
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
        if (endingHold || !controlling || !fighting || view == null) return;
        fighting = false;
        returnElapsed = 0f;
        returnPosition = view.transform.position;
        returnSize = view.orthographicSize;
    }

    private void LateUpdate()
    {
        if (!controlling || view == null) return;
        // The focus is drawn on top of the camera's own pose each frame; undo it first so the pose logic below
        // keeps working from its own state rather than from the zoomed one.
        if (focusApplied)
        {
            view.transform.position = basePosition;
            view.orthographicSize = baseSize;
            focusApplied = false;
        }
        UpdateBasePose();
        ApplyFocus();
    }

    private void ApplyFocus()
    {
        if (focusTarget == null) return;
        focusProgress = Mathf.Clamp01(focusProgress + focusDirection * Time.unscaledDeltaTime / focusSeconds);
        if (focusDirection < 0f && focusProgress <= 0f)
        {
            focusTarget = null;
            focusDirection = 0f;
            return;
        }

        basePosition = view.transform.position;
        baseSize = view.orthographicSize;
        float weight = Mathf.SmoothStep(0f, 1f, focusProgress);
        float size = baseSize * focusZoom;
        Vector3 target = FocusPoint();
        if (TryGetArenaBounds(out Bounds bounds))
            target.x = ConfineX(target.x, bounds.min.x, bounds.max.x, size * view.aspect);
        view.transform.position = new Vector3(Mathf.Lerp(basePosition.x, target.x, weight),
            Mathf.Lerp(basePosition.y, target.y, weight), basePosition.z);
        view.orthographicSize = Mathf.Lerp(baseSize, size, weight);
        focusApplied = true;
    }

    // Where the zoomed view is centred: the target (plus height), or on its way there from the previous target.
    private Vector3 FocusPoint()
    {
        Vector3 point = focusTarget.position + Vector3.up * focusHeight;
        if (!panning) return point;
        panProgress = Mathf.Clamp01(panProgress + Time.unscaledDeltaTime / focusSeconds);
        if (panProgress >= 1f) panning = false;
        return Vector3.Lerp(panFrom, point, Mathf.SmoothStep(0f, 1f, panProgress));
    }

    private void UpdateBasePose()
    {
        if (endingHold)
        {
            view.transform.SetPositionAndRotation(endingPosition, endingRotation);
            view.orthographicSize = endingSize;
            return;
        }
        if (fighting && TryGetArenaBounds(out Bounds bounds) && (fixedCombatView || player != null))
        {
            float centerY = bounds.center.y + combatVerticalOffset;
            float size = fixedCombatView
                ? GetFixedCombatSize(bounds)
                : Mathf.Clamp(Mathf.Max(combatSize, Mathf.Abs(player.position.y - centerY) + playerMargin),
                    combatSize, Mathf.Max(combatSize, maximumCombatSize));
            float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 5f / blendDuration);
            view.orthographicSize = Mathf.Lerp(view.orthographicSize, size, blend);
            float x = fixedCombatView
                ? bounds.center.x
                : ConfineX(player.position.x, bounds.min.x, bounds.max.x, view.orthographicSize * view.aspect);
            Vector3 position = new Vector3(x, centerY - combatCameraDrop, view.transform.position.z);
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

    private float GetFixedCombatSize(Bounds bounds)
    {
        Camera output = view != null ? view : Camera.main;
        float aspect = output != null ? output.aspect : 16f / 9f;
        return Mathf.Max(combatSize, bounds.size.x / (2f * Mathf.Max(.01f, aspect)));
    }

    private void RestoreExploration()
    {
        // Restore virtual cameras before their output brain so tracking starts from the new player position.
        for (int i = suspended.Count - 1; i >= 0; i--)
            if (suspended[i] != null) suspended[i].enabled = true;
        suspended.Clear();
        controlling = fighting = false;
        endingHold = false;
    }

    private void OnDisable()
    {
        ClearFocus();
        if (controlling && view != null) view.orthographicSize = explorationSize;
        RestoreExploration();
    }
}
