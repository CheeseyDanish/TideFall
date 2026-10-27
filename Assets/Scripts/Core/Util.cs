// TideWall 3D — small helpers shared by play mode and the editor tools.
using UnityEngine;

namespace TideWall
{
    public static class Util
    {
        /// <summary>Destroy in play mode, DestroyImmediate in the editor (where Destroy isn't allowed).</summary>
        public static void Destroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }
    }
}
