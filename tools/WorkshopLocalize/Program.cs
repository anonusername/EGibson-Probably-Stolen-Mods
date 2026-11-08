using System.Text;
using System.Text.RegularExpressions;
using Steamworks;

// Pushes each packages\50 - packages\steam\descriptions\<language>.bbcode (repo-root packages\) to the Workshop item as that language's title and
// description, then reads it back to confirm. The file name is the Steam API language code (schinese, koreana, ...).
// Needs the Steam client running, online, and logged in as the item's owner. A steamcmd login with the same account
// signs the client out ("Session Replaced"); click Go Online in Steam before running this.
//
//   dotnet run -c Release -- --verify-only     read back every language, change nothing
//   dotnet run -c Release -- --only schinese   push one language
//   dotnet run -c Release                      push every language except english (steamcmd writes english)
//   dotnet run -c Release -- --tags "UI,QoL"   tag update only: replaces the item's whole tag list
//   options: --item <id>  --dry-run (prints what it would do, no Steam calls)  --include-english

const uint APP_ID = 4348910; // Probably Stolen
const string OFFLINE_HINT = "Usually the Steam client is offline after a steamcmd login: click Steam > Go Online, then run again.";
Console.OutputEncoding = Encoding.UTF8; // otherwise CJK, Thai and Cyrillic print as '?'

string? itemArg = Arg("--item");
string[]? only = Arg("--only")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
bool dryRun = args.Contains("--dry-run");
bool verifyOnly = args.Contains("--verify-only");
bool includeEnglish = args.Contains("--include-english");
string[]? tags = Arg("--tags")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
if (args.Contains("--tags"))
{
    // An empty list would wipe the item's tags, so refuse it. Steam: each tag at most 255 printable chars, no comma.
    if (tags == null || tags.Length == 0) return Fail("--tags needs at least one tag, e.g. --tags \"UI,QoL\".");
    string? badTag = tags.FirstOrDefault(t => t.Length > 255 || t.Any(char.IsControl));
    if (badTag != null) return Fail($"Tag \"{badTag}\" is invalid: Steam allows at most 255 printable characters.");
    tags = tags.Distinct(StringComparer.Ordinal).ToArray();
}

string steamDir = FindSteamPackageDir();
string vdf = File.ReadAllText(Path.Combine(steamDir, "workshop.vdf"));
ulong itemId = ulong.Parse(itemArg ?? VdfValue(vdf, "publishedfileid"));
string title = VdfValue(vdf, "title");
if (itemId == 0) return Fail("workshop.vdf has publishedfileid 0; upload the item with steamcmd first.");

var languages = Directory.GetFiles(Path.Combine(steamDir, "descriptions"), "*.bbcode")
    .Select(path => (code: Path.GetFileNameWithoutExtension(path), text: Normalize(File.ReadAllText(path))))
    .Where(l => only != null ? only.Contains(l.code) : includeEnglish || l.code != "english")
    .OrderBy(l => l.code)
    .ToList();
if (only != null && only.Except(languages.Select(l => l.code)).Any())
    return Fail("No description file for: " + string.Join(", ", only.Except(languages.Select(l => l.code))));

if (tags != null)
    Console.WriteLine($"Item {itemId}, tag update only, setting: {string.Join(", ", tags)}");
else
    Console.WriteLine($"Item {itemId}, title \"{title}\", {languages.Count} language(s): {string.Join(", ", languages.Select(l => l.code))}");
if (dryRun) return 0;

// Run as the game, like the game's own uploader would; avoids needing a steam_appid.txt in the working directory.
Environment.SetEnvironmentVariable("SteamAppId", APP_ID.ToString());
Environment.SetEnvironmentVariable("SteamGameId", APP_ID.ToString());
if (!SteamAPI.Init()) return Fail("SteamAPI.Init failed. Is the Steam client running and logged in as the item's owner?");

