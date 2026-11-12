using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class EnemyHitReactionPlayModeTests
{
    private GameObject player;
    private HitReactionMovementEnemy enemy;
    private PlayerAttack attack;
    private Rigidbody2D body;

    private static Object Asset(string path, Type type)
    {
        Type database = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.AssetDatabase", false))
            .First(t => t != null);
        return (Object)database.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(Type) })
            .Invoke(null, new object[] { path, type });
    }

    private static void Set(object target, string name, object value)
    {
        Type type = target.GetType();
        while (type != null)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) { field.SetValue(target, value); return; }
            type = type.BaseType;
        }
        throw new MissingFieldException(name);
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Time.timeScale = 1f;
        player = Object.Instantiate((GameObject)Asset("Assets/Prefabs/Player/Player.prefab", typeof(GameObject)));
        player.transform.position = Vector3.zero;
        PlayerMove movement = player.GetComponent<PlayerMove>();
        movement.enabled = false;
        player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
        attack = player.GetComponent<PlayerAttack>();
        attack.enabled = false;

        var root = new GameObject("Hit reaction movement enemy");
        root.SetActive(false);
        root.AddComponent<BoxCollider2D>();
        BeliefController belief = root.AddComponent<BeliefController>();
        Set(belief, "profileCatalog", Asset("Assets/Data/Belief/BeliefProfileCatalog.asset", typeof(BeliefProfileCatalog)));
        Set(belief, "beliefType", BeliefType.RearPosition);
        enemy = root.AddComponent<HitReactionMovementEnemy>();
        root.transform.position = Vector3.right;
        root.SetActive(true);
        enemy.Bind(movement);
        Set(enemy, "targetMovement", movement);
        body = root.GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        Physics2D.IgnoreCollision(enemy.BodyCollider, player.GetComponent<PlayerHealth>().BodyCollider);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1f;
        Object.Destroy(player);
        Object.Destroy(enemy.gameObject);
        yield return null;
    }

    private int GroundHit()
    {
        attack.TryGetComboStep(0, out PlayerAttack.ComboStep step);
        using var swing = new PlayerAttackContext(player);
        return (int)typeof(PlayerAttack).GetMethod("DoHit", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(attack, new object[] { swing, step, 1f });
    }

    [UnityTest]
    public IEnumerator GroundHitOverridesChasingThenRestoresIt()
    {
        yield return new WaitForFixedUpdate();
        Assert.That(body.linearVelocity.x, Is.LessThan(0f));
        Assert.That(GroundHit(), Is.EqualTo(1));
        Assert.That(enemy.IsMovementLocked, Is.True);
        for (int i = 0; i < 5; i++)
        {
            yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.x, Is.GreaterThan(0f), "추격 속도가 넉백을 상쇄하면 안 된다.");
        }
        yield return new WaitForSeconds(.5f);
        Assert.That(enemy.IsMovementLocked, Is.False);
        Assert.That(body.linearVelocity.x, Is.LessThan(0f));
    }

    [UnityTest]
    public IEnumerator AirHitStopsHorizontalMovementWithoutKnockingDownOrOut()
    {
        body.position = new Vector2(0f, -1.2f);
        enemy.transform.position = body.position;
        body.linearVelocity = new Vector2(-2f, -3f);
        Physics2D.SyncTransforms();
        using var swing = new PlayerAttackContext(player);
        int hits = (int)typeof(PlayerAttack).GetMethod("DoDownFanHit", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(attack, new object[] { swing, 1f });
        Assert.That(hits, Is.EqualTo(1));
        Assert.That(body.linearVelocity.x, Is.Zero);
        Assert.That(body.linearVelocity.y, Is.EqualTo(-3f), "공중 공격이 추가 수직 충격을 주면 안 된다.");
        float x = body.position.x;
        for (int i = 0; i < 5; i++)
        {
            yield return new WaitForFixedUpdate();
            Assert.That(body.position.x, Is.EqualTo(x).Within(.001f));
        }
        yield return new WaitForSeconds(.25f);
        Assert.That(enemy.IsMovementLocked, Is.False);
        Assert.That(body.linearVelocity.x, Is.LessThan(0f));
    }

    [UnityTest]
    public IEnumerator RepeatedStunRefreshesAndAttackUnlockCannotReleaseIt()
    {
        enemy.HitReaction.Apply(.3f, 2.8f, -1f);
        yield return new WaitForSeconds(.1f);
        enemy.HitReaction.Apply(.3f, 0f, -1f);
        enemy.LockMovement();
        enemy.UnlockMovement();
        Assert.That(enemy.IsMovementLocked, Is.True);
        Assert.That(body.linearVelocity.x, Is.Zero, "스턴 전용 타격은 이전 넉백도 멈춘다.");
        yield return new WaitForSeconds(.2f);
        Assert.That(enemy.IsMovementLocked, Is.True);
        yield return new WaitForSeconds(.16f);
        Assert.That(enemy.IsMovementLocked, Is.False, "경직 시간을 누적 합산하지 않는다.");
    }

    [UnityTest]
    public IEnumerator DisableAndDeathClearReactionsAndDeadEnemiesRejectHits()
    {
        enemy.HitReaction.Apply(.5f, 2.8f, -1f);
        enemy.enabled = false;
        Assert.That(enemy.HitReaction.IsMovementSuppressed, Is.False);
        enemy.enabled = true;
        enemy.HitReaction.Apply(.5f, 2.8f, -1f);
        enemy.TakeDamage(new DamageInfo(100000f, DamageSource.Doctrine, player));
        Assert.That(enemy.IsDead, Is.True);
        Assert.That(enemy.HitReaction.IsMovementSuppressed, Is.False);
        Assert.That(enemy.HitReaction.Apply(.5f, 2.8f, -1f), Is.False);
        yield return null;
    }
}

// Exercise the actual EnemyUnit physics/movement locks with a deterministic chasing input.
public sealed class HitReactionMovementEnemy : EnemyUnit
{
    public void Bind(PlayerMove player) { target = player.transform; maxHp = currentHp = 1000f; moveSpeedRatio = .32f; }
    protected override void Start() { }
    protected override void Update() => UpdateAI();
    protected override void UpdateAI() => SetHorizontalMove(-1f);
}
