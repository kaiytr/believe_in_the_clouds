using System;
using System.Collections.Generic;
using UnityEngine;

// Doctrine execution engine.
// Responsibilities: active doctrine list, register/unregister, event intake,
// condition match, effect execution request.
// Flow: game event (DoctrineEventBus) -> NotifyEvent ->
//         scan active doctrines -> matching doctrines execute (DoctrineEffectExecutor).
// Common match (MatchesRule) + common execution path; no per-doctrine branches.
// One doctrine's execution never stops the remaining doctrines.
// Table rules:
//   Heal has a per-doctrine 20s cooldown (keyed by doctrine rule + owner).
//   IncreaseSpeed / IncreaseAttack: 5s duration, 10s cooldown, replace (never stack).
public class DoctrineManager : MonoBehaviour
{
    public static DoctrineManager Instance { get; private set; }

    [Header("Safety")]
    [Tooltip("Max nesting depth to break chains like Heal re-triggering OnHit.")]
    [SerializeField] private int maxNestedDepth = 8;

    [Header("Log")]
    [Tooltip("Enable for trigger/activation logs. Off by default (console spam lags the editor).")]
    [SerializeField] private bool verboseLogs = false;

    [Serializable]
    public sealed class ActiveDoctrine
    {
        public Doctrine Doctrine;
        public GameObject Owner;

        public ActiveDoctrine(Doctrine doctrine, GameObject owner)
        {
            Doctrine = doctrine;
            Owner = owner;
        }
    }

    [Serializable]
    private sealed class CooldownEntry
    {
        public string Key;
        public float ReadyAt;
    }

    private readonly List<ActiveDoctrine> activeDoctrines = new List<ActiveDoctrine>();
    private readonly List<CooldownEntry> cooldowns = new List<CooldownEntry>();
    private int notifyDepth;
    private bool executingExplosion;

    // Display-only events. Match/execute logic never depends on these.
    public event Action<ActiveDoctrine> Activated;
    public event Action<ActiveDoctrine> Deactivated;
    public event Action<ActiveDoctrine, GameObject> Triggered;
    public event Action Cleared;