int failures = 0;
try
{
    // SteamAPI.Init succeeds whenever the Steam process runs, even while it's signed out of Steam's servers.
    if (!SteamUser.BLoggedOn())
    {
        Console.WriteLine("Steam client is offline (a steamcmd login with this account signs it out). Click Steam > Go Online now; waiting 15 s...");
        for (int i = 0; i < 30 && !SteamUser.BLoggedOn(); i++)
        {
            SteamAPI.RunCallbacks();
            Thread.Sleep(500);
        }
        if (!SteamUser.BLoggedOn()) return Fail("Steam client is still offline. Click Steam > Go Online, then run again.");
    }
    Console.WriteLine($"Logged in as {SteamFriends.GetPersonaName()} ({SteamUser.GetSteamID().m_SteamID})");

    // Look the item up first: this shows what Steam knows about it, and loads it into the client before any update.
    Console.WriteLine($"Steam sees this process as app {SteamUtils.GetAppID()}");
    SteamUGCDetails_t? info = Details(itemId, "english", report: true);
    if (info == null) return Fail("Steam returned no details for the item (query failed, see above). " + OFFLINE_HINT);
    SteamUGCDetails_t item = info.Value;
    Console.WriteLine($"Item lookup: {item.m_eResult}, owner {item.m_ulSteamIDOwner}, creator app {item.m_nCreatorAppID}, " +
                      $"consumer app {item.m_nConsumerAppID}, visibility {item.m_eVisibility}, title \"{item.m_rgchTitle}\"");
    if (item.m_eResult != EResult.k_EResultOK) return Fail("Steam can't see the item from this account and app.");
    if (item.m_ulSteamIDOwner != SteamUser.GetSteamID().m_SteamID)
        return Fail("The Steam client is logged in as a different account than the item's owner.");
    AppId_t consumerApp = item.m_nConsumerAppID;

    if (tags != null)
    {
        Console.WriteLine($"  current tags: {(item.m_rgchTags.Length == 0 ? "(none)" : item.m_rgchTags)}");
        if (!verifyOnly)
        {
            // A tag-only update: no language, title or description setters, so no language text changes.
            EResult result = Submit(consumerApp, itemId, "tags", handle =>
                SteamUGC.SetItemTags(handle, tags) ? null
                    : $"the tags (longest is {tags.Max(t => t.Length)} chars, max 255)");
            if (!Report(result, "tags")) return 1;
            Thread.Sleep(1500);
        }
        SteamUGCDetails_t? after = Details(itemId, "english");
        if (after == null) return Fail("  tags read-back FAILED.");
        string live = after.Value.m_rgchTags;
        // Steam's tag filter is an exact match, so compare exactly (as a set; order doesn't matter).
        var liveSet = live.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        if (after.Value.m_bTagsTruncated)
            Console.WriteLine($"  live tags (truncated by Steam, can't compare): {live}");
        else if (liveSet.SetEquals(tags))
            Console.WriteLine($"  tags set: {live}");
        else
        {
            Console.WriteLine($"  tags DIFFER from requested. Live: {(live.Length == 0 ? "(none)" : live)}");
            failures++;
        }
        languages.Clear(); // --tags runs only the tag update
    }

    foreach (var (code, text) in languages)
    {
        if (!verifyOnly)
        {
            // Steam's limits are UTF-8 bytes: title 128, description 8000.
            EResult result = Submit(consumerApp, itemId, code, handle =>
                !SteamUGC.SetItemUpdateLanguage(handle, code) ? $"the language code \"{code}\" (unknown to Steam?)"
                : !SteamUGC.SetItemTitle(handle, title) ? $"the title ({Encoding.UTF8.GetByteCount(title)} bytes, max 128)"
                : !SteamUGC.SetItemDescription(handle, text) ? $"the description ({Encoding.UTF8.GetByteCount(text)} bytes, max 8000)"
                : null);
            if (!Report(result, code))
            {
                failures++;
                continue;
            }
            Thread.Sleep(1500); // stay well under Steam's update rate limit
        }

        string? live = ReadBack(itemId, code);
        string state = live == null ? "read-back FAILED" : live == text ? "matches file" : "DIFFERS from file";
        if (live != text) failures++;
        Console.WriteLine($"  {code,-12} {(verifyOnly ? "" : "uploaded, ")}{state}");
        if (live != null && live != text) PrintDifference(text, live);
    }
}
finally
{
    SteamAPI.Shutdown();
}
Console.WriteLine(failures == 0 ? "Done." : $"Done with {failures} problem(s).");
return failures == 0 ? 0 : 1;

// Prints a failed update with the offline hint where it applies; true when the update succeeded.
static bool Report(EResult result, string label)
{
    if (result == EResult.k_EResultOK) return true;
    Console.WriteLine($"  {label,-12} upload FAILED: {result}");
    if (result is EResult.k_EResultFileNotFound or EResult.k_EResultNotLoggedOn or EResult.k_EResultNoConnection)
        Console.WriteLine($"  {"",-12} (Steam couldn't fetch the item. {OFFLINE_HINT})");
    return false;
}

// One item update: apply() calls the setters and returns what Steam rejected, or null. Retries when rate limited.
static EResult Submit(AppId_t consumerApp, ulong itemId, string label, Func<UGCUpdateHandle_t, string?> apply)
{
    for (int attempt = 1; ; attempt++)
    {
        UGCUpdateHandle_t handle = SteamUGC.StartItemUpdate(consumerApp, new PublishedFileId_t(itemId));
        if (handle == UGCUpdateHandle_t.Invalid)
        {
            Console.WriteLine($"  {label,-12} StartItemUpdate returned an invalid handle");
            return EResult.k_EResultFail;
        }
        string? rejected = apply(handle);
        if (rejected != null)
        {
            Console.WriteLine($"  {label,-12} Steam rejected {rejected}");
            return EResult.k_EResultInvalidParam;
        }
        var result = Wait<SubmitItemUpdateResult_t>(SteamUGC.SubmitItemUpdate(handle, null));
        if (result.m_bUserNeedsToAcceptWorkshopLegalAgreement)
            Console.WriteLine("  (Steam says the Workshop legal agreement still needs accepting)");
        bool rateLimited = result.m_eResult is EResult.k_EResultLimitExceeded or EResult.k_EResultRateLimitExceeded;
        if (!rateLimited || attempt == 3) return result.m_eResult;
        Console.WriteLine($"  {label,-12} rate limited, waiting {attempt * 15} s");
        Thread.Sleep(attempt * 15000);
    }
}

