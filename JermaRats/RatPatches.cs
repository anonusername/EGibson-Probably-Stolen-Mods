using System;
using HarmonyLib;

namespace JermaRats
{
    /// <summary>
    /// The mod's only Harmony patch. HusbandryHelper.InitAnimalItem runs for every rat the game creates: new rats,
    /// births, trapped and bought rats, and every rat rebuilt from a save (all go through the "rat" item factory).
    /// The one-line InitRatItem wrapper is not patched because the runtime may inline it, which skips the detour.
    /// The loader applies this patch automatically; never create a Harmony instance or call PatchAll.
    /// </summary>
    [HarmonyPatch(typeof(HusbandryHelper), nameof(HusbandryHelper.InitAnimalItem))]
    internal static class InitAnimalItemPatch
    {
        private static void Postfix(GameItem item, AnimalSpecies species)
        {
            if (item == null || species != AnimalSpecies.Rat)
            {
                return;
            }
            try
            {
                RatSetup.Attach(item);
            }
            catch (Exception e)
            {
                ModEntry.Warn($"Could not set up a rat: {e}");
            }
        }
    }
}
