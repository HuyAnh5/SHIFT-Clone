using UnityEngine;
using UnityEngine.EventSystems;

public class MobileTapButton : MonoBehaviour, IPointerDownHandler
{
    public enum Type { Jump, Mark }
    [SerializeField] private Type type;

    public void OnPointerDown(PointerEventData e)
    {
        if (type == Type.Jump) MobileUIInput.TriggerJump();
        else MobileUIInput.TriggerMark();
    }
}
