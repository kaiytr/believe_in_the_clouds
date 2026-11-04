using System;
using UnityEngine;

// 교리 슬롯(재단의 교리 칸) 개수 저장소.
// - 기본 1칸이 주어지고, 상점에서 구매할 때마다 1칸씩 늘어난다.
// - 메모리 전용(DontDestroyOnLoad), 디스크 저장 없음. 인벤토리와 같은 정책.
// - 재단 화면과 상점 화면은 이 개수를 구독해 UI를 늘린다.
public class DoctrineSlotStore : MonoBehaviour
{
    public const int DefaultSlots = 1;

    public static DoctrineSlotStore Instance { get; private set; }

    private int slotCount = DefaultSlots;

    public int SlotCount => slotCount;

    public event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBoot()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject go = new GameObject("[DoctrineSlotStore]");
        DontDestroyOnLoad(go);
        go.AddComponent<DoctrineSlotStore>();
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

    public static DoctrineSlotStore EnsureExists()
    {
        if (Instance != null)
        {
            return Instance;
        }

        DoctrineSlotStore existing = UnityEngine.Object.FindFirstObjectByType<DoctrineSlotStore>();
        if (existing != null)
        {
            UnityEngine.Object.DontDestroyOnLoad(existing.gameObject);
            return existing;
        }

        GameObject go = new GameObject("[DoctrineSlotStore]");
        UnityEngine.Object.DontDestroyOnLoad(go);
        return go.AddComponent<DoctrineSlotStore>();
    }

    /// <summary>교리 칸을 1개 늘린다. 새 총 개수를 반환한다.</summary>
    public int PurchaseSlot()
    {
        slotCount++;
        Debug.Log($"[교리칸] 구매 → 총 {slotCount}칸");
        Changed?.Invoke();
        return slotCount;
    }
}
