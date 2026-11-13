# Mod Match

A small [Slay the Spire 2](https://store.steampowered.com/app/2868840/) mod for co-op players. It works on both the live branch (v0.111) and the public beta (v0.113).

Co-op only works when everyone runs the same **gameplay** mods at the same versions, on the same game version. Otherwise joining fails with "Mod mismatch!". Mod Match helps you sort that out before you try to join.

## What it does

- **Your list on the multiplayer menu (Host / Join / Load).** Shows the gameplay mods your friends need to match, with versions. It also lists the mods only you need, such as advisors, trackers and cosmetic mods.
- **Copy my list.** Copies one line like `ModMatch v1 | game v0.113.0 | RelicShare-0.2.0, GoldTransfer-v0.1.76` to send to friends.
- **Compare with copied list.** Copy a friend's line and press Compare. It reports different game versions (live vs beta), mods only one of you has, and version differences.
- **Clearer "Mod mismatch!" popup.** Adds a breakdown under the game's message that pairs up version differences ("Different versions of RelicShare: you 0.2.0, the host 0.1.0") and says who has what.

Mod Match doesn't change gameplay and is marked that way. Friends don't need it to play with you, and it never causes a mismatch itself.

## Install

Subscribe on the Steam Workshop, or copy `ModMatch.dll` and `ModMatch.json` into `Slay the Spire 2/mods/ModMatch/`.

## Build

```
dotnet build -c Release -p:GameLibs="<Slay the Spire 2>/data_sts2_windows_x86_64"
```

## License

MIT, see [LICENSE](LICENSE).
