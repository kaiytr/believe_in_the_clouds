using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class ParallelWorkIntegrationSetup
{
    public static void Install()
    {
        CutsceneSceneInstaller.InstallBossIntroductionForIntegration();
        CutsceneSceneInstaller.InstallTutorialFlowForIntegration();
        InstallShopExit();
        AssetDatabase.SaveAssets();
    }

    private static void InstallShopExit()
    {
        Scene scene = EditorSceneManager.OpenScene(ProjectScenes.Shop, OpenSceneMode.Single);
        LobbySceneController controller = UnityEngine.Object.FindFirstObjectByType<LobbySceneController>();
        GameObject floorObject = GameObject.Find("Platformer Floor");
        Tilemap floor = floorObject != null ? floorObject.GetComponentInChildren<Tilemap>() : null;
        if (controller == null || floor == null) throw new InvalidOperationException("상점 씬/바닥 참조가 없습니다.");
        var sceneData = new SerializedObject(controller);
        TemporaryStageExit exit = sceneData.FindProperty("authoredExit").objectReferenceValue as TemporaryStageExit;
        if (exit == null)
        {
            var root = new GameObject("ShopReturnExit");
            exit = root.AddComponent<TemporaryStageExit>();
            var box = root.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(2f, 3f);
            var panel = new GameObject("Door");
            panel.transform.SetParent(root.transform, false);
            panel.transform.localPosition = new Vector3(0f, -1.5f, 0f);
            var renderer = panel.AddComponent<SpriteRenderer>();
            renderer.sprite = sceneData.FindProperty("returnDoorSprite").objectReferenceValue as Sprite;
            renderer.sharedMaterial = sceneData.FindProperty("worldMaterial").objectReferenceValue as Material;
            renderer.sortingOrder = -8;
            sceneData.FindProperty("authoredExit").objectReferenceValue = exit;
            sceneData.ApplyModifiedPropertiesWithoutUndo();
        }
        // 엑셀 L열이 바닥 시작, AB열 중심 = 33번째 타일 경계에서 한 칸의 중앙.
        float x = floor.transform.position.x + 33f * floor.transform.lossyScale.x;
        float floorY = floor.CellToWorld(new Vector3Int(0, floor.cellBounds.yMax, 0)).y;
        exit.transform.position = new Vector3(x, floorY + 1.5f, 0f);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("기존 로비 복귀 규칙을 사용하는 상점 출구를 엑셀 AB열에 연결했습니다.");
    }
}
