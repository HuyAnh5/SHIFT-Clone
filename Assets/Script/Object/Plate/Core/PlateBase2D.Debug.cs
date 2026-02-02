using System.Collections.Generic;
using UnityEngine;

public abstract partial class PlateBase2D
{
    protected void PlateDbg(string msg)
    {
        if (!debugPlate) return;
        Debug.Log($"[Plate] {name} owner={ownerWorld} active={activeInWorld} on={IsOn} latched={swapBlockLatched} :: {msg}", this);
    }

    private string DescribeHits(List<Collider2D> hits)
    {
        if (hits == null) return "null";
        if (hits.Count == 0) return "0";

        int p = 0, s = 0, other = 0;
        for (int i = 0; i < hits.Count; i++)
        {
            if (TryGetOccupantKey(hits[i], out _, out OccupantKind k))
            {
                if (k == OccupantKind.Player) p++;
                else if (k == OccupantKind.SwapBlock) s++;
                else other++;
            }
            else other++;
        }
        return $"{hits.Count} (Player:{p}, Swap:{s}, Other:{other})";
    }
}
