using UnityEditor;
using UnityEngine;

public static class BossPresentationAssetSetup
{
    public const string StormCloudPath = "Assets/Design/Boss/BossStormCloud.png";
    public const string WhiteCloudPath = "Assets/Design/Boss/BossWhiteCloud.png";

    public static void Apply()
    {
        ConfigureTexture(StormCloudPath, 32f, new Vector4(24f, 8f, 24f, 8f));
        ConfigureTexture(WhiteCloudPath, 64f, new Vector4(12f, 8f, 12f, 8f));
        ConfigureCloud("Assets/Prefabs/Boss/Effects/BlackCloud.prefab", StormCloudPath);
        ConfigureCloud("Assets/Prefabs/Boss/Effects/WhiteCloud.prefab", WhiteCloudPath);
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureTexture(string path, float pixelsPerUnit, Vector4 border)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new System.InvalidOperationException("구름 이미지가 없습니다: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.spriteBorder = border;
        var spriteSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(spriteSettings);
        spriteSettings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(spriteSettings);
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
    }

    private static void ConfigureCloud(string prefabPath, string spritePath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
            Sprite sprite = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(spritePath))
                if (asset is Sprite candidate) { sprite = candidate; break; }
            if (sprite == null) throw new System.InvalidOperationException("구름 스프라이트 임포트가 완료되지 않았습니다: " + spritePath);
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.drawMode = spritePath == WhiteCloudPath ? SpriteDrawMode.Tiled : SpriteDrawMode.Sliced;
            renderer.size = Vector2.one;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
