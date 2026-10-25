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
//   IncreaseSpeed (+40%) is persistent: re-applied on every matching event.
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
    public bool TryActivate(Doctrine doctrine, GameObject owner, out string reason)
    {
        if (!DoctrineValidator.TryValidate(doctrine, owner, out reason))
        {
            Debug.LogWarning($"[Doctrine] Activation failed: {reason}");
            return false;
        }

        ActiveDoctrine entry = new ActiveDoctrine(doctrine.Clone(), owner);
        activeDoctrines.Add(entry);
        if (entry.Doctrine.Result == ResultType.IncreaseSpeed)
        {
            ReapplyPersistentSpeed(entry);
        }

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
        if (index < 0)
        {
            return false;
        }

        ActiveDoctrine removed = activeDoctrines[index];
        activeDoctrines.RemoveAt(index);
        if (removed.Doctrine != null && removed.Doctrine.Result == ResultType.IncreaseSpeed)
        {
            RefreshPersistentSpeed(removed.Owner, removed.Doctrine);
        }

        RemoveCooldownsFor(removed);
        if (verboseLogs)
        {
            Debug.Log($"[Doctrine] Deactivated: {doctrine}");
        }
        Deactivated?.Invoke(removed);
        return true;
    }

    public void Clear()
    {
        // Reset persistent speed buffs held by owners.
        for (int i = 0; i < activeDoctrines.Count; i++)
        {
            ActiveDoctrine entry = activeDoctrines[i];
            if (entry == null || entry.Doctrine == null)
            {
                continue;
            }

            if (entry.Doctrine.Result == ResultType.IncreaseSpeed && entry.Owner != null)
            {
                DoctrineStats stats = entry.Owner.GetComponent<DoctrineStats>();
                if (stats != null)
                {
                    stats.ClearPersistentSpeedBuff();
                }
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

                if (IsOnCooldown(entry, target))
                {
                    continue;
                }

                bool ok = false;
                try
                {
                    ok = DoctrineEffectExecutor.Execute(entry.Doctrine, target, evt.SourceObject);
                    if (ok)
                    {
                        MarkCooldown(entry, target);
                        if (entry.Doctrine.Result == ResultType.IncreaseSpeed)
                        {
                            ReapplyPersistentSpeed(entry);
                        }
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

    // Persistent +40% speed: assert it on activate and on every matching event
    // so revives / stat resets cannot silently drop it.
    private static void ReapplyPersistentSpeed(ActiveDoctrine entry)
    {
        if (entry == null || entry.Doctrine == null)
        {
            return;
        }

        GameObject owner = entry.Owner;
        if (owner == null)
        {
            return;
        }

        DoctrineStats stats = owner.GetComponent<DoctrineStats>();
        if (stats == null)
        {
            return;
        }

        stats.ApplySpeedBuff(1f + DoctrineTable.SpeedIncreaseRatio, 0f);
    }

    // Removing one IncreaseSpeed doctrine while others remain must keep x1.4.
    private void RefreshPersistentSpeed(GameObject owner, Doctrine removed)
    {
        if (owner == null || removed == null)
        {
            return;
        }

        DoctrineStats stats = owner.GetComponent<DoctrineStats>();
        if (stats == null)
        {
            return;
        }

        stats.ClearPersistentSpeedBuff();
        for (int i = 0; i < activeDoctrines.Count; i++)
        {
            ActiveDoctrine entry = activeDoctrines[i];
            if (entry == null || entry.Doctrine == null || entry.Owner != owner)
            {
                continue;
            }

            if (entry.Doctrine.Result == ResultType.IncreaseSpeed)
            {
                stats.ApplySpeedBuff(1f + DoctrineTable.SpeedIncreaseRatio, 0f);
            }
        }
    }
}
