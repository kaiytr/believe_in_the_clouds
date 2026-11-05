using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class DangerIndicator : MonoBehaviour
{
    [SerializeField, Min(0f)] private float initialWarningAlpha = 0.12f;

    private Transform followTarget;
    private Vector2 followOffset;
    private float followAngle;
    private Vector2 indicatorSize;
    private Vector2 indicatorCenter;
    private bool followTargetEnabled;
    private SpriteRenderer fill;
    private SpriteRenderer sortingReference;
    private float fullAlpha = 0.3f;

    private void Awake()
    {
        fill = GetComponent<SpriteRenderer>();
        if (fill != null)
            fullAlpha = fill.color.a;
    }

    public void Setup(Vector2 center, float angle, Vector2 size)
    {
        followTargetEnabled = false;
        followTarget = null;
        indicatorSize = size;
        Present(center, angle, size);
        SetProgress(0f);
    }

    // Boss and ordinary enemy attacks share this shape presentation.
    public void Present(Vector2 center, float angle, Vector2 size)
    {
        followTargetEnabled = false;
        followTarget = null;
        indicatorCenter = center;
        indicatorSize = size;
        transform.position = center;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        transform.localScale = new Vector3(size.x, size.y, 1f);
    }

    // Follows an already-computed warning shape while keeping its locked offset and angle.
    public void SetupFollowing(Transform followTarget, Vector2 centerOffset, float angle, Vector2 size)
    {
        this.followTarget = followTarget;
        followOffset = centerOffset;
        followAngle = angle;
        indicatorSize = size;
        followTargetEnabled = true;
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

    public void SetSortingBehind(SpriteRenderer reference)
    {
        if (reference == null)
            return;

        sortingReference = reference;
        ApplySortingBehindReference();
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
        if (followTarget == null)
            return;

        indicatorCenter = (Vector2)followTarget.position + followOffset;
        transform.position = indicatorCenter;
        transform.rotation = Quaternion.Euler(0f, 0f, followAngle);
        transform.localScale = new Vector3(indicatorSize.x, indicatorSize.y, 1f);
    }

    private void ApplySortingBehindReference()
    {
        if (sortingReference == null)
            return;

        if (fill == null)
            fill = GetComponent<SpriteRenderer>();
        if (fill != null)
        {
            fill.sortingLayerID = sortingReference.sortingLayerID;
            fill.sortingOrder = sortingReference.sortingOrder - 2;
        }
    }
}
