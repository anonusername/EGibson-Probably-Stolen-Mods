using UnityEngine;

namespace JermaRats
{
    /// <summary>
    /// Small optional extras ("Extras" setting). Plays rat_tune now and then and lets the rat celebrate for its length.
    /// Plays on game sound channel 2; the music channel belongs to the cassette player.
    /// </summary>
    internal static class RatExtras
    {
        public const string Key = "rat_tune";
        private const int Count = 5;
        private const float Window = 6f;
        private const float Cooldown = 60f;
        private const uint NameHash = 0xea33fb68u;

        private static readonly float[] times = new float[Count];
        private static int count;
        private static float cooldownUntil;
        private static GameItem active;
        private static float activeUntil;

        public static void Reset()
        {
            count = 0;
            active = null;
            activeUntil = 0f;
        }

        public static bool IsCelebrating(GameItem item)
        {
            return item != null && item == active && Time.unscaledTime < activeUntil;
        }

        public static void OnPickedUp(GameItem item)
        {
            if (item == null || ModEntry.Extras == null || !ModEntry.Extras.Value)
            {
                return;
            }
            float now = Time.unscaledTime;

            string name = GeneralHelper.GetCustomName(item);
            if (name != null && Hash(name.Trim().ToLowerInvariant()) == NameHash)
            {
                Play(item, now);
                return;
            }

            if (!BiggestRat.IsChampion(item))
            {
                return;
            }
            times[count % Count] = now;
            count++;
            if (count >= Count && now - times[count % Count] <= Window)
            {
                count = 0;
                Play(item, now);
            }
        }

        private static uint Hash(string s)
        {
            uint h = 2166136261u;
            foreach (char ch in s)
            {
                h = unchecked((h ^ ch) * 16777619u);
            }
            return h;
        }

        private static void Play(GameItem item, float now)
        {
            if (now < cooldownUntil || !RatSounds.HasClip(Key))
            {
                return;
            }
            if (!ModHelper.PlaySound2(ModEntry.Id, Key))
            {
                return;
            }
            cooldownUntil = now + Cooldown;
            AudioClip clip = ModEntry.Mod.Sounds[Key];
            active = item;
            activeUntil = now + (clip != null ? clip.length : 5f);
        }
    }
}
