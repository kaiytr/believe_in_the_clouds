using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Presentation owns no coins. Grant/token IDs settle only their reservation.
public sealed class CoinFlightView : MonoBehaviour
{
    private sealed class Flight
    {
        public string GrantId, TokenId;
        public Vector3 WorldPosition, LandingPosition;
        public Camera SourceCamera;
        public CoinDropOrigin DropOrigin;
        public Vector2 LaunchPosition;
        public float HorizontalScatter, ArcHeight, Age, Delay, GroundSeconds, Duration, NextRetry;
        public Image Image;
        public bool DropInitialized, HudFlightStarted, Landed;
    }
    private EconomyService service;
    private EconomySettings settings;
    private Canvas canvas;
    private RectTransform canvasRect;
    private Sprite droppedCoinSprite;
    private readonly Queue<Flight> waiting = new Queue<Flight>();
    private readonly List<Flight> active = new List<Flight>();
    private readonly Stack<Image> pool = new Stack<Image>();
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[16];
    private const float MinSoundGap = .04f;   // many coins land together; do not stack a sound for each
    private AudioSource audioSource;
    private float lastLandSound = -1f;
    public int ActiveCount => active.Count;
    public int CreatedCount { get; private set; }
    public bool HasFlights => active.Count > 0 || waiting.Count > 0;
    private Camera CanvasCamera => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

    public void Initialize(EconomyService economy, EconomySettings presentationSettings)
    {
        service = economy; settings = presentationSettings;
        droppedCoinSprite = Resources.Load<Sprite>("UI/Hud/DroppedCoin");
        if (droppedCoinSprite == null)
            Debug.LogError("Missing dropped coin artwork at Resources/UI/Hud/DroppedCoin.png.");
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150; // HUD 100, shop/modal 200.
        canvasRect = GetComponent<RectTransform>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false; audioSource.loop = false; audioSource.spatialBlend = 0f;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        service.FlightRequested += Enqueue;
    }

    private void Enqueue(CoinFlightRequest request)
    {
        Camera source = request.SourceCamera != null ? request.SourceCamera : Camera.main;
        for (int i = 0; i < request.Grant.tokens.Count; i++)
        {
            PendingToken token = request.Grant.tokens[i];
            if (token.settled) continue;
            waiting.Enqueue(new Flight
            {
                GrantId = request.Grant.grantId, TokenId = token.tokenId,
                WorldPosition = request.Position, SourceCamera = source, DropOrigin = request.DropOrigin,
                HorizontalScatter = Random.insideUnitCircle.x * Mathf.Max(0f, settings.scatterRadius),
                ArcHeight = Mathf.Max(0f, settings.dropArcHeight) * Random.Range(.75f, 1.25f),
                GroundSeconds = Mathf.Max(0f, settings.groundRestSeconds),
                Duration = Random.Range(Mathf.Max(.01f, settings.flightMinSeconds), Mathf.Max(.01f, settings.flightMaxSeconds)),
                Delay = request.PresentationDelay + i * Mathf.Max(0f, settings.tokenStaggerSeconds)
            });
        }
    }

    private Vector3 FindLandingPosition(Flight flight)
    {
        Vector3 screen = flight.SourceCamera.WorldToScreenPoint(flight.WorldPosition);
        screen.x += flight.HorizontalScatter * canvas.scaleFactor;
        Vector3 scattered = flight.SourceCamera.ScreenToWorldPoint(screen);
        Vector3 probe = new Vector3(scattered.x, flight.WorldPosition.y + .1f, flight.WorldPosition.z);
        if (TryFindGround(probe, out Vector3 landing)) return landing;
        // Keep drops on a narrow ledge when the scattered position would fall off its edge.
        if (TryFindGround(flight.WorldPosition + Vector3.up * .1f, out landing)) return landing;
        // Diagnostic rewards can originate outside the terrain. They must still finish and settle.
        return new Vector3(scattered.x, flight.WorldPosition.y - .75f, flight.WorldPosition.z);
    }

