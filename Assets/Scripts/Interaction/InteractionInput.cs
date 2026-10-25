using UnityEngine;

// 상호작용 입력을 한 곳에서만 읽는다.
// "모든 상호작용은 E 키"라는 규칙이 바뀌거나 Input System 전용으로 전환할 때
// 이 파일만 고치면 되도록 분리했다.
//
// 참고: 이 프로젝트의 activeInputHandler는 Both(2)라 구 Input API가 살아 있다.
// New 전용으로 바꾸면 아래 WasPressed 구현만 Input System 버전으로 교체하면 된다.
public static class InteractionInput
{
    public static bool WasPressed(KeyCode key)
    {
        return Input.GetKeyDown(key);
    }

    public static bool IsHeld(KeyCode key)
    {
        return Input.GetKey(key);
    }
}
