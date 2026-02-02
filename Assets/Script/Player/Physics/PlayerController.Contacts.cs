using UnityEngine;

public partial class PlayerController
{
    private void UpdateContactsFixed()
    {
        groundedFixed = false;
        wallLeftFixed = false;
        wallRightFixed = false;
        dynLeftFixed = false;
        dynRightFixed = false;

        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = solidMask,
            useTriggers = false
        };

        ContactPoint2D[] contacts = new ContactPoint2D[16];
        int count = rb.GetContacts(filter, contacts);

        Vector2 upRelToGravity = GravityUp;

        for (int i = 0; i < count; i++)
        {
            Vector2 n = contacts[i].normal;

            float dotGround = Vector2.Dot(n, upRelToGravity);
            if (dotGround >= groundNormalThreshold)
                groundedFixed = true;

            float dotLeft = Vector2.Dot(n, Vector2.right);
            float dotRight = Vector2.Dot(n, Vector2.left);

            if (dotLeft >= wallNormalThreshold) wallLeftFixed = true;
            if (dotRight >= wallNormalThreshold) wallRightFixed = true;

            var col = contacts[i].collider;
            var otherRb = col != null ? col.attachedRigidbody : null;
            bool otherDynamic = otherRb != null && otherRb.bodyType == RigidbodyType2D.Dynamic;

            if (otherDynamic && dotGround < groundNormalThreshold)
            {
                if (dotLeft >= wallNormalThreshold) dynLeftFixed = true;
                if (dotRight >= wallNormalThreshold) dynRightFixed = true;
            }
        }
    }
}
