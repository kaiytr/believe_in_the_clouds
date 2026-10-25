using System;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class CombatAfterimage : MonoBehaviour
{
    [SerializeField, Range(0f,1f)] private float opacity=.3f;
    [SerializeField, Min(.01f)] private float lifetime=.16f;
    [SerializeField] private Color tint=new Color(1f,.86f,.45f,1f);
    private SpriteRenderer image;
    private Color capturedColor;
    private float startedAt;
    private Action<CombatAfterimage> release;
    private void Awake() => image=GetComponent<SpriteRenderer>();
    public void Capture(SpriteRenderer source,int sortingOrder,Color? tintOverride,Action<CombatAfterimage> onRelease)
    {
        release=onRelease;
        if(source==null) { Release(); return; }
        if(image==null) image=GetComponent<SpriteRenderer>();
        Color chosen=tintOverride??tint;
        image.sprite=source.sprite; image.sharedMaterial=source.sharedMaterial;
        image.drawMode=source.drawMode; image.size=source.size;
        image.flipX=source.flipX; image.flipY=source.flipY;
        image.sortingLayerID=source.sortingLayerID; image.sortingOrder=sortingOrder;
        image.color=new Color(source.color.r*chosen.r,source.color.g*chosen.g,source.color.b*chosen.b,
            source.color.a*(tintOverride.HasValue?chosen.a:opacity));
        transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
        transform.localScale=source.transform.lossyScale;
        capturedColor=image.color; startedAt=Time.time;
    }
    private void Update()
    {
        float remaining=Mathf.Clamp01(1f-(Time.time-startedAt)/Mathf.Max(.01f,lifetime));
        Color color=capturedColor; color.a*=remaining; image.color=color;
        if(remaining<=0f) Release();
    }
    public void Release()
    {
        Action<CombatAfterimage> callback=release; release=null;
        if(callback!=null) callback(this); else Destroy(gameObject);
    }
}
