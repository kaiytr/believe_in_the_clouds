using UnityEngine;

// Optional secondary input; ordinary E interactions do not depend on it.
public interface ISecondaryInteractable
{
    void InteractSecondary(GameObject interactor);
}
