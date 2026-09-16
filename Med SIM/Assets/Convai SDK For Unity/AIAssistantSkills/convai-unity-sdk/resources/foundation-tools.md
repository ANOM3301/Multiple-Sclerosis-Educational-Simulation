# Convai foundation tool reference

Unity Assistant calls the dot-named tools below. External MCP clients receive the equivalent underscore-normalized names shown in parentheses.

All tools return `{ success, message, data }`. Treat `success=false` as a failed operation even when the tool call itself completed.

These twenty tools are the complete shipped Convai tool set. Contract version: 4.

## `Convai.GetGuidance` (`Convai_GetGuidance`)

- Input: `topic` — `Overview`, `Setup`, `Actions`, `DynamicContext`, `Vision`, `Narrative`, `Embodiment`, `Events`, or `Runtime`.
- Use before feature-specific work.
- Output includes summary, prerequisites, workflow, relevant Convai and Unity tools, and documentation paths.

## `Convai.GetProjectStatus` (`Convai_GetProjectStatus`)

- No input.
- Returns SDK, Unity, Assistant and tool-contract versions; credential presence; non-secret server and feature settings; Editor state; package root.
- It never returns the API key. Never ask the user to paste one into chat.

## `Convai.InspectScene` (`Convai_InspectScene`)

- Input: `includeInactive` (default `true`).
- Returns open scenes plus managers, rooms, players, characters, counts, component IDs, GameObject IDs, names, active state, Character IDs, and player names.
- Pass returned instance IDs to later operations. Names are display context, not stable identifiers.

## `Convai.ValidateSetup` (`Convai_ValidateSetup`)

- Input: `scope` — `All` (default), `Project`, or `Scene`.
- Returns `errors`, `warnings`, and `nextSteps`.
- Validation is read-only. Run before and after mutations.

## `Convai.BootstrapScene` (`Convai_BootstrapScene`)

- Input: `dryRun` (Unity Assistant default `true`).
- Edit Mode only. With `dryRun=true`, returns `wouldAddManager` and `wouldAddRoomManager` without mutation.
- With `dryRun=false`, idempotently creates or reuses the manager object and adds required manager/room components through Unity Undo.
- Does not add players or characters, set credentials, save the scene, or enter Play Mode.
- Prefer `Convai.SetupConversationScene` for end-to-end setup. Use bootstrap only for manager/room-only work.

## `Convai.ConfigureRoom` (`Convai_ConfigureRoom`)

- Requires an explicit target GameObject instance ID; previews by default.
- Ensures `ConvaiManager` and `ConvaiRoomManager`, then writes inline Audio/Video, input, startup, endpoint, vision-policy, and PTT settings or assigns an existing room profile.
- Rejects incomplete dynamic-vision Video configuration instead of opening a modal prompt.

## `Convai.ConfigurePlayer` (`Convai_ConfigurePlayer`)

- Requires an explicit target GameObject instance ID; previews by default.
- Adds/configures `ConvaiPlayer` and binds an explicit or unambiguous same-scene manager.
- Never repurposes or modifies `Main Camera`.

## `Convai.ConfigureCharacter` (`Convai_ConfigureCharacter`)

- Requires an explicit target GameObject instance ID; previews by default.
- Adds/configures `ConvaiCharacter`, `AudioSource`, and `ConvaiAudioOutput`, then binds manager ownership and active target.
- Empty Character ID permits independent authoring but returns `complete=false` and `requiredInputs=["characterId"]`.

## `Convai.SetupConversationScene` (`Convai_SetupConversationScene`)

- Active-scene orchestrator; previews by default.
- Selection order is explicit instance ID, one unambiguous existing component, then a safe placeholder when none exists.
- Creates `[Convai Manager]`, standalone `Convai Player`, and visible Capsule `Convai Character` as needed; configures Audio, HandsFree, automatic connection, audio output, and explicit ownership.
- Applies all unblocked work, reports ambiguity instead of guessing, and never saves or enters Play Mode.

## `Convai.DiagnoseConversation` (`Convai_DiagnoseConversation`)

- Read-only in Edit and Play Mode.
- Returns `readyToRun`, configuration/runtime snapshots, and ranked issues with stable codes, evidence, auto-fixability, and suggested tool arguments.
- Use Unity Console tools when it suggests console evidence; the diagnostic never changes runtime state.

## Feature tools

- `Convai.ConfigureActions` safely upserts definitions and explicit targets; missing executors receive UnityEvent placeholders and remain incomplete until wired.
- `Convai.DiagnoseActions` returns validator evidence. `Convai.SimulateAction` validates in Edit Mode and executes only in Play Mode.
- `Convai.ConfigureLipSync` assigns existing meshes/maps and uniquely detected or explicit shipped profiles. `Convai.DiagnoseLipSync` reports configuration and runtime buffer evidence.
- `Convai.ConfigureTranscripts` supports relay, chat, and world-space chat modes without changing project settings. `Convai.DiagnoseTranscripts` returns metadata, not transcript text.
- `Convai.TraceRuntimeEvents` starts, reads, clears, or stops a 256-entry editor-only trace. Transcript capture defaults off.
- `Convai.ConfigureNarrative` upserts Unity-side section/key/trigger configuration. `Convai.DiagnoseNarrative` reports local configuration and sanitized runtime state; neither tool contacts the backend.

## Error handling

- `PLAY_MODE_ACTIVE`: authoring tools require Edit Mode. Do not stop Play Mode unless requested; diagnosis remains available.
- Invalid or missing IDs: inspect again; never fall back to a name guess.
- Missing credentials: direct user to Project Settings, then call project status again.
- Compilation or domain reload: wait for Editor readiness, then repeat the last read-only inspection before continuing.
