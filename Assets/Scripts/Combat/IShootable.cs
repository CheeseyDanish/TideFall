// TideWall 3D — anything that isn't a creature but can still be shot, cut or blown up: Part II's brood spires, the
// wreck-wall, the Heart. Combat checks for it after creatures, so guns, melee and explosions need no special cases.
namespace TideWall
{
    public interface IShootable
    {
        /// <summary>Still standing (a dead one lets shots pass through).</summary>
        bool Alive { get; }

        /// <summary>Took a hit (shot, swing or explosion: see DamageInfo.melee / .explosion).</summary>
        void Shot(DamageInfo hit);
    }
}
