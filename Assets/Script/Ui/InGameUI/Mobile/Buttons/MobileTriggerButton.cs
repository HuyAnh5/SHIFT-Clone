using UnityEngine;
using UnityEngine.EventSystems;

public class MobileTriggerButton : MonoBehaviour, IPointerDownHandler
{
    public enum Type { Jump, Mark, Swap, Shift, ActionSmart }
    [SerializeField] private Type type;
    [SerializeField] private PlayerController player;

    public void OnPointerDown(PointerEventData e)
    {
        switch (type)
        {
            case Type.Jump: MobileUIInput.TriggerJump(); break;
            case Type.Mark: MobileUIInput.TriggerMark(); break;
            case Type.Swap: MobileUIInput.TriggerSwap(); break;
            case Type.Shift: MobileUIInput.TriggerShift(); break;

            case Type.ActionSmart:
                // Ưu tiên GravityFlipTrigger nếu đang đứng trong vùng
                if (GravityFlipTrigger.PlayerInsideAny && GravityFlipTrigger.TryActivateFromUI())
                    return;

                if (player != null && player.IsGroundedNow) MobileUIInput.TriggerShift();
                else MobileUIInput.TriggerSwap();
                break;
        }
    }
}
