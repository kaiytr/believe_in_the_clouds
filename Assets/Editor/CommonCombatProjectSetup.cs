using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>One serialized world HUD prefab for both regular enemies and bosses.</summary>
public static class CommonCombatProjectSetup
{
    public const string HudPath = "Assets/Resources/Combat/CombatantHud.prefab";
    public static CombatantHudView EnsureHudPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<CombatantHudView>(HudPath);
        return existing != null ? existing : BuildHudPrefab();
    }
    private static CombatantHudView BuildHudPrefab()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Combat")) AssetDatabase.CreateFolder("Assets/Resources", "Combat");
        var root = new GameObject("Combatant HUD");
        try
        {
            var view = root.AddComponent<CombatantHudView>();
            var belief = WorldCanvas("WorldSpaceBeliefUI", root.transform, new Vector2(300f, 110f), 32);
            var bubble = Image("BeliefSpeechBubble", belief.transform, new Color(.84f,.84f,.84f,.96f));
            Anchors(bubble.rectTransform, new Vector2(.08f,.12f), new Vector2(.92f,.68f));
            var tail = Image("BeliefSpeechBubbleTail", bubble.transform, bubble.color);
            tail.rectTransform.anchorMin = tail.rectTransform.anchorMax = new Vector2(.5f,0f);
            tail.rectTransform.sizeDelta = new Vector2(18f,18f);
            tail.rectTransform.anchoredPosition = new Vector2(0f,-5f);
            tail.rectTransform.localRotation = Quaternion.Euler(0f,0f,45f);
            var text = new GameObject("BeliefPhrase",typeof(RectTransform),typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(bubble.transform,false);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize=19; text.resizeTextForBestFit=true; text.resizeTextMinSize=12; text.resizeTextMaxSize=19;
            text.color=new Color(.08f,.08f,.08f); text.alignment=TextAnchor.MiddleCenter;
            text.horizontalOverflow=HorizontalWrapMode.Wrap; text.verticalOverflow=VerticalWrapMode.Overflow; text.raycastTarget=false;
            Anchors(text.rectTransform,Vector2.zero,Vector2.one);
            text.rectTransform.offsetMin=new Vector2(10f,4f); text.rectTransform.offsetMax=new Vector2(-10f,-4f);
            var background=Image("BeliefGaugeBackground",belief.transform,new Color(.73f,.73f,.73f));
            Anchors(background.rectTransform,new Vector2(.08f,.84f),new Vector2(.92f,.94f));
            var beliefFill=Fill("BeliefGaugeFill",background.transform);
            var health=WorldCanvas("EnemyHealthWorldSpaceUI",root.transform,new Vector2(220f,32f),31);
            var panel=Image("EnemyHealthPanel",health.transform,new Color(.84f,.84f,.84f,.94f));
            Anchors(panel.rectTransform,Vector2.zero,Vector2.one);
            var hpBackground=Image("EnemyHealthGaugeBackground",health.transform,new Color(.72f,.72f,.72f));
            Anchors(hpBackground.rectTransform,new Vector2(.16f,.32f),new Vector2(.84f,.68f));
            var hp=Fill("EnemyHealthGaugeFill",hpBackground.transform);
            Set(view,"beliefCanvas",belief); Set(view,"healthCanvas",health); Set(view,"healthFill",hp);
            Set(view,"beliefFill",beliefFill); Set(view,"beliefBackground",background); Set(view,"speechBubble",bubble); Set(view,"phrase",text);
            return PrefabUtility.SaveAsPrefabAsset(root,HudPath).GetComponent<CombatantHudView>();
        }
        finally { Object.DestroyImmediate(root); }
    }
    [MenuItem("Tools/Combat/Upgrade shared HUD and afterimages")]
    public static void UpgradeSharedCombatAssets()
    {
        EnsureHudPrefab();
        BossProjectSetup.UpdateGameplayPresentation();
        string path="Assets/Prefabs/Boss/Effects/BossDashTrail.prefab";
        GameObject root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (LineRenderer line in root.GetComponentsInChildren<LineRenderer>(true))
                if(line.gameObject!=root) Object.DestroyImmediate(line.gameObject); else Object.DestroyImmediate(line);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("Shared combat HUD and outline-free afterimage prefab upgraded.");
    }
    private static Canvas WorldCanvas(string name,Transform parent,Vector2 size,int order)
    {
        var canvas=new GameObject(name,typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();
        canvas.transform.SetParent(parent,false); canvas.renderMode=RenderMode.WorldSpace;
        canvas.overrideSorting=true; canvas.sortingOrder=order; canvas.enabled=false;
        ((RectTransform)canvas.transform).sizeDelta=size; canvas.transform.localScale=Vector3.one*.01f;
        return canvas;
    }
    private static Image Image(string name,Transform parent,Color color)
    {
        var image=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent,false); image.color=color; image.raycastTarget=false;
        image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Boss/UnitSquare.png");
        return image;
    }
    private static Image Fill(string name,Transform parent)
    {
        var fill=Image(name,parent,new Color(.4f,.4f,.4f)); Anchors(fill.rectTransform,Vector2.zero,Vector2.one);
        fill.type=UnityEngine.UI.Image.Type.Filled; fill.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;
        fill.fillOrigin=(int)UnityEngine.UI.Image.OriginHorizontal.Left;
        return fill;
    }
    private static void Anchors(RectTransform rect,Vector2 min,Vector2 max)
    { rect.anchorMin=min; rect.anchorMax=max; rect.offsetMin=rect.offsetMax=Vector2.zero; }
    private static void Set(Object target,string name,Object value)
    { var data=new SerializedObject(target); data.FindProperty(name).objectReferenceValue=value; data.ApplyModifiedPropertiesWithoutUndo(); }
}
