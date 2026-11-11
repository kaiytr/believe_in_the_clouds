using System;
using UnityEngine;
using UnityEngine.UI;

// The data is separate from this temporary paper layout so final artwork can replace it.
public sealed class StoryDocumentScreen : MonoBehaviour
{
    private Canvas canvas;
    private Text body, title, error;
    private ScrollRect scroll;
    private Func<CurrencyResult> markRead;
    private Action closed;
    private int openedFrame;
    private bool closing;
    public bool IsOpen => canvas != null && canvas.gameObject.activeSelf;

    public void Open(FirstAltarStoryDefinition definition, Func<CurrencyResult> commit, Action onClosed)
    {
        if (IsOpen) return;
        if (canvas == null) Build();
        title.text = definition.documentTitle;
        body.text = definition.documentBody;
        markRead = commit; closed = onClosed; error.text = "";
        openedFrame = Time.frameCount; closing = false;
        canvas.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1f;
        UiEventSystemBoot.Ensure(); UiModal.Push(this);
    }

    private void OnEnable() => UiModal.CloseRequested += RequestClose;
    private void OnDisable()
    {
        UiModal.CloseRequested -= RequestClose;
        if (canvas != null) canvas.gameObject.SetActive(false);
        StopAllCoroutines();
        closing = false; closed = null; markRead = null;
        UiModal.Pop(this);
    }

    public void RequestClose()
    {
        if (!IsOpen || closing || Time.frameCount <= openedFrame + 1) return;
        CurrencyResult result = markRead?.Invoke();
        if (result == null || !result.Success)
        {
            error.text = "저장하지 못했습니다. 닫기를 눌러 다시 시도해 주세요.";
            return;
        }
        closing = true;
        canvas.gameObject.SetActive(false);
        // Keep this modal through the click/key release and the following frame.
        StartCoroutine(ReleaseAfterInput());
    }

    private System.Collections.IEnumerator ReleaseAfterInput()
    {
        yield return null;
        while (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Escape) || Input.GetMouseButton(0)) yield return null;
        yield return null;
        UiModal.Pop(this); closing = false; closed?.Invoke();
    }

    private void Build()
    {
        canvas = DeathResultView.CreateCanvas("First altar document", 12000);
        canvas.transform.SetParent(transform, false);
        RectTransform dim = DeathResultView.Box("Screen blocker", canvas.transform, new Color(0, 0, 0, .8f), 0, 0, 1, 1);
        dim.GetComponent<Image>().raycastTarget = true;
        RectTransform paper = DeathResultView.Box("Story document", dim, DeathResultView.Paper, .18f, .12f, .82f, .88f);
        title = DeathResultView.Label(paper, "", 34, DeathResultView.Ink, .06f, .84f, .94f, .96f);
        RectTransform viewport = DeathResultView.Box("Scroll viewport", paper, new Color(0, 0, 0, .02f), .07f, .20f, .93f, .81f);
        viewport.GetComponent<Image>().raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        var content = new GameObject("Document content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform rect = content.GetComponent<RectTransform>(); rect.SetParent(viewport, false);
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
        rect.sizeDelta = Vector2.zero;
        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        body = UiTheme.CreateText("Story text", rect, "", 27, DeathResultView.Ink, TextAnchor.UpperLeft);
        body.supportRichText = false; body.horizontalOverflow = HorizontalWrapMode.Wrap;
        body.verticalOverflow = VerticalWrapMode.Overflow; body.raycastTarget = false;
        scroll.content = rect;
        error = DeathResultView.Label(paper, "", 18, new Color(.65f, .13f, .1f), .05f, .12f, .95f, .19f);
        DeathResultView.MenuButton(paper, "닫기  E / ESC", .30f, .035f, .70f, .12f, RequestClose);
        canvas.gameObject.SetActive(false);
    }
}
