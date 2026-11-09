using System;
using UnityEngine;

namespace JermaRats
{
    /// <summary>
    /// Entry point. The loader applies the [HarmonyPatch] classes of this assembly, then calls Init and OnEnable from
    /// the main menu, before any run exists. Nothing here touches game state; the hooks do that once a run is loaded.
    /// </summary>
    public class ModEntry : IMod
    {
        public const string Id = "jerma_rats";
        private const string Section = "Jerma Rats";

        public static ModEntry Instance { get; private set; }
        public static ModLog Log { get; private set; }

        /// <summary>This mod's LoadedMod (sprites and sounds), cached when the loader reports our assets ready.</summary>
        public static LoadedMod Mod { get; private set; }
        public static bool AssetsLoaded => Mod != null;

        public static ConfigEntry<bool> Animate;
        public static ConfigEntry<float> AnimSpeed;
        public static ConfigEntry<bool> RotationOnly;
        public static ConfigEntry<bool> ChampionGraphic;
        public static ConfigEntry<bool> CustomSounds;
        public static ConfigEntry<bool> Extras;

        public ModManifest Manifest { get; private set; }

        public void Init(ModManifest manifest)
        {
            Instance = this;
            Manifest = manifest;
            Log = new ModLog(manifest);

            ModConfig config = ModHelper.GetConfig(Id);
            Animate = config.Define("animate", true, Section, () => "Animate rats",
                () => "Rats breathe and twitch, and wiggle while you drag them.");
            AnimSpeed = config.Define("anim_speed", 1f, Section, () => "Animation speed", 0.25f, 3f,
                () => "Speed of the idle motion and of animation frames.");
            RotationOnly = config.Define("rotation_only", false, Section, () => "Gentle motion (tilt only)",
                () => "Only tilt rats, never stretch them. Use this if the breathing looks blurry.");
            ChampionGraphic = config.Define("champion_graphic", true, Section, () => "Biggest Rat graphic",
                () => "The heaviest rat you own gets its own animated look.");
            CustomSounds = config.Define("custom_sounds", true, Section, () => "Custom rat sounds",
                () => "Use the mod's pick-up and put-down sounds instead of the game's.");
            Extras = config.Define("extras", true, Section, () => "Extras", () => "Little surprises.");

            CustomSounds.OnChanged += _ => RatSounds.ReapplyAll();
            ChampionGraphic.OnChanged += _ => RatSounds.ReapplyAll();

            Log.Log($"{manifest.Name} v{manifest.ModVersion} loaded.");
        }

        /// <summary>False once the loader stopped the mod. Per-rat delegates outlive OnDisable, so they check this.</summary>
        public static bool Enabled { get; private set; }

        public void OnEnable()
        {
            Enabled = true;
            ModHook.OnModAssetsLoaded += OnModAssetsLoaded;
            ModHook.OnGameLoadedInit += OnRunStarting;
            ModHook.OnNewGameStarted += OnNewGameStarted;
            ModHook.OnGameLoadedLate += BiggestRat.Rescan;
            ModHook.OnGoingSleepLate += BiggestRat.Rescan;
            ModHook.OnWakingUpLate += BiggestRat.Rescan;

            // F9 > Command (needs dev.txt and a loaded run).
            ModHelper.AddDebugFunction(Id, ModHelper.DEBUG_CATEGORY_COMMAND, "JermaRats: Recompute biggest",
                () => { BiggestRat.Rescan(); Log.Log(BiggestRat.Describe()); });
        }

        public void OnDisable()
        {
            Enabled = false;
            ModHook.OnModAssetsLoaded -= OnModAssetsLoaded;
            ModHook.OnGameLoadedInit -= OnRunStarting;
            ModHook.OnNewGameStarted -= OnNewGameStarted;
            ModHook.OnGameLoadedLate -= BiggestRat.Rescan;
            ModHook.OnGoingSleepLate -= BiggestRat.Rescan;
            ModHook.OnWakingUpLate -= BiggestRat.Rescan;
            ModHelper.RemoveDebugFunctions(Id);
            RatSounds.RestoreVanillaAll();
        }

        private static void OnModAssetsLoaded(LoadedMod mod)
        {
            if (mod?.Manifest == null || mod.Manifest.ID != Id)
            {
                return;
            }
            Mod = mod;
            RatFrames.Clear();
            Log.Log($"Assets ready: {mod.Sprites.Count} item sprite(s), {mod.Sounds.Count} sound(s).");
            RatSounds.ReapplyAll();
        }

        // A save is being loaded: forget the previous run's champion (OnGameLoadedLate rescans afterwards).
        private static void OnRunStarting()
        {
            BiggestRat.ResetForNewRun();
            RatExtras.Reset();
        }

        // New games don't fire OnGameLoadedLate, so reset and rescan here (debounced, after the start kit is placed).
        private static void OnNewGameStarted(string startId)
        {
            OnRunStarting();
            BiggestRat.QueueRescan();
        }

        /// <summary>True once a run is loaded. PlayerStore.Instance creates a store on first access, so check this first.</summary>
        public static bool InRun => EmporiumEntry.Instance != null;

        public static void Warn(string message)
        {
            Log?.Warning(message);
        }
    }
}
