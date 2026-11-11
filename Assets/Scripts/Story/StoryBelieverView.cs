using UnityEngine;

public sealed class StoryBelieverView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private Sprite deathSprite;
    private MaterialPropertyBlock properties;
    private static readonly int Glitch = Shader.PropertyToID("_Glitch");
    private static readonly int UvRect = Shader.PropertyToID("_SpriteUVRect");
    private bool dead, trembling;
    public bool IsConfigured => body != null && idleFrames != null && idleFrames.Length > 0 && deathSprite != null;
    public void Configure(SpriteRenderer renderer, Sprite[] idle, Sprite death)
    { body = renderer; idleFrames = idle; deathSprite = death; }
    public void SetDead(bool value)
    {
        dead = value;
        if (body != null && value) body.sprite = deathSprite;
        SetTrembling(false);
    }
    public void SetTrembling(bool value) { trembling = value; ApplyGlitch(value ? .025f : 0f); }
    private void Update()
    {
        if (!IsConfigured || dead) return;
        body.sprite = idleFrames[Mathf.FloorToInt(Time.time * 6f) % idleFrames.Length];
        ApplyGlitch(trembling ? Time.frameCount % 57 < 7 ? .065f : .004f : 0f);
    }
    private void ApplyGlitch(float value)
    {
        if (body == null) return;
        if (properties == null) properties = new MaterialPropertyBlock();
        body.GetPropertyBlock(properties); properties.SetFloat(Glitch, value);
        if (body.sprite != null)
        {
            Rect rect = body.sprite.textureRect;
            Texture2D texture = body.sprite.texture;
            properties.SetVector(UvRect, new Vector4(rect.x / texture.width, rect.y / texture.height,
                rect.width / texture.width, rect.height / texture.height));
        }
        body.SetPropertyBlock(properties);
    }
    private void OnDisable() => ApplyGlitch(0f);
}
