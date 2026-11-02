// TideWall 3D — Enemy: taking orders from the Tide Caller. A rally sends it to a spot (it still bites whoever it meets);
// a rider drives it directly, hitting harder than it would on its own.
// (One part of the Enemy class; Enemy.cs has the overview, the fields and the frame loop.)
using UnityEngine;

namespace TideWall
{
    public partial class Enemy
    {
        public Player Rider { get; private set; }
        float orderT, riderCd, riderDashCd;
        Vector3 orderAt;
        public bool Ordered => orderT > 0;

        public void SetRider(Player p)
        {
            Rider = p;
            orderT = 0;
            windupT = 0; windupTarget = null;
            if (p != null) Fx.Splash(transform.position + Vector3.up * .5f, 6);
        }

        public void Order(Vector3 at, float seconds)
        {
            if (Rider != null || headless) return;
            orderAt = at; orderT = seconds;
        }

        void FollowOrder(float dt)
        {
            orderT -= dt;
            var prey = NearestPlayer(Mathf.Max(3, Def.attackRange + 1), false);
            if (prey != null && FlatDist(prey.transform.position) <= Def.attackRange * .8f + Radius * .5f) { TryAttackPlayer(prey, dt); return; }
            if (MoveToward(orderAt, Speed * 1.35f, dt, 1 + Radius)) orderT = 0;
        }

        void UpdateRidden(float dt)
        {
            riderCd -= dt; riderDashCd -= dt;
            var dir = Rider.RideDir();
            float speed = Speed * gm.DB.lastLight.rideSpeed;
            moveSpeed = Mathf.MoveTowards(moveSpeed, speed * dir.magnitude, Accel * dt);
            if (dir.sqrMagnitude > .01f) Face(dir, dt * 2);
            transform.position += dir * moveSpeed * dt;
        }

        /// <summary>The rider's blow: at a sailor in reach (with the usual wind-up, so it can be dodged), or the lighthouse.</summary>
        public void RiderStrike()
        {
            if (riderCd > 0 || windupT > 0 || Rider == null) return;
            riderCd = 1 / Mathf.Max(.1f, Def.attackRate);
            float reach = Def.attackRange * 1.3f + Radius * .7f + .35f;
            if (NearestPlayer(reach, false) is { } prey)
            {
                windupTarget = prey;
                windupT = Mathf.Max(.01f, Def.windup);
                Sfx.Play("growl", Mathf.Clamp(.25f + Radius * .15f, .25f, .7f), .15f);
                return;
            }
            Lunge();
            if (gm.Level.AtObjective(transform.position, Def.attackRange + Radius)) gm.Level.DamageObjective(Damage * Def.wallDamage, transform.position);
            else Sfx.Play("swing", .5f, .08f);
        }

        public void RiderDash()
        {
            if (riderDashCd > 0 || Rider == null) return;
            riderDashCd = gm.DB.lastLight.rideDashCooldown;
            knock = transform.forward * gm.DB.lastLight.rideDashSpeed;
            Sfx.Play("swing", .7f, .1f);
        }
    }
}
