using UnityEditor;
using UnityEngine;

// Shared authoring for BossTest, StageTest and the reusable encounter prefab.
public static class BossArenaProjectSetup
{
    public static void Configure(BossEncounterTrigger entry, BossCombatController boss, float minX, float maxX,
        float floorY, Camera view, bool fixedExplorationCamera = false)
    {
        entry.transform.position = new Vector3((minX + maxX) * .5f, 2f, 0f);
        BoxCollider2D trigger = entry.GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(maxX - minX, 16f);

        Collider2D left = Gate(entry.transform, "Entrance seal", minX - .25f, floorY + 4.5f);
        Collider2D right = Gate(entry.transform, "Far seal", maxX + .25f, floorY + 4.5f);
        GameObject boundsObject = Child(entry.transform, "Camera bounds");
        boundsObject.transform.position = new Vector3((minX + maxX) * .5f, 1f, 0f);
        BoxCollider2D bounds = boundsObject.GetComponent<BoxCollider2D>();
        if (bounds == null) bounds = boundsObject.AddComponent<BoxCollider2D>();
        bounds.isTrigger = true;
        bounds.size = new Vector2(maxX - minX, 20f);

        BossArenaCamera camera = entry.GetComponent<BossArenaCamera>();
        if (camera == null) camera = entry.gameObject.AddComponent<BossArenaCamera>();
        var cameraData = new SerializedObject(camera);
        cameraData.FindProperty("view").objectReferenceValue = view;
        cameraData.FindProperty("cameraBounds").objectReferenceValue = bounds;
        cameraData.FindProperty("combatSize").floatValue = 8f;
        cameraData.FindProperty("maximumCombatSize").floatValue = 10.5f;
        cameraData.FindProperty("explorationOffset").vector3Value = fixedExplorationCamera
            ? new Vector3(2f, 0f, -10f) : new Vector3(1.5f, 2.2f, -10f);
        cameraData.FindProperty("explorationFixedY").boolValue = fixedExplorationCamera;
        cameraData.FindProperty("clampExplorationX").boolValue = fixedExplorationCamera;
        cameraData.FindProperty("explorationXLimits").vector2Value = new Vector2(-18f, 18f);
        cameraData.ApplyModifiedPropertiesWithoutUndo();

        var entryData = new SerializedObject(entry);
        entryData.FindProperty("boss").objectReferenceValue = boss;
        entryData.FindProperty("arenaCamera").objectReferenceValue = camera;
        SerializedProperty gates = entryData.FindProperty("gates");
        gates.arraySize = 2;
        gates.GetArrayElementAtIndex(0).objectReferenceValue = left;
        gates.GetArrayElementAtIndex(1).objectReferenceValue = right;
        entryData.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject Child(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) return child.gameObject;
        var result = new GameObject(name);
        result.transform.SetParent(parent, false);
        return result;
    }

    private static Collider2D Gate(Transform parent, string name, float x, float y)
    {
        GameObject gate = Child(parent, name);
        gate.layer = LayerMask.NameToLayer("Ground");
        gate.transform.position = new Vector3(x, y, 0f);
        gate.transform.localScale = new Vector3(.5f, 10f, 1f);
        BoxCollider2D collider = gate.GetComponent<BoxCollider2D>();
        if (collider == null) collider = gate.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        SpriteRenderer visual = gate.GetComponent<SpriteRenderer>();
        if (visual == null) visual = gate.AddComponent<SpriteRenderer>();
        visual.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Boss/UnitSquare.png");
        visual.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Boss/BossUnlit.mat");
        visual.color = new Color(.48f, .65f, .76f, .45f);
        visual.sortingOrder = 4;
        visual.enabled = false;
        return collider;
    }
}
