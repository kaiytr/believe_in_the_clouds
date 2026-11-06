using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class HubSceneryProjectSetup
{
    private const string DecorationRootName = "Hub Background Decoration";
    private const string BackgroundSkyPath = "Assets/PlatformerSet1/01 background.png";
    private const string BackgroundSilhouettePath = "Assets/PlatformerSet1/04 background.png";
    private const string TerrainSpritePath = "Assets/Art/LevelTiles/PlatformerTerrain.png";
    private const string UnlitMaterialPath = "Assets/Art/Boss/BossUnlit.mat";

    [MenuItem("Tools/Scene Decoration/Enrich Hub And Level Backgrounds")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            ApplyLobbyDecoration();
            ApplyShopDecoration();
            ApplyDesignedLevelBackgrounds();
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (previousSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }

        Debug.Log("Lobby, shop, and designed level backgrounds have been applied.");
    }

    private static void ApplyLobbyDecoration()
    {
        Scene scene = EditorSceneManager.OpenScene(ProjectScenes.Lobby, OpenSceneMode.Single);
        Transform decoration = GameObject.Find(DecorationRootName)?.transform;
        if (decoration == null)
        {
            decoration = CreateDecorationRoot();
            SpriteRenderer backdrop = GameObject.Find("Backdrop")?.GetComponent<SpriteRenderer>();
            if (backdrop != null)
                backdrop.sortingOrder = -50;

            Sprite sky = LoadSprite(BackgroundSkyPath);
            Sprite silhouette = LoadSprite(BackgroundSilhouettePath);
            Sprite pillar = LoadTerrainSprite("Pillar");
            Sprite window = LoadTerrainSprite("Window");

            AddRepeatedBackground(decoration, "Distant Mountains", sky, new Color(.82f, .69f, .61f, 1f), -40,
                new[] { -42f, -22f, -2f, 18f, 38f, 58f }, new Vector3(4.6995f, 8.3333f, 1f), 6f);
            AddRepeatedBackground(decoration, "Distant Forest", silhouette, new Color(.65f, .65f, .72f, .9f), -39,
                new[] { -42f, -22f, -2f, 18f, 38f, 58f }, new Vector3(4.6995f, 4.6875f, 1f), 3f);

            // Replace the old narrow placeholder bars with the same stone columns used in combat maps.
            foreach (Transform existing in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                         .Where(transform => transform.name.StartsWith("Background pillar ")))
            {
                SpriteRenderer renderer = existing.GetComponent<SpriteRenderer>();
                if (renderer == null)
                    continue;

                existing.position = new Vector3(existing.position.x, .25f, 0f);
                existing.localScale = new Vector3(1.65f, 2.3f, 1f);
                renderer.sprite = pillar;
                renderer.color = new Color(.64f, .6f, .63f, .82f);
                renderer.sortingOrder = -32;
            }

            foreach (float x in new[] { -18f, -6f, 6f, 18f, 30f })
                AddSprite(decoration, "Stone Window", window, new Vector3(x, 1.15f, 0f), Vector3.one,
                    new Color(.7f, .66f, .66f, .64f), -33);
        }

        AdjustLobbyBackgroundAndFloor(decoration);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void AdjustLobbyBackgroundAndFloor(Transform decoration)
    {
        foreach (Transform child in decoration)
        {
            Vector3 position = child.localPosition;
            if (child.name == "Distant Mountains")
                position.y = 6f;
            else if (child.name == "Distant Forest")
                position.y = 3f;
            else
                continue;
            child.localPosition = position;
        }

        GameObject floor = GameObject.Find("Platformer Floor");
        Tilemap tilemap = floor != null ? floor.GetComponentInChildren<Tilemap>() : null;
        if (tilemap != null)
        {
            int firstColumn = tilemap.origin.x;
            int columnCount = tilemap.size.x;
            const int existingBottomRow = -2;
            var bottomPattern = new TileBase[columnCount];
            for (int column = 0; column < columnCount; column++)
                bottomPattern[column] = tilemap.GetTile(new Vector3Int(firstColumn + column, existingBottomRow, 0));

            for (int row = 1; row <= 6; row++)
            {
                int y = existingBottomRow - row;
                for (int column = 0; column < columnCount; column++)
                {
                    if (bottomPattern[column] != null)
                        tilemap.SetTile(new Vector3Int(firstColumn + column, y, 0), bottomPattern[column]);
                }
            }

            TilemapRenderer tilemapRenderer = tilemap.GetComponent<TilemapRenderer>();
            if (tilemapRenderer != null)
                tilemapRenderer.sortingOrder = 1;
        }

        SpriteRenderer ground = GameObject.Find("Ground")?.GetComponent<SpriteRenderer>();
        if (ground != null)
            ground.sortingOrder = -1;
    }

    private static void ApplyDesignedLevelBackgrounds()
    {
        var scenePaths = new string[7];
        for (int level = 1; level <= 6; level++)
            scenePaths[level - 1] = ProjectScenes.CombatLevelPath(level);
        scenePaths[6] = ProjectScenes.Tutorial;

        foreach (string path in scenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            GameObject backdrop = GameObject.Find("Platformer Backdrop");
            if (backdrop == null)
            {
                Debug.LogWarning("Platformer Backdrop was not found in " + path);
                continue;
            }

            foreach (Transform child in backdrop.GetComponentsInChildren<Transform>(true))
            {
                Vector3 position = child.localPosition;
                if (child.name == "Sky")
                    position.y = 4f;
                else if (child.name == "Silhouette")
                    position.y = 3f;
                else
                    continue;

                child.localPosition = position;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static void ApplyShopDecoration()
    {
        Scene scene = EditorSceneManager.OpenScene(ProjectScenes.Shop, OpenSceneMode.Single);
        if (GameObject.Find(DecorationRootName) != null)
            return;

        Transform decoration = CreateDecorationRoot();
        Sprite sky = LoadSprite(BackgroundSkyPath);
        Sprite silhouette = LoadSprite(BackgroundSilhouettePath);
        Sprite pillar = LoadTerrainSprite("Pillar");
        Sprite window = LoadTerrainSprite("Window");

        AddSprite(decoration, "Distant Mountains", sky, Vector3.zero, new Vector3(4.6995f, 8.3333f, 1f),
            new Color(.82f, .69f, .61f, 1f), -40);
        AddSprite(decoration, "Distant Forest", silhouette, new Vector3(0f, -1f, 0f),
            new Vector3(4.6995f, 4.6875f, 1f), new Color(.65f, .65f, .72f, .9f), -39);

        foreach (float x in new[] { -7.5f, 7.5f })
            AddSprite(decoration, "Stone Column", pillar, new Vector3(x, .25f, 0f),
                new Vector3(1.45f, 2.15f, 1f), new Color(.64f, .6f, .63f, .82f), -32);

        foreach (float x in new[] { -6f, 0f, 6f })
            AddSprite(decoration, "Stone Window", window, new Vector3(x, 1.15f, 0f), Vector3.one,
                new Color(.7f, .66f, .66f, .64f), -33);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static Transform CreateDecorationRoot()
    {
        var root = new GameObject(DecorationRootName);
        return root.transform;
    }

    private static void AddRepeatedBackground(Transform parent, string name, Sprite sprite, Color color,
        int sortingOrder, float[] positions, Vector3 scale, float y)
    {
        foreach (float x in positions)
            AddSprite(parent, name, sprite, new Vector3(x, y, 0f), scale, color, sortingOrder);
    }

    private static GameObject AddSprite(Transform parent, string name, Sprite sprite, Vector3 position,
        Vector3 scale, Color color, int sortingOrder)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = position;
        gameObject.transform.localScale = scale;

        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return gameObject;
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            throw new System.InvalidOperationException("Could not load scene decoration sprite: " + path);
        return sprite;
    }

    private static Sprite LoadTerrainSprite(string name)
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(TerrainSpritePath)
            .OfType<Sprite>()
            .FirstOrDefault(candidate => candidate.name == name);
        if (sprite == null)
            throw new System.InvalidOperationException("Could not find PlatformerTerrain sprite: " + name);
        return sprite;
    }
}
