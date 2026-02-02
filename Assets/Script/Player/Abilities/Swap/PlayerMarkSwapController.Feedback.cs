using UnityEngine;

public partial class PlayerMarkSwapController
{
    private void ShakeFail()
    {
        if (cameraShake != null) cameraShake.ShakeFail();
        else if (CameraShake2D.I != null) CameraShake2D.I.ShakeFail();
    }
}
