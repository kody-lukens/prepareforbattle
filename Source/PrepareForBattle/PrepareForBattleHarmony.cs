using HarmonyLib;
using Verse;

namespace PrepareForBattle
{
    [StaticConstructorOnStartup]
    public static class PrepareForBattleHarmony
    {
        static PrepareForBattleHarmony()
        {
            Harmony harmony = new Harmony("kodyl.prepareforbattle");
            harmony.PatchAll();
            Log.Message("[PrepareForBattle] Harmony patches loaded.");
        }
    }
}
