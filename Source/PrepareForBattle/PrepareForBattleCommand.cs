using RimWorld;
using UnityEngine;
using Verse;

namespace PrepareForBattle
{
    public class PrepareForBattleCommand : Command_Action
    {
        private const float OverlaySize = 24f;
        private const float OverlayPadding = 4f;
        private readonly Pawn _pawn;

        public PrepareForBattleCommand(Pawn pawn)
        {
            _pawn = pawn;
            defaultLabel = "Prepare For Battle";
            defaultDesc = BattlePrepUtility.BuildGizmoTooltip();
            icon = ContentFinder<Texture2D>.Get("UI/Commands/PrepareForBattle");
            action = () => BattlePrepUtility.TryPrepare(_pawn);
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), Height);
            Rect overlayRect = new Rect(
                rect.xMax - OverlaySize - OverlayPadding,
                rect.y + OverlayPadding,
                OverlaySize,
                OverlaySize);

            bool overlayClicked = Widgets.ButtonInvisible(overlayRect);
            if (overlayClicked)
            {
                ToggleAutoGoJuice();
            }

            GizmoResult result = base.GizmoOnGUI(topLeft, maxWidth, parms);

            DrawOverlay(overlayRect);
            TooltipHandler.TipRegion(overlayRect, "Auto Go-juice for this pawn");

            if (overlayClicked)
            {
                return new GizmoResult(GizmoState.Interacted);
            }

            return result;
        }

        private void DrawOverlay(Rect rect)
        {
            ThingDef goJuiceDef = DefDatabase<ThingDef>.GetNamedSilentFail("GoJuice");
            if (goJuiceDef != null)
            {
                Widgets.DefIcon(rect, goJuiceDef);
            }
            else
            {
                Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.75f));
            }

            bool enabled = AutoGoJuiceComponent.GetForCurrentGame()?.IsEnabled(_pawn) ?? false;
            Rect checkRect = rect.ContractedBy(2f);
            GUI.color = enabled ? Color.green : new Color(1f, 1f, 1f, 0.35f);
            Widgets.CheckboxDraw(checkRect.x, checkRect.y, enabled, false, checkRect.width);
            GUI.color = Color.white;
        }

        private void ToggleAutoGoJuice()
        {
            AutoGoJuiceComponent component = AutoGoJuiceComponent.GetForCurrentGame();
            if (component == null)
            {
                return;
            }

            bool next = !component.IsEnabled(_pawn);
            component.SetEnabled(_pawn, next);
        }
    }
}
