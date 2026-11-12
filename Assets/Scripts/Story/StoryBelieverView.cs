using UnityEngine;

public sealed class StoryBelieverView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private Sprite deathSprite;
    private bool dead;
    public bool IsConfigured => body != null && idleFrames != null && idleFrames.Length > 0 && deathSprite != null;
    public void Configure(SpriteRenderer renderer, Sprite[] idle, Sprite death)
    { body = renderer; idleFrames = idle; deathSprite = death; }
    public void SetDead(bool value)
    {
        dead = value;
        if (body != null && value) body.sprite = deathSprite;
    }
    private void Update()
    {
        if (!IsConfigured || dead) return;
        body.sprite = idleFrames[Mathf.FloorToInt(Time.time * 6f) % idleFrames.Length];
    }
}
