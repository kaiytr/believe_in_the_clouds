using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Presentation owns no coins. Grant/token IDs settle only their reservation.
public sealed class CoinFlightView : MonoBehaviour
{
    private sealed class Flight
    {
        public string GrantId, TokenId;
        public Vector2 Start, Scatter;
        public float Age, Delay, Duration, NextRetry;
        public Image Image;
        public bool HasCamera;
    }
    private EconomyService service;
    private EconomySettings settings;
    private Canvas canvas;
    private RectTransform canvasRect;
    private readonly Queue<Flight> waiting = new Queue<Flight>();
    private readonly List<Flight> active = new List<Flight>();
    private readonly Stack<Image> pool = new Stack<Image>();
    public int ActiveCount => active.Count;
    public int CreatedCount { get; private set; }
    public bool HasFlights => active.Count > 0 || waiting.Count > 0;

    public void Initialize(EconomyService economy, EconomySettings presentationSettings)
    {
        service = economy; settings = presentationSettings;
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150; // HUD 100, shop/modal 200.
        canvasRect = GetComponent<RectTransform>();
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        service.FlightRequested += Enqueue;
    }

    private void Enqueue(CoinFlightRequest request)
    {
        Camera source = request.SourceCamera != null ? request.SourceCamera : Camera.main;
        Vector3 point = source != null ? source.WorldToScreenPoint(request.Position) : Vector3.zero;
        if (point.z < 0f) { point.x = Screen.width - point.x; point.y = Screen.height - point.y; }
        point.x = Mathf.Clamp(point.x, 12f, Mathf.Max(12f, Screen.width - 12f));
        point.y = Mathf.Clamp(point.y, 12f, Mathf.Max(12f, Screen.height - 12f));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, point, null, out Vector2 start);
        for (int i = 0; i < request.Grant.tokens.Count; i++)
        {
            PendingToken token = request.Grant.tokens[i];
            if (token.settled) continue;
            waiting.Enqueue(new Flight
            {
                GrantId = request.Grant.grantId, TokenId = token.tokenId, Start = start,
                Scatter = Random.insideUnitCircle * settings.scatterRadius,
                Duration = Random.Range(Mathf.Max(.01f, settings.flightMinSeconds), Mathf.Max(.01f, settings.flightMaxSeconds)),
                Delay = i * Mathf.Max(0f, settings.tokenStaggerSeconds), HasCamera = source != null
            });
        }
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
            flight.Age += Time.unscaledDeltaTime;
            bool noPresentation = target == null || !flight.HasCamera;
            float elapsed = flight.Age - flight.Delay;
            float scatter = Mathf.Max(0f, settings.scatterSeconds);
            bool arrived = noPresentation || elapsed >= scatter + flight.Duration;
            flight.Image.enabled = !noPresentation && elapsed >= 0f && !arrived;
            if (arrived)
            {
                if (Time.unscaledTime < flight.NextRetry) continue;
                flight.NextRetry = Time.unscaledTime + .5f;
                if (service.TrySettleToken(flight.GrantId, flight.TokenId).Success) Release(i);
                continue;
            }
            if (elapsed < 0f) continue;
            Vector2 position;
            if (elapsed < scatter)
                position = Vector2.Lerp(flight.Start, flight.Start + flight.Scatter, Mathf.SmoothStep(0, 1, elapsed / scatter));
            else
            {
                Canvas targetCanvas = target.GetComponentInParent<Canvas>();
                Camera uiCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? targetCanvas.worldCamera : null;
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, target.TransformPoint(target.rect.center));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 destination);
                float t = Mathf.SmoothStep(0f, 1f, (elapsed - scatter) / flight.Duration);
                Vector2 from = flight.Start + flight.Scatter;
                Vector2 control = (from + destination) * .5f + Vector2.up * 100f;
                position = (1 - t) * (1 - t) * from + 2 * (1 - t) * t * control + t * t * destination;
            }
            flight.Image.rectTransform.anchoredPosition = position;
        }
    }

    private Image Rent()
    {
        Image token;
        if (pool.Count > 0) token = pool.Pop();
        else
        {
            token = UiTheme.CreateImage("Coin", transform, settings.tokenColor).GetComponent<Image>();
            token.sprite = UiTheme.CircleSprite;
            token.raycastTarget = false;
            CreatedCount++;
        }
        token.rectTransform.sizeDelta = Vector2.one * Mathf.Max(1f, settings.tokenDiameter);
        token.color = settings.tokenColor;
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
