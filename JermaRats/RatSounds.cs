namespace JermaRats
{
    /// <summary>
    /// Points a rat's drag sounds (played by every pick-up and put-down path through AudioManager.TryPlayOne) at the
    /// mod's clips. A pair is used only when BOTH clips are registered: TryPlayOne falls back to the item's other
    /// field, not to the vanilla sound, so a half-set pair could play a drop sound on pick-up.
    /// </summary>
    internal static class RatSounds
    {
        public const string Pickup = "rat_pickup";
        public const string Drop = "rat_drop";
        public const string BigPickup = "big_pickup";
        public const string BigDrop = "big_drop";

        public static void Apply(GameItem item)
        {
            if (item == null || item.isDebugMenu || !RatSetup.IsRat(item))
            {
                return;
            }

            string start = AudioHelper.RAT_PICKUP;
            string end = AudioHelper.RAT_DROP;
            if (ModEntry.CustomSounds != null && ModEntry.CustomSounds.Value)
            {
                bool champion = BiggestRat.IsChampion(item);
                if (champion && HasClip(BigPickup) && HasClip(BigDrop))
                {
                    start = Sound(BigPickup);
                    end = Sound(BigDrop);
                }
                else if (HasClip(Pickup) && HasClip(Drop))
                {
                    start = Sound(Pickup);
                    end = Sound(Drop);
                }
            }
            item.soundDragStart = start;
            item.soundDragEnd = end;
        }

        public static void ReapplyAll()
        {
            if (!ModEntry.InRun)
            {
                return;
            }
            foreach (GameItem item in PlayerStore.Instance.FindAllItem(false))
            {
                Apply(item);
            }
        }

        public static void RestoreVanillaAll()
        {
            if (!ModEntry.InRun)
            {
                return;
            }
            foreach (GameItem item in PlayerStore.Instance.FindAllItem(false))
            {
                if (RatSetup.IsRat(item))
                {
                    item.soundDragStart = AudioHelper.RAT_PICKUP;
                    item.soundDragEnd = AudioHelper.RAT_DROP;
                }
            }
        }

        public static string Sound(string key)
        {
            return ModHelper.GetSoundIdentifier(ModEntry.Id, key);
        }

        /// <summary>True when the clip has finished loading and is registered with the game's audio directory.</summary>
        public static bool HasClip(string key)
        {
            return ModEntry.AssetsLoaded
                && ModEntry.Mod.Sounds.ContainsKey(key)
                && AudioManager.Exists
                && AudioManager.Instance.audioDirectory != null
                && AudioManager.Instance.audioDirectory.HasAudioClip(Sound(key));
        }
    }
}
