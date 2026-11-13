using UnityEngine;

/// <summary>
/// Keeps the game picture at 16:9 on every display. The art, the HUD layout and the level framing are all authored for
/// 16:9, so on a 16:10 laptop, an ultra-wide or a 4:3 screen the world cameras are confined to the largest centred 16:9
/// area and the rest of the screen is filled with black bars. Overlay UI is not confined (it is anchored to the screen
/// and scales with it), only the world picture is.
/// </summary>
[DefaultExecutionOrder(32000)]
public sealed class AspectLock : MonoBehaviour
{
    public const float TargetAspect = 16f / 9f;
    private const float Tolerance = .005f;

    private static AspectLock instance;
    private Camera barsCamera;
    private Camera[] buffer = new Camera[8];

    /// <summary>The normalized screen area the game picture occupies (the whole screen when it is already 16:9).</summary>
    public static Rect Viewport
    {
        get
        {
            float screenAspect = Screen.height > 0 ? (float)Screen.width / Screen.height : TargetAspect;
            if (Mathf.Abs(screenAspect / TargetAspect - 1f) <= Tolerance) return new Rect(0f, 0f, 1f, 1f);
            if (screenAspect > TargetAspect)
            {
                float width = TargetAspect / screenAspect;       // pillarbox: bars left and right
                return new Rect((1f - width) * .5f, 0f, width, 1f);
            }
            float height = screenAspect / TargetAspect;           // letterbox: bars top and bottom
            return new Rect(0f, (1f - height) * .5f, 1f, height);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (instance != null) return;
        var go = new GameObject("[AspectLock]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AspectLock>();
    }

    private void Awake()
    {
        // The black bars: a camera that draws nothing but clears the whole screen before the game cameras draw.
        var cameraObject = new GameObject("Aspect bars");
        cameraObject.transform.SetParent(transform, false);
        barsCamera = cameraObject.AddComponent<Camera>();
        barsCamera.clearFlags = CameraClearFlags.SolidColor;
        barsCamera.backgroundColor = Color.black;
        barsCamera.cullingMask = 0;
        barsCamera.orthographic = true;
        barsCamera.depth = -1000f;
        barsCamera.rect = new Rect(0f, 0f, 1f, 1f);
        barsCamera.useOcclusionCulling = false;
        barsCamera.allowHDR = false;
        barsCamera.allowMSAA = false;
        barsCamera.enabled = false;
    }

    private void LateUpdate()
    {
        Rect viewport = Viewport;
        bool full = viewport.width >= 1f && viewport.height >= 1f;
        if (barsCamera != null && barsCamera.enabled == full) barsCamera.enabled = !full;

        int count = Camera.allCamerasCount;
        if (buffer.Length < count) buffer = new Camera[count];
        count = Camera.GetAllCameras(buffer);
        for (int i = 0; i < count; i++)
        {
            Camera view = buffer[i];
            if (view == null || view == barsCamera || view.targetTexture != null || view.cameraType != CameraType.Game) continue;
            if (view.rect != viewport) view.rect = viewport;
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
