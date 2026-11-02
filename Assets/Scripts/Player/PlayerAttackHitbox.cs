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

    [Tooltip("부채꼴 비주얼 색상.")]
    [SerializeField] private Color fanColor = new Color(1f, 0.6f, 0.15f, 0.4f);

    private SpriteRenderer spriteRenderer;
    private MeshFilter fanMeshFilter;
    private MeshRenderer fanMeshRenderer;
    private Coroutine hideRoutine;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = CreatePixelSprite();
        spriteRenderer.sortingOrder = sortingOrder;
        spriteRenderer.drawMode = SpriteDrawMode.Simple;
        spriteRenderer.enabled = false;

        GameObject fanObject = new GameObject("DownFanVisual");
        fanObject.transform.SetParent(transform, false);
        fanObject.transform.localPosition = Vector3.zero;
        fanObject.transform.localRotation = Quaternion.identity;
        fanObject.transform.localScale = Vector3.one;
        fanMeshFilter = fanObject.AddComponent<MeshFilter>();
        fanMeshRenderer = fanObject.AddComponent<MeshRenderer>();
        fanMeshRenderer.material = new Material(Shader.Find("Sprites/Default"));
        fanMeshRenderer.material.color = fanColor;
        fanMeshRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        fanMeshRenderer.sortingOrder = sortingOrder;
        fanObject.SetActive(false);
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
        if (fanMeshRenderer != null)
            fanMeshRenderer.gameObject.SetActive(false);
        spriteRenderer.enabled = false;
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

    /// <summary>
    /// 아래쪽 부채꼴 비주얼을 표시한다. 이 오브젝트는 Player의 자식이므로
    /// 부모의 좌우 뒤집힘(scale.x 부호)은 자동으로 따라가고, 아래쪽 부채꼴은
    /// 좌우 대칭이라 미러링이 판정과 어긋나지 않는다. offset은 원본 값을 그대로 넘긴다.
    /// </summary>
    public void ShowDownFan(Vector2 offset, Vector2 downDirection, float halfAngleDeg, float radius, float duration)
    {
        spriteRenderer.enabled = false;
        if (fanMeshFilter == null || fanMeshRenderer == null)
            return;

        const int segments = 24;
        Vector2 dir = downDirection.sqrMagnitude > 0.0001f ? downDirection.normalized : Vector2.down;
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        Mesh mesh = fanMeshFilter.mesh;
        if (mesh == null)
        {
            mesh = new Mesh();
            fanMeshFilter.mesh = mesh;
        }
        mesh.Clear();

        int vertexCount = segments + 2;
        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[segments * 3];
        vertices[0] = Vector3.zero;
        for (int i = 0; i <= segments; i++)
        {
            float angle = (baseAngle - halfAngleDeg) + (halfAngleDeg * 2f * i / segments);
            float rad = angle * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, 0f);
            if (i < segments)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        Vector3 parentScale = transform.parent != null ? transform.parent.localScale : Vector3.one;
        float absX = Mathf.Max(0.001f, Mathf.Abs(parentScale.x));
        float scaleY = Mathf.Abs(parentScale.y) > 0.001f ? parentScale.y : 1f;
        transform.localPosition = new Vector3(offset.x / absX, offset.y / scaleY, 0f);
        transform.localRotation = Quaternion.identity;
        // 부모 좌우 뒤집힘을 메쉬까지 그대로 물려받지 않게 크기를 역보정한다.
        // 부채꼴이 좌우 대칭이라 미러링돼도 모양은 같지만, 삼각형 와인딩이 뒤집혀
        // 뒷면으로 보이는 걸 막기 위함이다.
        transform.localScale = new Vector3(
            parentScale.x >= 0f ? 1f / absX : -1f / absX, 1f / Mathf.Abs(scaleY), 1f);
        fanMeshRenderer.gameObject.SetActive(true);

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
        if (fanMeshRenderer != null)
            fanMeshRenderer.gameObject.SetActive(false);
    }

    private IEnumerator HideAfter(float duration)
    {
        if (duration > 0f)
            yield return new WaitForSeconds(duration);
        else
            yield return null;
        spriteRenderer.enabled = false;
        if (fanMeshRenderer != null)
            fanMeshRenderer.gameObject.SetActive(false);
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
