using NUnit.Framework;
using UnityEngine;

// 상호작용 거리 판정 테스트.
// Shop처럼 큰 콜라이더를 가진 대상은 "중심까지의 거리"가 아니라
// "표면까지의 거리"로 판정해야 손이 닿는다. 그 회귀를 막는다.
public class InteractionRangeTests
{
    private GameObject target;
    private GameObject player;

    [TearDown]
    public void TearDown()
    {
        if (target != null)
        {
            Object.DestroyImmediate(target);
        }

        if (player != null)
        {
            Object.DestroyImmediate(player);
        }
    }

    // 콜라이더가 없는 대상은 중심 기준 거리로 판정한다.
    [Test]
    public void DistanceTo_WithoutCollider_UsesCenter()
    {
        target = new GameObject("Target");
        target.transform.position = Vector3.zero;
        TestInteractable interactable = target.AddComponent<TestInteractable>();

        Assert.AreEqual(3f, interactable.DistanceTo(new Vector3(3f, 0f, 0f)), 0.01f);
        Assert.IsFalse(interactable.IsInRange(new Vector3(3f, 0f, 0f)));
    }

    // 큰 콜라이더를 가진 대상은 표면 기준이라, 중심에서 멀어도 닿는다.
    [Test]
    public void DistanceTo_WithLargeCollider_UsesSurface()
    {
        target = new GameObject("Shop");
        target.transform.position = Vector3.zero;
        BoxCollider2D box = target.AddComponent<BoxCollider2D>();
        box.size = new Vector2(8f, 2f); // 표면은 x = ±4
        TestInteractable interactable = target.AddComponent<TestInteractable>();

        // 중심에서 5m 떨어진 점 = 표면에서 1m
        Assert.AreEqual(1f, interactable.DistanceTo(new Vector3(5f, 0f, 0f)), 0.01f);
        Assert.IsTrue(interactable.IsInRange(new Vector3(5f, 0f, 0f)));

        // 중심 기준으로는 5m라 예전 방식이면 실패했을 위치
        Assert.Greater(Vector2.Distance(new Vector2(5f, 0f), target.transform.position),
            interactable.InteractionRadius);
    }

    // 콜라이더 안쪽에 있으면 거리 0.
    [Test]
    public void DistanceTo_InsideCollider_IsZero()
    {
        target = new GameObject("Shop");
        target.transform.position = Vector3.zero;
        BoxCollider2D box = target.AddComponent<BoxCollider2D>();
        box.size = new Vector2(8f, 2f);
        TestInteractable interactable = target.AddComponent<TestInteractable>();

        Assert.AreEqual(0f, interactable.DistanceTo(new Vector3(1f, 0f, 0f)), 0.01f);
        Assert.IsTrue(interactable.IsInRange(new Vector3(1f, 0f, 0f)));
    }

    private class TestInteractable : InteractableBehaviour
    {
        protected override void OnInteract(GameObject interactor)
        {
        }
    }
}
