using UnityEngine;

public class HoldPlate2D : PlateBase2D
{

    protected override void OnBecameInactiveWorld()
    {
        PlateDbg($"OnBecameInactiveWorld: hadPlayer={deactivatedHadPlayer} hadSwap={deactivatedHadSwapBlock} latched={IsSwapBlockLatched}");

        // Player đè -> SHIFT = rời => OFF
        // SwapBlock đè -> giữ ON xuyên world
        SetOn(IsSwapBlockLatched);

        KillAllTweens();
        rb.position = basePos;
    }




    protected override void OnOccupancyChanged()
    {
        if (!activeInWorld) return;

        PlateDbg($"OnOccupancyChanged: hasOcc={HasOccupant} latched={IsSwapBlockLatched}");

        if (HasOccupant)
        {
            SetOn(true);
            TweenPressToBottom();
        }
        else
        {
            if (IsSwapBlockLatched)
            {
                SetOn(true);
                KillAllTweens();
                RecomputePressedPos();
                rb.position = pressedPos;
            }
            else
            {
                SetOn(false);
                TweenRaiseToTop(raiseDuration);
            }
        }
    }


}
