using Godot;

namespace ModMatch;

/// <summary>The Mod Match card on the multiplayer menu (Host / Join / Load): your gameplay mods, copy, compare.</summary>
public static class Panel
{
    private static readonly Color Gold = new(0.85f, 0.70f, 0.37f);
    private static readonly Color Muted = new(0.75f, 0.73f, 0.68f);
    private static readonly Color Good = new(0.44f, 0.81f, 0.54f);
    private static readonly Color Bad = new(0.89f, 0.45f, 0.37f);
    private const string NodeName = "ModMatch_Panel";

    public static void ReadyPostfix(object __instance)
    {
        try
        {
            if (__instance is not Control menu) return;
            menu.GetNodeOrNull(NodeName)?.QueueFree();
            menu.AddChild(Build());
        }
        catch (Exception e) { ModMatchMod.LogError($"panel failed: {e.Message}"); }
    }

    private static Control Build()
    {
        var panel = new PanelContainer { Name = NodeName, MouseFilter = Control.MouseFilterEnum.Stop };
        var box = new StyleBoxFlat { BgColor = new Color(0.07f, 0.08f, 0.10f, 0.92f), BorderColor = new Color(0.3f, 0.32f, 0.38f), ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 12, ContentMarginBottom = 14 };
        box.SetBorderWidthAll(1); box.SetCornerRadiusAll(10);
        panel.AddThemeStyleboxOverride("panel", box);
        panel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        panel.GrowHorizontal = Control.GrowDirection.Begin;
        panel.GrowVertical = Control.GrowDirection.Begin;
        panel.OffsetRight = -24; panel.OffsetBottom = -24;
        panel.CustomMinimumSize = new Vector2(440, 0);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);
        panel.AddChild(col);

        col.AddChild(Text("Mod Match", 22, Gold));
        var gameplay = ModList.Gameplay();
        var other = ModList.Other();
        col.AddChild(Text($"Game {ModList.GameVersion}. Everyone in a co-op lobby needs these, same versions:", 15, Muted, wrap: true));
        col.AddChild(Text(gameplay.Count == 0 ? "  none: you can join unmodded players" : string.Join("\n", gameplay.Select(m => $"  • {m.Name} {m.Version}" + (m.Workshop ? "" : "  (local copy)"))), 16, Colors.White, wrap: true));
        if (other.Count > 0)
            col.AddChild(Text("Only you need: " + string.Join(", ", other.Select(m => m.Name)), 15, Muted, wrap: true));

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        var result = Text("", 15, Colors.White, wrap: true);
        buttons.AddChild(Btn("Copy my list", () =>
        {
            DisplayServer.ClipboardSet(ModList.Share());
            Set(result, "Copied. Paste it to your friends; they press Compare after copying it.", Good);
        }));
        buttons.AddChild(Btn("Compare with copied list", () => Compare(result)));
        col.AddChild(buttons);
        col.AddChild(result);
        return panel;
    }

    private static void Compare(Label result)
    {
        var parsed = ModList.ParseShare(DisplayServer.ClipboardGet() ?? "");
        if (parsed == null)
        {
            Set(result, "No Mod Match list on your clipboard. Ask your friend to press \"Copy my list\" and send it to you, then copy their message and press Compare.", Muted);
            return;
        }
        var (game, theirs) = parsed.Value;
        var lines = new List<string>();
        if (game != "?" && game != ModList.GameVersion)
            lines.Add($"Different game versions: you {ModList.GameVersion}, them {game}. Switch to the same branch (live or beta) in Steam first.");
        lines.AddRange(ModList.Diff(ModList.Gameplay(), theirs, "you", "they"));
        if (lines.Count == 0) Set(result, "Match: same game version and same gameplay mods. You can play together.", Good);
        else Set(result, string.Join("\n", lines.Select(l => "• " + l)), Bad);
    }

    private static void Set(Label l, string text, Color c) { l.Text = text; l.AddThemeColorOverride("font_color", c); }

    private static Label Text(string s, int size, Color c, bool wrap = false)
    {
        var l = new Label { Text = s, MouseFilter = Control.MouseFilterEnum.Ignore };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", c);
        if (wrap) { l.AutowrapMode = TextServer.AutowrapMode.WordSmart; l.CustomMinimumSize = new Vector2(408, 0); }
        return l;
    }

    private static Button Btn(string s, Action onPress)
    {
        var b = new Button { Text = s, FocusMode = Control.FocusModeEnum.None };
        b.AddThemeFontSizeOverride("font_size", 15);
        b.Pressed += onPress;
        return b;
    }
}
