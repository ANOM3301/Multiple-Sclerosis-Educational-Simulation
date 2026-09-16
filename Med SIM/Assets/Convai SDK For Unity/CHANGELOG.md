# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [Released]

## [4.4.1] - 2026-07-30
### Fixes
- Push-to-talk release now keeps the microphone and backend STT open while waiting for ASR-final. If the first configurable `PushToTalkPolicy.ReleaseTailMs` window expires, the SDK signals the authoritative stop and allows one more bounded window for provider finalization before closing capture.
- Fixed WebGL builds crashing from a stale `NativeLib` reference in `livekit-bridge.jslib`, and restored first-turn LipSync by correcting audio-timing registration order, warming the WebGL analyser, and recovering missed `PlaybackStarted` callbacks.

## [Released]

## [4.4.0] - 2026-07-21
### Feature Additions
- Added active-session Actions updates through `ConvaiActionConfigPatch`, with exact omitted-versus-empty list semantics, object/character/attention replacement, generated update IDs, and typed backend action-update acknowledgement metadata.
- Added a native **AI Coding** section to the Convai Editor window for Assistant/package/tool health, Unity MCP settings routing, and explicit managed instructions for supported coding clients. **Convai > AI Coding Setup** now opens this integrated section.
- Rebuilt the **Convai SDK** Project Settings page (Edit > Project Settings > Convai SDK) as UI Toolkit section views that are also mounted by a new **Settings** section in the Convai Editor window (`Convai > Settings`) — one implementation, two hosts, always in sync. Sections: Setup Health, Credentials, Runtime Defaults, Diagnostics, Advanced, and About.
- Credentials now include a **Validate & Save** button with a cached validation badge (valid/invalid/not validated, surviving domain reloads via `SessionState`) and an **Environment** preset (Production / Beta / Custom). The preset drives the REST base URL through the new `ConvaiRestOptionsFactory`; Custom unlocks raw core-server and REST base URL fields.
- Runtime Defaults gained a **microphone picker** listing actual device names (with System Default and refresh), plus new project-wide defaults: `DefaultPlayerDisplayName`, `CharacterAudioVolume`, and `AudioFeedbackEnabled`, all seeding the runtime settings service and runtime preferences.
- Diagnostics gained one-click logging presets (Verbose / Default / Errors Only) above the per-category override list; sections expose Reset-to-defaults buttons.
- Advanced gained a **feature-flag manager** that toggles the `CONVAI_DEBUG_LOGGING`, `CONVAI_ENABLE_SERVER_ANIMATION`, `CONVAI_ENABLE_UPDATES_SECTION`, and `CONVAI_ANIMATION_RIGGING` scripting defines for the active build target (with recompile confirmation and cross-target drift detection).
- Added a **Setup Health** section with project checks (settings asset present, API key set/validated, iOS microphone usage description, Android permission guidance, define drift, platform caveats) and one-click fix buttons, and an **About** section with SDK version, canonical links, and a Copy Support Info button (never includes the API key).
- Expanded the optional Unity MCP integration to 20 tools (contract version 4) with a privacy-preserving 256-entry runtime event trace and Unity-side Narrative Design configuration/diagnosis. Added **Convai > AI Coding Setup** for compatibility/tool checks, official Unity MCP settings routing, and explicit atomic managed-instruction installation for Codex, Claude Code, Cursor, Gemini, and VS Code Copilot. Expanded the packaged skill with progressive quickstart, action, runtime-debugging, dynamic-context, and narrative references. Added dev-only MCP recording-scene builder and four-prompt runbook under `Assets/ConvaiDev/MCPDemo/`.
- Added seven optional Unity MCP and Assistant tools for action authoring/simulation, lip-sync configuration/diagnosis, and transcript scene setup/diagnosis.
- Added a canonical room-scoped transcript timeline backed by `RoomTranscriptEngine` and exposed through `ConvaiManager.Transcripts`. The new immutable `TranscriptTimeline`, `TranscriptTurn`, `TranscriptSegment`, `TranscriptSpeaker`, and `TranscriptChange` models provide stable turn IDs, revisions, speaker/source/state metadata, active and committed history, and explicit added/updated/committed/interrupted/corrected/removed changes. Consumers can read `CurrentTimeline`, query with `GetTurns`/`GetTurn`/`GetLatestTurn`, react through `Changed` and the turn-specific events, subscribe with replay and speaker filters through `Subscribe`/`SubscribeCommitted`, clear history, and export committed turns as plain text, Markdown, or JSON.
- Added a separate speech-aligned caption projection on `ConvaiManager.Transcripts`: `CurrentCaptions`, `CaptionsChanged`, and `SubscribeCaptions` expose streaming/final captions without treating ephemeral TTS text as durable chat history. `IsPresentationEnabled` and `PresentationEnabledChanged` let shipped presentation components hide and replay without stopping canonical room recording; `ChatTranscriptUI` consumes history turns, while the sample `SubtitleTranscriptUI` consumes captions.
- Added an optional Unity AI Assistant 2.13 integration foundation with ten official Unity MCP and matching Assistant tools: guidance, project status, scene inspection, setup validation, manager bootstrap, room/player/character configuration, end-to-end conversation-scene orchestration, and ranked Edit/Play Mode diagnosis. Mutations are previewable, idempotent, Undo-enabled, active-scene scoped, never save scenes, and never read or write API-key values. The package-discovered `convai-unity-sdk` skill uses these tools proactively with recommended hands-free defaults before asking irreducible questions.
- Added an actions speech gate: `ConvaiActionDefinition.WaitForBotSpeech` (mirrored on `ConvaiActionCommand`) makes the first action of a fresh batch wait for character speech before executing, with an optional `DelayAfterBotSpeechSeconds` pause after the gate releases and a dispatcher-level `Speech Gate Timeout` (default 2 s) so a silent turn never stalls the batch.
- Added dynamic context vision support: rooms can opt into backend frame sampling with a new **Dynamic Vision Context** section on `ConvaiRoomManager` and `ConvaiRoomManagerProfile` (`ConvaiVisionContextMode`, `ConvaiVisionInputSettings`, `ConvaiVisionRespondModeSettings`). `respond_modes` is always sent on connect (its `context_update`/`trigger`/`scene_metadata` lanes govern non-vision features too); `vision_input_config` is sent only while dynamic vision resolves enabled. See `Documentation~/DYNAMIC-VISION-CONTEXT.md`.
- Added runtime vision controls on `IConvaiRoomConnectionService`/`ConvaiRoomManager`: `RequestVisionStatus()` queries the backend frame buffer, `TriggerVision(ConvaiVisionTriggerRequest)` attaches buffered frames to a turn with an optional prompt, respond mode, relative frame window, or absolute PTS pinning, and `UpdateRespondMode(lane, mode)` changes an input lane's respond mode mid-session.
- Added `VisionContextStatusReceived`, `VisionContextTriggerReceived`, and `RespondModeUpdateResultReceived` domain events carrying the backend acknowledgements (outcomes, downgrades, attached frame PTS, token estimates).
- Added a vision target rig and a Sample Debug Hub with a vision panel to the LipSync sample for exercising status/trigger flows end to end.

