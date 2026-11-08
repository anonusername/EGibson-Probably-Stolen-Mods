namespace FiftyAppraisal
{
    /// <summary>
    /// Entry point. The loader applies the [HarmonyPatch] classes of this assembly, then calls
    /// Init and OnEnable from the main menu, before any save (or InspectionUIManager) exists.
    /// </summary>
    public class ModEntry : IMod
    {
        public static ModEntry Instance { get; private set; }
        public ModManifest Manifest { get; private set; }
        public ModLog Log { get; private set; }

        public void Init(ModManifest manifest)
        {
            Instance = this;
            Manifest = manifest;
            Log = new ModLog(manifest);
            Log.Log($"{manifest.Name} v{manifest.ModVersion} loaded.");
        }

        public void OnEnable()
        {
            // F9 > Command. Prints the appraisal panel hierarchy to the F8 console (needs dev.txt and a loaded run).
            ModHelper.AddDebugFunction(Manifest.ID, ModHelper.DEBUG_CATEGORY_COMMAND, "Dump Appraisal Panel",
                () => Log.Log(AppraisalButtons.DumpPanel()));
        }

        public void OnDisable()
        {
            ModHelper.RemoveDebugFunctions(Manifest.ID);
        }
    }
}
