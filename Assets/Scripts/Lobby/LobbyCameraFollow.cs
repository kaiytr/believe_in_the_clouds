using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class LobbyCameraFollow : MonoBehaviour
{
    public Transform Target;
    public float MinX = -24f, MaxX = 32f;
    public bool FollowVertical;
    public float MinY = 0f, MaxY = 8f;
    private float velocity;
    private float verticalVelocity;
    private Camera view;
    private void Awake() { view = GetComponent<Camera>(); }
    private void LateUpdate()
    {
        if (Target == null) return;
        float halfWidth = view.orthographicSize * view.aspect;
        float min = MinX + halfWidth, max = MaxX - halfWidth;
        float desired = min > max ? (MinX + MaxX) * .5f : Mathf.Clamp(Target.position.x + 2f, min, max);
        float x = Mathf.SmoothDamp(transform.position.x, desired, ref velocity, .15f, Mathf.Infinity, Time.unscaledDeltaTime);
        float y = FollowVertical
            ? Mathf.SmoothDamp(transform.position.y, Mathf.Clamp(Target.position.y + 1f, MinY, MaxY),
                ref verticalVelocity, .15f, Mathf.Infinity, Time.unscaledDeltaTime)
            : 0f;
        transform.position = new Vector3(x, y, -10);
    }
}