### Improvements
- Removed bundled LiveKit `ffi-*` architecture payloads from the released package. The retained editor downloader now installs only the running Unity Editor architecture plus architectures enabled for the active build target into the writable project `Assets/Convai/...` path; managed LiveKit source, Protobuf, and generated plugin import settings remain packaged.
- Hardened native NeuroSync playback alignment by anchoring each response to the exact PCM source
  frame where speech starts, interpolating the rendered audio position within Unity's DSP callback,
  compensating bounded LipSync smoothing delay, blending visual discontinuities after audio skips,
  recovering missing animation against the audible deadline, and retaining five minutes of indexed
  response frames.
- Expanded **Convai > Developer > Action Debug Window** with backend-confirmed runtime state, pending update IDs and age, exact action-update ACK metadata, a previewable runtime patch composer with omitted-versus-empty controls, and privacy-safe action filter counts/reason codes.
- Runtime action state is now backend-confirmed: sends remain pending, successful ACKs commit in send order, and errors, malformed/mismatched metadata, disconnects, or 30-second timeouts discard local mutations without retry. Realtime `requires_reconnect` status is surfaced without automatic reconnect.
- Unified direct and nested `action-response` handling behind one fail-closed filter. Unknown/unexecutable actions, unresolved required targets, and unresolved reference parameters are removed before public events and dispatch; logs expose counts and stable reason codes without raw action payloads.
- The API key is now stored obfuscated (XOR+Base64) in `ConvaiSettings.asset` instead of plaintext, with automatic one-time migration of existing plaintext keys on editor load. This is a deterrent against casual asset/VCS grepping, not encryption — any key shipped in a client build remains extractable; use a runtime `ICredentialProvider` with server-issued tokens where that matters.
- All REST client construction now flows through `ConvaiRestOptionsFactory` so the project-wide environment preset applies to narrative fetching, room auth, editor tools, and account/usage requests uniformly.
- Runtime settings now apply `PlayerDisplayName` to `ConvaiPlayer`; the optional settings-panel input remains null-safe until assigned in the shipped prefab.
- AI coding setup now validates the exact 20-tool MCP contract instead of accepting any matching count, rejects stale or unexpected tool names, and reconnects a running Unity MCP bridge after registry repair so external clients refresh their catalog.
- **Convai > AI Coding Setup** now shows status-specific **Fix** buttons. Missing or unsupported Unity AI Assistant installs the supported `2.13.0-pre.2` package through Unity Package Manager, then survives domain reload, refreshes assets, recompiles, refreshes the Unity MCP registry, and verifies all 20 Convai tools. Tool/skill refresh repairs are also available without reinstalling a compatible package; repair never changes Play Mode automatically and reports failures in-window.
- Restructured the parameterized actions runtime for maintainability: split the multi-type `ConvaiActionResolution.cs` into `ConvaiActionDefinition.cs`, `ConvaiActionInvocation.cs`, `ConvaiActionExecutionResult.cs`, and `IConvaiActionExecutor.cs` (all types keep their names and namespace — no code changes needed), added XML documentation across the public actions surface, and deduplicated target-kind/reference resolution between the dispatcher and `ConvaiActionInvocation.GetReference` into one shared resolver.
- Actions runtime logging now routes through `ConvaiLogger` (`LogCategory.Character`) instead of raw `Debug.*` in `ConvaiActionResponseParser`, `ConvaiActionDispatcher`, and `ConvaiActionConfigSource`.
- Added `ConvaiActionCommand.Enriched`: the response parser marks commands it has enriched and the dispatcher re-enriches only unmarked commands, replacing the previous zero-parameters heuristic that re-parsed legitimate parameter-less commands on every dispatch.
- Reduced steady-state actions overhead: `ConvaiActionExecutor<TParameters>` caches its reflection member map per parameter type, the response parser's regexes are compiled statics, and `ConvaiActionDefinition.ToActionConfigString()` renders are memoized per definition (hash-validated, so live edits still re-render).
- Step failure messages are now composed structurally: executor results carry the raw cause and the dispatcher appends the batch-abort/continue consequence exactly once when building `ConvaiActionStepReport.FailureMessage` (previously the dispatcher string-sniffed its own suffix to avoid double-appending). Final report text is unchanged.
- Removed the unused `ConvaiTargetActionParameters`, `ConvaiTextActionParameters`, and `ConvaiReferenceNumberActionParameters` DTOs (never released, zero references).
- The **Action Debug Window** is now sample-agnostic: project-specific templates and injection shortcuts live behind an editor extension seam (`IConvaiActionDebugPresetProvider` + `ConvaiActionDebugPresetRegistry`); the window includes manual injection fields and one-click injection for every authored action, and writes templates through an internal setter instead of private-field reflection.
- Vision input settings clamp every field into the backend's validated ranges and trim sampling windows into the frames-per-turn budget, so an inspector-authored config can never be rejected at connect.
- Published Convai vision frames are no longer vertically flipped a second time during LiveKit texture readback; orientation is owned by the Convai frame sources, so frames arrive upright at the backend vision model. Note for advanced users publishing a raw `RenderTexture` directly through the LiveKit `TextureVideoSource` (bypassing Convai frame sources): the plugin no longer flips for you — apply your own Y-flip or route through a Convai `IVisionFrameSource`.
- Half-configured sampling windows (interval left at 0) are now dropped instead of being clamped up to a 1 ms horizon that would request maximal backend capture load.
- The dynamic-context batch window is exposed as `ConvaiCharacter.DynamicContextBatchDelaySeconds` so tooling can display the real value instead of hardcoding it.
- RTVI `server-response` event types are now matched case-insensitively.
- Canonical transcript history now keeps recording while runtime transcript presentation is disabled; reenabling the shipped chat/subtitle UIs replays current state instead of losing turns captured while hidden.
- Cached `TranscriptTimeline.Turns` ordering and the facade's snapshot mapping, so repeated `ConvaiManager.Transcripts.CurrentTimeline` reads return the same immutable instance until the engine publishes a changed snapshot instead of rebuilding the complete timeline on every access.
- Tuned the CC4 Extended LipSync map mouth-shape multipliers and fade timing for clearer articulation and smoother settling.
- Refactored LipSync around a pure response-owned indexed session with strict owner precedence,
  bounded future-response buffering, deterministic gap recovery, centralized reset, and direct
  native source-sample sampling. WebGL and legacy transports retain the compatibility clock path.
