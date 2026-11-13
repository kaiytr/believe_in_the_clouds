using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Ending scene. Fades in on a sunset: the player walks in from off-screen left along a ground strip,
/// and once near the centre keeps walking in place while the ground and backdrop scroll past forever.
/// The credits roll up over it; once the last line has left the screen it fades to black and returns to the title.
/// Everything is built at runtime; only the sprite references and the credits text are serialized.
/// </summary>
public sealed class EndingSceneController : MonoBehaviour
{
    private const float GroundTopY = -3f;
    private const float ViewHalfHeight = 5.5f;
    private const float TileSize = 1f;
    private const int PatternWidth = 4;      // the interior ground art repeats every 4 tiles

    [Header("Art (the 16 interior ground tiles, index = column + row * 4)")]
    [SerializeField] private Sprite[] groundSprites = new Sprite[0];
    [Header("Backdrop layers (Assets/Art/Ending)")]
    [SerializeField] private EndingBackdrop.Art backdropArt;
    [Tooltip("World height of the bottom edge of the backdrop art (the ground top is at -3).")]
    [SerializeField] private float backdropBottomY = -4f;
    [Header("Player")]
    [SerializeField] private Sprite[] playerWalkFrames = new Sprite[0];
    [SerializeField, Min(1f)] private float walkFramesPerSecond = 8f;
    [SerializeField, Min(.1f)] private float walkSpeed = 2.8f;
    [Tooltip("The player stops advancing here; from then on the world scrolls instead.")]
    [SerializeField] private float centerX = 0f;
    [SerializeField, Min(.5f)] private float handOverDistance = 3f;
    [Header("Timing")]
    [SerializeField, Min(0f)] private float fadeInSeconds = 1.8f;
    [SerializeField, Min(0f)] private float creditsDelay = 2.5f;
    [SerializeField, Min(1f)] private float creditsScrollSpeed = 55f;
    [SerializeField, Min(0f)] private float fadeOutSeconds = 1.5f;
    [Header("BGM")]
    [SerializeField] private AudioClip endingBgm;
    [SerializeField, Range(0f, 1f)] private float endingBgmVolume = .45f;
    private AudioSource bgmSource;

    [Header("Credits (temporary placeholder text, edit freely)")]
    [Tooltip("\"# \" = title, \"## \" = section heading, empty line = gap, anything else = normal line.")]
    [SerializeField, TextArea(10, 30)] private string creditsText = DefaultCredits;

    // Placeholder credits; replace in the Inspector (or here) when the real list is ready.
    private const string DefaultCredits =
        "# 구름 위의 믿음\n\n\n" +
        "## 기획\n[이름]\n[이름]\n\n" +
        "## 프로그래밍\n[이름]\n[이름]\n\n" +
        "## 아트\n[이름]\n[이름]\n\n" +
        "## 사운드\n[이름]\n\n" +
        "## Special Thanks\n[이름]\n[이름]\n\n\n" +
        "플레이해 주셔서 감사합니다";

    private Transform world;
    private Transform ground, player;
    private Transform playerFeet;
    private float halfWidth;
    private float playerX, scrollDistance, groundSpeed;
    private bool arrived;
    private float arrivedAt = -1f;
    private EndingBackdrop backdrop;
    private EndingCredits credits;
    private bool leaving;
    private CanvasGroup fade;
    private Canvas canvas;

    private void Start()
    {
        Time.timeScale = 1f;
        Camera view = EnsureCamera();
        halfWidth = ViewHalfHeight * view.aspect;
        world = new GameObject("Ending world").transform;
        world.SetParent(transform, false);

        BuildLight();
        backdrop = EndingBackdrop.Build(world, backdropBottomY, backdropArt);
        BuildGround();
        BuildPlayer();
        BuildUi();
        PlayEndingBgm();
        StartCoroutine(FadeIn());
    }

    // A warm sunset tint over everything lit (ground and player); the sky layers are unlit and keep their own colours.
    private void BuildLight()
    {
        var holder = new GameObject("Sunset light");
        holder.transform.SetParent(world, false);
        Light2D light = holder.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.color = new Color(1f, .74f, .56f);
        light.intensity = 1.05f;
    }

