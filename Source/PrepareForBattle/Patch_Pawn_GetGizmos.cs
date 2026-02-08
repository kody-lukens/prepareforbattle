using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace PrepareForBattle
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_Pawn_GetGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (Gizmo gizmo in __result)
            {
                yield return gizmo;
            }

            if (!ShouldShow(__instance))
            {
                yield break;
            }

            yield return new PrepareForBattleCommand(__instance);
        }

        private static bool ShouldShow(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Downed)
            {
                return false;
            }

            if (!pawn.IsColonistPlayerControlled)
            {
                return false;
            }

            return pawn.drafter != null && pawn.drafter.Drafted;
        }
    }
}
