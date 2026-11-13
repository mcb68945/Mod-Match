using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Modding;

namespace ModMatch;

/// <summary>
/// Mod Match: co-op only works when everyone runs the same gameplay mods at the same versions, on the same game version.
/// This mod shows your list on the multiplayer menu, copies it as a short line you can send to friends, compares it with a
/// friend's copied line, and adds a plain breakdown to the game's "Mod mismatch!" popup. It changes nothing in a run and is
/// marked as not affecting gameplay, so it never causes a mismatch itself.
/// </summary>
[ModInitializer("Initialize")]
public static class ModMatchMod
{
    public const string Id = "ModMatch";
    private static Harmony? _harmony;

    public static void Initialize()
    {
        try
        {
            _harmony = new Harmony("modmatch");
            Hook("MegaCrit.Sts2.Core.Nodes.Screens.MainMenu.NMultiplayerSubmenu", "_Ready", postfix: nameof(Panel.ReadyPostfix), owner: typeof(Panel));
            Hook("MegaCrit.Sts2.Core.Entities.Multiplayer.NetErrorInfo", "GetErrorString", postfix: nameof(MismatchText.Postfix), owner: typeof(MismatchText));
            Log($"loaded {typeof(ModMatchMod).Assembly.GetName().Version?.ToString(3)} · {ModList.Gameplay().Count} gameplay mod(s), {ModList.Other().Count} other");
        }
        catch (Exception e) { LogError($"failed to initialize: {e}"); }
    }

    private static void Hook(string typeName, string method, string postfix, Type owner)
    {
        try
        {
            var target = AccessTools.TypeByName(typeName) is { } t ? AccessTools.Method(t, method) : null;
            if (target == null) { LogError($"{typeName}.{method} not found (game changed?), that part is off"); return; }
            _harmony!.Patch(target, postfix: new HarmonyMethod(AccessTools.Method(owner, postfix)));
        }
        catch (Exception e) { LogError($"could not hook {typeName}.{method}: {e.Message}"); }
    }

    public static void Log(string m) => GD.Print($"[{Id}] {m}");
    public static void LogError(string m) => GD.PrintErr($"[{Id}] {m}");
}

/// <summary>A mod as the game identifies it in the co-op handshake: "id-version".</summary>
public readonly record struct ModRef(string Id, string Version, string Name, bool Workshop)
{
    public string Key => $"{Id}-{Version}";

    /// <summary>Split the game's "id-version" string. Ids can contain '-', versions don't start with a letter other than v.</summary>
    public static ModRef Parse(string key)
    {
        int i = key.LastIndexOf('-');
        return i <= 0 ? new ModRef(key, "", key, false) : new ModRef(key[..i], key[(i + 1)..], key[..i], false);
    }
}

public static class ModList
{
    private static IEnumerable<ModRef> Loaded(bool gameplay) =>
        ModManager.GetLoadedMods()
            .Where(m => (m.manifest?.affectsGameplay ?? true) == gameplay)
            .Select(m => new ModRef(m.manifest?.id ?? "?", m.manifest?.version ?? "", m.manifest?.name ?? m.manifest?.id ?? "?", m.workshopId.HasValue))
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase);

    public static List<ModRef> Gameplay() { try { return Loaded(true).ToList(); } catch { return new(); } }
    public static List<ModRef> Other() { try { return Loaded(false).ToList(); } catch { return new(); } }

    public static string GameVersion => ReleaseInfoManager.Instance.ReleaseInfo?.Version ?? "?";

    // ---- the line friends send each other ----
    private const string Prefix = "ModMatch v1";

    public static string Share() =>
        $"{Prefix} | game {GameVersion} | " + (Gameplay() is { Count: > 0 } g ? string.Join(", ", g.Select(m => m.Key)) : "no gameplay mods");

    /// <summary>Parse a friend's line. Returns null if the text isn't a Mod Match line.</summary>
    public static (string game, List<ModRef> mods)? ParseShare(string text)
    {
        var line = text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase));
        if (line == null) return null;
        var parts = line.Split('|').Select(p => p.Trim()).ToArray();
        if (parts.Length < 3) return null;
        string game = parts[1].StartsWith("game ") ? parts[1][5..].Trim() : "?";
        var mods = parts[2] == "no gameplay mods" ? new List<ModRef>()
            : parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(ModRef.Parse).ToList();
        return (game, mods);
    }

    /// <summary>Plain-language differences between your gameplay mods and theirs.</summary>
    public static List<string> Diff(IReadOnlyCollection<ModRef> mine, IReadOnlyCollection<ModRef> theirs, string meLabel, string themLabel)
    {
        var lines = new List<string>();
        var a = mine.GroupBy(m => m.Id).ToDictionary(g => g.Key, g => g.First());
        var b = theirs.GroupBy(m => m.Id).ToDictionary(g => g.Key, g => g.First());
        foreach (var id in a.Keys.Intersect(b.Keys).OrderBy(x => x))
            if (a[id].Version != b[id].Version)
                lines.Add($"Different versions of {a[id].Name}: {meLabel} {a[id].Version}, {themLabel} {b[id].Version}. Everyone needs the same version.");
        foreach (var id in b.Keys.Except(a.Keys).OrderBy(x => x))
            lines.Add($"Only {themLabel} {Have(themLabel)} {b[id].Name} {b[id].Version}.");
        foreach (var id in a.Keys.Except(b.Keys).OrderBy(x => x))
            lines.Add($"Only {meLabel} {Have(meLabel)} {a[id].Name} {a[id].Version}.");
        return lines;
    }

    private static string Have(string who) => who is "you" or "they" ? "have" : "has";
}

/// <summary>Adds a plain breakdown under the game's "Mod mismatch!" text (version differences paired up, who has what).</summary>
public static class MismatchText
{
    public static void Postfix(ref NetErrorInfo __instance, ref string __result)
    {
        try
        {
            var info = __instance.ConnectionExtraInfo;
            if (info == null) return;
            var local = info.localInfo.gameplayAffectingMods ?? new List<string>();
            var remote = info.remoteInfo.gameplayAffectingMods ?? new List<string>();
            if (local.ToHashSet().SetEquals(remote)) return;
            string me = "you", them = info.localIsHost ? "the player joining" : "the host";
            var lines = ModList.Diff(local.Select(ModRef.Parse).ToList(), remote.Select(ModRef.Parse).ToList(), me, them);
            if (lines.Count == 0) return;
            __result = (__result ?? "").TrimEnd() + "\n\nMod Match:\n" + string.Join("\n", lines.Select(l => "• " + l)) +
                       "\nMods that don't affect gameplay (like Wait Tracker) never cause this.";
            ModMatchMod.Log("mismatch explained: " + string.Join(" | ", lines));
        }
        catch (Exception e) { ModMatchMod.LogError($"couldn't explain mismatch: {e.Message}"); }
    }
}
