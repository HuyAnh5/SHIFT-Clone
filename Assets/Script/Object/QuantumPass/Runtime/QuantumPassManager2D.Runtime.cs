using System.Collections.Generic;
using UnityEngine;

public partial class QuantumPassManager2D
{
    private void TickRuntime()
    {
        if (playerCollider == null || markerTilemap == null || _groups.Count == 0)
            return;

        var activeWorld = (WorldShiftManager.I != null) ? WorldShiftManager.I.SolidWorld : WorldState.Black;

        // Overlap only counts for groups that are OPEN in the active world
        Bounds probe = GetFootProbeBounds(playerCollider.bounds);
        var insideNow = GetOverlappingOpenGroupIds(probe, activeWorld);

        float t = Time.time;
        for (int i = 0; i < _groups.Count; i++)
        {
            var g = _groups[i];

            bool groupOpenHere = (g.openWorld == activeWorld);
            bool inside = groupOpenHere && insideNow.Contains(g.id);

            if (inside)
            {
                g.isInside = true;
                if (g.countdownActive)
                    g.countdownActive = false;

                continue;
            }

            // outside
            if (g.isInside)
            {
                g.isInside = false;
                g.countdownActive = true;
                g.countdownEndTime = t + swapDelay;
                g.countdownClosingWorld = activeWorld; // lock world at leave-time
            }
            else if (g.countdownActive && t >= g.countdownEndTime)
            {
                g.countdownActive = false;
                DoSwapFromWorld(g, g.countdownClosingWorld);
            }
        }
    }

    private void RefreshInsideFlagsOnly()
    {
        if (playerCollider == null || markerTilemap == null || _groups.Count == 0)
            return;

        var activeWorld = (WorldShiftManager.I != null) ? WorldShiftManager.I.SolidWorld : WorldState.Black;

        Bounds probe = GetFootProbeBounds(playerCollider.bounds);
        var insideNow = GetOverlappingOpenGroupIds(probe, activeWorld);

        for (int i = 0; i < _groups.Count; i++)
        {
            _groups[i].isInside = insideNow.Contains(_groups[i].id);
            // do NOT touch countdownActive / countdownEndTime / countdownClosingWorld
        }
    }

    private Bounds GetFootProbeBounds(Bounds playerBounds)
    {
        float w = playerBounds.size.x * footProbeWidthFactor;
        float h = footProbeHeight;

        Vector3 center = new Vector3(playerBounds.center.x, playerBounds.min.y + footProbeYOffset + h * 0.5f, 0f);
        Vector3 size = new Vector3(w, h, 0.1f);

        return new Bounds(center, size);
    }

    // Only OPEN groups in active world count as "inside"
    private HashSet<int> GetOverlappingOpenGroupIds(Bounds b, WorldState activeWorld)
    {
        HashSet<int> result = new();

        var min = markerTilemap.WorldToCell(b.min);
        var max = markerTilemap.WorldToCell(b.max);

        int xMin = min.x - 1, xMax = max.x + 1;
        int yMin = min.y - 1, yMax = max.y + 1;

        Vector3 cellSize = markerTilemap.cellSize;

        for (int y = yMin; y <= yMax; y++)
            for (int x = xMin; x <= xMax; x++)
            {
                var c = new Vector3Int(x, y, 0);
                if (!_cellToGroup.TryGetValue(c, out int gid))
                    continue;

                var g = _groups[gid];
                if (g.openWorld != activeWorld)
                    continue;

                Vector3 bl = markerTilemap.CellToWorld(c);
                float x0 = bl.x, x1 = bl.x + cellSize.x;
                float y0 = bl.y, y1 = bl.y + cellSize.y;

                if (b.max.x > x0 && b.min.x < x1 && b.max.y > y0 && b.min.y < y1)
                    result.Add(gid);
            }

        return result;
    }

    // When world flips, don't treat it as "leaving"
    private void RefreshInsideStatesNoCountdown()
    {
        if (playerCollider == null || markerTilemap == null || _groups.Count == 0)
            return;

        var activeWorld = (WorldShiftManager.I != null) ? WorldShiftManager.I.SolidWorld : WorldState.Black;
        var insideNow = GetOverlappingOpenGroupIds(playerCollider.bounds, activeWorld);

        for (int i = 0; i < _groups.Count; i++)
        {
            var g = _groups[i];
            g.countdownActive = false;
            g.isInside = insideNow.Contains(g.id);
        }
    }
}
