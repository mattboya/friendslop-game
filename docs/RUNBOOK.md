# Prototype runbook

## Toolchain

Install/activate Unity `6000.3.24f1` plus macOS and Windows Build Support. Set the editor explicitly:

```sh
export UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity"
```

## Generate and validate

```sh
node scripts/unity.mjs generate
node scripts/unity.mjs validate
node scripts/unity.mjs test-edit
node scripts/unity.mjs test-play
```

Inspect `artifacts/unity-*.log` and the test result XML. Open `Assets/Festival/Generated/Bootstrap.unity` for visual review. Batch/headless runs do not verify shaders, audible timing or microphones.

## Build

```sh
node scripts/unity.mjs build-mac-development
node scripts/unity.mjs build-windows-development
node scripts/unity.mjs build-windows-release
```

Expected outputs:

- `Builds/macOS/Development/FestivalCoop.app`
- `Builds/Windows/Development/FestivalCoop.exe`
- `Builds/Windows/Release/FestivalCoop.exe`

## Launch

Host plus one local client:

```sh
node scripts/launch-local-clients.mjs --binary "Builds/macOS/Development/FestivalCoop.app/Contents/MacOS/Festival Co-op Prototype" --players 2 --port 7777
```

Host plus seven local clients:

```sh
node scripts/launch-local-clients.mjs --binary "Builds/macOS/Development/FestivalCoop.app/Contents/MacOS/Festival Co-op Prototype" --players 8 --port 7777
```

Windows PowerShell equivalents can call the executable directly with `--host` or `--join`, `--port`, `--profile` and `--name`. Each process must use a unique profile. Local processes establish transport/state behavior only; they do not prove eight physical PCs or eight-player performance.

For a second machine, host normally and join using the host's LAN IPv4 address and port. Permit the executable/UDP port through the host firewall. Direct-address transport does not provide NAT traversal.

## Required first validation sequence

1. Import and fix any compile/API error before changing gameplay.
2. Run EditMode and PlayMode tests.
3. Build macOS development; visually inspect menu, map, navigation, rhythm, effects and UI at 1080p.
4. Run host plus one client through shopping, rescue, extraction, results and reset.
5. Run the bust, broke-pair, spirit/revival, preview and disconnect paths.
6. Run host plus seven local clients and record frame/network measurements.
7. Build Windows development and repeat on two Windows machines.
8. Build release; verify F8 and development logging/overlays are absent.

Do not mark voice, Steam internet play, host migration, graphics/audio quality or human fun as passed from these automated commands.
