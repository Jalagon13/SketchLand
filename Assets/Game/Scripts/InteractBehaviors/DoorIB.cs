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

            if (doorTd.State == 0)
            {
                doorTd.State = 1;
                doorTd.IsSolid = false;
                
                AudioManager.Instance.PlayOneShot(FMODEvents.Instance.DoorOpen, default);
            }
            else if (doorTd.State == 1)
            {
                doorTd.State = 0;
                doorTd.IsSolid = true;

                AudioManager.Instance.PlayOneShot(FMODEvents.Instance.DoorClosed, default);
            }

            WorldManager.Instance.FgGrid.ChangeMultiTileData(posX, posY, doorTd, true);
        }
    }
}
