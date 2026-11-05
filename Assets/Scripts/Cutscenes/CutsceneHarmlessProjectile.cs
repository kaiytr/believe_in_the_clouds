using UnityEngine;

public static class CutsceneHarmlessProjectile
{
    /// <summary>
    /// Call only on a newly instantiated visual clone. Disables every physics/damage behavior
    /// while leaving its renderers in place, so the art can be animated without combat effects.
    /// </summary>
    public static bool Prepare(GameObject visualClone)
    {
        if (visualClone == null)
            return false;

        foreach (Collider2D collider in visualClone.GetComponentsInChildren<Collider2D>(true))
            collider.enabled = false;

        foreach (Rigidbody2D body in visualClone.GetComponentsInChildren<Rigidbody2D>(true))
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }

        foreach (MonoBehaviour behavior in visualClone.GetComponentsInChildren<MonoBehaviour>(true))
            behavior.enabled = false;

        return true;
    }
}
