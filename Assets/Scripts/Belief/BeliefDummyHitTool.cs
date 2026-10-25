using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct DummyBeliefHitData
{
    [Min(.001f)] public float BaseDamage;
    public PlayerAttackKind Kind;
    [Min(0)] public int HitIndex;

    public DummyBeliefHitData(float damage, PlayerAttackKind kind = PlayerAttackKind.Direct, int hitIndex = 0)
    {
        BaseDamage = damage;
        Kind = kind;
        HitIndex = hitIndex;
    }
}

/// <summary>Test-scene data injector using the normal HP and belief path.</summary>
public sealed class BeliefDummyHitTool : MonoBehaviour
{
    [SerializeField] private PlayerHealth player;
    [SerializeField] private EnemyUnit[] targets = Array.Empty<EnemyUnit>();
    [SerializeField] private DummyBeliefHitData hitData = new DummyBeliefHitData(1f);
    [SerializeField, Min(.01f)] private float burstInterval = .25f;
    [SerializeField] private bool showPanel = true;
    private int selectedIndex;
    private Coroutine burst;
    private PlayerAttackContext lastAttack;
    private EnemyUnit lastTarget;
    private DummyBeliefHitData lastData;
    private GUIStyle labelStyle, buttonStyle, headingStyle;

    public PlayerHealth Player => player;
    public EnemyUnit[] Targets => (EnemyUnit[])targets.Clone();
    public EnemyUnit SelectedTarget => selectedIndex >= 0 && selectedIndex < targets.Length ? targets[selectedIndex] : null;
    public bool IsBurstRunning => burst != null;
    public string LastStatus { get; private set; } = "더미 적중 데이터 대기";

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F2)) showPanel = !showPanel;
    }

    public bool SelectTarget(int index)
    {
        if (index < 0 || index >= targets.Length) return false;
        StopBurst();
        selectedIndex = index;
        return true;
    }

    public bool SendHit() => SendHit(hitData);
    public bool SendHit(DummyBeliefHitData data) => SendTo(SelectedTarget, data);

    private bool CanSend(EnemyUnit target, DummyBeliefHitData data) =>
        isActiveAndEnabled && player != null && player.gameObject.activeInHierarchy && !player.IsDead &&
        target != null && target.gameObject.activeInHierarchy && !target.IsDead &&
        target.Target == player.transform && target.HasCombatTarget && Time.timeScale > 0f &&
        data.BaseDamage > 0f && !float.IsNaN(data.BaseDamage) && !float.IsInfinity(data.BaseDamage) &&
        data.HitIndex >= 0;

    private bool SendTo(EnemyUnit target, DummyBeliefHitData data)
    {
        if (!CanSend(target, data))
        {
            LastStatus = "주입 불가: 살아 있는 플레이어·대상과 유효한 피해 데이터가 필요합니다.";
            return false;
        }
        lastAttack = new PlayerAttackContext(player.gameObject, data.Kind);
        lastTarget = target;
        lastData = data;
        bool accepted = lastAttack.TryHit(target, data.BaseDamage, data.HitIndex);
        LastStatus = accepted
            ? $"{target.name} 더미 적중 #{lastAttack.Id} · 기본 피해 {data.BaseDamage:0.###}"
            : "더미 적중 거부";
        return accepted;
    }

    public bool ReplayLastHit()
    {
        if (lastAttack == null || !CanSend(lastTarget, lastData))
        {
            LastStatus = "재전송할 유효한 타격 데이터가 없습니다.";
            return false;
        }
        bool accepted = lastAttack.TryHit(lastTarget, lastData.BaseDamage, lastData.HitIndex);
        LastStatus = accepted ? "동일 타격 재전송 수락" : $"동일 타격 #{lastAttack.Id} 중복 거부";
        return accepted;
    }

    public bool StartFiveHits()
    {
        if (burst != null || !CanSend(SelectedTarget, hitData)) return false;
        burst = StartCoroutine(FiveHits(SelectedTarget, hitData));
        return true;
    }

    private IEnumerator FiveHits(EnemyUnit target, DummyBeliefHitData data)
    {
        // Assign the coroutine handle before a first sample can collapse belief.
        yield return null;
        int sent = 0;
        for (int i = 0; i < 5; i++)
        {
            if (!CanSend(target, data) || !SendTo(target, data)) break;
            sent++;
            if (target.IsDead || target.IsVulnerable) break;
            if (i < 4) yield return new WaitForSeconds(burstInterval);
        }
        LastStatus = $"더미 연속 주입 {sent}회 완료 · 붕괴/사망 시 종료";
        burst = null;
    }

    public void StopBurst()
    {
        if (burst != null) StopCoroutine(burst);
        burst = null;
    }

    [ContextMenu("더미 데이터/적중 1회")]
    private void ContextSendHit() => SendHit();
    [ContextMenu("더미 데이터/연속 적중 5회")]
    private void ContextFiveHits() => StartFiveHits();
    [ContextMenu("더미 데이터/동일 타격 재전송")]
    private void ContextReplayHit() => ReplayLastHit();

    private void OnDisable()
    {
        StopBurst();
        lastAttack = null;
        lastTarget = null;
    }

    private void OnGUI()
    {
        if (!showPanel) return;
        EnsureStyles();
        float width = Mathf.Min(390f, Screen.width - 24f);
        GUILayout.BeginArea(new Rect(12f, 12f, width, Mathf.Min(330f, Screen.height - 24f)), GUI.skin.box);
        GUILayout.Label("믿음 검증 · 더미 데이터  [F2 숨김]", headingStyle);
        GUILayout.BeginHorizontal();
        for (int i = 0; i < targets.Length; i++)
            if (GUILayout.Button(targets[i] != null ? targets[i].name : "대상 없음", buttonStyle))
                SelectTarget(i);
        GUILayout.EndHorizontal();
        EnemyUnit selected = SelectedTarget;
        string phrase = selected != null && selected.Belief.Profile != null ? selected.Belief.Profile.Phrase : "대상 없음";
        GUILayout.Label((selected != null ? selected.name + ": " : "") + phrase, labelStyle);
        if (selected != null)
            GUILayout.Label($"HP {selected.CurrentHp:0.##}/{selected.MaxHp:0.##} · 믿음 {selected.Belief.CurrentBelief:0.##}", labelStyle);
        GUILayout.Label($"더미 기본 피해 {hitData.BaseDamage:0.###} · 연속 간격 {burstInterval:0.##}초", labelStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("적중 데이터 1회", buttonStyle)) SendHit();
        if (GUILayout.Button("연속 데이터 5회", buttonStyle)) StartFiveHits();
        GUILayout.EndHorizontal();
        if (GUILayout.Button("직전 타격 재전송 (중복 검사)", buttonStyle)) ReplayLastHit();
        GUILayout.Label(LastStatus, labelStyle);
        GUILayout.EndArea();
    }

    private void EnsureStyles()
    {
        if (labelStyle != null && labelStyle.font != null) return;
        Font font = null;
        foreach (EnemyUnit target in targets)
        {
            if (target == null) continue;
            Text label = target.GetComponentInChildren<Text>(true);
            if (label != null) { font = label.font; break; }
        }
        labelStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 14, wordWrap = true };
        buttonStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 14, wordWrap = true, fixedHeight = 30f };
        headingStyle = new GUIStyle(labelStyle) { fontStyle = FontStyle.Bold };
    }
}
