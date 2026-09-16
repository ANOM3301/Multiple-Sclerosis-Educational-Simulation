# Convai Samples

These are **optional** scenes, prefabs, and scripts that demonstrate how to use the SDK. Samples are imported via Package Manager and can be removed without breaking the core SDK.

If you’re integrating Convai, start with:

- `Documentation~/SETUP.md`
- `Documentation~/SOURCE-REFERENCE.md` (feature → script map for the whole package)

## Which sample should I open?

| What you’re trying to do | Start here |
|---|---|
| Get a native “hello world” scene working | `Samples/BasicSample/Scenes/Basic Sample.unity` |
| Validate browser permission/connect flow (WebGL build) | `Samples/BasicSample/Scenes/Basic Sample.unity` |
| Try the cinematic LipSync showcase | `Samples/LipSyncSample/Scenes/LipSync Sample.unity` |
| Hook transcripts into gameplay triggers/actions | `Behaviors/README.md`, `Documentation~/ACTIONS.md`, `Documentation~/ACTIONS-INTEGRATION-TUTORIAL.md` |
| Compare the engineer, newcomer, and designer event paths | `Documentation~/WORKING-WITH-EVENTS.md` and `Scripts/Events/` |

## Folder overview

- `SamplesShared/` — shared sample-owned code and future shared sample assets
- `SamplesShared/Art/Animations/Dialogue/` - reusable sample dialogue animation clips, masks, placeholders, and animator controller
- `Samples/BasicSample/` — minimal single-character demo and its Basic-only scene assets
- `Samples/LipSyncSample/` — LipSync/URP showcase scene and its sample-local assets
- `Behaviors/` — shared sample behavior and action-executor implementations; use `Documentation~/ACTIONS.md` for current action runtime semantics
- `Scripts/Events/` — event-surface examples (`TypedEventLoggerSample`, `TranscriptListenerSample`, relay-driven samples)
- `Scripts/Runtime/` — runtime-instantiation examples (`RuntimeCharacterSpawner`)
- `Scripts/UI/Settings/` — sample settings-panel bootstrap (`SettingsHandler`)
- `Scripts/UI/Notifications/` — sample notification service (`NotificationService`)
- `Scripts/UI/DynamicContext/` — Sample Debug Hub dynamic-context drawer (`DynamicContextDebugPanel`)
- `Prefabs/UI/Debug/` — drag-in runtime panels for emotion and dynamic-context visualization
- `Scripts/UI/Transcript/` — sample transcript metadata and subtitle UI (`SubtitleTranscriptUI`)
- `Scripts/UI/Utilities/` — sample interaction and transcript-filter helpers

For WebGL validation, build `Samples/BasicSample/Scenes/Basic Sample.unity` to WebGL and follow the browser gesture/HTTPS requirements in `Documentation~/PLATFORMS.md`.

> **URP note:** The LipSync sample is sample-owned and depends on the Unity Universal Render Pipeline packages. Those dependencies are still kept in the main package manifest for safe sample import/install behavior, even though the real ownership is sample-side.

> **Note:** Reusable UI prefabs (settings panel, transcript UI, notifications) ship with the package under `Prefabs/` (from the package root, same folder that contains `SDK/` and `Samples/`).

Event-system reference scripts live under `SamplesShared/Scripts/Events/`:

- engineer path: typed `ConvaiManager.Events`
- newcomer path: transcript UI/listener path
- designer path: relay-driven UnityEvent examples

Shared UI reference scripts live under `SamplesShared/Scripts/UI/`:

- `Settings/SettingsHandler.cs` wires the package `SettingsPanel` prefab and fallback runtime settings services
- `Notifications/NotificationService.cs` shows a sample notification-service implementation
- `DynamicContext/DynamicContextDebugPanel.cs` powers the Sample Debug Hub dynamic-context drawer
- `Prefabs/UI/Debug/Sample Debug Hub.prefab` is the recommended sample debug launcher (emotion, dynamic context, vision drawers)
- `Prefabs/UI/Debug/Emotion State Debug Panel.prefab` shows backend emotion events beside resolved face output
- `Prefabs/UI/Debug/Dynamic Context Debug Panel.prefab` sends and inspects dynamic-context state, events, attention, and backend ACKs
- `Transcript/Subtitle/SubtitleTranscriptUI.cs` demonstrates `ConvaiManager.Transcripts.SubscribeCaptions`

## For developers

Treat these as **reference implementations**. Copy what you need into your project, then customize.

Basic sample uses the shared `Convai.Sample.*` assemblies; LipSync sample is in `Convai.Samples.LipSyncSample`. The sample assemblies:

- references core SDK assemblies
- is **not** referenced by core SDK code (one-way dependency)
- can be removed without affecting the SDK runtime

Sample ownership is intentionally explicit:

- `Samples/BasicSample/` should only own Basic-sample-specific scenes/assets
- `Samples/LipSyncSample/` should only own LipSync-sample-specific scenes/assets
- anything reused across both samples should live in `SamplesShared/` or remain a deliberate core package asset
- Basic and LipSync should not reference each other directly
