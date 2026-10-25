using System;
using System.Collections.Generic;
using UnityEngine;

// 획득한 교리 조각(주어/조건/결과)을 보관하는 가상 인벤토리.
// 상점에서 산 부품이 교리로 바로 활성화되지 않고 일단 여기 쌓인다.
// 나중에 재단(Altar)에서 이 조각들을 조합해 실제 교리로 장착한다.
public class DoctrineInventory : MonoBehaviour
{
    public static DoctrineInventory Instance { get; private set; }

    private readonly Dictionary<SubjectType, int> subjects = new Dictionary<SubjectType, int>();
    private readonly Dictionary<ConditionType, int> conditions = new Dictionary<ConditionType, int>();
    private readonly Dictionary<ResultType, int> results = new Dictionary<ResultType, int>();

    public event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBoot()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject go = new GameObject("[DoctrineInventory]");
        DontDestroyOnLoad(go);
        go.AddComponent<DoctrineInventory>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
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

    public void AddSubject(SubjectType subject)
    {
        if (subject == SubjectType.None)
        {
            return;
        }

        subjects.TryGetValue(subject, out int count);
        subjects[subject] = count + 1;
        Changed?.Invoke();
    }

    public void AddCondition(ConditionType condition)
    {
        if (condition == ConditionType.None)
        {
            return;
        }

        conditions.TryGetValue(condition, out int count);
        conditions[condition] = count + 1;
        Changed?.Invoke();
    }

    public void AddResult(ResultType result)
    {
        if (result == ResultType.None)
        {
            return;
        }

        results.TryGetValue(result, out int count);
        results[result] = count + 1;
        Changed?.Invoke();
    }

    public void AddEntry(ShopEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        switch (entry.Column)
        {
            case ShopColumn.Subject: AddSubject(entry.Subject); break;
            case ShopColumn.Condition: AddCondition(entry.Condition); break;
            case ShopColumn.Result: AddResult(entry.Result); break;
        }
    }

    public int CountSubject(SubjectType subject)
    {
        return subjects.TryGetValue(subject, out int count) ? count : 0;
    }

    public int CountCondition(ConditionType condition)
    {
        return conditions.TryGetValue(condition, out int count) ? count : 0;
    }

    public int CountResult(ResultType result)
    {
        return results.TryGetValue(result, out int count) ? count : 0;
    }

    public int TotalCount
    {
        get
        {
            int total = 0;
            foreach (int c in subjects.Values) total += c;
            foreach (int c in conditions.Values) total += c;
            foreach (int c in results.Values) total += c;
            return total;
        }
    }

    /// <summary>재단 UI용 요약. 예: "주어 2 · 조건 1 · 결과 3"</summary>
    public string Describe()
    {
        int s = 0, c = 0, r = 0;
        foreach (int v in subjects.Values) s += v;
        foreach (int v in conditions.Values) c += v;
        foreach (int v in results.Values) r += v;
        return $"인벤토리 — 주어 {s} · 조건 {c} · 결과 {r} (총 {s + c + r})";
    }
}
