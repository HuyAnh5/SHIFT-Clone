using UnityEngine;

public static class MobileUIInput
{
    static bool leftHeld, rightHeld;
    static bool jumpDown, shiftDown, markDown, swapDown;

    public static float Horizontal
    {
        get
        {
            float x = 0f;
            if (leftHeld) x -= 1f;
            if (rightHeld) x += 1f;
            return Mathf.Clamp(x, -1f, 1f);
        }
    }

    public static void SetLeft(bool held) => leftHeld = held;
    public static void SetRight(bool held) => rightHeld = held;

    public static void TriggerJump() => jumpDown = true;
    public static void TriggerShift() => shiftDown = true;
    public static void TriggerMark() => markDown = true;
    public static void TriggerSwap() => swapDown = true;

    public static bool ConsumeJumpDown() { var v = jumpDown; jumpDown = false; return v; }
    public static bool ConsumeShiftDown() { var v = shiftDown; shiftDown = false; return v; }
    public static bool ConsumeMarkDown() { var v = markDown; markDown = false; return v; }
    public static bool ConsumeSwapDown() { var v = swapDown; swapDown = false; return v; }
}
