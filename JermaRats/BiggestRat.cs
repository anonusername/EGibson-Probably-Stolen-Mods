using System;
using System.Collections.Generic;

namespace JermaRats
{
    /// <summary>
    /// Tracks the champion: the heaviest live rat the player owns. The champion carries JERMA_BIGGEST_TAG, an item tag,
    /// so it is saved with the rat and restored before the rat is placed on load.
    /// Rescans run after the nightly growth (OnGoingSleepLate, before the save), on game load, on waking, when a rat
    /// heavier than the champion is placed, and when the champion dies.
    /// </summary>
    internal static class BiggestRat
    {
        public const string Tag = "JERMA_BIGGEST_TAG";

        private static GameItem champion;
        private static float championWeight;
        private static bool rescanQueued;

        public static GameItem Champion => champion;

        public static bool IsChampion(GameItem item)
        {
            return item != null && item.IsTag(Tag);
        }

        public static void Rescan()
        {
            rescanQueued = false;
            if (!ModEntry.InRun)
            {
                return;
            }
            try
            {
                GameItem best = null;
                float bestWeight = 0f;
                int bestAge = 0;
                foreach (GameItem item in PlayerStore.Instance.FindAllItem(true))
                {
                    if (!RatSetup.IsLiveRat(item))
                    {
                        continue;
                    }
                    float weight = RatSetup.Weight(item);
                    int age = RatSetup.Age(item);
                    if (best == null || weight > bestWeight
                        || (weight == bestWeight && (age > bestAge || (age == bestAge && item.uniqueId < best.uniqueId))))
                    {
                        best = item;
                        bestWeight = weight;
                        bestAge = age;
                    }
                }

                // Clear the tag everywhere, including rats no longer owned (a sold rat can still sit in a store window).
                var changed = new List<GameItem>();
                foreach (GameItem item in PlayerStore.Instance.FindAllItem(false))
                {
                    // Validate (no skipValidate): IsTag reads modifiedState, which only ValidateShapeState rebuilds.
                    if (item != best && item.IsTag(Tag))
                    {
                        item.DisableTag(Tag);
                        changed.Add(item);
                    }
                }
                if (best != null && !best.IsTag(Tag))
                {
                    best.EnableTag(Tag);
                    changed.Add(best);
                }

                champion = best;
                championWeight = bestWeight;
                foreach (GameItem item in changed)
                {
                    RatSounds.Apply(item);
                }
            }
            catch (Exception e)
            {
                ModEntry.Warn($"Biggest-rat rescan failed: {e}");
            }
        }

        /// <summary>Called whenever a rat is placed in an inventory (spawn, drop, purchase, load). O(1) unless it wins.</summary>
        public static void OnRatPlaced(GameItem item)
        {
            if (!ModEntry.InRun || !RatSetup.IsLiveRat(item) || item == champion)
            {
                return;
            }
            if (champion == null || RatSetup.Weight(item) > championWeight)
            {
                QueueRescan();
            }
        }

        /// <summary>Debounced: many rats placed in one frame (a save load, a kit spawn) cause one rescan.</summary>
        public static void QueueRescan()
        {
            if (rescanQueued || !ModEntry.Enabled)
            {
                return;
            }
            rescanQueued = true;
            try
            {
                // Delay returns null (no exception) when the mod is no longer running.
                if (ModHelper.Delay(ModEntry.Id, 0.1f, Rescan) == null)
                {
                    rescanQueued = false;
                }
            }
            catch (Exception)
            {
                // No delay available: the next nightly or load rescan catches up.
                rescanQueued = false;
            }
        }

        /// <summary>Forget the previous run (new game or another save loaded); its items are gone.</summary>
        public static void ResetForNewRun()
        {
            champion = null;
            championWeight = 0f;
            rescanQueued = false;
        }

        public static string Describe()
        {
            if (champion == null)
            {
                return "JermaRats: no live owned rat, so no Biggest Rat.";
            }
            return $"JermaRats: Biggest Rat is #{champion.uniqueId}, {RatSetup.Weight(champion):0} g, "
                + $"age {RatSetup.Age(champion)} day(s), sprite {champion.spritePath}.";
        }
    }
}
