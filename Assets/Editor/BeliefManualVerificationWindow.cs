using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Editor-only fixtures. Every case starts a fresh, unsaved test scene.</summary>
public sealed class BeliefManualVerificationWindow : EditorWindow
{
    private const string ScenePath = "Assets/Scenes/BeliefTest.unity";
    private const string PendingKey = "BeliefManualVerification.Pending";
    private const string OwnedKey = "BeliefManualVerification.OwnsScene";
    private static readonly string[] Ids = { "DOG-01", "DOG-02", "DOG-03", "BEL-01", "BEL-02", "BEL-03", "EXE-01", "EXE-02", "EXE-03" };
    [SerializeField] private int caseIndex;
    [SerializeField] private string report = "항목을 누르면 새 테스트 개체로 실제 판정을 실행합니다.";
    [SerializeField] private bool passed;
    private readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    private EnemyUnit enemy;
    private EnemyAttack attack;
    private PlayerHealth player;
    private BeliefDummyHitTool dummy;
    private EnemyAttackResult? result;
    private bool running;
    private Vector2 scroll;

    [MenuItem("Tools/Belief/9종 직접 검증")]
    public static void Open()
    {
        var window = GetWindow<BeliefManualVerificationWindow>("믿음 9종 검증");
        window.minSize = new Vector2(410f, 430f);
        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.update += Tick;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorApplication.update -= Tick;
        steps.Clear();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("실제 판정 + 더미 적중 데이터", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("각 항목은 BeliefTest를 새로 열어 후보를 하나로 고정합니다. 플레이어 공격은 추가하지 않습니다. 완료 시 일시정지하며, Game 화면의 말풍선·게이지를 확인할 수 있습니다.", MessageType.Info);
        using (new EditorGUI.DisabledScope(running || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying))
        {
            for (int row = 0; row < 3; row++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int column = 0; column < 3; column++)
                {
                    int index = row * 3 + column;
                    if (GUILayout.Button(Ids[index], GUILayout.Height(32f))) Queue(index);
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(Ids[Mathf.Clamp(caseIndex, 0, 8)] + (running ? " 실행 중" : passed ? " 통과 · 화면 확인 대기" : ""), EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.SelectableLabel(report, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(220f), GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        if (GUILayout.Button("검증 종료 · 저장된 테스트 씬 복원", GUILayout.Height(30f)))
        {
            SessionState.SetInt(PendingKey, -1);
            running = false;
            steps.Clear();
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            else Restore();
        }
    }

    private void Queue(int index)
    {
        SessionState.SetInt(PendingKey, index);
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        else Prepare();
    }

    private void Prepare()
    {
        int index = SessionState.GetInt(PendingKey, -1);
        if (index < 0) return;
        if (SceneManager.GetActiveScene().isDirty && !SessionState.GetBool(OwnedKey, false))
        {
            report = "열려 있는 씬에 저장되지 않은 변경이 있습니다. 저장 후 다시 실행하세요.";
            SessionState.SetInt(PendingKey, -1);
            return;
        }
        caseIndex = index;
        passed = false;
        report = Ids[index] + " 새 개체 준비 중…";
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var unit = root.GetComponent<EnemyUnit>();
            if (unit == null) continue;
            bool selected = root.name == (index < 3 ? "Dog" : index < 6 ? "Believer" : "Executive");
            root.SetActive(selected);
            if (!selected) continue;
            var serialized = new SerializedObject(root.GetComponent<BeliefController>());
            var candidates = serialized.FindProperty("beliefTypeCandidates");
            candidates.arraySize = 1;
            candidates.GetArrayElementAtIndex(0).intValue = (int)BeliefType.DogCounterattack + index;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // Hold the real AI until EnemyUnit.Start has acquired the player and built its UI.
            unit.enabled = false;
            Set(unit.GetComponent<EnemyAttack>(), "cooldownRemaining", 1000f);
        }
        SessionState.SetBool(OwnedKey, true);
        EditorApplication.isPlaying = true;
    }

    private void OnPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetInt(PendingKey, -1) >= 0)
        {
            caseIndex = SessionState.GetInt(PendingKey, 0);
            SessionState.SetInt(PendingKey, -1);
            report = Ids[caseIndex] + "\n";
            running = true;
            steps.Clear();
            steps.Push(Run());
        }
        else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(OwnedKey, false))
        {
            running = false;
            steps.Clear();
            EditorApplication.delayCall += () => { Restore(); Prepare(); };
        }
    }

    private static void Restore()
    {
        if (!SessionState.GetBool(OwnedKey, false)) return;
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(OwnedKey, false);
    }

    private void Tick()
    {
        if (!running || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
        try
        {
            if (steps.Count == 0) return;
            IEnumerator step = steps.Peek();
            if (!step.MoveNext() && steps.Count > 0 && ReferenceEquals(steps.Peek(), step)) steps.Pop();
            else if (step.Current is IEnumerator nested) steps.Push(nested);
        }
        catch (Exception exception)
        {
            report += "\n실패: " + exception.Message;
            Debug.LogException(exception);
            Finish(false);
        }
        Repaint();
    }

    private IEnumerator Run()
    {
        Time.timeScale = 1f;
        enemy = UnityEngine.Object.FindFirstObjectByType<EnemyUnit>();
        player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerHealth>();
        dummy = UnityEngine.Object.FindFirstObjectByType<BeliefDummyHitTool>();
        Require(enemy != null && player != null && dummy != null, "테스트 대상이 없습니다.");
        dummy.SelectTarget(caseIndex / 3);
        player.GetComponent<PlayerMove>().enabled = false;
        Kinematic(player.GetComponent<Rigidbody2D>());
        Kinematic(enemy.GetComponent<Rigidbody2D>());
        attack = enemy.GetComponent<EnemyAttack>();
        Set(attack, "cooldownRemaining", 1000f);
        enemy.enabled = true;
        yield return Frames(2);
        enemy.enabled = false;
        enemy.LockMovement();
        foreach (var contact in enemy.GetComponentsInChildren<EnemyContactDamage>(true)) contact.SetSuppressed(true);
        player.ResetHealth();
        attack.AttackResolved += OnResolved;
        Camera camera = Camera.main;
        camera.orthographicSize = 5f;
        camera.transform.position = new Vector3(enemy.transform.position.x + 1f, enemy.transform.position.y + 1.2f, -10f);
        Check(100f, "초기값");
        switch (caseIndex)
        {
            case 0:
                yield return Evade();
                Check(100f, "회피만으로 감소하지 않음");
                yield return Wait(2.1f);
                Hit(); Check(100f, "2초 초과 반격 제외");
                yield return Evade();
                Hit(); Check(0f, "2초 이내 반격 -100");
                break;
            case 1:
                yield return Evade(); Check(50f, "실제 돌진 회피 -50");
                yield return Until(() => !attack.IsAttacking);
                Place(Vector2.right * 2f); Begin();
                attack.SetVulnerable(true);
                Check(50f, "발동 전 취소는 제외");
                attack.SetVulnerable(false);
                yield return Evade(); Check(0f, "두 번째 HP 피해 실패 -50");
                break;
            case 2:
                enemy.SetFacingDirection(1f);
                Place(Vector2.right * 2f); Hit(); Check(100f, "정면 적중 제외");
                Place(Vector2.left * 2f); Hit(); Check(60f, "후방 적중 -40");
                float hp = enemy.CurrentHp;
                Require(!dummy.ReplayLastHit(), "중복 타격이 수락되었습니다.");
                Require(Mathf.Approximately(hp, enemy.CurrentHp), "중복 HP 감소");
                Check(60f, "동일 타격 재전송 거부");
                Hit(); Check(20f, "후방 두 번째 -40");
                Hit(); Check(0f, "후방 세 번째 붕괴");
                break;
            case 3:
                Place(Vector2.right * 2f);
                Hit(); Check(100f, "첫 타격은 연속 아님");
                yield return Wait(.25f); Hit(); Check(75f, "인접 적중 -25");
                yield return Wait(2.1f); Hit(); Check(75f, "2초 초과 시 연속 초기화");
                Require(dummy.StartFiveHits(), "연속 더미 주입 실패");
                yield return Until(() => !dummy.IsBurstRunning);
                Check(0f, "0.25초 간격 더미로 붕괴");
                break;
            case 4:
                Place(Vector2.left * 2f); Begin();
                yield return Until(() => result.HasValue);
                Require(result.Value.PlayerHpDamaged, "실제 돌 HP 적중이 필요합니다.");
                Check(100f, "돌 HP 적중은 감소하지 않음");
                yield return Evade(); Check(50f, "돌 비행 완료 후 회피 -50");
                yield return Evade(); Check(0f, "두 번째 돌 HP 피해 실패 -50");
                break;
            case 5:
                Place(Vector2.right * 3f); Hit(); Check(100f, "후퇴 전 적중 제외");
                enemy.enabled = true;
                yield return Until(() => ((BelieverEnemy)enemy).IsRetreating);
                enemy.enabled = false;
                yield return Frames(2);
                Hit(); Check(50f, "실제 후퇴 중 거리 진입 후 적중 -50");
                Hit(); Check(50f, "같은 후퇴 구간의 중복 성공 제외");
                enemy.ChangeState(EnemyState.Idle); yield return Frames(2);
                enemy.enabled = true;
                yield return Until(() => ((BelieverEnemy)enemy).IsRetreating);
                enemy.enabled = false; yield return Frames(2);
                Hit(); Check(0f, "새 후퇴 구간에서 두 번째 성공");
                break;
            case 6:
                yield return Evade();
                Check(0f, "실제 마법진 효과 시 이탈 -100");
                Require(Mathf.Approximately(player.CurrentHp, player.MaxHp), "회피 중 HP 감소");
                break;
            case 7:
                enemy.SetFacingDirection(1f);
                Place(Vector2.left * 2f);
                yield return Wait(1f); Check(100f, "후방 1초는 미충족");
                Place(Vector2.right * 2f); yield return Frames(2);
                Begin(); yield return Frames(2);
                Place(Vector2.left * 2f); yield return Wait(2.1f);
                enemy.enabled = true;
                yield return Frames(1);
                enemy.enabled = false;
                Check(75f, "중도 이탈 후 새 연속 2초 -25");
                // Keep the frozen fixture behind the enemy for the UI inspection.
                enemy.LockMovement();
                break;
            case 8:
                Place(Vector2.left * 2f); Hit(); Check(100f, "시전 준비 전 적중 제외");
                Begin(); yield return Frames(2);
                Hit(); Check(50f, "실제 준비 중 적중 -50");
                Hit(); Check(0f, "준비 중 두 번째 적중으로 붕괴");
                Require(!attack.IsWindingUp && result.HasValue && !result.Value.WasLaunched,
                    "붕괴 후 발동 전 마법진이 취소되어야 합니다.");
                report += "발동 전 시전 취소 확인\n";
                break;
        }
        camera.transform.position = new Vector3(enemy.transform.position.x - 2f, enemy.transform.position.y + 1.2f, -10f);
        Canvas.ForceUpdateCanvases();
        Require(enemy.GetComponentInChildren<UnityEngine.UI.Text>().text == enemy.Belief.Profile.Phrase, "믿음 문구 불일치");
        report += $"최종 HP {enemy.CurrentHp:0.##}/{enemy.MaxHp:0.##} · 믿음 {enemy.Belief.CurrentBelief:0.##}\n";
        report += "판정 통과. Game 화면의 한국어·게이지·더미 패널을 직접 확인하세요.";
        yield return Frames(2);
        Finish(true);
    }

    private void OnResolved(EnemyAttack source, EnemyAttackResult value) => result = value;

    private IEnumerator Evade()
    {
        yield return Until(() => !attack.IsAttacking);
        player.ResetHealth();
        Place(caseIndex >= 4 ? Vector2.left * 2f : Vector2.right * 2f);
        Begin();
        yield return Until(() => attack.WasPlayerThreatenedDuringWarning);
        Place(caseIndex >= 6 ? Vector2.left * 5f : new Vector2(caseIndex >= 4 ? -2f : 2f, 3f));
        yield return Until(() => result.HasValue);
        Require(result.Value.Outcome == AttackOutcome.EvadedByPlayer && result.Value.CompletedNormally, "실제 회피 결과가 필요합니다.");
    }

    private void Begin()
    {
        result = null;
        Set(attack, "cooldownRemaining", 0f);
        Require(attack.TryAttack(player.transform), "실제 공격 시작 실패");
    }

    private void Hit() => Require(dummy.SendHit(new DummyBeliefHitData(1f)), "더미 적중 거부");

    private void Check(float expected, string label)
    {
        Require(Mathf.Approximately(enemy.Belief.CurrentBelief, expected), $"{label}: 기대 {expected}, 실제 {enemy.Belief.CurrentBelief}");
        report += $"✓ {label}: {enemy.Belief.CurrentBelief:0.##}\n";
    }

    private void Finish(bool success)
    {
        running = false;
        passed = success;
        steps.Clear();
        if (attack != null) attack.AttackResolved -= OnResolved;
        Directory.CreateDirectory("Logs");
        File.AppendAllText("Logs/BeliefManualVerification.log", $"\n{DateTime.Now:O} {Ids[caseIndex]} {(success ? "PASS" : "FAIL")}\n{report}\n");
        EditorApplication.isPaused = true;
        Repaint();
    }

    private void Place(Vector2 offset)
    {
        Physics2D.SyncTransforms();
        Vector2 delta = (Vector2)enemy.BodyCollider.bounds.center + offset - (Vector2)player.BodyCollider.bounds.center;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        body.position += delta;
        player.transform.position = new Vector3(body.position.x, body.position.y, player.transform.position.z);
        body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
    }

    private static void Kinematic(Rigidbody2D body)
    {
        body.bodyType = RigidbodyType2D.Kinematic;
        body.linearVelocity = Vector2.zero;
    }

    private static IEnumerator Frames(int count)
    {
        // EditorApplication.update may tick more often than the player loop.
        int frame = Time.frameCount;
        while (Time.frameCount < frame + count) yield return null;
    }

    private static IEnumerator Wait(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
    }

    private static IEnumerator Until(Func<bool> predicate)
    {
        double deadline = EditorApplication.timeSinceStartup + 15d;
        while (!predicate())
        {
            Require(EditorApplication.timeSinceStartup < deadline, "실제 상태 변경 시간 초과");
            yield return null;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Set(object target, string name, object value)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field == null) continue;
            field.SetValue(target, value);
            return;
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }
}
