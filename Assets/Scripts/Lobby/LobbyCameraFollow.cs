using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class LobbyCameraFollow : MonoBehaviour
{
    public Transform Target;
    public float MinX = -24f, MaxX = 32f;
    private float velocity;
    private Camera view;
    private void Awake() { view = GetComponent<Camera>(); }
    private void LateUpdate()
    {
        if (Target == null) return;
        float halfWidth = view.orthographicSize * view.aspect;
        float min = MinX + halfWidth, max = MaxX - halfWidth;
        float desired = min > max ? (MinX + MaxX) * .5f : Mathf.Clamp(Target.position.x + 2f, min, max);
        float x = Mathf.SmoothDamp(transform.position.x, desired, ref velocity, .15f, Mathf.Infinity, Time.unscaledDeltaTime);
        transform.position = new Vector3(x, 0, -10);
    }
}
