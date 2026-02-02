using UnityEngine;

public partial class QuantumPassManager2D
{
    private void RepaintAllGroups()
    {
        if (blackSolidFill) blackSolidFill.ClearAllTiles();
        if (blackGhostTrigger) blackGhostTrigger.ClearAllTiles();
        if (whiteSolidFill) whiteSolidFill.ClearAllTiles();
        if (whiteGhostTrigger) whiteGhostTrigger.ClearAllTiles();

        for (int i = 0; i < _groups.Count; i++)
            ApplyGroupTiles(_groups[i]);
    }

    private void ApplyGroupTiles(Group g)
    {
        bool openInBlack = (g.openWorld == WorldState.Black);

        foreach (var cell in g.cells)
        {
            if (openInBlack)
            {
                // Black OPEN => ghost in black, solid in white
                if (blackGhostTrigger) blackGhostTrigger.SetTile(cell, paintTile);
                if (blackSolidFill) blackSolidFill.SetTile(cell, null);

                if (whiteSolidFill) whiteSolidFill.SetTile(cell, paintTile);
                if (whiteGhostTrigger) whiteGhostTrigger.SetTile(cell, null);
            }
            else
            {
                // White OPEN => ghost in white, solid in black
                if (whiteGhostTrigger) whiteGhostTrigger.SetTile(cell, paintTile);
                if (whiteSolidFill) whiteSolidFill.SetTile(cell, null);

                if (blackSolidFill) blackSolidFill.SetTile(cell, paintTile);
                if (blackGhostTrigger) blackGhostTrigger.SetTile(cell, null);
            }
        }
    }

    private void DoSwapFromWorld(Group g, WorldState closingWorld)
    {
        // Rule: closingWorld closes, other opens
        g.openWorld = (closingWorld == WorldState.Black) ? WorldState.White : WorldState.Black;
        ApplyGroupTiles(g);
    }
}
