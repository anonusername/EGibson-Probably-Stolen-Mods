using System;
using UnityEngine;

namespace JermaRats
{
    /// <summary>Per-rat wiring, done once when the game builds the rat item.</summary>
    internal static class RatSetup
    {
        public const string SpeciesRat = "ANIMAL_RAT";

        public static void Attach(GameItem item)
        {
            // Sounds are set when the rat is placed, not here: the factory calls AudioHelper.InitRat right after
            // InitAnimalItem and would overwrite them. Placement also fires on save load, after saved tags are back.
            item.onSlotAddItemFunc += OnPlaced;

            if (item is GameItemElement element && element.handler != null)
            {
                GameObject go = element.handler.gameObject;
                RatAnimator animator = go.GetComponent<RatAnimator>();
                if (animator == null)
                {
                    animator = go.AddComponent<RatAnimator>();
                }
                animator.Bind(element);
            }
        }

        private static void OnPlaced(GameItem item, GameInventory inventory, SlotMarker slot)
        {
            if (item == null || item.isDebugMenu || !ModEntry.Enabled)
            {
                return;
            }
            try
            {
                RatSounds.Apply(item);
                BiggestRat.OnRatPlaced(item);
            }
            catch (Exception e)
            {
                ModEntry.Warn($"Rat placement hook failed: {e}");
            }
        }

        public static bool IsRat(GameItem item)
        {
            return item != null
                && item.IsTag("ANIMAL_ITEM_TAG")
                && item.GetTagReadonly("ANIMAL_SPECIES_TAG").GetString() == SpeciesRat;
        }

        public static bool IsLiveRat(GameItem item)
        {
            return IsRat(item) && !item.IsTag("ANIMAL_IS_DEAD_TAG") && !item.isDebugMenu;
        }

        public static float Weight(GameItem item)
        {
            return item.GetTagReadonly("ANIMAL_WEIGHT_TAG").GetFloat();
        }

        public static int Age(GameItem item)
        {
            return item.GetTagReadonly("ANIMAL_AGE_TAG").GetInt();
        }

        public static bool IsFemale(GameItem item)
        {
            return item.GetTagReadonly("ANIMAL_SEXE_TAG").GetString() == "FEMALE";
        }
    }
}
