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

            AutoGoJuiceComponent goJuiceComponent = AutoGoJuiceComponent.GetForCurrentGame();
            if (goJuiceComponent != null)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = $"Auto Go-juice: {(goJuiceComponent.IsEnabled(__instance) ? "On" : "Off")}",
                    defaultDesc = "When enabled, this pawn will queue a Go-juice dose before other prep actions.",
                    isActive = () => goJuiceComponent.IsEnabled(__instance),
                    toggleAction = () =>
                    {
                        bool next = !goJuiceComponent.IsEnabled(__instance);
                        goJuiceComponent.SetEnabled(__instance, next);
                    }
                };
            }

            yield return new Command_Action
            {
                defaultLabel = "Prepare For Battle",
                defaultDesc = BattlePrepUtility.BuildGizmoTooltip(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/PrepareForBattle"),
                action = () => BattlePrepUtility.TryPrepare(__instance)
            };
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
