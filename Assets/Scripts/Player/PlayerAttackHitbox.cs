using System.Collections;
using UnityEngine;

// 플레이어 공격 판정 박스의 시각화용 컴포넌트.
// 실제 타격 판정은 PlayerAttack의 OverlapBox가 담당하고,
// 이 컴포넌트는 그 박스와 같은 크기/위치/각도로 반투명 박스를 띄워
// 공격 범위를 눈으로 확인할 수 있게 한다.
//
// 물리 콜라이더는 달지 않는다(순수 비주얼).
// 트리거 콜라이더를 달면 적 ContactDamage가 플레이어 자식으로 오인해
// 자해 판정이 들어갈 수 있기 때문이다.
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerAttackHitbox : MonoBehaviour
{
    [Header("연출")]
    [Tooltip("박스 색상 (타수별).")]
    [SerializeField] private Color[] comboColors = new Color[]
    {
        new Color(0f, 1f, 1f, 0.35f),
        new Color(0f, 1f, 0.3f, 0.35f),
        new Color(1f, 1f, 0f, 0.35f),
        new Color(1f, 0.25f, 0.25f, 0.4f),
    };

    [Tooltip("스프라이트 렌더러 SortingOrder.")]
    [SerializeField] private int sortingOrder = 10;

    private SpriteRenderer spriteRenderer;
    private Coroutine hideRoutine;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = CreatePixelSprite();
        spriteRenderer.sortingOrder = sortingOrder;
        spriteRenderer.drawMode = SpriteDrawMode.Simple;
        spriteRenderer.enabled = false;
    }

    /// <summary>
    /// 이번 타의 판정 박스를 표시한다. 이 오브젝트는 Player의 자식이므로
    /// 부모의 좌우 뒤집힘(scale.x 부호)은 자동으로 따라가고, 각도도
    /// 미러링되어 실제 판정(DoHit의 각도 반전)과 일치한다.
    /// 부모 스케일 크기로 인한 찌그러짐만 여기서 보정한다.
    /// offset/angle은 뒤집지 않은 원본 값을 그대로 넘긴다.
    /// </summary>
    public void ShowSwing(int comboIndex, Vector2 size, Vector2 offset, float angle, float duration)
    {
        Vector3 parentScale = transform.parent != null
            ? transform.parent.localScale
            : Vector3.one;
        float absX = Mathf.Max(0.001f, Mathf.Abs(parentScale.x));
        float scaleY = Mathf.Abs(parentScale.y) > 0.001f ? parentScale.y : 1f;

        // 부모 스케일을 나눠 월드 크기를 판정 박스와 같게 유지한다.
        // x는 부호를 살려야 좌우 미러링이 유지된다.
        transform.localPosition = new Vector3(offset.x / absX, offset.y / scaleY, 0f);
        transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        transform.localScale = new Vector3(
            Mathf.Max(0.01f, size.x) / absX,
            Mathf.Max(0.01f, size.y) / Mathf.Abs(scaleY),
            1f);

        spriteRenderer.color = comboColors.Length > 0
            ? comboColors[comboIndex % comboColors.Length]
            : new Color(1f, 0f, 0f, 0.35f);
        spriteRenderer.enabled = true;

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(HideAfter(duration));
    }

    public void Hide()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;
    }

    private IEnumerator HideAfter(float duration)
    {
        if (duration > 0f)
            yield return new WaitForSeconds(duration);
        else
            yield return null;
        spriteRenderer.enabled = false;
        hideRoutine = null;
    }

    private static Sprite CreatePixelSprite()
    {
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        return Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }

    private void OnDisable()
    {
        Hide();
    }
}
