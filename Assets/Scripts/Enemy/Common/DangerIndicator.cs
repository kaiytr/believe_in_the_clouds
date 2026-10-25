using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class DangerIndicator : MonoBehaviour
{
    [SerializeField, Min(0f)] private float initialWarningAlpha = 0.12f;
    [SerializeField] private Color directionColor = new Color(1f, 0.88f, 0.65f, 1f);
    [SerializeField, Min(0.001f)] private float directionWidth = 0.055f;
    [SerializeField, Min(0)] private int directionSortingOffset = 2;

    private Transform followTarget;
    private Vector2 lockedDirection;
    private Vector2 indicatorSize;
    private Vector2 indicatorCenter;
    private Vector2 markerDirection;
    private bool followTargetEnabled;
    private bool hasDirection;
    private SpriteRenderer fill;
    private LineRenderer directionMarker;
    private float fullAlpha = 0.3f;
    private readonly Vector3[] directionPoints = new Vector3[5];

    private void Awake()
    {
        fill = GetComponent<SpriteRenderer>();
        if (fill != null)
            fullAlpha = fill.color.a;
    }

    public void Setup(Vector2 center, float angle, Vector2 size)
    {
        followTargetEnabled = false;
        indicatorSize = size;
        Present(center, angle, size);
        SetProgress(0f);
    }

    // Boss and ordinary enemy attacks share this shape presentation.
    public void Present(Vector2 center, float angle, Vector2 size)
    {
        followTargetEnabled = false;
        indicatorCenter = center;
        indicatorSize = size;
        transform.position = center;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        transform.localScale = new Vector3(size.x, size.y, 1f);
        UpdateDirectionMarker();
    }

    public void SetupFollowing(Transform followTarget, Vector2 direction, Vector2 size)
    {
        this.followTarget = followTarget;
        lockedDirection = direction.normalized;
        indicatorSize = size;
        followTargetEnabled = true;
        SetDirection(lockedDirection);
        UpdateFollowPosition();
        SetProgress(0f);
    }

    public void SetProgress(float progress)
    {
        if (this == null)
            return;

        if (fill == null)
            fill = GetComponent<SpriteRenderer>();
        if (fill == null)
            return;

        Color color = fill.color;
        color.a = Mathf.Lerp(Mathf.Min(initialWarningAlpha, fullAlpha), fullAlpha, Mathf.Clamp01(progress));
        fill.color = color;
    }

    public void SetDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            hasDirection = false;
            if (directionMarker != null)
                directionMarker.enabled = false;
            return;
        }

        markerDirection = direction.normalized;
        hasDirection = true;
        EnsureDirectionMarker();
        UpdateDirectionMarker();
    }

    public void Cancel()
    {
        followTargetEnabled = false;
        if (gameObject != null)
            Destroy(gameObject);
    }

    private void LateUpdate()
    {
        if (!followTargetEnabled)
            return;

        if (followTarget == null)
        {
            Destroy(gameObject);
            return;
        }

        UpdateFollowPosition();
    }

    private void UpdateFollowPosition()
    {
        Vector2 origin = followTarget.position;
        indicatorCenter = origin + lockedDirection * (indicatorSize.x * 0.5f);
        float angle = Mathf.Atan2(lockedDirection.y, lockedDirection.x) * Mathf.Rad2Deg;
        transform.position = indicatorCenter;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        transform.localScale = new Vector3(indicatorSize.x, indicatorSize.y, 1f);
        UpdateDirectionMarker();
    }

    private void EnsureDirectionMarker()
    {
        if (directionMarker != null)
        {
            directionMarker.enabled = true;
            return;
        }

        var markerObject = new GameObject("Attack direction");
        markerObject.transform.SetParent(transform, false);
        directionMarker = markerObject.AddComponent<LineRenderer>();
        directionMarker.useWorldSpace = true;
        directionMarker.sharedMaterial = fill != null ? fill.sharedMaterial : null;
        directionMarker.startColor = directionMarker.endColor = directionColor;
        directionMarker.startWidth = directionMarker.endWidth = directionWidth;
        directionMarker.sortingLayerID = fill != null ? fill.sortingLayerID : 0;
        directionMarker.sortingOrder = (fill != null ? fill.sortingOrder : 0) + directionSortingOffset;
        directionMarker.positionCount = 5;
    }

    private void UpdateDirectionMarker()
    {
        if (!hasDirection || directionMarker == null)
            return;

        directionMarker.enabled = true;
        float length = Mathf.Min(0.3f, indicatorSize.x * 0.25f);
        Vector2 tip = indicatorCenter + markerDirection * length;
        Vector2 side = new Vector2(-markerDirection.y, markerDirection.x);
        float head = Mathf.Min(0.18f, indicatorSize.y * 0.35f);
        directionPoints[0] = indicatorCenter - markerDirection * length;
        directionPoints[1] = tip;
        directionPoints[2] = tip - markerDirection * head + side * head;
        directionPoints[3] = tip;
        directionPoints[4] = tip - markerDirection * head - side * head;
        directionMarker.SetPositions(directionPoints);
    }
}