    private bool TryFindGround(Vector3 origin, out Vector3 landing)
    {
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = LayerMask.GetMask("Ground"), useTriggers = false };
        int count = Physics2D.Raycast(origin, Vector2.down, filter, groundHits, 100f);
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = groundHits[i];
            if (hit.distance <= 0f || hit.normal.y < .5f) continue;
            landing = new Vector3(hit.point.x, hit.point.y, origin.z);
            return true;
        }
        landing = default;
        return false;
    }

    // Drop and rest positions are reprojected every frame as the camera moves.
    private Vector2 ProjectToCanvas(Camera camera, Vector3 worldPosition)
    {
        Vector3 point = camera != null ? camera.WorldToScreenPoint(worldPosition) : Vector3.zero;
        if (point.z < 0f) { point.x = Screen.width - point.x; point.y = Screen.height - point.y; }
        point.x = Mathf.Clamp(point.x, 12f, Mathf.Max(12f, Screen.width - 12f));
        point.y = Mathf.Clamp(point.y, 12f, Mathf.Max(12f, Screen.height - 12f));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, point, CanvasCamera, out Vector2 local);
        return local;
    }

    private void Update()
    {
        if (service == null) return;
        if (service.HasPendingDeath) { CancelFlights(); return; }
        int limit = Mathf.Clamp(settings.maxVisibleTokens, 1, 64);
        while (waiting.Count > 0 && active.Count < limit)
        {
            Flight flight = waiting.Dequeue();
            if (!service.IsPendingToken(flight.GrantId, flight.TokenId)) continue;
            flight.Image = Rent();
            active.Add(flight);
        }
        RectTransform target = DoctrineHud.CurrencyTarget;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Flight flight = active[i];
            if (!service.IsPendingToken(flight.GrantId, flight.TokenId)) { Release(i); continue; }
            bool noPresentation = target == null || flight.SourceCamera == null;
            if (!noPresentation && flight.DropOrigin != null && !flight.DropOrigin.IsReady)
            {
                // Keep a fallback position even if the source disappears unexpectedly.
                flight.WorldPosition = flight.DropOrigin.Position;
                flight.Image.enabled = false;
                continue;
            }
            flight.Age += Time.unscaledDeltaTime;
            float elapsed = flight.Age - flight.Delay;
            float scatter = Mathf.Max(0f, settings.scatterSeconds);
            float groundEnd = scatter + flight.GroundSeconds;
            bool arrived = noPresentation || elapsed >= groundEnd + flight.Duration;
            flight.Image.enabled = !noPresentation && elapsed >= 0f && !arrived;
            if (arrived)
            {
                if (Time.unscaledTime < flight.NextRetry) continue;
                flight.NextRetry = Time.unscaledTime + .5f;
                if (service.TrySettleToken(flight.GrantId, flight.TokenId).Success) Release(i);
                continue;
            }
            if (elapsed < 0f) continue;
            if (!flight.DropInitialized)
            {
                if (flight.DropOrigin != null) flight.WorldPosition = flight.DropOrigin.Position;
                flight.LandingPosition = FindLandingPosition(flight);
                flight.DropInitialized = true;
            }
            // The coin touches the ground when its arc ends.
            if (!flight.Landed && elapsed >= scatter)
            {
                flight.Landed = true;
                PlayLandSound();
            }
            Vector2 position;
            float radius = flight.Image.rectTransform.rect.height * .5f;
            if (elapsed < groundEnd)
            {
                float t = scatter <= 0f ? 1f : Mathf.Clamp01(elapsed / scatter);
                Vector3 world = Vector3.Lerp(flight.WorldPosition, flight.LandingPosition, t);
                world.y += 4f * flight.ArcHeight * t * (1f - t);
                // The landing point is the surface; lift the icon's centre by its visual radius.
                position = ProjectToCanvas(flight.SourceCamera, world) + Vector2.up * (radius * t);
            }
            else
            {
                if (!flight.HudFlightStarted)
                {
                    flight.LaunchPosition = ProjectToCanvas(flight.SourceCamera, flight.LandingPosition) + Vector2.up * radius;
                    flight.HudFlightStarted = true;
                }
                Canvas targetCanvas = target.GetComponentInParent<Canvas>();
                Camera uiCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? targetCanvas.worldCamera : null;
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, target.TransformPoint(target.rect.center));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, CanvasCamera, out Vector2 destination);
                float t = Mathf.Clamp01((elapsed - groundEnd) / flight.Duration);
                t *= t; // Accelerate into the HUD after resting on the ground.
                Vector2 from = flight.LaunchPosition;
                Vector2 control = (from + destination) * .5f + Vector2.up * 100f;
                position = (1 - t) * (1 - t) * from + 2 * (1 - t) * t * control + t * t * destination;
            }
            flight.Image.rectTransform.anchoredPosition = position;
        }
    }

    private void PlayLandSound()
    {
        if (audioSource == null || settings.coinDropSound == null) return;
        if (Time.unscaledTime - lastLandSound < MinSoundGap) return;
        lastLandSound = Time.unscaledTime;
        audioSource.pitch = Random.Range(.94f, 1.08f);
        audioSource.PlayOneShot(settings.coinDropSound, settings.coinDropVolume);
    }

    private Image Rent()
    {
        Image token;
        if (pool.Count > 0) token = pool.Pop();
        else
        {
            token = UiTheme.CreateImage("Coin", transform, Color.white).GetComponent<Image>();
            token.raycastTarget = false;
            CreatedCount++;
        }
        token.sprite = droppedCoinSprite != null ? droppedCoinSprite : UiTheme.CircleSprite;
        token.rectTransform.sizeDelta = Vector2.one * Mathf.Max(1f, settings.tokenDiameter);
        token.color = Color.white;
        token.gameObject.SetActive(true);
        token.enabled = false;
        return token;
    }

    private void Release(int index)
    {
        Image token = active[index].Image;
        token.gameObject.SetActive(false);
        pool.Push(token);
        active.RemoveAt(index);
    }

    public void CancelFlights()
    {
        waiting.Clear();
        for (int i = active.Count - 1; i >= 0; i--) Release(i);
    }

    public void Shutdown()
    {
        CancelFlights();
        if (service != null) service.FlightRequested -= Enqueue;
        service = null;
    }

    private void OnDisable()
    {
        CancelFlights();
        service?.SettleAllPending();
    }
    private void OnDestroy() { if (service != null) service.FlightRequested -= Enqueue; }
}
