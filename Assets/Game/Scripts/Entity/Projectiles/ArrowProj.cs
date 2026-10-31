using UnityEngine;

namespace SketchLand
{
    public class ArrowProj : Projectile
    {
        [Header("Gravity")]
        public float GravityForce = 20f;
        public float TerminalVelocity = -18f;

        protected override void OnInit()
        {
            base.OnInit();
        }

        protected override void OnFixedUpdateBehavior(float fixedDeltaTime)
        {
            Velocity.y -= GravityForce * fixedDeltaTime;

            if (Velocity.y < TerminalVelocity)
            {
                Velocity.y = TerminalVelocity;
            }

            base.OnFixedUpdateBehavior(fixedDeltaTime);
        }

        protected override void OnTileCollide(GridPhysics.CollisionResult collision)
        {
            base.OnTileCollide(collision);
            
            AudioManager.Instance.PlayOneShot(Data.ImpactSFX, transform.position);
        }

        protected override void OnHitEntity(Entity target)
        {
            base.OnHitEntity(target);
            
            AudioManager.Instance.PlayOneShot(Data.ImpactSFX, transform.position);
        }

        protected override void OnKill()
        {
            base.OnKill();
        }
    }
}
