using UnityEngine;

public class DangerIndicator : MonoBehaviour
{
    public void Setup(
        Vector2 center,
        float angle,
        Vector2 size,
        float duration
    )
    {
        transform.position = center;

        transform.rotation =
            Quaternion.Euler(
                0f,
                0f,
                angle
            );

        transform.localScale =
            new Vector3(
                size.x,
                size.y,
                1f
            );

        Destroy(
            gameObject,
            duration
        );
    }
}