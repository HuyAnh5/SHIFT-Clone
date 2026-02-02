using UnityEngine;
using UnityEngine.Tilemaps;

public partial class QuantumPassManager2D
{
    private void AutoFindIfMissing()
    {
        if (playerCollider == null)
        {
            if (!TryAssignPlayerColliderFromTag())
            {
                var pc = FindObjectOfType<PlayerController>();
                if (pc != null) playerCollider = pc.GetComponent<Collider2D>();
            }

            if (playerCollider == null)
                Debug.LogWarning("[QuantumPass] playerCollider is still null. Assign it manually or ensure Player has tag + collider.");
        }

        if (markerTilemap == null || blackSolidFill == null || blackGhostTrigger == null || whiteSolidFill == null || whiteGhostTrigger == null)
        {
            var tms = GetComponentsInChildren<Tilemap>(true);
            for (int i = 0; i < tms.Length; i++)
            {
                var tm = tms[i];
                switch (tm.name)
                {
                    case "Tilemap_QuantumPass": markerTilemap ??= tm; break;
                    case "QP_Black_SolidFill": blackSolidFill ??= tm; break;
                    case "QP_Black_GhostTrig": blackGhostTrigger ??= tm; break;
                    case "QP_White_SolidFill": whiteSolidFill ??= tm; break;
                    case "QP_White_GhostTrig": whiteGhostTrigger ??= tm; break;
                }
            }
        }

        if (outlineRoot == null)
        {
            var child = transform.Find("QuantumPassOutlines");
            if (child != null) outlineRoot = child;
        }
    }

    private bool TryAssignPlayerColliderFromTag()
    {
        GameObject go = null;

        try
        {
            go = GameObject.FindGameObjectWithTag(playerTag);
        }
        catch
        {
            Debug.LogWarning($"[QuantumPass] Tag '{playerTag}' is missing. Add it in Tag Manager or change playerTag.");
            return false;
        }

        if (go == null) return false;

        var cols = go.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null && !cols[i].isTrigger)
            {
                playerCollider = cols[i];
                return true;
            }
        }

        if (cols.Length > 0)
        {
            playerCollider = cols[0];
            Debug.LogWarning("[QuantumPass] Found only trigger colliders on Player. Please use a NON-trigger collider as hitbox.");
            return true;
        }

        return false;
    }
}
