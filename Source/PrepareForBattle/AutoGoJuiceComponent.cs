using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace PrepareForBattle
{
    public class AutoGoJuiceComponent : WorldComponent
    {
        private Dictionary<string, bool> _pawnToggles = new Dictionary<string, bool>();

        public AutoGoJuiceComponent(World world) : base(world)
        {
        }

        public static AutoGoJuiceComponent GetForCurrentGame()
        {
            return Current.Game?.World?.GetComponent<AutoGoJuiceComponent>();
        }

        public bool IsEnabled(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            return _pawnToggles.TryGetValue(pawn.GetUniqueLoadID(), out bool enabled) && enabled;
        }

        public void SetEnabled(Pawn pawn, bool enabled)
        {
            if (pawn == null)
            {
                return;
            }

            _pawnToggles[pawn.GetUniqueLoadID()] = enabled;
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref _pawnToggles, "AutoGoJuicePawnToggles", LookMode.Value, LookMode.Value);
            if (_pawnToggles == null)
            {
                _pawnToggles = new Dictionary<string, bool>();
            }
        }
    }
}
