using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Reproducible boss asset setup. The Boss scene itself is authored by hand and is never rebuilt here.</summary>
public static class BossProjectSetup
{
    public const string SettingsPath = "Assets/Data/Boss/BossCombatSettings.asset";
    public const string BossPath = "Assets/Prefabs/Boss/Boss.prefab";
    public const string ScenePath = ProjectScenes.Boss;
    private const string ShockwaveMaterialPath = "Assets/Art/Boss/WhiteGroundShockwave.mat";
    private const string EnemyProjectileSpritePath = "Assets/Art/Enemy/EnemyProjectile.png";
    private const string Art = "Assets/Art/Boss";
    private const string Fx = "Assets/Prefabs/Boss/Effects";
    private static Material material;
    private static Sprite square, disc, cloud, slash, spikes, flame, wave;

    [MenuItem("Tools/Boss/Rebuild Boss assets (Boss scene untouched)")]
    public static void Build()
    {
        Folder("Assets/Art"); Folder(Art); Folder("Assets/Data/Boss"); Folder(Fx);
        square = SpriteAsset("UnitSquare", (x, y) => true);
        disc = SpriteAsset("UnitDisc", (x, y) => x * x + y * y <= 0.25f);
        cloud = PlatformSprite();
        slash = SpriteAsset("Slash", (x, y) => Mathf.Abs(y - 0.7f * x) < 0.10f || Mathf.Abs(Mathf.Sqrt(x*x+y*y) - 0.42f) < 0.035f);
        spikes = SpriteAsset("Spikes", (x, y) => y < 0.5f - Mathf.Abs(Mathf.Repeat(x + 0.5f, 1f / 3f) - 1f / 6f) * 6f);
        flame = SpriteAsset("Flame", (x, y) => Mathf.Abs(x) < (0.5f - y) * 0.45f && y > -0.45f);
        wave = SpriteAsset("GroundWave", (x, y) => y < 0.22f + 0.22f * Mathf.Cos(x * 11f) && y > -0.5f);
        material = AssetDatabase.LoadAssetAtPath<Material>(Art + "/BossUnlit.mat");
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, Art + "/BossUnlit.mat");
        }
        var data = AssetDatabase.LoadAssetAtPath<BossCombatSettings>(SettingsPath);
        if (data == null) { data = ScriptableObject.CreateInstance<BossCombatSettings>(); AssetDatabase.CreateAsset(data, SettingsPath); }
        data.WarningPrefab = Effect("BossWarning", square, new Color(1f, 0f, 0f, 0.29803923f), true, false);
        data.SlashPrefab = Effect("BossSlash", slash, new Color(1f, 0.92f, 0.55f, 1f));
        data.DashPrefab = AfterimageEffect("BossDashTrail", square, new Color(1f, 0.86f, 0.45f, 0.3f));
        data.ImpactPrefab = Effect("BossImpact", disc, new Color(1f, 0.95f, 0.75f, 1f), false, false);
        Sprite enemyProjectileSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EnemyProjectileSpritePath);
        if (enemyProjectileSprite == null)
            throw new InvalidOperationException("Enemy projectile sprite is missing.");
        var projectile = Effect("BossProjectile", enemyProjectileSprite, Color.white, false, false);
        GameObject edit = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(projectile));
        var rigidbody = edit.AddComponent<Rigidbody2D>();
        rigidbody.bodyType = RigidbodyType2D.Kinematic; rigidbody.gravityScale = 0f;
        rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        edit.AddComponent<CircleCollider2D>().isTrigger = true;
        edit.AddComponent<BossAttackProjectile>();
        data.ProjectilePrefab = PrefabUtility.SaveAsPrefabAsset(edit, Fx + "/BossProjectile.prefab").GetComponent<BossAttackProjectile>();
        PrefabUtility.UnloadPrefabContents(edit);
        var waveVisual = Effect("BossGroundWave", wave, Color.white, false, false);
        edit = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(waveVisual));
        var shockwaveMaterial = AssetDatabase.LoadAssetAtPath<Material>(ShockwaveMaterialPath);
        if (shockwaveMaterial == null)
        {
            PrefabUtility.UnloadPrefabContents(edit);
            throw new InvalidOperationException("WhiteGroundShockwave shader material is missing.");
        }
        edit.GetComponent<SpriteRenderer>().sharedMaterial = shockwaveMaterial;
        edit.AddComponent<BossGroundWave>();
        data.WavePrefab = PrefabUtility.SaveAsPrefabAsset(edit, Fx + "/BossGroundWave.prefab").GetComponent<BossGroundWave>();
        PrefabUtility.UnloadPrefabContents(edit);
        var spikeVisual = Effect("BossSpike", spikes, new Color(1f, 0.72f, 0.25f, 1f));
        edit = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(spikeVisual));
        edit.AddComponent<BossTimedDamageHazard>();
        data.SpikePrefab = PrefabUtility.SaveAsPrefabAsset(edit, Fx + "/BossSpike.prefab").GetComponent<BossTimedDamageHazard>();
        PrefabUtility.UnloadPrefabContents(edit);
        data.WhiteCloudPrefab = CloudPrefab(true);
        data.BlackCloudPrefab = CloudPrefab(false);
        data.BurnSprite = flame; data.EffectMaterial = material;
        EditorUtility.SetDirty(data);
        BuildBoss(data);
        BuildEncounterPrefab();
        AssetDatabase.SaveAssets();
        Debug.Log("Boss assets and encounter prefab are ready; the Boss scene was not changed.");
    }

    [MenuItem("Tools/Boss/Update cloud lightning prefabs only")]
    public static void UpdateCloudLightningPrefabs()
    {
        cloud = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/Cloud.png");
        material = AssetDatabase.LoadAssetAtPath<Material>(Art + "/BossUnlit.mat");
        CloudPrefab(true); CloudPrefab(false);
        AssetDatabase.SaveAssets();
    }

    private static bool Circle(float x, float y, float cx, float cy, float radius) => (x-cx)*(x-cx)+(y-cy)*(y-cy) <= radius*radius;
    private static Sprite PlatformSprite() => SpriteAsset("Cloud", (x, y) =>
        y >= -.1f || Circle(x, y, -.3f, -.1f, .2f) || Circle(x, y, 0f, -.1f, .4f) || Circle(x, y, .3f, -.1f, .2f));
    private static Sprite SpriteAsset(string name, Func<float, float, bool> inside)
    {
        string path = Art + "/" + name + ".png";
        var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        var pixels = new Color32[128 * 128];
        for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            pixels[y*128+x] = inside((x+0.5f)/128f-0.5f, (y+0.5f)/128f-0.5f) ? new Color32(255,255,255,255) : new Color32(255,255,255,0);
        texture.SetPixels32(pixels); texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 128f;
        importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var spriteSettings = new TextureImporterSettings(); importer.ReadTextureSettings(spriteSettings);
        spriteSettings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(spriteSettings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static BossAttackVisual Effect(string name, Sprite sprite, Color color, bool warning = false, bool hasOutline = true)
    {
        // Keep the ordinary-enemy warning's flat sprite/material and DangerIndicator contract.
        GameObject root = warning ? (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/Common/DangerIndicator_Line.prefab")) : new GameObject(name);
        if (warning) PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        root.name = name;
        var renderer = root.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = root.AddComponent<SpriteRenderer>();
        if (!warning) { renderer.sprite = sprite; renderer.sharedMaterial = material; }
        renderer.color = color; renderer.sortingOrder = warning ? 20 : 24;
        var visual = root.AddComponent<BossAttackVisual>();
        Set(visual, "baseColor", color); Set(visual, "warning", warning);
        if (hasOutline)
        {
            var line = new GameObject("Visible damage boundary").AddComponent<LineRenderer>();
            line.transform.SetParent(root.transform, false); line.useWorldSpace = false; line.loop = true;
            line.positionCount = 4; line.SetPositions(new[] { new Vector3(-.5f,-.5f), new Vector3(-.5f,.5f), new Vector3(.5f,.5f), new Vector3(.5f,-.5f) });
            line.startWidth = line.endWidth = 0.035f; line.sharedMaterial = material;
            line.startColor = line.endColor = warning ? new Color(1f, .45f, .35f) : new Color(1f, .93f, .6f);
            line.sortingOrder = renderer.sortingOrder + 1;
            Set(visual, "outline", line);
        }
        var saved = PrefabUtility.SaveAsPrefabAsset(root, Fx + "/" + name + ".prefab").GetComponent<BossAttackVisual>();
        Object.DestroyImmediate(root);
        return saved;
    }

    private static CombatAfterimage AfterimageEffect(string name, Sprite sprite, Color color)
    {
        var root = new GameObject(name);
        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.color = color;
        renderer.sortingOrder = 24;

        var afterimage = root.AddComponent<CombatAfterimage>();
        Set(afterimage, "opacity", color.a);
        Set(afterimage, "lifetime", 0.16f);
        Set(afterimage, "tint", new Color(color.r, color.g, color.b, 1f));
        var saved = PrefabUtility.SaveAsPrefabAsset(root, Fx + "/" + name + ".prefab").GetComponent<CombatAfterimage>();
        Object.DestroyImmediate(root);
        return saved;
    }

    private static BossCloudPlatform CloudPrefab(bool white)
    {
        var root = new GameObject(white ? "WhiteCloud" : "BlackCloud");
        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(white ? BossPresentationAssetSetup.WhiteCloudPath : BossPresentationAssetSetup.StormCloudPath) ?? cloud;
        renderer.sharedMaterial = material;
        renderer.color = Color.white;
        renderer.drawMode = white ? SpriteDrawMode.Tiled : SpriteDrawMode.Sliced;
        renderer.size = Vector2.one;
        renderer.sortingOrder = 26;
        var collider = root.AddComponent<BoxCollider2D>(); collider.size = Vector2.one; collider.isTrigger = true;
        root.AddComponent<BossCloudPlatform>();
        var saved = PrefabUtility.SaveAsPrefabAsset(root, Fx + "/" + root.name + ".prefab").GetComponent<BossCloudPlatform>();
        Object.DestroyImmediate(root); return saved;
    }

    private static void BuildBoss(BossCombatSettings data)
    {
        var root = new GameObject("Boss");
        var body = root.AddComponent<Rigidbody2D>(); body.gravityScale = 4f; body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        root.AddComponent<CapsuleCollider2D>().size = new Vector2(.85f,1.9f);
        var controller = root.AddComponent<BossCombatController>();
        Set(controller, "settings", data); Set(controller, "attacks", root.AddComponent<BossAttackExecutor>());
        Set(controller, "hazards", root.AddComponent<BossHazardDirector>());
        var attackOrigin = new GameObject("AttackOrigin").transform; attackOrigin.SetParent(root.transform, false); attackOrigin.localPosition = new Vector3(.43f, 0f);
        var projectileOrigin = new GameObject("ProjectileOrigin").transform; projectileOrigin.SetParent(root.transform, false); projectileOrigin.localPosition = new Vector3(.7f,.3f);
        var speechOrigin = new GameObject("SpeechAnchor").transform; speechOrigin.SetParent(root.transform, false); speechOrigin.localPosition = new Vector3(0f,1.65f);
        Set(controller, "attackOrigin", attackOrigin); Set(controller, "projectileOrigin", projectileOrigin);
        var visualRoot = new GameObject("VisualRoot").transform; visualRoot.SetParent(root.transform, false);
        var bodyRenderer = Part(visualRoot, "Body", Vector2.zero, new Vector2(.85f,1.9f), Color.white, square, 10);
        var view = root.AddComponent<BossCombatView>(); Set(view,"visualRoot",visualRoot); Set(view,"bodyRenderer",bodyRenderer);
        ConfigureBossSprites(view, bodyRenderer);
        Set(controller,"presentation",view);
        var hud = BuildHud(root.transform); Set(controller,"hud",hud);
        PrefabUtility.SaveAsPrefabAsset(root, BossPath); Object.DestroyImmediate(root);
    }

    [MenuItem("Tools/Boss/Update placeholder body (Boss scene untouched)")]
    public static void UpdatePlaceholderBody()
    {
        square = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/UnitSquare.png");
        material = AssetDatabase.LoadAssetAtPath<Material>(Art + "/BossUnlit.mat");
        GameObject root = PrefabUtility.LoadPrefabContents(BossPath);
        try
        {
            Transform visualRoot = root.transform.Find("VisualRoot");
            for (int i = visualRoot.childCount - 1; i >= 0; i--) Object.DestroyImmediate(visualRoot.GetChild(i).gameObject);
            Transform aura = root.transform.Find("State aura");
            if (aura != null) Object.DestroyImmediate(aura.gameObject);
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one;
            var renderer = Part(visualRoot,"Body",Vector2.zero,root.GetComponent<CapsuleCollider2D>().size,Color.white,square,10);
            var view = root.GetComponent<BossCombatView>();
            Set(view,"visualRoot",visualRoot); Set(view,"bodyRenderer",renderer);
            ConfigureBossSprites(view, renderer);
            PrefabUtility.SaveAsPrefabAsset(root,BossPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("Boss body updated; Boss scene untouched.");
    }

    internal static void ConfigureBossSprites(BossCombatView view, SpriteRenderer renderer)
    {
        Sprite[] Frames(string name) => AssetDatabase.LoadAllAssetsAtPath("Assets/Design/Boss/" + name + ".png")
            .OfType<Sprite>().OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();

        Sprite[] idle = Frames("BossIdle");
        if (idle.Length == 0) return;

        Sprite[] attack = Frames("BossAttack");
        var data = new SerializedObject(view);
        void SetFrames(string field, Sprite[] frames)
        {
            SerializedProperty property = data.FindProperty(field);
            property.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        }

        SetFrames("idleFrames", idle);
        SetFrames("walkFrames", Frames("BossWalk"));
        SetFrames("jumpFrames", Frames("BossJump"));
        SetFrames("deathFrames", Frames("BossDeath"));
        SetFrames("attack1Frames", attack.Take(3).ToArray());
        SetFrames("attack2Frames", attack.Skip(3).Take(3).ToArray());
        SetFrames("attack3Frames", attack.Skip(6).Take(3).ToArray());
        SetFrames("downAttackFrames", Frames("BossDownAttack"));
        data.FindProperty("dashFrame").objectReferenceValue = Frames("BossDash").FirstOrDefault();
        data.ApplyModifiedPropertiesWithoutUndo();
        renderer.sprite = idle[0];
        renderer.transform.localScale = Vector3.one;
    }

    [MenuItem("Tools/Boss/Upgrade pattern UX (Boss scene untouched)")]
    public static void UpgradePatternPresentation()
    {
        square = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/UnitSquare.png");
        material = AssetDatabase.LoadAssetAtPath<Material>(Art + "/BossUnlit.mat");
        cloud = PlatformSprite();
        var data = AssetDatabase.LoadAssetAtPath<BossCombatSettings>(SettingsPath);
        data.WhiteCloudPrefab = CloudPrefab(true); data.BlackCloudPrefab = CloudPrefab(false);
        EditorUtility.SetDirty(data);
        GameObject root = PrefabUtility.LoadPrefabContents(BossPath);
        try
        {
            ClearHudHierarchy(root.transform);
            var hud = BuildHud(root.transform, root.GetComponent<BossCombatHud>());
            Set(root.GetComponent<BossCombatController>(), "hud", hud);
            PrefabUtility.SaveAsPrefabAsset(root, BossPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("Cloud lightning markers and pattern HUD upgraded; Boss scene untouched.");
    }

    [MenuItem("Tools/Boss/Update gameplay and debug UI (Boss scene untouched)")]
    public static void UpdateGameplayPresentation()
    {
        square = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/UnitSquare.png");
        GameObject root = PrefabUtility.LoadPrefabContents(BossPath);
        try
        {
            ClearHudHierarchy(root.transform);
            Set(root.GetComponent<BossCombatController>(), "hud", BuildHud(root.transform, root.GetComponent<BossCombatHud>()));
            PrefabUtility.SaveAsPrefabAsset(root, BossPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("Enemy-style gameplay UI added; detailed HUD retained for debug mode. Boss scene untouched.");
    }

    private static void ClearHudHierarchy(Transform parent)
    {
        foreach (string name in new[] { "Combat HUD", "Debug combat HUD", "Gameplay HUD" })
        {
            Transform child = parent.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }
    }

    private static BossCombatHud BuildHud(Transform parent, BossCombatHud existing = null)
    {
        var canvasObject = new GameObject("Debug combat HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(parent, false);
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f,900f); scaler.matchWidthOrHeight = .5f;
        var panel = Image("Boss HUD panel", canvasObject.transform, new Color(.035f,.045f,.08f,.92f));
        RectTransform r = panel.rectTransform; r.anchorMin = r.anchorMax = new Vector2(.5f,1f); r.pivot = new Vector2(.5f,1f);
        r.sizeDelta = new Vector2(1000f,230f); r.anchoredPosition = new Vector2(0f,-16f);
        var title = Label("Boss name HP and phase", r, new Vector2(0f,-22f), new Vector2(960f,30f), 20);
        var hp = Bar("Boss HP", r, -46f, new Color(.85f,.24f,.25f));
        var belief = Bar("Shared belief", r, -63f, new Color(.9f,.75f,.35f));
        var state = Label("Attack stage and countdown", r, new Vector2(0f,-90f), new Vector2(960f,32f), 24);
        var phrase = Label("Belief phrase", r, new Vector2(0f,-120f), new Vector2(960f,26f), 18);
        phrase.color = new Color(.72f, .78f, .86f);
        var hint = Label("How to respond", r, new Vector2(0f,-151f), new Vector2(960f,34f), 22);
        var progress = Bar("Time remaining", r, -181f, new Color(.95f,.88f,.65f));
        ((RectTransform)progress.transform.parent).sizeDelta = new Vector2(930f, 6f);
        var feedback = Label("Combat result", r, new Vector2(0f,-210f), new Vector2(960f,30f), 18);
        var player = Label("Player HP and burn", canvasObject.transform, new Vector2(0f,30f), new Vector2(850f,40f), 22);
        player.rectTransform.anchorMin = player.rectTransform.anchorMax = new Vector2(.5f,0f);
        var hud = existing != null ? existing : parent.gameObject.AddComponent<BossCombatHud>();
        Set(hud,"canvas",canvas); Set(hud,"hpFill",hp); Set(hud,"beliefFill",belief); Set(hud,"progressFill",progress);
        Set(hud,"title",title); Set(hud,"phrase",phrase); Set(hud,"state",state); Set(hud,"playerStatus",player);
        Set(hud,"hint",hint); Set(hud,"feedback",feedback);
        Set(hud,"gameplayHud",BuildGameplayHud(parent));
        canvasObject.SetActive(false);
        return hud;
    }

    private static BossGameplayHud BuildGameplayHud(Transform parent)
    {
        var group = new GameObject("Gameplay HUD").transform; group.SetParent(parent, false);
        var sharedPrefab = CommonCombatProjectSetup.EnsureHudPrefab();
        var worldView = ((GameObject)PrefabUtility.InstantiatePrefab(sharedPrefab.gameObject, group)).GetComponent<CombatantHudView>();
        var hud = parent.GetComponent<BossGameplayHud>(); if (hud == null) hud = parent.gameObject.AddComponent<BossGameplayHud>();
        Set(hud,"worldView",worldView);
        return hud;
    }

    private static void Anchors(RectTransform rect, Vector2 min, Vector2 max)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static Image GaugeFill(string name, Transform parent)
    {
        var fill = Image(name,parent,new Color(.4f,.4f,.4f)); Anchors(fill.rectTransform,Vector2.zero,Vector2.one);
        fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left; return fill;
    }
    private static Image Bar(string name, Transform parent, float y, Color color)
    {
        var background = Image(name + " background",parent,new Color(.18f,.2f,.27f));
        background.rectTransform.anchorMin = background.rectTransform.anchorMax = new Vector2(.5f,1f);
        background.rectTransform.sizeDelta = new Vector2(930f,12f); background.rectTransform.anchoredPosition = new Vector2(0f,y);
        var fill = Image(name + " fill",background.transform,color);
        fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; fill.fillOrigin = 0;
        return fill;
    }
    private static Image Image(string name, Transform parent, Color color)
    {
        var obj = new GameObject(name,typeof(RectTransform),typeof(Image)); obj.transform.SetParent(parent,false);
        var image = obj.GetComponent<Image>(); image.sprite = square; image.color = color; image.raycastTarget = false; return image;
    }
    private static Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize)
    {
        var obj = new GameObject(name,typeof(RectTransform),typeof(Text)); obj.transform.SetParent(parent,false);
        var text = obj.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter; text.color = new Color(.96f,.94f,.84f); text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f,1f);
        text.rectTransform.sizeDelta = size; text.rectTransform.anchoredPosition = position; return text;
    }

    private static SpriteRenderer Part(Transform parent, string name, Vector2 position, Vector2 size, Color color, Sprite sprite, int order)
    {
        var obj = new GameObject(name); obj.transform.SetParent(parent,false); obj.transform.localPosition = position;
        obj.transform.localScale = new Vector3(size.x,size.y,1f);
        var renderer = obj.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sharedMaterial = material;
        renderer.color = color; renderer.sortingOrder = order; return renderer;
    }
    private static void BuildEncounterPrefab()
    {
        var root = new GameObject("BossEncounter");
        var boss = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BossPath)); boss.transform.SetParent(root.transform,false);
        var trigger = new GameObject("Final area entry trigger"); trigger.transform.SetParent(root.transform,false);
        trigger.AddComponent<BoxCollider2D>();
        var entry = trigger.AddComponent<BossEncounterTrigger>();
        BossArenaProjectSetup.Configure(entry, boss.GetComponent<BossCombatController>(),
            BossStageSceneryProjectSetup.ArenaMinX, BossStageSceneryProjectSetup.ArenaMaxX, -3.35f, null);
        CutsceneSceneInstaller.ConfigureBossIntroduction(entry);
        PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/Boss/BossEncounter.prefab"); Object.DestroyImmediate(root);
    }
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0,path.LastIndexOf('/')); Folder(parent);
        AssetDatabase.CreateFolder(parent,path.Substring(path.LastIndexOf('/')+1));
    }
    private static void Set(Object target, string field, Object value)
    { var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Set(Object target, string field, float value)
    { var data = new SerializedObject(target); data.FindProperty(field).floatValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Set(Object target, string field, bool value)
    { var data = new SerializedObject(target); data.FindProperty(field).boolValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Set(Object target, string field, Color value)
    { var data = new SerializedObject(target); data.FindProperty(field).colorValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
}