- Reduced LipSync runtime log noise to one terminal response summary at `Info`, actionable recovery
  warnings, and opt-in owner/sample/gate timing at `Debug`; disabled detailed diagnostics no longer
  format per-packet strings.
- Simplified LipSync runtime/editor composition, moved map authoring and catalog invalidation fully
  into the Editor assembly, made map/profile collections immutable to consumers, and restricted map
  import to the canonical versioned JSON format.

### Changed
- Centralized internal component log tags through the existing `ILogger` seam. Injected consumers now apply one ownership tag, while static `ConvaiLogger` calls derive tags from simple source filenames without exposing directories (partial files normalize to their base filename). Semantic and dynamic bracket tokens remain; legacy static aliases normalize to filename tags.
- `ConvaiDynamicContextRelay` missing-character warnings now route through `ConvaiLogger` under the `Character` category instead of raw Unity `Debug` logging.
- Consolidated internal dynamic-context batch staging into `ConvaiDynamicContextTracker`; public behavior and wire payloads are unchanged.
- Consolidated whitespace-aware transcript text joining and prefix-extension handling into the internal `TranscriptTextMerge` helper; public transcript behavior is unchanged.

### Bug Fixes
- Fixed runtime action patches conflating omitted and empty lists, optimistically mutating `ConvaiCharacter.ActionConfig`, losing local `GameObjectReference` bindings, and retaining stale attention after object replacement.
- Fixed MCP action diagnosis failing to report null UnityEvent placeholders as `ACTION_EVENT_UNWIRED`.
- Locally configured player names from the `ConvaiPlayer` inspector or runtime settings are now authoritative for local transcript and caption display; settings-panel renames apply immediately through the manager's runtime settings service, retroactively correct existing turns, and pre-populate from the effective player name. The shipped sample-panel field now matches its existing `playerNameInputField` prefab binding. Backend speaker names remain available through `SpeakerInfo` and speaker IDs still drive actor identity.
- Fixed LiveKit remote-audio playback throwing `OverflowException` when peak detection encountered the valid PCM16 minimum sample (`-32768`).
- Fixed Dynamic Context debug-panel scrolling throwing inside `TMP_InputField.OnScroll` by giving generated multiline inputs a proper masked text viewport; refreshed the shared debug hub with clearer panel chrome, close controls, hover states, and consistent input/button styling.
- Fixed the chat transcript UI remaining invisible on startup when its Canvas Group begins at zero alpha; active chat prefabs now run their configured fade-in automatically.
- Fixed Unity 6000 EntityId resolution across MCP feature authoring targets, and made default LipSync diagnosis auto-resolve one unambiguous active-scene character.
- Fixed the `convai-unity-sdk` package skill being rejected by Unity Assistant because its required Editor version used an invalid two-component constraint.
- Fixed canonical player turns receiving duplicate processed-final reducer input. Late processed finals still reach player callbacks, correct the existing turn when identifiable, and do not restart a completed speaking session.
- Fixed `ConvaiTranscriptEventRelay` reporting every unmapped or unknown `TranscriptTextSource` as `PlayerAsr`; interim/final ASR sources now map explicitly to `PlayerAsr`, while unknown and future unmapped sources report `TranscriptSegmentSourceKind.Unknown`.
- Fixed MCP transcript diagnosis depending on hardcoded type-name strings and scanning every `MonoBehaviour`; it now detects `ConvaiTranscriptDisplay` and `ChatTranscriptUI` through typed active-scene queries and explicitly notes that sample UIs are not counted.
- Fixed indexed LipSync starting after audible audio had already ended when delayed data/lifecycle
  packets rebound response zero to still-advancing silent PCM. A new response now requires a current
  audible-audio start before its gate can open; sample timing only keeps an already-open response
  locked through silence or underrun. Added response/audio/sample ordering diagnostics.
