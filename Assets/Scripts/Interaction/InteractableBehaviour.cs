using System;
using System.Collections.Generic;
using UnityEngine;

// 씬에 배치된 상호작용 대상의 공통 베이스.
// - 자동 등록/해제 (OnEnable/OnDisable)
// - 가장 가까운 대상 탐색
// - 거리 계산은 "중심점"이 아니라 "콜라이더 표면" 기준
//   (Shop처럼 큰 오브젝트는 중심까지의 거리로 판정하면 손이 닿지 않는다)
// - 상호작용 발생 시 Interacted 이벤트 발행 (UI/연출/구매 로직은 여기에 붙인다)
//
// 파생 클래스는 표시 정보(이름/문구/반경)와 OnInteract만 구현하면 된다.
public abstract class InteractableBehaviour : MonoBehaviour, IInteractable
{
    private static readonly List<InteractableBehaviour> All = new List<InteractableBehaviour>();

    [Header("상호작용")]
    [Tooltip("대상 표면에서 이 거리 안에 있으면 상호작용할 수 있다.")]
    [SerializeField, Min(0.1f)] protected float interactRadius = 2.5f;

    /// <summary>문구를 직접 지정하지 않았을 때의 기본값.</summary>
    public const string DefaultPrompt = "E : 상호작용";

    [Tooltip("안내 문구. 비워두면 파생 클래스가 만든다.")]
    [SerializeField] protected string promptText = DefaultPrompt;

    [Tooltip("로그/UI에 표시할 이름. 비워두면 오브젝트 이름을 쓴다.")]
    [SerializeField] protected string displayName = string.Empty;

    public virtual float InteractionRadius => interactRadius;

    public virtual string InteractionPrompt => promptText;

    public virtual string InteractionName =>
        string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;

    /// <summary>상호작용이 실제로 일어났을 때. (대상, 상호작용한 오브젝트)</summary>
    public static event Action<InteractableBehaviour, GameObject> Interacted;

    public static IReadOnlyList<InteractableBehaviour> Instances => All;

    protected virtual void OnEnable()
    {
        if (!All.Contains(this))
        {
            All.Add(this);
        }
    }

    protected virtual void OnDisable()
    {
        All.Remove(this);
    }

    /// <summary>
    /// 상점·재단처럼 통과 가능한 상호작용 지점의 루트 콜라이더를 Trigger로 만든다.
    /// 실제 구조물을 막히게 하려면 별도 자식 오브젝트에 물리 콜라이더를 둔다.
    /// </summary>
    protected void SetInteractionCollidersToTriggers()
    {
        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].isTrigger = true;
            }
        }
    }

    /// <summary>World gap between the top of the object's art and the bottom of its prompt.</summary>
    protected const float PromptArtGap = .15f;

    // Keep the prompt right above what the player sees. Trigger colliders are usually much taller than the
    // art, so the top of the sprites is used and the collider only as a fallback.
    // Return local coordinates for InteractionPrompt.Follow.
    protected Vector3 GetPromptOffsetAboveArt(float gap = PromptArtGap)
    {
        bool found = false;
        Bounds art = default;
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(false))
        {
            if (renderer == null || !renderer.enabled || renderer.sprite == null) continue;
            if (!found) { art = renderer.bounds; found = true; }
            else art.Encapsulate(renderer.bounds);
        }
        if (found)
            return transform.InverseTransformPoint(new Vector3(art.center.x, art.max.y + Mathf.Max(0, gap), transform.position.z));
        return GetPromptOffsetAboveCollider(gap);
    }

    // Centre of the visible art (falls back to the collider centre), in local coordinates.
    protected Vector3 GetPromptOffsetAtArtCenter()
    {
        bool found = false;
        Bounds art = default;
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(false))
        {
            if (renderer == null || !renderer.enabled || renderer.sprite == null) continue;
            if (!found) { art = renderer.bounds; found = true; }
            else art.Encapsulate(renderer.bounds);
        }
        if (!found)
        {
            Collider2D collider2D = GetComponent<Collider2D>() ?? GetComponentInChildren<Collider2D>();
            if (collider2D == null) return Vector3.zero;
            art = collider2D.bounds;
        }
        return transform.InverseTransformPoint(new Vector3(art.center.x, art.center.y, transform.position.z));
    }

    // Prompt above the collider top; the gap is in world units.
    protected Vector3 GetPromptOffsetAboveCollider(float gap)
    {
        Collider2D collider2D = GetComponent<Collider2D>() ?? GetComponentInChildren<Collider2D>();
        Collider collider3D = collider2D == null ? GetComponent<Collider>() ?? GetComponentInChildren<Collider>() : null;
        Bounds bounds = collider2D != null ? collider2D.bounds : collider3D != null ? collider3D.bounds : new Bounds(transform.position, Vector3.zero);
        return transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.max.y + Mathf.Max(0, gap), transform.position.z));
    }

    public virtual bool CanInteract(GameObject interactor)
    {
        return isActiveAndEnabled && interactor != null;
    }

    /// <summary>상호작용 수행. 조건이 맞지 않으면 아무 일도 하지 않는다.</summary>
    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        OnInteract(interactor);
        Interacted?.Invoke(this, interactor);
    }

    /// <summary>실제 동작. 파생 클래스에서 구현한다.</summary>
    protected abstract void OnInteract(GameObject interactor);

    /// <summary>
    /// 대상 표면까지의 거리. 콜라이더가 있으면 표면 기준, 없으면 중심 기준.
    /// 콜라이더 안쪽에 있으면 0.
    /// </summary>
    public float DistanceTo(Vector3 position)
    {
        if (TryGetSurfacePoint(position, out Vector3 surface))
        {
            return Vector2.Distance(position, surface);
        }

        return Vector2.Distance(position, transform.position);
    }

    public bool IsInRange(Vector3 position)
    {
        return DistanceTo(position) <= interactRadius;
    }

    private bool TryGetSurfacePoint(Vector3 position, out Vector3 point)
    {
        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D == null)
        {
            collider2D = GetComponentInChildren<Collider2D>();
        }

        if (collider2D != null && collider2D.enabled)
        {
            point = collider2D.bounds.ClosestPoint(position);
            return true;
        }

        Collider collider3D = GetComponent<Collider>();
        if (collider3D == null)
        {
            collider3D = GetComponentInChildren<Collider>();
        }

        if (collider3D != null && collider3D.enabled)
        {
            point = collider3D.bounds.ClosestPoint(position);
            return true;
        }

        point = transform.position;
        return false;
    }

    // 파괴된 항목을 정리하면서 가장 가까운 대상을 찾는다.
    // 후보가 없으면 nearest는 null, distance는 float.MaxValue.
    public static InteractableBehaviour FindNearest(Vector3 position, out float distance)
    {
        InteractableBehaviour nearest = null;
        distance = float.MaxValue;

        for (int i = All.Count - 1; i >= 0; i--)
        {
            InteractableBehaviour target = All[i];
            if (target == null)
            {
                All.RemoveAt(i);
                continue;
            }

            if (!target.isActiveAndEnabled)
            {
                continue;
            }

            float current = target.DistanceTo(position);
            if (current < distance)
            {
                distance = current;
                nearest = target;
            }
        }

        return nearest;
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.9f, 0.6f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, InteractionRadius);
    }
}
