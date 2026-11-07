using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Actor-owned, bounded pool shared by ordinary and boss dashes.</summary>
public sealed class CombatAfterimageEmitter : MonoBehaviour
{
    [SerializeField, Min(.02f)] private float emissionInterval=.05f;
    private const int MaximumRetained=16;
    private readonly Stack<CombatAfterimage> available=new Stack<CombatAfterimage>();
    private readonly List<CombatAfterimage> active=new List<CombatAfterimage>();
    private CombatAfterimage pooledPrefab;
    private Transform poolRoot;
    private Action<CombatAfterimage> returnToPool;
    private float nextEmission;
    public int CreatedCount { get; private set; }
    public int ActiveCount => active.Count;
    private void Awake() => returnToPool=Return;
    public void Emit(CombatAfterimage prefab,SpriteRenderer source,int order,Color? tint=null)
    {
        if(!isActiveAndEnabled || prefab==null || source==null || Time.time<nextEmission) return;
        if(pooledPrefab!=prefab)
        {
            Clear(); available.Clear();
            if(poolRoot!=null) Destroy(poolRoot.gameObject);
            poolRoot=null; pooledPrefab=prefab;
        }
        nextEmission=Time.time+Mathf.Max(.02f,emissionInterval);
        if(poolRoot==null)
        {
            var root=new GameObject(name+" afterimage pool");
            SceneManager.MoveGameObjectToScene(root,gameObject.scene); poolRoot=root.transform;
        }
        CombatAfterimage image=null;
        while(available.Count>0 && image==null) image=available.Pop();
        if(image==null) { image=Instantiate(prefab,poolRoot); CreatedCount++; }
        active.Add(image); image.gameObject.SetActive(true);
        image.Capture(source,order,tint,returnToPool);
    }
    private void Return(CombatAfterimage image)
    {
        active.Remove(image);
        if(image==null) return;
        image.gameObject.SetActive(false);
        if(available.Count<MaximumRetained) available.Push(image); else Destroy(image.gameObject);
    }
    public void Clear()
    {
        for(int i=active.Count-1;i>=0;i--) if(active[i]!=null) active[i].Release();
        active.Clear(); nextEmission=0f;
    }
    private void OnDisable() => Clear();
    private void OnDestroy() { if(poolRoot!=null) Destroy(poolRoot.gameObject); }
}
