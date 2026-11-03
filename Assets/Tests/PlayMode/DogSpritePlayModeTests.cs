using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class DogSpritePlayModeTests
{
    private EconomyService previous;
    private DogEnemy dog;
    private DogChargeAttack attack;
    private Rigidbody2D body;
    private SpriteRenderer visual;
    private PlayerHealth player;

    [UnitySetUp] public IEnumerator SetUp()
    {
        Time.timeScale = 1f;
        EconomyRuntime.Ensure(); previous = Currency.Service;
        EconomyRuntime.Instance.SetService(new EconomyService(new MemoryEconomyStore(), new EconomyRandom(5)));
        yield return SceneManager.LoadSceneAsync("DogCombatTest");
        yield return null;
        dog = Object.FindFirstObjectByType<DogEnemy>();
        Assert.That(dog, Is.Not.Null); Assert.That(dog.HasCombatTarget, Is.True);
        dog.enabled = false;
        // Isolate the charge's damage from the separate contact-damage component.
        foreach (var contact in dog.GetComponentsInChildren<EnemyContactDamage>(true)) contact.SetSuppressed(true);
        attack = dog.GetComponent<DogChargeAttack>();
        body = dog.GetComponent<Rigidbody2D>(); body.gravityScale = 0f; body.linearVelocity = Vector2.zero;
        visual = dog.GetComponentInChildren<DogSpriteAnimator>().GetComponent<SpriteRenderer>();
        player = Object.FindFirstObjectByType<PlayerHealth>();
        player.GetComponent<PlayerMove>().enabled = false;
        player.GetComponent<PlayerAttack>().enabled = false;
        player.GetComponent<PlayerDash>().enabled = false;
        var playerBody = player.GetComponent<Rigidbody2D>(); playerBody.bodyType = RigidbodyType2D.Kinematic;
        playerBody.linearVelocity = Vector2.zero;
        var ground = Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None).First(collider => collider.name == "Ground");
        body.position = new Vector2(0f, ground.bounds.max.y + dog.BodyCollider.bounds.extents.y + .01f);
        playerBody.position = body.position + Vector2.right * 6f;
        dog.transform.position = body.position; player.transform.position = playerBody.position;
        dog.ChangeState(EnemyState.Idle); dog.SetFacingDirection(1f);
        Physics2D.SyncTransforms();
        yield return null;
    }

    [UnityTearDown] public IEnumerator TearDown()
    {
        Time.timeScale = 1f;
        if (dog != null) attack.enabled = false;
        EconomyRuntime.Instance.SetService(previous);
        yield return null;
    }

    private static IEnumerator Until(Func<bool> condition, float timeout = 5f)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, "Dog sprite state timeout");
    }

    [UnityTest] public IEnumerator IdleWalkPauseAndFacingUseTheImportedDogFrames()
    {
        Assert.That(visual.sprite.name, Does.StartWith("DogIdle_"));
        Sprite first = visual.sprite;
        yield return new WaitForSeconds(.25f);
        Assert.That(visual.sprite, Is.Not.SameAs(first));
        Capture("dog-idle");
        body.linearVelocity = Vector2.right * .5f; dog.ChangeState(EnemyState.Chase);
        yield return new WaitForSeconds(.15f);
        Assert.That(visual.sprite.name, Does.StartWith("DogWalk_"));
        Capture("dog-walk");
        Time.timeScale = 0f; Sprite paused = visual.sprite;
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(visual.sprite, Is.SameAs(paused));
        dog.SetFacingDirection(-1f);
        Assert.That(dog.FacingDirection, Is.EqualTo(-1f));
        Assert.That(visual.flipX, Is.True); Assert.That(visual.transform.lossyScale.x, Is.LessThan(0f));
        Capture("dog-facing-left");
        dog.SetFacingDirection(1f);
        Assert.That(visual.transform.lossyScale.x, Is.GreaterThan(0f));
    }

    [UnityTest] public IEnumerator ChargeKeepsItsHitboxAndUsesWindupDashRecoveryAndDogAfterimages()
    {
        var capsule = dog.GetComponent<CapsuleCollider2D>();
        Vector2 size = capsule.size;
        player.GetComponent<Rigidbody2D>().position = body.position + Vector2.right * 2.5f;
        player.transform.position = player.GetComponent<Rigidbody2D>().position;
        Physics2D.SyncTransforms();
        Assert.That(attack.TryAttack(player.transform), Is.True);
        yield return null;
        Assert.That(visual.sprite.name, Does.StartWith("DogAttack_"));
        Capture("dog-windup");
        yield return Until(() => attack.IsCharging);
        yield return null;
        Assert.That(new[] { "DogAttack_03", "DogAttack_04" }, Does.Contain(visual.sprite.name));
        var afterimages = Object.FindObjectsByType<CombatAfterimage>(FindObjectsSortMode.None);
        Assert.That(afterimages.Length, Is.GreaterThan(0));
        foreach (var image in afterimages)
            Assert.That(image.GetComponent<SpriteRenderer>().sprite.name, Does.StartWith("DogAttack_"));
        Capture("dog-charge");
        yield return Until(() => dog.CurrentState == EnemyState.Recovery);
        yield return null;
        Assert.That(visual.sprite.name, Is.EqualTo("DogAttack_05"));
        Assert.That(capsule.size, Is.EqualTo(size));
        Assert.That(attack.ChargeTriggerRange, Is.EqualTo(4f));
        Assert.That(player.CurrentHp, Is.EqualTo(85f), "The visual replacement must keep the existing single 15-damage charge.");
    }

    [UnityTest] public IEnumerator DeathShowsTheSingleCorpseImageAndKeepsCoinReleaseAtCorpseRemoval()
    {
        CoinDropOrigin origin = null;
        Currency.Service.FlightRequested += request => origin = request.DropOrigin;
        Assert.That(dog.TakeDamage(new DamageInfo(10000, DamageSource.Environment)), Is.True);
        yield return null;
        Assert.That(visual.sprite.name, Is.EqualTo("DogDeath_00"));
        Assert.That(origin, Is.Not.Null); Assert.That(origin.IsReady, Is.False);
        Capture("dog-death");
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(dog != null, Is.True); Assert.That(origin.IsReady, Is.False);
        Assert.That(visual.sprite.name, Is.EqualTo("DogDeath_00"));
        Time.timeScale = 1f;
        yield return Until(() => dog == null);
        Assert.That(origin.IsReady, Is.True);
    }

    private void Capture(string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Camera camera = Camera.main;
        Vector3 position = camera.transform.position;
        float size = camera.orthographicSize;
        RenderTexture previousTarget = camera.targetTexture, previousActive = RenderTexture.active;
        var target = new RenderTexture(1024, 576, 24);
        var image = new Texture2D(1024, 576, TextureFormat.RGB24, false);
        try
        {
            camera.transform.position = new Vector3(dog.transform.position.x + .5f, dog.BodyCollider.bounds.min.y + .8f, position.z);
            camera.orthographicSize = 1.8f;
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1024, 576), 0, 0); image.Apply();
            string directory = Path.GetFullPath("Logs/DogVisualVerification"); Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget; camera.transform.position = position; camera.orthographicSize = size;
            RenderTexture.active = previousActive;
            Object.Destroy(target); Object.Destroy(image);
        }
    }
}
