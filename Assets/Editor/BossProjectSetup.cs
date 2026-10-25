using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Reproducible asset/scene setup. Never changes PlayerTEST or general-enemy assets.</summary>
public static class BossProjectSetup
{
    public const string SettingsPath = "Assets/Data/Boss/BossCombatSettings.asset";
    public const string BossPath = "Assets/Prefabs/Boss/Boss.prefab";
    public const string ScenePath = "Assets/Scenes/BossTest.unity";
    private const string Art = "Assets/Art/Boss";
    private const string Fx = "Assets/Prefabs/Boss/Effects";
    private static Material material;
    private static Sprite square, disc, cloud, slash, spikes, flame, wave;

    [MenuItem("Tools/Boss/Rebuild Boss assets and BossTest from PlayerTEST")]
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
        var projectile = Effect("BossProjectile", disc, new Color(1f, 0.83f, 0.3f, 1f), false, false);
        GameObject edit = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(projectile));
        var rigidbody = edit.AddComponent<Rigidbody2D>();
        rigidbody.bodyType = RigidbodyType2D.Kinematic; rigidbody.gravityScale = 0f;
        rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        edit.AddComponent<CircleCollider2D>().isTrigger = true;
        edit.AddComponent<BossAttackProjectile>();
        data.ProjectilePrefab = PrefabUtility.SaveAsPrefabAsset(edit, Fx + "/BossProjectile.prefab").GetComponent<BossAttackProjectile>();
        PrefabUtility.UnloadPrefabContents(edit);
        var waveVisual = Effect("BossGroundWave", wave, new Color(1f, 0.89f, 0.4f, 1f));
        edit = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(waveVisual));
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
        BuildScene();
        AssetDatabase.SaveAssets();
        Debug.Log("Boss assets, encounter prefab and BossTest are ready.");
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
        var root = new GameObject(white ? "WhiteCloud" : "BlackCloud"); root.layer = white ? 6 : 0;
        var renderer = root.AddComponent<SpriteRenderer>(); renderer.sprite = cloud; renderer.sharedMaterial = material;
        renderer.color = white ? new Color(.82f, .93f, 1f) : new Color(.22f, .25f, .39f); renderer.sortingOrder = 16;
        var collider = root.AddComponent<BoxCollider2D>(); collider.size = Vector2.one; collider.isTrigger = !white;
        if (white)
        {
            var effector = root.AddComponent<PlatformEffector2D>(); effector.useOneWay = true; effector.useOneWayGrouping = true; effector.surfaceArc = 170f;
            collider.usedByEffector = true;
        }
        // Continuous cyan top = solid one-way platform; broken purple top = pass-through hazard.
        if (white)
            Part(root.transform, "Solid landing surface", new Vector2(0f, .44f), new Vector2(1f, .12f), new Color(.5f, .93f, 1f), square, 17);
        else
        {
            for (int i = 0; i < 5; i++)
                Part(root.transform, "Broken crossing surface " + i, new Vector2(-.4f + i * .2f, .44f),
                    new Vector2(.13f, .12f), new Color(.85f, .55f, 1f), square, 17);
            var bolt = new GameObject("Storm mark").AddComponent<LineRenderer>(); bolt.transform.SetParent(root.transform, false);
            bolt.useWorldSpace = false; bolt.positionCount = 4;
            bolt.SetPositions(new[] { new Vector3(.01f,.25f),new Vector3(-.07f,-.02f),new Vector3(.06f,.01f),new Vector3(-.01f,-.28f) });
            bolt.startWidth = bolt.endWidth = .04f; bolt.sharedMaterial = material; bolt.sortingOrder = 18;
            bolt.startColor = bolt.endColor = new Color(1f, .72f, .45f);
        }
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
        Set(controller,"presentation",view);
        var hud = BuildHud(root.transform); Set(controller,"hud",hud);
        PrefabUtility.SaveAsPrefabAsset(root, BossPath); Object.DestroyImmediate(root);
    }

    [MenuItem("Tools/Boss/Update placeholder body (preserve BossTest scene)")]
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
            PrefabUtility.SaveAsPrefabAsset(root,BossPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("Boss placeholder updated to one rectangle; BossTest scene preserved.");
    }

    [MenuItem("Tools/Boss/Upgrade pattern UX (preserve BossTest scene)")]
    public static void UpgradePatternPresentation()
    {
        square = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/UnitSquare.png");
        material = AssetDatabase.LoadAssetAtPath<Material>(Art + "/BossUnlit.mat");
        cloud = PlatformSprite();
        var data = AssetDatabase.LoadAssetAtPath<BossCombatSettings>(SettingsPath);
        data.CloudSize = new Vector2(2.8f, .7f); data.CloudSpacing = 2f; data.SpikeWarning = 3f;
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
        Debug.Log("Cloud platforms, spike timing and pattern HUD upgraded; BossTest scene preserved.");
    }

    [MenuItem("Tools/Boss/Update gameplay and debug UI (preserve BossTest scene)")]
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
        Debug.Log("Enemy-style gameplay UI added; detailed HUD retained for debug mode. BossTest scene preserved.");
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
        var playerObject = new GameObject("Player gameplay HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        playerObject.transform.SetParent(group,false);
        var playerCanvas = playerObject.GetComponent<Canvas>(); playerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        playerCanvas.sortingOrder = 80; playerCanvas.enabled = false;
        playerObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        var playerPanel = Image("Player health panel",playerObject.transform,new Color(.84f,.84f,.84f,.94f));
        playerPanel.rectTransform.anchorMin = playerPanel.rectTransform.anchorMax = playerPanel.rectTransform.pivot = Vector2.zero;
        playerPanel.rectTransform.sizeDelta = new Vector2(228f,32f); playerPanel.rectTransform.anchoredPosition = new Vector2(24f,24f);
        var hpLabel = Label("HP label",playerPanel.transform,new Vector2(-91f,-16f),new Vector2(36f,28f),16);
        hpLabel.text = "HP"; hpLabel.color = new Color(.08f,.08f,.08f);
        var playerBackground = Image("Player health background",playerPanel.transform,new Color(.72f,.72f,.72f));
        Anchors(playerBackground.rectTransform,new Vector2(.24f,.34f),new Vector2(.93f,.66f));
        var playerFill = GaugeFill("Player health fill",playerBackground.transform);
        var playerState = Label("Player state",playerObject.transform,new Vector2(266f,40f),new Vector2(74f,26f),16);
        playerState.rectTransform.anchorMin = playerState.rectTransform.anchorMax = new Vector2(0f,0f);
        playerState.rectTransform.pivot = new Vector2(0f,.5f); playerState.alignment = TextAnchor.MiddleLeft;
        playerState.color = new Color(1f,.62f,.38f);
        var hud = parent.GetComponent<BossGameplayHud>(); if (hud == null) hud = parent.gameObject.AddComponent<BossGameplayHud>();
        Set(hud,"worldView",worldView); Set(hud,"playerCanvas",playerCanvas);
        Set(hud,"playerHealthFill",playerFill); Set(hud,"playerState",playerState);
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
        trigger.transform.localPosition = new Vector3(-5f,1f); trigger.AddComponent<BoxCollider2D>().size = new Vector2(18f,8f);
        trigger.GetComponent<BoxCollider2D>().isTrigger = true;
        var entry = trigger.AddComponent<BossEncounterTrigger>(); Set(entry,"boss",boss.GetComponent<BossCombatController>());
        PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/Boss/BossEncounter.prefab"); Object.DestroyImmediate(root);
    }
    private static void BuildScene()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/PlayerTEST.unity",OpenSceneMode.Single);
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player.GetComponent<PlayerHealth>() == null) player.AddComponent<PlayerHealth>();
        if (player.GetComponent<PlayerCombatEvents>() == null) player.AddComponent<PlayerCombatEvents>();
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == "Ground" || root.name == "CinemachineCamera") Object.DestroyImmediate(root);
        Camera camera = Camera.main;
        foreach (MonoBehaviour component in camera.GetComponents<MonoBehaviour>())
            if (component.GetType().Name == "CinemachineBrain") component.enabled = false;
        camera.transform.position = new Vector3(0f,1f,-10f); camera.orthographicSize = 10.5f; camera.backgroundColor = new Color(.075f,.1f,.17f);
        float floor = -3.35f;
        var arena = new GameObject("Arena (36 world units)");
        Solid(arena.transform,"Main ground",new Vector2(0f,floor-.5f),new Vector2(36f,1f));
        Solid(arena.transform,"Left wall",new Vector2(-18.25f,floor+4.5f),new Vector2(.5f,10f));
        Solid(arena.transform,"Right wall",new Vector2(18.25f,floor+4.5f),new Vector2(.5f,10f));
        var fixture = Solid(arena.transform,"Optional wall fixture (enable for blocking tests)",new Vector2(5f,floor+1.7f),new Vector2(.25f,3.4f));
        fixture.SetActive(false);
        var boss = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BossPath)); boss.transform.position = new Vector3(2f,floor+.95f);
        var bootstrap = new GameObject("BossTest tools").AddComponent<BossTestBootstrap>(); Set(bootstrap,"boss",boss.GetComponent<BossCombatController>());
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene,ScenePath);
    }
    private static GameObject Solid(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var renderer = Part(parent,name,position,size,new Color(.22f,.28f,.38f),square,0);
        renderer.gameObject.layer = 6; renderer.gameObject.AddComponent<BoxCollider2D>(); return renderer.gameObject;
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