- Fixed indexed LipSync continuing after native audio ended when the sample clock stalled before
  the buffered animation boundary. Closed responses now fade the current mouth pose after a bounded
  no-progress window.
- Fixed indexed LipSync packets failing on the first response because an uninitialized internal
  owner dereferenced a null response id. Metadata-free/scalar bot speech lifecycle payloads now
  degrade to an empty owner instead of throwing in protocol dispatch.
- Fixed cumulative native LipSync drift and long-response freezes by sampling animation from an absolute LiveKit source-frame clock, treating silent PCM separately from underruns, recovering missing indexed frames after a bounded grace period, and enforcing strict response ownership.
- Hardened long native audio responses with a two-second bounded ring buffer, all-or-nothing PCM writes, oldest-frame overflow recovery, and overflow/skip/underrun diagnostics. LipSync completion remains read-only and cannot stop or mutate audio playback.

### Breaking Changes
- `ConvaiSettings.DefaultMicrophoneIndex` was replaced by `DefaultMicrophoneDeviceId` (string; empty = system default). The old integer index is not value-migrated — re-pick your device in Settings > Runtime Defaults if you used a non-zero index.
- Removed the dead `ConvaiSettings.NativeRuntimeMode` property and the `NativeRuntimeMode` enum (it was forced to `Transport` since the transport unification).
- `ConvaiSettings.ServerUrl` is now derived from the environment preset: the serialized URL is honored only when the environment is `Custom`; Production and Beta always use `https://live.convai.com`.
- The `Convai > Logger Settings` menu and window section were removed; logging configuration lives in `Convai > Settings` (Diagnostics) and Project Settings. The Account section no longer contains API key entry — it moved to Settings > Credentials (the Account section links there).
- Removed the editor types `APIKeySetupLogic`, `ConvaiAccountSectionLogic`, `ConvaiLoggerSettingSection`, and `LoggerSettingsLogic`, plus the `ConvaiAccountSection.APIInputField`/`ShowHideAPIKeyButton`/`UpdateSaveButton` properties. Use `Convai.Editor.Settings.Services.ApiKeyValidationService` and `LogOverrideEditing` instead.
- Removed `ConvaiTranscriptToolMode.FacadeOnly`; it was advertised but had no handler, so requests silently did nothing while reporting success. Migration: the transcript facade needs no configuration; use `EventRelay`, `ChatUI`, or `WorldSpaceChatUI` to set up presentation.
- Removed `TranscriptTurnState.Committing` and `TranscriptSegmentSourceKind.BotTtsCaption`; neither was ever assigned or produced. Migration: use `TranscriptTurnState.Committed` or turn completion, and `TranscriptSegmentSourceKind.Unknown`, respectively.
- Replaced the snapshot-based `ConvaiManager.Transcripts` contract with the public timeline model: `CurrentTimeline` now returns `TranscriptTimeline` instead of `TranscriptTimelineSnapshot`; `Changed` now supplies `TranscriptChangeBatch` instead of `TranscriptUpdateBatch`; and `GetTurns`/`GetTurn`/`GetLatestTurn` now return `TranscriptTurn` values instead of `TranscriptTurnSnapshot`. `Subscribe` and `SubscribeCommitted` callbacks now receive `TranscriptChange` instead of `TranscriptTurn`, and `TranscriptSubscriptionOptions.IncludeInterim`/`IncludeCommitted` were renamed to `IncludeActive`/`IncludeTerminal` to describe listening, streaming, stable, committed, interrupted, and corrected states accurately.
- Removed the legacy conversation-history types `ConversationHistoryService`, `TranscriptEntry`, and `ConversationExportFormat`; canonical history, clearing, and export now live on `ConvaiManager.Transcripts`.
- Removed the legacy transcript presentation layer: `TranscriptUIController`, `ITranscriptUI`, `ITranscriptListener`, `TranscriptViewModel`, `Convai.Runtime.Presentation.Presenters.TranscriptSpeaker`, `ChatPresentationStrategy`, and `ITranscriptPresentationStrategy`. Custom UIs now consume `TranscriptChange` values directly from `ConvaiManager.Transcripts`; the domain model still provides the distinct `Convai.Domain.Models.TranscriptSpeaker` type.
- Removed the legacy transcript filtering/formatting types `ITranscriptFilter`, `DefaultTranscriptFilter`, `ITranscriptFormatter`, `DefaultTranscriptFormatter`, and `TranscriptFilterBase`, plus the sample-only `ProximityCharacterFilter`, `SingleCharacterFilter`, and `IVisionConeProvider` utilities. Use `TranscriptSubscriptionOptions`/`TranscriptCaptionSubscriptionOptions` for speaker, participant, active/terminal, and streaming/final selection, then apply UI-specific formatting or spatial filtering in the subscriber.
- Renamed the `Convai_DiagnoseTranscripts` runtime-turn JSON key from `lifecycle` to `state`; MCP clients that parse `runtime.turns[]` must read `state`.
- Removed `ConvaiActionConfigSource.TryResolveObject(string, out ConvaiActionObjectDefinition)`, an unused lookup that no SDK system called. Migration: enumerate the component's authored objects via the `Objects` inspector list, or — inside an action executor — read the already-resolved target from `ConvaiActionInvocation.ResolvedTarget` / `GetReference(name)`.
- Internalized the actions wire-format helpers `ConvaiActionResponseParser` and `ConvaiActionTemplateRenderer`: enrichment always runs automatically before `OnActionsReceived`/dispatch, and `ConvaiActionDefinition.ToActionConfigString()` remains the public rendering entry point, so there is no supported scenario that requires calling either type directly.
- Reshaped `ConvaiActionParameterReference` (the enriched reference handle on `ConvaiActionParameterValue.ResolvedReference`): `Kind` is now the `ConvaiActionTargetKind` enum instead of a free-form string, the enum moved from `Convai.Runtime.Actions` to `Convai.Shared.Types` (add a using if you referenced it by namespace), and the write-only `GameObjectReference` payload was removed — it was populated but never read by any SDK system. Migration: compare `Kind` against `ConvaiActionTargetKind` values instead of strings, and resolve scene objects through `ConvaiActionInvocation.GetReference(name).GameObjectReference`.
- `IConvaiRoomConnectionService` gained three members: `RequestVisionStatus(string)`, `TriggerVision(ConvaiVisionTriggerRequest)`, and `UpdateRespondMode(ConvaiRespondModeLane, ConvaiRespondMode, string)`. Code that consumes the interface (e.g. via `ConvaiRoomManager`) is unaffected; custom implementations of the interface must add the three methods (return `false` when vision is unsupported).
- Removed `ConvaiContextStateEntry`, an inert inspector DTO that was never read by any SDK system.
- Removed the legacy `SampleDynamicContextUI` component and `Prefabs/SampleDynamicContextUI.prefab`. Use the Dynamic Context panel in `SamplesShared/Prefabs/UI/Debug/Sample Debug Hub.prefab` instead; scenes that instantiated the removed prefab should replace missing-prefab entries with `SamplesShared/Prefabs/UI/Debug/Dynamic Context Debug Panel.prefab`.
- Unified the respond-mode vocabulary: `ConvaiContextReactionMode` is removed and every dynamic-context API now uses `ConvaiRespondMode` (moved from `Convai.Runtime.Vision.Context` to `Convai.Runtime`). Migration: `SyncOnly` → `Silent`, `ReactImmediately` → `MustRespond`, `Auto` unchanged. The wire format is untouched (`run_llm` still sends `auto`/`true`/`false`). If a scene or prefab saved during the beta serialized a reaction override (`ConvaiDynamicContextRelay`, `ConvaiTrackedContextProperty`), re-check the value in the Inspector — the enum was renumbered, so every saved index changes meaning: old `Auto` (0) deserializes as `Silent`, old `ReactImmediately` (1) as `Auto`, and old `SyncOnly` (2) as `MustRespond`. Shipped SDK assets carry no such serialized values; this only affects scenes saved against unreleased beta builds.
- Removed the public `IBlendshapeSink` extension seam and internalized
  `SkinnedMeshBlendshapeSink`, `LipSyncDriftMonitor`, `LipSyncDriftSample`, and `LipSyncDriftEvent`.
  The supported diagnostics surface is the **Convai → LipSync Drift Monitor** editor window and CSV
  export.
