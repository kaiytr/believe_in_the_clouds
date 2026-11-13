using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A rectangle on an overlay canvas that always covers exactly the 16:9 game area (see <see cref="AspectLock"/>), clipped
/// to it. Screens made of full-screen artwork or scrolling text (title, ending credits) are built inside one so that
/// nothing spills into the black bars on screens that are not 16:9.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class AspectFrame : MonoBehaviour
{
    private RectTransform rect;
    private Rect applied = new Rect(-1f, -1f, 0f, 0f);

    public static RectTransform Create(Transform canvasRoot, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RectMask2D), typeof(AspectFrame));
        go.transform.SetParent(canvasRoot, false);
        go.GetComponent<AspectFrame>().Apply();
        return go.GetComponent<RectTransform>();
    }

    private void Update() => Apply();

    private void Apply()
    {
        if (rect == null) rect = GetComponent<RectTransform>();
        Rect viewport = AspectLock.Viewport;
        if (viewport == applied) return;
        applied = viewport;
        rect.anchorMin = new Vector2(viewport.xMin, viewport.yMin);
        rect.anchorMax = new Vector2(viewport.xMax, viewport.yMax);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
