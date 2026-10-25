using UnityEngine;

// Game object's doctrine subject identity.
// DoctrineManager reads this component from the event's SubjectObject
// and compares it with the doctrine's SubjectType.
// Fallback inference (no manual setup needed in any scene):
//   PlayerHealth -> Player, EnemyUnit -> Enemy (name contains "boss" -> Boss).
// Objects with neither are SubjectType.None and match only Subject == All.
public class DoctrineSubject : MonoBehaviour
{
    public SubjectType Identity = SubjectType.Enemy;

    public static SubjectType GetIdentity(GameObject go)
    {
        if (go == null)
        {
            return SubjectType.None;
        }

        if (go.TryGetComponent(out DoctrineSubject subject) && subject != null)
        {
            return subject.Identity;
        }

        if (go.GetComponent<PlayerHealth>() != null)
        {
            return SubjectType.Player;
        }

        EnemyUnit enemy = go.GetComponent<EnemyUnit>();
        if (enemy == null)
        {
            enemy = go.GetComponentInParent<EnemyUnit>();
        }

        if (enemy != null)
        {
            if (go.name.ToLowerInvariant().Contains("boss") ||
                enemy.name.ToLowerInvariant().Contains("boss"))
            {
                return SubjectType.Boss;
            }

            return SubjectType.Enemy;
        }

        return SubjectType.None;
    }
}