// The item's description as Steam returns it for this language (uncached).
static string? ReadBack(ulong itemId, string language) =>
    Details(itemId, language) is { } details && details.m_eResult == EResult.k_EResultOK ? Normalize(details.m_rgchDescription) : null;

static SteamUGCDetails_t? Details(ulong itemId, string language, bool report = false)
{
    UGCQueryHandle_t query = SteamUGC.CreateQueryUGCDetailsRequest(new[] { new PublishedFileId_t(itemId) }, 1);
    if (query == UGCQueryHandle_t.Invalid)
    {
        if (report) Console.WriteLine("Query: CreateQueryUGCDetailsRequest returned an invalid handle");
        return null;
    }
    try
    {
        SteamUGC.SetLanguage(query, language);
        SteamUGC.SetReturnLongDescription(query, true);
        SteamUGC.SetAllowCachedResponse(query, 0);
        SteamAPICall_t call = SteamUGC.SendQueryUGCRequest(query);
        if (call == SteamAPICall_t.Invalid)
        {
            if (report) Console.WriteLine("Query: SendQueryUGCRequest returned an invalid call handle");
            return null;
        }
        var completed = Wait<SteamUGCQueryCompleted_t>(call);
        SteamUGCDetails_t details = default;
        bool got = completed.m_eResult == EResult.k_EResultOK && completed.m_unNumResultsReturned > 0
                   && SteamUGC.GetQueryUGCResult(query, 0, out details);
        if (report)
            Console.WriteLine($"Query: {completed.m_eResult}, returned {completed.m_unNumResultsReturned}, " +
                              $"matching {completed.m_unTotalMatchingResults}, cached {completed.m_bCachedData}");
        return got ? details : null;
    }
    finally
    {
        SteamUGC.ReleaseQueryUGCRequest(query);
    }
}

static T Wait<T>(SteamAPICall_t call) where T : struct
{
    T? result = null;
    bool ioFailure = false;
    var callResult = CallResult<T>.Create((r, failed) => { result = r; ioFailure = failed; });
    callResult.Set(call);
    DateTime deadline = DateTime.UtcNow.AddSeconds(60);
    while (result == null)
    {
        if (DateTime.UtcNow > deadline) throw new TimeoutException($"No reply from Steam for {typeof(T).Name}");
        SteamAPI.RunCallbacks();
        Thread.Sleep(50);
    }
    callResult.Dispose();
    if (ioFailure) throw new IOException($"Steam reported an IO failure for {typeof(T).Name}");
    return result.Value;
}

static string Normalize(string text) => text.Replace("\r\n", "\n").Trim();

// Where the live text first differs from the file, so stale text can be told from Steam re-formatting it.
static void PrintDifference(string file, string live)
{
    int i = 0;
    int common = Math.Min(file.Length, live.Length);
    while (i < common && file[i] == live[i]) i++;
    Console.WriteLine($"  {"",-12} file {file.Length} chars / {Encoding.UTF8.GetByteCount(file)} bytes, " +
                      $"live {live.Length} chars / {Encoding.UTF8.GetByteCount(live)} bytes, first difference at char {i}");
    Console.WriteLine($"  {"",-12} file: {Snippet(file, i)}");
    Console.WriteLine($"  {"",-12} live: {Snippet(live, i)}");
}

// About 40 chars either side of offset, whitespace made visible, without splitting a surrogate pair.
static string Snippet(string s, int offset)
{
    if (offset >= s.Length) return "<end>";
    int start = Math.Max(0, offset - 40), end = Math.Min(s.Length, offset + 40);
    if (start > 0 && char.IsLowSurrogate(s[start])) start--;
    if (end < s.Length && char.IsHighSurrogate(s[end - 1])) end++;
    return s[start..end].Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t").Replace("\u00A0", "\\u00A0");
}

static string VdfValue(string vdf, string key) =>
    Regex.Match(vdf, "\"" + key + "\"\\s*\"([^\"]*)\"") is { Success: true } m ? m.Groups[1].Value
        : throw new InvalidDataException($"workshop.vdf has no \"{key}\"");

// packages\50 - packages\steam (the 50 mod's folder in the repo-root packages\), found by walking up from the working
// directory (works from the repo root, the mod folder or the tool folder).
static string FindSteamPackageDir()
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
    {
        string candidate = Path.Combine(dir.FullName, "packages", "50 - packages", "steam");
        if (File.Exists(Path.Combine(candidate, "workshop.vdf"))) return candidate;
    }
    throw new DirectoryNotFoundException("packages\\50 - packages\\steam\\workshop.vdf not found above the working directory");
}

string? Arg(string name)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}
