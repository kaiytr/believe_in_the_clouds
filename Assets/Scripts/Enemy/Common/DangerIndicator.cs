using UnityEngine;

public class DangerIndicator : MonoBehaviour
{
    private Transform followTarget;
    private Vector2 lockedDirection;
    private Vector2 indicatorSize;
    private bool followTargetEnabled;

    public void Setup(Vector2 center, float angle, Vector2 size, float duration)
    {
        followTargetEnabled = false;
        transform.position = center;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        transform.localScale = new Vector3(size.x, size.y, 1f);
        Destroy(gameObject, duration);
    }

    public void SetupFollowing(Transform followTarget, Vector2 direction, Vector2 size, float duration)
    {
        this.followTarget = followTarget;
        lockedDirection = direction.normalized;
        indicatorSize = size;
        followTargetEnabled = true;
        UpdateFollowPosition();
        Destroy(gameObject, duration);
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
        Vector2 center = origin + lockedDirection * (indicatorSize.x * 0.5f);
        float angle = Mathf.Atan2(lockedDirection.y, lockedDirection.x) * Mathf.Rad2Deg;
        transform.position = center;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        transform.localScale = new Vector3(indicatorSize.x, indicatorSize.y, 1f);
    }
}
