using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SketchLand
{
    [Serializable]
    [MovedFrom(true, sourceNamespace: "OceanGame", sourceAssembly: "Assembly-CSharp", sourceClassName: "DoorIB")]
    public class DoorIB : InteractBehavior
    {
        public override void Interact(int posX, int posY)
        {
            var doorTd = WorldManager.Instance.FgGrid.GetTileData(posX, posY);

            if (doorTd.State == DoorTileConfigSO.Closed)
            {
                float doorCenterX = posX - doorTd.OffsetX + doorTd.TileConfig.Size.x * 0.5f;
                doorTd.State = Player.Instance.Character.transform.position.x <= doorCenterX
                    ? DoorTileConfigSO.OpenRight : DoorTileConfigSO.OpenLeft;
                doorTd.IsSolid = false;
                
                AudioManager.Instance.PlayOneShot(FMODEvents.Instance.DoorOpen, default);
            }
            else
            {
                doorTd.State = DoorTileConfigSO.Closed;
                doorTd.IsSolid = true;

                AudioManager.Instance.PlayOneShot(FMODEvents.Instance.DoorClosed, default);
            }

            WorldManager.Instance.FgGrid.ChangeMultiTileData(posX, posY, doorTd, true);
        }
    }
}
