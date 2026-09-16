# Convai SDK (Internal Source Guide)

If you are integrating the SDK into a Unity project, start with:

- `Documentation~/SETUP.md`
- `Documentation~/API-ENTRYPOINTS.md`
- `Documentation~/SOURCE-REFERENCE.md` (types ↔ `SDK/` file paths)

This `SDK/` folder is the implementation of the Convai Unity SDK. Use it when you need to debug runtime behavior, trace ownership between layers, or change subsystem internals.

## Core source-tree layout

- `Domain/` — engine-free models, domain events, logging, and shared runtime value types
- `Runtime/` — Unity-facing shells, facades, configuration assets, composition host, and room runtime contracts
- `Infrastructure/Networking/` — Native and WebGL LiveKit transport implementations
- `Modules/` — optional Vision, Lip Sync, and Narrative features
- `Editor/` — inspectors, project settings UI, tooling, and authoring workflows
- `Samples/` and `SamplesShared/` — reference scenes and shared sample assets

## Primary runtime entrypoints

- `Runtime/Components/ConvaiManager.cs` — main Unity entrypoint and lifecycle shell
- `Runtime/Core/Composition/ConvaiRuntimeHost.cs` — explicit composition root for the active runtime
- `Runtime/Adapters/Networking/ConvaiRoomManager.cs` — room/session adapter owned by `ConvaiManager`
- `Runtime/Components/ConvaiCharacter.cs` — per-character scene component
- `Runtime/Components/ConvaiPlayer.cs` — local player identity and text-input surface
- `Runtime/Components/ConvaiAudioOutput.cs` — recommended audio output companion

## Start points by task

- Scene setup and room lifecycle: `Runtime/Components/ConvaiManager.cs`
- Character identity and conversation flow: `Runtime/Components/ConvaiCharacter.cs`
- Room transport, mic, and ownership internals: `Runtime/Adapters/Networking/ConvaiRoomManager.cs`
- Transport deep dive: `Infrastructure/Networking/README.md`
- Vision publishing: `Modules/Vision/README.md`
- Lip sync pipeline: `Modules/LipSync/README.md`
- Narrative integration: `Modules/Narrative/README.md`

## Configuration pointers

- Project-wide settings live in `Edit > Project Settings > Convai SDK`
- `ConvaiRoomManager` uses `Room Setup Source` to switch between scene defaults and a Room Manager Profile asset
- `ConvaiCharacter` uses `Character Setup Source` to switch between inline values and a Character Profile asset

## Logging

- Prefer `ConvaiLogger` or injected `ILogger` for new SDK logging.
- The project settings logging controls feed the built-in Unity console sink.
- `Include Stack Traces` and `Colored Output` apply to that sink.
- Raw `Debug.*` calls still exist in a few legacy runtime/editor paths, so logging is not fully centralized yet.

## Scene transition lifecycle defaults

- `ConvaiManager` persists by default and now auto-handles scene transitions by refreshing ownership and re-injecting owned agents when `sceneLoaded` fires.
- When ownership changes while already connected, `ConvaiManager` triggers a guarded disconnect/connect cycle through `ConvaiRoomManager` to apply the new scene ownership safely.
- Global transport helpers (for example LiveKit singleton contexts) are intentionally preserved; avoid deleting SDK `DontDestroyOnLoad` objects from app-level scene navigation code.
