// TideWall 3D — one shootable, severable part of a creature (a claw, a leg, a head): its HP and what losing it does.
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public class Limb : MonoBehaviour
    {
        public Enemy Owner;
        public LimbDef Def;
        public float Hp;
        public bool Severed;
        public Quaternion BaseRotation;
        public string Kind;   // Leg / Arm / Wing / Tent / Claw / Tail / Head / Eyes / Fin / Shell
        public int Side;      // -1 left, +1 right, 0 centre
        public float Phase;   // animation offset so tentacles don't move in lockstep
    }
}