- Removed runtime map-authoring methods `ConvaiLipSyncMapAsset.ClearMappings`,
  `InitializeWithDefaults`, and `AutoDetectFromMeshes`. Equivalent undo-aware actions remain in the
  LipSync inspectors. Runtime import now accepts canonical version-1 JSON only.

### Migration Notes
- API keys migrate automatically: on first editor load after upgrading, a plaintext `_apiKey` in `Assets/Resources/ConvaiSettings.asset` is re-written as `_apiKeyObfuscated` and the plaintext field is cleared. Commit the updated asset. Code reading `ConvaiSettings.ApiKey` is unaffected. If you previously edited `_serverUrl` for staging, set Environment to `Custom` in Settings > Credentials to keep using it.
- Replace direct `new ConvaiRestClientOptions(apiKey)` construction with `ConvaiRestOptionsFactory.Create(apiKey)` so the environment preset applies.
- Replace `ConversationHistoryService.Entries`/`GetEntriesByPlayerOrCharacterId(...)` with `ConvaiManager.Transcripts.CurrentTimeline.Turns` or `GetTurns(...)`; replace `EntryAdded` with `SubscribeCommitted(...)`; keep using `ConvaiManager.Transcripts.Clear()` for canonical history removal; and replace `Export(ConversationExportFormat)` with `Export(TranscriptExportFormat)`, whose supported values are `PlainText`, `Markdown`, and `Json`.
- Replace `TranscriptUIController`, `ITranscriptUI`, `ITranscriptListener`, and presentation-strategy/view-model integrations with `ConvaiManager.Transcripts.Subscribe(...)`. Update rows from `change.Turn` keyed by `change.Turn.Id`, inspect `change.Kind` for lifecycle transitions, and remove rows by `change.TurnId` when the kind is `Removed`; `ChatTranscriptUI` and `ConvaiTranscriptEventRelay` are shipped references for code-driven and Inspector-driven history reactions.
- Split old single-stream transcript consumers by purpose: use `Subscribe(...)`/`CurrentTimeline.Turns` for durable chat, history, replay, and corrections; use `SubscribeCaptions(...)`/`CurrentCaptions` for low-latency speech-aligned subtitles. `ConvaiTranscriptDisplay` remains a character-local TTS convenience component, not the canonical room transcript surface.
- Update new-pipeline beta integrations from `Action<TranscriptTurn>` subscription callbacks to `Action<TranscriptChange>` and read the turn from `change.Turn`; rename option assignments from `IncludeInterim` to `IncludeActive` and `IncludeCommitted` to `IncludeTerminal`. Treat `CurrentTimeline` as immutable: repeated reads return the same instance while transcript state is unchanged and a new instance after an engine timeline change.
- Code that implemented `IBlendshapeSink` must drive a supported map/profile through
  `ConvaiLipSyncComponent`; custom runtime sink injection is no longer supported. Replace direct
  drift-type calls with the editor drift window/CSV workflow. Move map generation into editor
  tooling and re-export old tuple, pair, array-root, or JSON-like files as canonical version-1 JSON.
