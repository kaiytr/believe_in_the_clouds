using UnityEngine;

/// <summary>Plays a looping flip-book on a SpriteRenderer (wall torches). Each instance starts at a different frame so rows of torches do not flicker in sync.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class SpriteFrameAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] frames = new Sprite[0];
    [SerializeField, Min(.1f)] private float framesPerSecond = 8f;

    private SpriteRenderer target;
    private float offset;
    private int shown = -1;

    private void Awake()
    {
        target = GetComponent<SpriteRenderer>();
        // Position-based so it is stable between loads but different from torch to torch.
        Vector3 p = transform.position;
        offset = Mathf.Abs(Mathf.Sin(p.x * 12.9898f + p.y * 78.233f) * 43758.5453f) % 1f * Mathf.Max(1, frames.Length);
    }

    private void Update()
    {
        if (target == null || frames == null || frames.Length == 0) return;
        int index = Mathf.FloorToInt(Time.time * framesPerSecond + offset) % frames.Length;
        if (index == shown) return;
        shown = index;
        if (frames[index] != null) target.sprite = frames[index];
    }
}