    public IReadOnlyList<ActiveDoctrine> ActiveDoctrines => activeDoctrines;
    public int ActiveCount => activeDoctrines.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[Doctrine] Duplicate DoctrineManager destroyed.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        EnsureSubscribed();
    }

    private void OnDisable()
    {
        DoctrineEventBus.EventRaised -= NotifyEvent;
    }

    // Idempotent subscribe for harnesses running without Awake/OnEnable messages.
    public void EnsureSubscribed()
    {
        DoctrineEventBus.EventRaised -= NotifyEvent;
        DoctrineEventBus.EventRaised += NotifyEvent;
    }

    // Shop/slot/UI entry: validates then activates the built Doctrine.
    public bool TryActivate(Doctrine doctrine, out string reason)
    {
        return TryActivate(doctrine, null, out reason);
    }

    // Owner-bound registration: only reacts to that object's events.
    public bool TryActivate(Doctrine doctrine, GameObject owner, out string reason) =>
        TryActivate(doctrine, owner, out reason, out _);

    public bool TryActivate(Doctrine doctrine, GameObject owner, out string reason, out ActiveDoctrine entry)
    {
        entry = null;
        if (!DoctrineValidator.TryValidate(doctrine, owner, out reason))
        {
            Debug.LogWarning($"[Doctrine] Activation failed: {reason}");
            return false;
        }

        entry = new ActiveDoctrine(doctrine.Clone(), owner);
        activeDoctrines.Add(entry);

        if (verboseLogs)
        {
            Debug.Log($"[Doctrine] Activated: {doctrine}");
        }
        Activated?.Invoke(entry);
        return true;
    }

    public bool Deactivate(Doctrine doctrine)
    {
        if (doctrine == null)
        {
            return false;
        }

        int index = activeDoctrines.FindIndex(
            a => a.Doctrine != null && a.Doctrine.HasSameRule(doctrine));
        return DeactivateAt(index);
    }

    public bool DeactivateEntry(ActiveDoctrine entry) => DeactivateAt(activeDoctrines.IndexOf(entry));

    private bool DeactivateAt(int index)
    {
        if (index < 0)
        {
            return false;
        }

        ActiveDoctrine removed = activeDoctrines[index];
        activeDoctrines.RemoveAt(index);
        ClearRemovedBuff(removed);

        RemoveCooldownsFor(removed);
        if (verboseLogs)
        {
            Debug.Log($"[Doctrine] Deactivated: {removed.Doctrine}");
        }
        Deactivated?.Invoke(removed);
        return true;
    }

    public void ClearForOwner(GameObject owner)
    {
        if (owner == null) return;
        // Remove by entry/owner, not by rule: an enemy can share the same rule.
        for (int i = activeDoctrines.Count - 1; i >= 0; i--)
        {
            ActiveDoctrine entry = activeDoctrines[i];
            if (entry.Owner != owner) continue;
            activeDoctrines.RemoveAt(i);
            RemoveCooldownsFor(entry);
            Deactivated?.Invoke(entry);
        }
        owner.GetComponent<DoctrineStats>()?.ClearAllBuffs();
    }

    public void Clear()
    {
        // 해제 시 남아 있는 이속/피해 버프도 함께 지운다.
        for (int i = 0; i < activeDoctrines.Count; i++)
        {
            ActiveDoctrine entry = activeDoctrines[i];
            if (entry == null || entry.Doctrine == null || entry.Owner == null)
            {
                continue;
            }

            DoctrineStats stats = entry.Owner.GetComponent<DoctrineStats>();
            if (stats == null)
            {
                continue;
            }

            if (entry.Doctrine.Result == ResultType.IncreaseSpeed)
            {
                stats.ClearSpeedBuff();
            }
            else if (entry.Doctrine.Result == ResultType.IncreaseAttack)
            {
                stats.ClearAttackBuff();
            }
        }

        activeDoctrines.Clear();
        cooldowns.Clear();
        if (verboseLogs)
        {
            Debug.Log("[Doctrine] Cleared all.");
        }
        Cleared?.Invoke();
    }

    public void NotifyEvent(DoctrineEventData evt)
    {
        // 폭발 데미지 적용 중에 TakeDamage가 일으키는 OnHit/LowHealth/OnDeath은 무시한다.
        // 없으면 "맞은 적이 또 폭발 → 그 폭발에 맞은 적이 또 폭발" 연쇄로 맵이 쓸린다.
        if (DoctrineEffectExecutor.IsApplyingDoctrineDamage)
        {
            return;
        }

        // No active doctrines: return without GetComponent/snapshot allocs.
        if (activeDoctrines.Count == 0)
        {
            return;
        }

        if (notifyDepth >= maxNestedDepth)
        {
            Debug.LogWarning("[Doctrine] Nesting depth exceeded; ignoring event (infinite recursion guard).");
            return;
        }

        notifyDepth++;
        try
        {
            GameObject target = evt.SubjectObject;
            if (target == null)
            {
                return;
            }

            // One GetComponent per event. Matches(entry, evt) called GetComponent
            // per doctrine and slowed the hot path.
            SubjectType identity = DoctrineSubject.GetIdentity(target);

            // Snapshot so register/unregister during execution stays safe.
            List<ActiveDoctrine> snapshot = new List<ActiveDoctrine>(activeDoctrines);
            foreach (ActiveDoctrine entry in snapshot)
            {
                if (entry == null || entry.Doctrine == null)
                {
                    continue;
                }

                if (!MatchesRule(entry.Doctrine, identity, evt.Condition))
                {
                    continue;
                }

                // Owner-bound entries only react to their owner's events.
                if (entry.Owner != null && entry.Owner != target)
                {
                    continue;
                }

                bool isExplosion = entry.Doctrine.Result == ResultType.Explode;
                if ((isExplosion && executingExplosion) || IsOnCooldown(entry, target))
                {
                    continue;
                }

                bool ok = false;
                // Explosion damage can synchronously raise OnHit or OnDeath.
                if (isExplosion) executingExplosion = true;
                try
                {
                    ok = DoctrineEffectExecutor.Execute(entry.Doctrine, target, evt.SourceObject);
                    if (ok)
                    {
                        MarkCooldown(entry, target);
                    }

                    if (verboseLogs)
                    {
                        Debug.Log($"[Doctrine] Triggered: {entry.Doctrine} -> {target.name} (ok:{ok}).");
                    }
                }
                catch (Exception ex)
                {
                    // One doctrine's failure never stops the rest.
                    Debug.LogError($"[Doctrine] Effect exception (continuing): {ex}");
                }
                finally
                {
                    if (isExplosion) executingExplosion = false;
                }

                // UI is told "it triggered" regardless of effect success.
                // UI exceptions are isolated so they never count as effect failure.
                try
                {
                    Triggered?.Invoke(entry, target);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Doctrine] Trigger event exception: {ex}");
                }
            }
        }
        finally
        {
            notifyDepth--;
        }
    }

    public static bool Matches(ActiveDoctrine entry, DoctrineEventData evt)
    {
        if (entry == null || entry.Doctrine == null)
        {
            return false;
        }

        if (!MatchesRule(entry.Doctrine, DoctrineSubject.GetIdentity(evt.SubjectObject), evt.Condition))
        {
            return false;
        }

        // Owner-bound entries only react to their owner's events.
        if (entry.Owner != null && entry.Owner != evt.SubjectObject)
        {
            return false;
        }

        return true;
    }

    // Common match reused without edits when new doctrine combos are added.
    // All matches every subject.
    public static bool MatchesRule(Doctrine doctrine, SubjectType actualIdentity, ConditionType condition)
    {
        if (doctrine == null || doctrine.Condition != condition)
        {
            return false;
        }

        if (doctrine.Subject == SubjectType.All)
        {
            return true;
        }

        return actualIdentity != SubjectType.None && doctrine.Subject == actualIdentity;
    }

    // Per-doctrine cooldown. Heal: table 20s, Explode: table 5s.
    // Explicit CooldownSec overrides both.
    public static float GetCooldownSeconds(Doctrine doctrine)
    {
        return ResolveCooldown(doctrine);
    }

    // UI: remaining cooldown for the exact entry+target that just triggered.
    // Returns 0 when ready, when the target has no cooldown, or when the
    // doctrine has no cooldown at all.
    public float GetCooldownRemaining(ActiveDoctrine entry, GameObject target)
    {
        if (entry == null || entry.Doctrine == null)
        {
            return 0f;
        }

        if (ResolveCooldown(entry.Doctrine) <= 0f)
        {
            return 0f;
        }

        string key = CooldownKey(entry, target);
        float now = Time.time;
        for (int i = 0; i < cooldowns.Count; i++)
        {
            if (cooldowns[i].Key == key)
            {
                return Mathf.Max(0f, cooldowns[i].ReadyAt - now);
            }
        }

        return 0f;
    }

    // UI: longest remaining cooldown among every target of this entry.
    // Used when the HUD rebuilds its slots while a cooldown is still running
    // (targets have separate cooldown keys, e.g. Explode per enemy).
    public float GetCooldownRemaining(ActiveDoctrine entry)
    {
        if (entry == null || entry.Doctrine == null)
        {
            return 0f;
        }

        if (ResolveCooldown(entry.Doctrine) <= 0f)
        {
            return 0f;
        }

        string prefix = CooldownPrefix(entry);
        float now = Time.time;
        float remaining = 0f;
        for (int i = 0; i < cooldowns.Count; i++)
        {
            string key = cooldowns[i].Key;
            if (key == null || !key.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            float targetRemaining = cooldowns[i].ReadyAt - now;
            if (targetRemaining > remaining)
            {
                remaining = targetRemaining;
            }
        }

        return Mathf.Max(0f, remaining);
    }

    private bool IsOnCooldown(ActiveDoctrine entry, GameObject target)
    {
        float cooldown = ResolveCooldown(entry.Doctrine);
        if (cooldown <= 0f)
        {
            return false;
        }

        string key = CooldownKey(entry, target);
        float now = Time.time;
        for (int i = 0; i < cooldowns.Count; i++)
        {
            if (cooldowns[i].Key == key && now < cooldowns[i].ReadyAt)
            {
                return true;
            }
        }

        return false;
    }

    private void MarkCooldown(ActiveDoctrine entry, GameObject target)
    {
        float cooldown = ResolveCooldown(entry.Doctrine);
        if (cooldown <= 0f)
        {
            return;
        }

        string key = CooldownKey(entry, target);
        float readyAt = Time.time + cooldown;
        for (int i = 0; i < cooldowns.Count; i++)
        {
            if (cooldowns[i].Key == key)
            {
                cooldowns[i].ReadyAt = readyAt;
                return;
            }
        }

        cooldowns.Add(new CooldownEntry { Key = key, ReadyAt = readyAt });
    }

    private void RemoveCooldownsFor(ActiveDoctrine entry)
    {
        if (entry == null || entry.Doctrine == null)
        {
            return;
        }

        float cooldown = ResolveCooldown(entry.Doctrine);
        if (cooldown <= 0f)
        {
            return;
        }

        for (int i = cooldowns.Count - 1; i >= 0; i--)
        {
            if (cooldowns[i].Key != null &&
                cooldowns[i].Key.StartsWith(CooldownPrefix(entry), StringComparison.Ordinal))
            {
                cooldowns.RemoveAt(i);
            }
        }
    }

    private static float ResolveCooldown(Doctrine doctrine)
    {
        if (doctrine == null)
        {
            return 0f;
        }

        if (doctrine.CooldownSec > 0f)
        {
            return doctrine.CooldownSec;
        }

        if (doctrine.Result == ResultType.Heal)
        {
            return DoctrineTable.HealCooldownSec;
        }

        if (doctrine.Result == ResultType.Explode)
        {
            return DoctrineTable.ExplodeCooldownSec;
        }

        // 이속/피해 증가: 지속 5초 · 쿨 10초.
        if (doctrine.Result == ResultType.IncreaseSpeed)
        {
            return DoctrineTable.SpeedCooldownSec;
        }

        if (doctrine.Result == ResultType.IncreaseAttack)
        {
            return DoctrineTable.AttackCooldownSec;
        }

        return 0f;
    }

    private static string CooldownPrefix(ActiveDoctrine entry)
    {
        Doctrine d = entry.Doctrine;
        int ownerId = entry.Owner != null ? entry.Owner.GetInstanceID() : 0;
        return $"{ownerId}:{(int)d.Subject}:{(int)d.Condition}:{(int)d.Result}:{d.Magnitude}:{d.Duration}:{d.Radius}:{d.CooldownSec}|";
    }

    private static string CooldownKey(ActiveDoctrine entry, GameObject target)
    {
        int targetId = target != null ? target.GetInstanceID() : 0;
        return CooldownPrefix(entry) + targetId;
    }

    // 해제된 교리가 걸어둔 이속/피해 버프만 정리한다.
    // 같은 결과의 교리가 아직 남아 있으면 그 버프는 건드리지 않는다.
    private void ClearRemovedBuff(ActiveDoctrine removed)
    {
        if (removed == null || removed.Doctrine == null || removed.Owner == null)
        {
            return;
        }

        ResultType result = removed.Doctrine.Result;
        if (result != ResultType.IncreaseSpeed && result != ResultType.IncreaseAttack)
        {
            return;
        }

        for (int i = 0; i < activeDoctrines.Count; i++)
        {
            ActiveDoctrine entry = activeDoctrines[i];
            if (entry != null && entry.Owner == removed.Owner
                && entry.Doctrine != null && entry.Doctrine.Result == result)
            {
                return;
            }
        }

        DoctrineStats stats = removed.Owner.GetComponent<DoctrineStats>();
        if (stats == null)
        {
            return;
        }

        if (result == ResultType.IncreaseSpeed)
        {
            stats.ClearSpeedBuff();
        }
        else
        {
            stats.ClearAttackBuff();
        }
    }
}