- Dynamic vision is off unless you opt in. `Auto` (the default mode) enables it only when the room's Connection Type is already `Video`; it never upgrades an Audio room, so existing Audio scenes keep their behavior and cost after upgrading. Set the mode to `Enabled` to force a video-capable room with vision.
- Rooms already configured with Connection Type `Video` before this release resolve `Auto` to vision **on** after upgrading and start sending `vision_input_config` on connect. If a Video room should keep its legacy native-video behavior (e.g. Gemini Live) without the new config, set Dynamic Vision Context to `Disabled`.
- `Disabled` suppresses the vision connect config without touching the configured Connection Type, so legacy native-video flows (e.g. Gemini Live) keep publishing video.
- The removed `SampleDynamicContextUI` also carried a microphone mute toggle and an initial-context readout; the Debug Hub panel does not replicate them. Use `ConvaiManager.Audio.SetMicMuted` (or the push-to-talk flow) for mic control.
- Vision frames attach image tokens to every model turn while enabled. The defaults mirror the backend (5 frames per turn, 1 s sampling, single horizon); richer configs such as dual-horizon sampling windows are an explicit opt-in — see the token-cost note in `Documentation~/DYNAMIC-VISION-CONTEXT.md`.

## [Released]

## [4.3.0] - 2026-06-23
### Feature Additions
- Added support with bundled native plugins, editor tooling, package version embedding, data stream updates, token-source helpers, and platform audio support across Windows, macOS, Linux, Android, and iOS.
- Added configurable connect-time user VAD settings for room connections, including room/profile inspector controls, transport mapping to `vad_params`, server-default handling, and resolved VAD logging.
- Added the consolidated dynamic context v2 flow with tracked state/events, batching, acknowledgement/result events, token feedback, attention object updates, and the `ConvaiDynamicContextRelay` authoring surface.
- Added synced world-object context support so tracked scene metadata and the current focus object can be sent through dynamic context.
- Added manual session resume id support, player-name metadata, request-trace metadata, end-user metadata, and the `InteractionCreated` runtime event.
- Added separate narrative trigger modes for saved triggers, inline events, and scripted speech.
- Added runtime debug panels and prefabs for dynamic context and emotion state, plus a world-space chat transcript prefab.
- Added action configuration validation, duplicate-binding preservation, step diagnostics, and action debug probing.
- Added Enter-to-focus behavior for chat input.

