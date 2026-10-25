using UnityEngine;

// 상호작용 가능한 대상의 공통 계약.
// 상점만의 규칙이 아니라, 앞으로 E 키로 상호작용할 모든 것(상점/문/NPC/제단 등)이
// 이 인터페이스를 구현한다. 입력 처리는 InteractionController 한 곳에서만 한다.
public interface IInteractable
{
    /// <summary>로그/UI에 표시할 대상 이름.</summary>
    string InteractionName { get; }

    /// <summary>안내 문구. 예: "E : 상점 열기"</summary>
    string InteractionPrompt { get; }

    /// <summary>대상 표면에서 허용하는 거리(미터).</summary>
    float InteractionRadius { get; }

    /// <summary>지금 상호작용할 수 있는 상태인지.</summary>
    bool CanInteract(GameObject interactor);

    /// <summary>실제 상호작용 수행.</summary>
    void Interact(GameObject interactor);
}