    private void BuildGround()
    {
        ground = new GameObject("Ground").transform;
        ground.SetParent(world, false);
        // The ground scrolls left by up to one pattern (4 tiles) before it wraps, so it needs a pattern of spare width on
        // each side plus some slack; otherwise the right edge of the strip shows up inside the screen.
        int columns = Mathf.CeilToInt((halfWidth * 2f + PatternWidth * 2f * TileSize + 6f) / TileSize);
        columns += (PatternWidth - columns % PatternWidth) % PatternWidth;
        const int rows = 6;
        float left = -columns * .5f * TileSize;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < columns; c++)
            {
                Sprite sprite = TileSprite(c % PatternWidth, Mathf.Min(r, 3));
                if (sprite == null) continue;
                var go = new GameObject("Tile");
                go.transform.SetParent(ground, false);
                go.transform.localPosition = new Vector3(left + (c + .5f) * TileSize, GroundTopY - (r + .5f) * TileSize, 0f);
                SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 0;
            }
    }

    private Sprite TileSprite(int column, int row)
    {
        int index = column + row * PatternWidth;
        return groundSprites != null && index < groundSprites.Length ? groundSprites[index] : null;
    }

    private void BuildPlayer()
    {
        var go = new GameObject("Player");
        go.transform.SetParent(world, false);
        player = go.transform;
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 10;
        if (playerWalkFrames != null && playerWalkFrames.Length > 0) renderer.sprite = playerWalkFrames[0];
        var animator = go.AddComponent<SpriteFrameAnimator>();
        animator.Configure(playerWalkFrames, walkFramesPerSecond);

        // Stand on the ground whatever the sprite's pivot is.
        float feetOffset = renderer.bounds.min.y - player.position.y;
        playerX = -(halfWidth + 2f);
        player.position = new Vector3(playerX, GroundTopY - feetOffset, 0f);
    }

    // 필드 BGM은 엔딩에서 이미 꺼진다. 이 씬에 있는 동안만 Ending 폴더의 BGM을 반복한다.
    private void PlayEndingBgm()
    {
        if (endingBgm == null)
        {
            AudioClip[] clips = Resources.LoadAll<AudioClip>("Sounds/Ending");
            if (clips != null && clips.Length > 0) endingBgm = clips[0];
        }
        if (endingBgm == null)
        {
            Debug.LogWarning("엔딩 BGM을 찾지 못했습니다. Assets/Resources/Sounds/Ending 을 확인해 주세요.", this);
            return;
        }

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.spatialBlend = 0f;
        bgmSource.volume = endingBgmVolume;
        bgmSource.clip = endingBgm;
        bgmSource.Play();
    }

    private void BuildUi()
    {
        canvas = DeathResultView.CreateCanvas("Ending UI", 1000);
        canvas.transform.SetParent(transform, false);

        credits = EndingCredits.Build(canvas.transform, creditsText);
        credits.ScrollSpeed = creditsScrollSpeed;

        // Black cover: fades away at the start and comes back before returning to the title.
        var cover = new GameObject("Fade", typeof(RectTransform), typeof(CanvasGroup));
        cover.transform.SetParent(canvas.transform, false);
        RectTransform coverRect = cover.GetComponent<RectTransform>();
        coverRect.anchorMin = Vector2.zero; coverRect.anchorMax = Vector2.one;
        coverRect.offsetMin = coverRect.offsetMax = Vector2.zero;
        fade = cover.GetComponent<CanvasGroup>();
        fade.blocksRaycasts = false;
        var black = new GameObject("Black", typeof(RectTransform), typeof(Image));
        black.transform.SetParent(cover.transform, false);
        RectTransform blackRect = black.GetComponent<RectTransform>();
        blackRect.anchorMin = Vector2.zero; blackRect.anchorMax = Vector2.one;
        blackRect.offsetMin = blackRect.offsetMax = Vector2.zero;
        Image image = black.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
    }

    private IEnumerator FadeIn()
    {
        for (float t = 0f; t < fadeInSeconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / fadeInSeconds);
            fade.alpha = 1f - k * k * (3f - 2f * k);
            yield return null;
        }
        fade.alpha = 0f;
    }

    private IEnumerator ReturnToTitle()
    {
        leaving = true;
        for (float t = 0f; t < fadeOutSeconds; t += Time.unscaledDeltaTime)
        {
            fade.alpha = Mathf.Clamp01(t / fadeOutSeconds);
            yield return null;
        }
        fade.alpha = 1f;
        SceneManager.LoadScene(TitleScreen.SceneName);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (player == null) return;

        // Walk in from the left. Near the centre the player's own advance fades out while the world's scroll
        // fades in, so the total pace stays constant and then the player just keeps walking on the spot.
        float remaining = centerX - playerX;
        float handOver = arrived ? 1f : Mathf.Clamp01(1f - remaining / handOverDistance);
        playerX += walkSpeed * (1f - handOver) * dt;
        groundSpeed = walkSpeed * handOver;
        if (!arrived && remaining < .15f)
        {
            arrived = true;
            arrivedAt = Time.time;
        }
        if (arrived) playerX = Mathf.MoveTowards(playerX, centerX, 2f * dt);
        Vector3 position = player.position;
        position.x = playerX;
        player.position = position;

        float step = groundSpeed * dt;
        scrollDistance = Mathf.Repeat(scrollDistance + step, PatternWidth * TileSize);
        ground.localPosition = new Vector3(-scrollDistance, 0f, 0f);
        backdrop.Scroll(step, dt);

        if (arrived && !credits.IsRunning && !credits.IsFinished && Time.time - arrivedAt >= creditsDelay)
            credits.Begin();
        if (credits.IsFinished && !leaving) StartCoroutine(ReturnToTitle());
    }

    private static Camera EnsureCamera()
    {
        Camera view = Camera.main;
        if (view == null)
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            view = cameraObject.GetComponent<Camera>();
        }
        view.transform.position = new Vector3(0f, 0f, -10f);
        view.clearFlags = CameraClearFlags.SolidColor;
        view.backgroundColor = new Color(.14f, .1f, .29f);
        view.orthographic = true;
        view.orthographicSize = ViewHalfHeight;
        return view;
    }
}