### Improvements
- Hardened native audio subscription reconciliation, FFI/audio lifecycle ownership, sample-rate handling, and native subscribed-track hydration during connect.
- Improved WebGL lipsync playback timing and typed-text RTVI stop handling.
- Improved emotion label resolution and aligned emotion scoring/docs with the current reading API.
- Improved scene metadata flushing so pending world-object metadata is sent reliably.
- Improved editor reuse for shared VAD inspector drawing and clarified inspector copy for server VAD behavior.
- Updated package documentation for dynamic context v2, actions, emotions, VAD setup, and source references.

### Bug Fixes
- Fixed unconditional Android AEC republishing.
- Fixed null or destroyed `AudioSource` handling.
- Fixed valid duplicate action bindings being treated as invalid.
- Fixed emotion debug panel scoping in shared sample assets.
- Fixed pending scene metadata not being flushed.

### Migration Notes
- Dynamic context now uses the v2 tracked update flow. Prefer `ConvaiCharacter.DynamicContext` or `ConvaiDynamicContextRelay` over the removed command-style dynamic context UI.
- Narrative trigger requests now carry an explicit mode. Use saved triggers, inline events, or scripted speech according to the desired backend behavior.
- Custom VAD values are sent only during room connect. Use the server-default option when the backend should own VAD defaults.

