using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SketchLand
{
    [Serializable]
    [MovedFrom(true, sourceNamespace: "OceanGame", sourceAssembly: "Assembly-CSharp", sourceClassName: "ChestIB")]
    public class ChestIB : InteractBehavior
    {
        public override void Interact(int posX, int posY)
        {
            var td = WorldManager.Instance.FgGrid.GetTileData(posX, posY);
            if (!td.HasTile) return;

            // Resolve to the root coordinate of the multi-tile
            int rootX = posX - td.OffsetX;
            int rootY = posY - td.OffsetY;

            ChestManager.Instance.OpenChest(new Vector2Int(rootX, rootY));
        }
    }
}
