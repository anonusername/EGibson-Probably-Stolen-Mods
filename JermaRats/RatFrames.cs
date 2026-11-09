using System.Collections.Generic;
using UnityEngine;

namespace JermaRats
{
    /// <summary>
    /// Looks up animation frames in the mod's Textures\Items\ sprites: "&lt;prefix&gt;_0", "&lt;prefix&gt;_1", ... with no
    /// gaps (at most 32). Results are cached per prefix; an empty array means "no frames for this prefix".
    /// </summary>
    internal static class RatFrames
    {
        private const int MaxFrames = 32;
        private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();
        private static readonly HashSet<string> warnedSize = new HashSet<string>();
        private static readonly HashSet<Sprite> ours = new HashSet<Sprite>();
        public static readonly Sprite[] Empty = new Sprite[0];

        /// <summary>Bumped whenever the cache is cleared (assets reloaded), so animators re-resolve their frame sets.</summary>
        public static int Version { get; private set; }

        public static void Clear()
        {
            cache.Clear();
            warnedSize.Clear();
            ours.Clear();
            Version++;
        }

        /// <summary>True for any sprite handed out as a frame, so it is never mistaken for the game's own sprite.</summary>
        public static bool IsOurs(Sprite sprite)
        {
            return sprite != null && ours.Contains(sprite);
        }

        public static Sprite[] Get(string prefix)
        {
            if (!ModEntry.AssetsLoaded)
            {
                return Empty;
            }
            if (cache.TryGetValue(prefix, out Sprite[] frames))
            {
                return frames;
            }
            var list = new List<Sprite>();
            Dictionary<string, Sprite> sprites = ModEntry.Mod.Sprites;
            while (list.Count < MaxFrames && sprites.TryGetValue(prefix + "_" + list.Count, out Sprite frame) && frame != null)
            {
                list.Add(frame);
            }
            frames = list.Count > 0 ? list.ToArray() : Empty;
            cache[prefix] = frames;
            foreach (Sprite s in frames)
            {
                ours.Add(s);
            }
            return frames;
        }

        /// <summary>
        /// Frames for a rat whose vanilla sprite is rat_&lt;size&gt;_&lt;m|f&gt;. The champion uses big_&lt;size&gt;_f_* (female, if
        /// present) or big_&lt;size&gt;_*; other rats use rat_&lt;size&gt;_&lt;m|f&gt;_*. Empty when there are none.
        /// </summary>
        public static Sprite[] For(string size, string sex, bool champion)
        {
            if (champion)
            {
                if (sex == "f")
                {
                    Sprite[] female = Get("big_" + size + "_f");
                    if (female.Length > 0)
                    {
                        return female;
                    }
                }
                Sprite[] big = Get("big_" + size);
                if (big.Length > 0)
                {
                    return big;
                }
            }
            return Get("rat_" + size + "_" + sex);
        }

        /// <summary>Warns once per frame set whose pixel size differs from the vanilla sprite it replaces (it would be stretched).</summary>
        public static void CheckSize(Sprite[] frames, Sprite vanilla)
        {
            if (frames.Length == 0 || vanilla == null)
            {
                return;
            }
            string label = frames[0].name + "|" + vanilla.name;
            if (warnedSize.Contains(label))
            {
                return;
            }
            Rect want = vanilla.rect;
            foreach (Sprite frame in frames)
            {
                if (Mathf.RoundToInt(frame.rect.width) != Mathf.RoundToInt(want.width)
                    || Mathf.RoundToInt(frame.rect.height) != Mathf.RoundToInt(want.height))
                {
                    warnedSize.Add(label);
                    ModEntry.Warn($"Frame '{frame.name}' is {frame.rect.width}x{frame.rect.height} px but the rat sprite it "
                        + $"replaces ({vanilla.name}) is {want.width}x{want.height} px. It will look stretched; resize it.");
                    return;
                }
            }
        }
    }
}
