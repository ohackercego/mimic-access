# Mimic Party Access

**Screen-reader accessibility mod for [Mimic Party](https://store.steampowered.com/app/5053820/) (Steam).**

> **Português:** [Leia este README em português](README.pt.md).

Mimic Party Access lets blind and low-vision players navigate the game's menus with the keyboard and hear what is happening through their screen reader (NVDA, JAWS, Narrator, and others). It runs on [MelonLoader](https://github.com/LavaGang/MelonLoader) and outputs speech through [Tolk](https://github.com/dkager/tolk). It never modifies `GameAssembly.dll` or `global-metadata.dat`.

## Features

- **Full keyboard menu navigation**: Up/Down move between controls, Left/Right change sliders and dropdowns, Enter activates, Esc closes overlays.
- **Screen announcements**: every menu (main menu, settings, room browser, lobby, matchmaking, game, results, shop, season pass, objectives, friends, and more) is announced when opened, and each control is announced with its position (`3 of 9: Create room`).
- **Match announcements**: round phases (announce, reveal, recording, playback, scoring, round results, wheel), round counter, per-player scores, round result summary, lobby roster, and final ranking.
- **Recording prompts**: "Your turn to speak!" when your recording turn starts, plus countdown announcements so you know when you can talk and when the take will be cut.
- **Push-to-talk settings**: the two push-to-talk options in Settings are fully accessible — current state and current key are read out, Enter toggles or starts key capture, and the new key is announced when set.
- **Language follows the game**: announcements are in Portuguese when the game is set to Portuguese, English otherwise.
- **Screen rows read properly**: room rows, friend rows, lobby players, shop items, and objective rows are announced with their real labels instead of internal field names.
- **Non-destructive**: only Harmony postfixes are used, and every hook is validated against the game's IL2CPP contract (type, method name, parameter types, enum values) before it is installed. Unmatched hooks are skipped and logged. Game binaries are never written to; their SHA-256 fingerprints are logged for identification.

## Requirements

- Mimic Party (Steam, Windows x64).
- [MelonLoader](https://github.com/LavaGang/MelonLoader/releases) installed for Mimic Party (tested with 0.7.3).
- A screen reader supported by Tolk (NVDA, JAWS, Narrator via SAPI, and others).
- .NET 6 runtime (already required by MelonLoader).

## Installation

1. **Install MelonLoader** (once): download the MelonLoader Installer from the [MelonLoader releases page](https://github.com/LavaGang/MelonLoader/releases), run it, and select `Mimic Party.exe` inside your Steam library folder (usually `C:\Program Files (x86)\Steam\steamapps\common\Mimic Party`).
2. **Download the mod**: get `MimicPartyAccess-v*.zip` from this repository's Releases page.
3. **Extract the zip** into the Mimic Party folder. It creates:
   - `Mods\MimicPartyAccess.dll` — the mod
   - `Tolk.dll` and `nvdaControllerClient64.dll` in the game folder — the speech bridge (the NVDA DLL is only needed for NVDA; other readers work without it)
4. **Start the game** from Steam. On launch the log will show `Tolk screen-reader output is available.` when a screen reader is detected.

To uninstall, delete `Mods\MimicPartyAccess.dll` (and optionally `Tolk.dll` / `nvdaControllerClient64.dll`).

## Controls

| Key | Action |
| --- | --- |
| Up / Down | Move between controls |
| Left / Right | Change slider/dropdown value, or move between buttons |
| Enter | Activate button, toggle, list, or text field |
| Esc | Close the open overlay (Settings, popups) or text field |
| Ctrl+Alt+F1 | Read navigation help |
| Ctrl+Alt+R | Repeat the last announcement |

The mod only intercepts these keys while an accessible screen is active; typing in text fields works normally.

## Building from source

Requirements: [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) and a MelonLoader installation (its `MelonLoader.dll` and `0Harmony.dll` are compile-time references).

```powershell
dotnet build MimicPartyAccess/MimicPartyAccess.MelonLoader.csproj -c Release `
  -p:MelonLoaderPath="C:\Program Files (x86)\Steam\steamapps\common\Mimic Party\MelonLoader\net6"
```

Output: `MimicPartyAccess/bin/MelonLoader/Release/net6.0/MimicPartyAccess.dll`.

Run the regression checks (also requires `-p:MelonLoaderPath=...`):

```powershell
dotnet build MimicPartyAccess/Tools/UiRegression/UiRegression.csproj -c Release `
  -p:MelonLoaderPath="C:\Program Files (x86)\Steam\steamapps\common\Mimic Party\MelonLoader\net6"
dotnet MimicPartyAccess/Tools/UiRegression/bin/Release/net7.0/UiRegression.dll
```

To test a local build, close the game and copy the DLL to `Mods\MimicPartyAccess.dll`.

## Troubleshooting

- Log file: `Mimic Party\MelonLoader\Latest.log`. Search for `Mimic Party Access` to see the mod's messages.
- On startup the mod logs the SHA-256 of `GameAssembly.dll` and `global-metadata.dat`. If a game update changes these, hooks that no longer match are skipped (the game still runs, but some announcements stop) — open an issue with the log.
- No speech: confirm `Tolk screen-reader output is available.` appears in the log; for NVDA make sure `nvdaControllerClient64.dll` sits next to `Mimic Party.exe`.

## How it works

The mod reflects over MelonLoader's generated IL2CPP proxy assemblies to find the game's UI types (`Mimick.UI.*`). Hooks are Harmony postfixes on UI callbacks such as `GameScreen.OnPhaseChanged(RoundPhase)` or `GameScreen.OnScored(...)`, installed only after the runtime contract matches what the mod expects. Menu discovery walks the live UI hierarchy of the active screen and pairs each control with its visible TextMeshPro label. See [docs/game-api.md](MimicPartyAccess/docs/game-api.md) for the researched game API.

## Credits and licenses

- Source code of this mod: see the repository license.
- [Tolk](https://github.com/dkager/tolk) by Davy Kager — LGPL-3.0 (bundled `Tolk.dll`).
- [NVDA](https://www.nvaccess.org/) controller client (bundled `nvdaControllerClient64.dll`).
- [MelonLoader](https://github.com/LavaGang/MelonLoader) and Harmony.

This is an unofficial fan-made accessibility project and is not affiliated with or endorsed by FoliesAPP.
