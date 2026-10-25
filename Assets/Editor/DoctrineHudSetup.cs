using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 교리 HUD를 프로젝트에 세팅하는 에디터 도구.
//
// HUD는 DoctrineHudBoot가 플레이 모드에서 자동으로 만들기 때문에
// 이 메뉴는 "씬마다 인스펙터 값/프리팹을 미리 맞춰두고 싶을 때"만 쓰는 선택 도구다.
//
// 메뉴
//   Tools/Doctrine/Create Slot Prefab   : 슬롯 프리팹만 생성
//   Tools/Doctrine/Setup HUD In Scene   : 슬롯 프리팹 + 확인용 교리 + HUD 오브젝트 배치(선택)
//
// YAML을 직접 쓰지 않고 Unity API로만 생성하므로 씬/프리팹이 깨지지 않는다.
public static class DoctrineHudSetup
{
    private const string UiFolder = "Assets/Prefabs/UI";
    private const string SlotPrefabPath = UiFolder + "/DoctrineSlot.prefab";
    private const string SampleFolder = "Assets/Data/Doctrine";
    private const string HudObjectName = "[DoctrineHud]";

    // 확인용 교리 3개. 교리 시스템을 실제로 연결하면 HUD는 이 목록 대신
    // DoctrineManager의 활성 교리를 표시한다.
    private static readonly string[] SampleNames =
    {
        "SampleDoctrine1",
        "SampleDoctrine2",
        "SampleDoctrine3"
    };

    [MenuItem("Tools/Doctrine/Create Slot Prefab")]
    public static void CreateSlotPrefab()
    {
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefabs", "UI");

        // 임시 루트 아래에서 슬롯을 만들고 프리팹으로 저장한 뒤 정리한다.
        GameObject tempRoot = new GameObject("DoctrineSlotTempRoot", typeof(RectTransform));
        try
        {
            DoctrineSlotView slot = DoctrineSlotView.BuildRuntime(tempRoot.transform);
            slot.name = "DoctrineSlot";

            // BuildRuntime이 넣는 폰트는 런타임에 OS 폰트로 만든 일시 객체라
            // 프리팹에 저장할 수 없다. 내장 폰트로 바꿔 저장하고,
            // 실제 한글 폰트는 게임 실행 중 DoctrineSlotView.Bind가 다시 지정한다.
            Font builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtinFont != null)
            {
                foreach (Text text in slot.GetComponentsInChildren<Text>(true))
                    text.font = builtinFont;
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(slot.gameObject, SlotPrefabPath);
            if (saved == null)
                throw new System.InvalidOperationException("Failed to save " + SlotPrefabPath);

            Debug.Log($"[DoctrineHud] 슬롯 프리팹 생성: {SlotPrefabPath}");
        }
        finally
        {
            Object.DestroyImmediate(tempRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Doctrine/Setup HUD In Scene")]
    public static void SetupInScene()
    {
        CreateSlotPrefab();
        var samples = CreateSampleDoctrines();

        DoctrineSlotView prefab = AssetDatabase.LoadAssetAtPath<DoctrineSlotView>(SlotPrefabPath);
        if (prefab == null)
            throw new System.InvalidOperationException("슬롯 프리팹을 불러오지 못했습니다: " + SlotPrefabPath);

        Scene scene = SceneManager.GetActiveScene();

        // 기존 HUD가 있으면 재사용한다 (중복 방지).
        DoctrineHud hud = Object.FindFirstObjectByType<DoctrineHud>();
        if (hud == null)
        {
            GameObject hudObject = new GameObject(HudObjectName, typeof(RectTransform));
            hudObject.transform.SetParent(null, false);
            hud = hudObject.AddComponent<DoctrineHud>();
            hudObject.AddComponent<Canvas>();
            hudObject.AddComponent<CanvasScaler>();

            // Canvas 루트는 화면 전체를 덮어야 한다. (스케일 0으로 저장되면 아무것도 안 보인다)
            RectTransform rect = hudObject.GetComponent<RectTransform>();
            rect.localScale = Vector3.one;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Canvas canvas = hudObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = hudObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        SerializedObject data = new SerializedObject(hud);
        data.FindProperty("slotPrefab").objectReferenceValue = prefab;

        // 확인용 교리 목록 연결 (교리를 연결하기 전 모양 확인용).
        SerializedProperty preview = data.FindProperty("previewDoctrines");
        preview.arraySize = samples.Count;
        for (int i = 0; i < samples.Count; i++)
            preview.GetArrayElementAtIndex(i).objectReferenceValue = samples[i];

        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hud);

        // 씬에 매니저가 없으면 함께 만들어 준다.
        if (Object.FindFirstObjectByType<DoctrineManager>() == null)
        {
            GameObject managerObject = new GameObject("[DoctrineManager]");
            managerObject.AddComponent<DoctrineManager>();
            Debug.Log("[DoctrineHud] 씬에 [DoctrineManager]를 생성했습니다.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[DoctrineHud] HUD 세팅 완료: {scene.name} (확인용 교리 {samples.Count}개)");
    }

    // 확인용 교리 에셋 3개를 만든다. 이미 있으면 재사용한다.
    private static System.Collections.Generic.List<DoctrineDefinition> CreateSampleDoctrines()
    {
        EnsureFolder("Assets", "Data");
        EnsureFolder("Assets/Data", "Doctrine");

        var result = new System.Collections.Generic.List<DoctrineDefinition>();

        // 모양이 서로 다르게 보이도록 조건/결과를 다르게 둔다.
        SubjectType[] subjects = { SubjectType.Player, SubjectType.Player, SubjectType.Enemy };
        ConditionType[] conditions = { ConditionType.OnHit, ConditionType.OnAttack, ConditionType.OnDeath };
        ResultType[] results = { ResultType.Heal, ResultType.IncreaseAttack, ResultType.Explode };
        float[] magnitudes = { 30f, 1.3f, 40f };
        float[] durations = { 0f, 5f, 0f };

        for (int i = 0; i < SampleNames.Length; i++)
        {
            string path = SampleFolder + "/" + SampleNames[i] + ".asset";
            DoctrineDefinition def = AssetDatabase.LoadAssetAtPath<DoctrineDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<DoctrineDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }

            def.Subject = subjects[i];
            def.Condition = conditions[i];
            def.Result = results[i];
            def.Magnitude = magnitudes[i];
            def.Duration = durations[i];
            EditorUtility.SetDirty(def);

            result.Add(def);
        }

        AssetDatabase.SaveAssets();
        return result;
    }

    private static void EnsureFolder(string parent, string folder)
    {
        string path = parent + "/" + folder;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, folder);
    }
}
