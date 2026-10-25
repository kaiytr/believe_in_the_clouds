using System.Collections.Generic;
using UnityEngine;

/// <summary>A locked ground route. Its warning boxes enclose exactly the segments swept by the wave.</summary>
public sealed class BossWavePath
{
    public IReadOnlyList<Vector2> Points => points;
    public IReadOnlyList<AttackGeometry> Warnings => warnings;
    public int Direction { get; }
    public float Distance { get; }
    public bool Blocked { get; }
    private readonly List<Vector2> points;
    private readonly List<AttackGeometry> warnings = new List<AttackGeometry>();
    private readonly Vector2 waveSize;

    public BossWavePath(List<Vector2> groundPoints, int direction, Vector2 size, bool blocked)
    {
        points = groundPoints;
        Direction = direction; waveSize = size; Blocked = blocked;
        Distance = Mathf.Abs(points[points.Count - 1].x - points[0].x);
        for (int i = 1; i < points.Count; i++) AddSegment(points[i - 1], points[i]);
        if (warnings.Count == 0) AddSegment(points[0], points[0]);
    }
    private void AddSegment(Vector2 from, Vector2 to)
    {
        float minX = Mathf.Min(from.x,to.x)-waveSize.x*.5f, maxX = Mathf.Max(from.x,to.x)+waveSize.x*.5f;
        float minY = Mathf.Min(from.y,to.y)+.02f, maxY = Mathf.Max(from.y,to.y)+waveSize.y+.02f;
        if (warnings.Count > 0 && Mathf.Abs(from.y-to.y)<.001f)
        {
            var previous=warnings[warnings.Count-1];
            if (Mathf.Abs(previous.Size.y-waveSize.y)<.001f && Mathf.Abs(previous.Center.y-(minY+maxY)*.5f)<.001f)
            {
                minX=Mathf.Min(minX,previous.Center.x-previous.Size.x*.5f);
                maxX=Mathf.Max(maxX,previous.Center.x+previous.Size.x*.5f);
                warnings.RemoveAt(warnings.Count-1);
            }
        }
        warnings.Add(new AttackGeometry(new Vector2((minX+maxX)*.5f,(minY+maxY)*.5f),new Vector2(maxX-minX,maxY-minY)));
    }
    public Vector2 GroundAt(float travelled)
    {
        float x=points[0].x+Direction*Mathf.Clamp(travelled,0f,Distance);
        for(int i=1;i<points.Count;i++)
        {
            if(Direction*(points[i].x-x)<-.001f) continue;
            float t=Mathf.InverseLerp(points[i-1].x,points[i].x,x);
            return new Vector2(x,Mathf.Lerp(points[i-1].y,points[i].y,t));
        }
        return points[points.Count-1];
    }
    public bool Threatens(Collider2D target,float travelled=0f)
    {
        float currentX=GroundAt(travelled).x;
        foreach(var shape in warnings)
        {
            float min=shape.Center.x-shape.Size.x*.5f, max=shape.Center.x+shape.Size.x*.5f;
            if(Direction>0) min=Mathf.Max(min,currentX-waveSize.x*.5f);
            else max=Mathf.Min(max,currentX+waveSize.x*.5f);
            if(max<min) continue;
            var remaining=new AttackGeometry(new Vector2((min+max)*.5f,shape.Center.y),new Vector2(max-min,shape.Size.y));
            if(remaining.Overlaps(target)) return true;
        }
        return false;
    }
    public bool Encloses(AttackGeometry body)
    {
        Vector2 min=body.Center-body.Size*.5f, max=body.Center+body.Size*.5f;
        foreach(var warning in warnings)
        {
            Vector2 low=warning.Center-warning.Size*.5f, high=warning.Center+warning.Size*.5f;
            if(min.x>=low.x-.002f && min.y>=low.y-.002f && max.x<=high.x+.002f && max.y<=high.y+.002f) return true;
        }
        return false;
    }
}
