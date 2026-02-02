using UnityEngine;

public partial class PlayerController
{
    private void Jump()
    {
        if (shifting) return;

        jumpAvailable = false;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;

        Vector2 v = rb.linearVelocity;
        v.y = 0f;
        rb.linearVelocity = v;

        rb.AddForce(GravityUp * jumpForce, ForceMode2D.Impulse);

        if (enableSquashStretch)
            PlayJumpStretch();
    }
}
