# Toolchain preflight

15 September 2026, macOS arm64. No existing code, Git metadata, Unity editor or Unity Hub found at standard paths, Spotlight or PATH. No engine license activation has been attempted.

Native target: Unity 6000.3.24f1 LTS (official release page verified), Windows x86_64 player. This is a selected target, not an installed or tested editor. Windows Mono build support allows initial cross-platform export; IL2CPP/Steam shipping validation remains later work. macOS development export supports local visual inspection once Unity is installed.

Portable .NET SDK 8.0.425 downloaded from Microsoft's official dotnet installer into `/tmp/friendslop-dotnet` for running the SAME engine-independent C# source. This neither installs nor emulates Unity. `FESTIVAL_DOTNET` can override this path. Runtime uses C# 9-compatible code and Unity's supported API subset; .NET tests cannot prove Unity API compatibility or serialization.

Exact Unity package versions are recorded in Packages/manifest.json and docs/package-resolution.json after registry inspection. Editor resolution is a separate gate. No fabricated Unity-generated package lock or successful import is claimed.

References:
- https://unity.com/releases/editor/whats-new/6000.3.24f1
- https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html
- https://packages.unity.com/com.unity.netcode.gameobjects
- https://packages.unity.com/com.unity.inputsystem
