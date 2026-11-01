using UnityEngine;

public sealed class ChestView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private SpriteRenderer border;
    [SerializeField] private TextMesh stateLabel;
    private TextMesh rewardLabel;
    private float rewardStarted;
    private bool lastOpened, lastSelected, hasState;

    private void Awake()
    {
        if (stateLabel == null) return;
        stateLabel.font = UiTheme.GetFont();
        stateLabel.GetComponent<MeshRenderer>().sharedMaterial = stateLabel.font.material;
    }

    public void SetState(bool opened, bool selected)
    {
        if (hasState && lastOpened == opened && lastSelected == selected) return;
        hasState = true; lastOpened = opened; lastSelected = selected;
        if (body != null)
        {
            body.color = opened ? new Color(.24f, .18f, .12f) : new Color(.72f, .46f, .2f);
            body.transform.localScale = new Vector3(1.2f, opened ? .45f : .9f, 1);
        }
        if (border != null) border.color = selected ? new Color(1, .88f, .52f) : new Color(.32f, .24f, .15f);
        if (stateLabel != null) { stateLabel.text = opened ? "열림" : "상자"; stateLabel.color = opened ? Color.gray : new Color(.92f, .86f, .7f); }
    }

    public void ShowReward(ChestRewardReceipt reward)
    {
        if (reward == null || !ShopCatalog.TryGet(reward.pieceId, out ShopEntry entry)) return;
        if (rewardLabel == null)
        {
            var root = new GameObject("Piece reward"); root.transform.SetParent(transform, false);
            rewardLabel = root.AddComponent<TextMesh>();
            rewardLabel.font = UiTheme.GetFont(); rewardLabel.GetComponent<MeshRenderer>().sharedMaterial = rewardLabel.font.material;
            rewardLabel.anchor = TextAnchor.MiddleCenter; rewardLabel.alignment = TextAlignment.Center;
            rewardLabel.fontSize = 48; rewardLabel.characterSize = .045f;
            rewardLabel.GetComponent<MeshRenderer>().sortingOrder = 30;
        }
        rewardLabel.text = $"{entry.RarityLabel} · {entry.Label} ×1" +
            (reward.appliedCoins < reward.rolledCoins ? "\n코인 보유 한도" : "");
        rewardLabel.color = reward.rarity == ShopRarity.Rare ? new Color(.4f, .72f, 1) :
            reward.rarity == ShopRarity.Uncommon ? new Color(.45f, 1, .6f) : new Color(1, .94f, .78f);
        rewardLabel.gameObject.SetActive(true); rewardStarted = Time.unscaledTime;
    }
    private void Update()
    {
        if (rewardLabel == null || !rewardLabel.gameObject.activeSelf) return;
        float t = (Time.unscaledTime - rewardStarted) / .8f;
        rewardLabel.transform.localPosition = new Vector3(0, 1.9f + t * .5f, 0);
        Color color = rewardLabel.color; color.a = Mathf.Clamp01((1 - t) * 3); rewardLabel.color = color;
        if (t >= 1) rewardLabel.gameObject.SetActive(false);
    }
}
