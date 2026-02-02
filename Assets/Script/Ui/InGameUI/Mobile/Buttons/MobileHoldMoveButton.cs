using UnityEngine;
using UnityEngine.EventSystems;

public class MobileHoldMoveButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public enum Dir { Left, Right }
    [SerializeField] private Dir dir;

    public void OnPointerDown(PointerEventData e)
    {
        if (dir == Dir.Left) MobileUIInput.SetLeft(true);
        else MobileUIInput.SetRight(true);
    }

    public void OnPointerUp(PointerEventData e) => Release();
    public void OnPointerExit(PointerEventData e) => Release();

    private void Release()
    {
        if (dir == Dir.Left) MobileUIInput.SetLeft(false);
        else MobileUIInput.SetRight(false);
    }
}