## [4.2.0] - 2026-05-08
### Feature Additions
- Added complete structured Actions support, including backend `action-response` handling, action configuration, target/character authoring, queued dispatch, target resolution, result-aware step reports, config validation, built-in executors, sample executors, and debug/editor validation tooling.
- Added Meta Quest passthrough vision support via `QuestVisionFrameSource`.
- Added runtime switching between push-to-talk and hands-free conversation modes without reconnecting.
- Expanded dynamic context runtime support with tracker APIs, inspector tooling, and sample UI commands.
- Added Convai scene setup API with setup wizard validation and bootstrap flow.
- Hands-free and push to talk support given in Settings Window. 

### Improvements
- Updated Basic Sample with action targets and action sample assets.
- Added and refreshed docs for Actions, the actions integration tutorial, turn-taking, API entrypoints, setup, troubleshooting, and source references.

### Bug Fixes
- Fixed Convai scene loading when a non-Convai scene is already open.
- Improved scene file opening with lazy, non-blocking retry behavior.
- Fixed FFI shutdown handling in audio callbacks.
- Improved setup health checks, logging, diagnostics, and test coverage.

## [4.1.0] - 2026-04-09
### Feature Additions
- Added dynamic context support.
- Made the LipSync sample's showcase camera work without a hard dependency on the Unity Input System by switching to reflection-based optional support.
- Refined the packaged sample scenes and transcript chat prefab defaults for a smoother out-of-box setup experience.

### Bug Fixes and Improvements
- Improved Vision module behavior and reliability.
- Improved editor startup reliability to reduce first-load native plugin errors during package import.
- Fixed compile-time issues around optional support libraries and improved native plugin compatibility for different Windows Unity editor architectures.
- Fixed an iOS crash related to support library handling.
- Fixed LipSync showcase eye-contact blend shape discovery so characters whose face meshes live outside the eye-bone subtree resolve correctly.
- Tuned showcase camera behavior and related sample assets for more stable presentation in the LipSync sample.

## [4.0.0] - 2026-03-12
### Feature Additions
- Introduced initial LipSync support in the Unity Core SDK, including core runtime integration points for LipSync-driven character workflows.
- Added bundled default ARKit blendshape maps to streamline early LipSync setup and reduce manual configuration.
- Expanded initial WebGL + LiveKit support, including support for vision canvas publishing in WebGL environments.
- Added an initial configurable native runtime mode to support evolving runtime selection behavior across supported platforms.
- Introduced early session resume UI and remote audio control support as part of the ongoing session and media control workflow improvements.
- Added support for passing emotion_config in room connection payloads.
- Continued evolving the SDK API surface with ConvaiRoomSession and broader session-oriented facade changes.
- Introduced a platform-aware networking bootstrap flow to support cleaner runtime registration for native and WebGL networking implementations.

### Bug Fixes and Improvements
- Improved stability of LipSync room-connect transport integration and related runtime connection flows.
- Continued simplifying the networking stack by removing older orchestration, reconnection, and legacy connection service layers in favor of more direct runtime ownership.
- Improved reliability of manager-driven startup and bootstrap behavior under the ConvaiManager flow.
- Updated native library download handling so native libraries are imported into a writable Unity project location, improving package install compatibility.
- Improved UPM sample packaging and sample structure to better align with Unity package distribution expectations.
- Resolved a number of compiler warnings, runtime integration issues, and editor UI sizing and stability issues in configuration and setup surfaces.

## [0.1.0] - 2026-02-20

### Added
- Real-time conversational AI characters via `ConvaiCharacter`, `ConvaiPlayer`, and `ConvaiRoomManager` components.
- Full conversation pipeline: Speech Recognition, Language Understanding and Generation, Text-to-Speech, Lipsync.
- Event-driven architecture with `IEventHub` for decoupled communication between SDK components.
- Modular behavior system (`ConvaiCharacterBehaviorBase`, `ConvaiPlayerBehaviorBase`) for extending character and player logic.
- Vision module for camera and webcam frame capture with configurable resolution and frame rate.
- Narrative Design module for trigger-based story progression and section synchronization.
- Scene metadata system (`ConvaiObjectMetadata`, `ConvaiSceneMetadataCollector`) for environment-aware AI.
- Configurable logging framework with pluggable sinks (Console, File, HTTP).
- UI components: transcript display (chat and subtitle modes), connection status indicator, notification system.
- Native transport layer powered by LiveKit for low-latency audio/video streaming.
- REST API client for character management, animation, long-term memory, and narrative services.
- Platform support for Windows, macOS, Linux, Android, and iOS (WebGL support planned).
- Editor tooling: Project Settings panel, custom inspectors, and menu items for quick setup.
- Sample scene with demo characters, animations, and interaction controller.
