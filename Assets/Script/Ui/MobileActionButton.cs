using UnityEngine;
using UnityEngine.EventSystems;

public class MobileActionButton : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private PlayerController player;

    public void OnPointerDown(PointerEventData e)
    {
        if (player != null && player.IsGroundedNow) MobileUIInput.TriggerShift();
        else MobileUIInput.TriggerSwap();
    }
}
