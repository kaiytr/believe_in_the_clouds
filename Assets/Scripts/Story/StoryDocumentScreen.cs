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

        // 예전 단색 네모와 같은 화면 비율(가로 64%, 세로 76%) 안에 storyBG를 채운다.
        RectTransform paper = DeathResultView.Box("Story document", dim, Color.white, .18f, .12f, .82f, .88f);

        Image frame = paper.GetComponent<Image>();
        Sprite art = LoadBackground();
        if (art != null)
        {
            frame.sprite = art;
            frame.color = Color.white;
            frame.type = Image.Type.Simple;
            frame.preserveAspect = true;
        }
        else
        {
            frame.color = DeathResultView.Paper;
        }
        frame.raycastTarget = true;

        title = DeathResultView.Label(paper, "", 32, DeathResultView.Ink, .10f, .70f, .90f, .82f);
        RectTransform viewport = DeathResultView.Box("Scroll viewport", paper, new Color(0, 0, 0, 0), .10f, .16f, .90f, .68f);
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
        body = UiTheme.CreateText("Story text", rect, "", 24, DeathResultView.Ink, TextAnchor.UpperLeft);
        body.supportRichText = false; body.horizontalOverflow = HorizontalWrapMode.Wrap;
        body.verticalOverflow = VerticalWrapMode.Overflow; body.raycastTarget = false;
        scroll.content = rect;
        error = DeathResultView.Label(paper, "", 16, new Color(.65f, .13f, .1f), .12f, .09f, .88f, .15f);
        DeathResultView.Label(paper, "닫기  E / ESC", 18, DeathResultView.Ink, .28f, .035f, .72f, .085f);
        canvas.gameObject.SetActive(false);
    }

    // Design/World/Story/storyBG.png 를 Resources로 복사해 둔 스프라이트.
    // Import 설정과 무관하게 Sprite/Texture2D 둘 다 시도한다.
    private static Sprite LoadBackground()
    {
        Sprite sprite = Resources.Load<Sprite>("UI/Story/storyBG");
        if (sprite != null) return sprite;
        Texture2D texture = Resources.Load<Texture2D>("UI/Story/storyBG");
        if (texture == null) return null;
        sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        sprite.name = "storyBG";
        return sprite;
    }
}
