using System;
using System.Collections.Generic;

// 모달 UI(상점 패널 등)가 열려 있는지 공용으로 추적한다.
// 열려 있는 동안에는 월드 상호작용(E)이 실행되지 않고,
// 대신 E/ESC가 "닫기 요청"으로 쓰인다.
public static class UiModal
{
    private static readonly List<object> Owners = new List<object>();

    public static bool IsOpen => Owners.Count > 0;

    /// <summary>모달이 닫기 입력을 요청할 때 발생.</summary>
    public static event Action CloseRequested;

    public static void Push(object owner)
    {
        if (owner == null || Owners.Contains(owner))
        {
            return;
        }

        Owners.Add(owner);
    }

    public static void Pop(object owner)
    {
        if (owner != null)
        {
            Owners.Remove(owner);
        }
    }

    public static void RequestClose()
    {
        CloseRequested?.Invoke();
    }
}
