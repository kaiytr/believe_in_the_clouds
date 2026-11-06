using UnityEngine;

/// <summary>Slanted rain for the ending: a thin far layer behind the ground, a heavier near layer in front, and small splashes on the ground line.</summary>
public static class EndingRain
{
    public static GameObject Build(Transform parent, float groundY, float viewTop, float halfWidth)
    {
        var root = new GameObject("Rain");
        root.transform.SetParent(parent, false);

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave, name = "Rain pixel" };
        texture.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) });
        texture.Apply(false, true);
        var material = new Material(Shader.Find("Sprites/Default")) { mainTexture = texture, hideFlags = HideFlags.DontSave, name = "Rain" };
        root.AddComponent<RainResources>().Own(texture, material);

        float width = halfWidth * 2f + 8f;
        // far: dim and thin, behind the ground; near: bright and fast, over everything.
        Layer(root.transform, "Far rain", material, -5, groundY, viewTop, width, rate: 150f, speed: new Vector2(11f, 14f), size: .03f,
            color: new Color(1f, .82f, .78f, .35f), stretch: .035f, lifetime: .88f);
        Layer(root.transform, "Near rain", material, 40, groundY, viewTop, width, rate: 70f, speed: new Vector2(17f, 22f), size: .045f,
            color: new Color(1f, .9f, .86f, .6f), stretch: .03f, lifetime: .51f);
        Splashes(root.transform, material, groundY, width);
        return root;
    }

    private static void Layer(Transform parent, string name, Material material, int order, float groundY, float viewTop,
        float width, float rate, Vector2 speed, float size, Color color, float stretch, float lifetime)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(0f, viewTop + 1.5f, 0f);
        var particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particles.main;
        main.loop = true; main.playOnAwake = false; main.prewarm = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .95f, lifetime * 1.05f);
        main.startSpeed = 0f;
        main.startSize = size;
        main.startColor = color;
        main.maxParticles = 600;
        var emission = particles.emission;
        emission.rateOverTime = rate;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(width, 1f, 1f);
        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-speed.x * .22f, -speed.x * .16f);
        velocity.y = new ParticleSystem.MinMaxCurve(-speed.y, -speed.x);
        velocity.z = 0f;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = stretch;
        renderer.lengthScale = 1f;
        renderer.sortingOrder = order;
        particles.Play();
    }

    private static void Splashes(Transform parent, Material material, float groundY, float width)
    {
        var go = new GameObject("Splashes");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(0f, groundY + .04f, 0f);
        var particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = true; main.playOnAwake = false; main.prewarm = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = .18f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(.07f, .12f);
        main.startColor = new Color(1f, .9f, .85f, .55f);
        main.maxParticles = 200;
        var emission = particles.emission;
        emission.rateOverTime = 55f;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(width, .02f, 1f);
        var fade = particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .6f), new Keyframe(1f, 1.6f)));
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = 41;
        particles.Play();
    }

    private sealed class RainResources : MonoBehaviour
    {
        private Texture2D texture;
        private Material material;
        public void Own(Texture2D ownedTexture, Material ownedMaterial) { texture = ownedTexture; material = ownedMaterial; }
        private void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (texture != null) Destroy(texture);
        }
    }
}
