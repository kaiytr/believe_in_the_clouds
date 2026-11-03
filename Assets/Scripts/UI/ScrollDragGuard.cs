using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 스크롤 영역을 드래그하는 동안 그 위의 카드가 클릭으로 처리되지 않게 하는 보조 컴포넌트.
// ScrollRect와 같은 오브젝트에 붙인다. 카드 위에서 드래그를 시작해도 ScrollRect가
// 드래그를 가져가므로, 이 컴포넌트도 같은 이벤트를 받아 "방금 드래그했는지"를 남긴다.
public class ScrollDragGuard : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    // 손을 떼는 순간의 미세한 움직임까지 클릭으로 새지 않게 잠깐 더 무시한다.
    private const float ClickSuppressSeconds = 0.12f;

    public bool Dragging { get; private set; }

    private float lastDragEnd = -10f;

    /// <summary>이번 클릭이 드래그였거나 방금 드래그가 끝났다면 true.</summary>
    public bool ShouldIgnoreClick => Dragging || Time.unscaledTime - lastDragEnd < ClickSuppressSeconds;

    public void OnBeginDrag(PointerEventData eventData)
    {
        Dragging = true;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Dragging = false;
        lastDragEnd = Time.unscaledTime;
    }

    public static ScrollDragGuard Attach(ScrollRect scroller)
    {
        if (scroller == null)
        {
            return null;
        }

        ScrollDragGuard guard = scroller.GetComponent<ScrollDragGuard>();
        return guard != null ? guard : scroller.gameObject.AddComponent<ScrollDragGuard>();
    }
}
