using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class QuantumPassManager2D
{
    private void CacheBaseColorsAndColliders()
    {
        _baseColors.Clear();
        CacheBaseColor(markerTilemap);
        CacheBaseColor(blackSolidFill);
        CacheBaseColor(blackGhostTrigger);
        CacheBaseColor(whiteSolidFill);
        CacheBaseColor(whiteGhostTrigger);

        _blackCols = CollectColliders(blackSolidFill, blackGhostTrigger);
        _whiteCols = CollectColliders(whiteSolidFill, whiteGhostTrigger);
    }

    private void CacheBaseColor(Tilemap tm)
    {
        if (tm == null) return;
        if (!_baseColors.ContainsKey(tm))
            _baseColors.Add(tm, tm.color);
    }

    private static Collider2D[] CollectColliders(params Tilemap[] tms)
    {
        List<Collider2D> cols = new();
        for (int i = 0; i < tms.Length; i++)
        {
            if (tms[i] == null) continue;
            cols.AddRange(tms[i].GetComponents<Collider2D>());
        }
        return cols.ToArray();
    }

    private void OnWorldChanged(WorldState solidWorld)
    {
        bool blackActive = (solidWorld == WorldState.Black);

        SetColliders(_blackCols, blackActive);
        SetColliders(_whiteCols, !blackActive);

        SetAlpha(blackSolidFill, blackActive ? activeAlpha : inactiveAlpha);
        SetAlpha(blackGhostTrigger, blackActive ? activeAlpha : inactiveAlpha);
        SetAlpha(whiteSolidFill, !blackActive ? activeAlpha : inactiveAlpha);
        SetAlpha(whiteGhostTrigger, !blackActive ? activeAlpha : inactiveAlpha);

        ApplyOutlineColor(solidWorld);

        // Avoid accidental "leave" triggered by world flip
        RefreshInsideFlagsOnly();
    }

    private void ApplyOutlineColor(WorldState world)
    {
        if (!buildOutline) return;
        if (_dashMatInstance == null) return;

        Color a = (world == WorldState.Black) ? outlineA_BlackWorld : outlineA_WhiteWorld;

        Color b;
        if (outlineB_UseSolidFillBaseColor)
        {
            Tilemap src = (world == WorldState.Black) ? blackSolidFill : whiteSolidFill;
            if (src != null && _baseColors.TryGetValue(src, out var baseC))
                b = baseC;
            else
                b = (world == WorldState.Black) ? Color.black : Color.white;

            b.a = 1f;
        }
        else
        {
            b = outlineB_Custom;
        }

        b.a *= outlineB_Alpha;

        _dashMatInstance.SetColor("_ColorA", a);
        _dashMatInstance.SetColor("_ColorB", b);
    }

    private static void SetColliders(Collider2D[] cols, bool enabled)
    {
        if (cols == null) return;
        for (int i = 0; i < cols.Length; i++)
            if (cols[i] != null) cols[i].enabled = enabled;
    }

    private void SetAlpha(Tilemap tm, float a)
    {
        if (tm == null) return;

        if (!_baseColors.TryGetValue(tm, out var baseC))
            baseC = tm.color;

        baseC.a = a;
        tm.color = baseC;
    }
}
