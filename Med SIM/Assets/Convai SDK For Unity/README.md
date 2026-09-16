# Convai Unity SDK

This SDK adds **real-time conversational characters** to Unity. A typical setup is:

- `ConvaiManager` on one scene object (usually `Systems`) to bootstrap Convai services
- `ConvaiRoomManager` on that same scene host to own conversation mode, room defaults, and connect-time setup
- `ConvaiCharacter` on each NPC/agent you want to talk to
- `ConvaiPlayer` on the player (identity + text input)
- Optional: `ConvaiAudioOutput` on each character for audio output tuning

If you are integrating this into a project (devs + designers), start here:

- `Documentation~/SETUP.md`

## Quickstart (scene works end-to-end)

1. In your scene: `GameObject > Convai > Setup Required Components`
2. Set your API key: `Edit > Project Settings > Convai SDK`
3. Add components:
  - NPC GameObject: `Convai/Convai Character` (+ recommended `Convai/Convai Audio Output`)
  - Player GameObject: `Convai/Convai Player`
  - Scene host: ensure `ConvaiManager` and `ConvaiRoomManager` are present
4. Press Play and start a conversation (auto-connect or via your UI).

## Documentation

- Feature → script map: `Documentation~/SOURCE-REFERENCE.md`
- Docs index (pick your path): `Documentation~/README.md`
- Setup (most common): `Documentation~/SETUP.md`
- API entrypoints + recipes: `Documentation~/API-ENTRYPOINTS.md`
- Actions (structured commands): `Documentation~/ACTIONS.md`
- Actions integration tutorial: `Documentation~/ACTIONS-INTEGRATION-TUTORIAL.md`
- Dynamic context: `Documentation~/DYNAMIC-CONTEXT.md`
- Events: `Documentation~/WORKING-WITH-EVENTS.md`
- Project Settings reference: `Documentation~/PROJECT-SETTINGS.md`
- Platform notes (mobile/WebGL): `Documentation~/PLATFORMS.md`
- Troubleshooting: `Documentation~/TROUBLESHOOTING.md`
- SDK architecture (for contributors): `SDK/README.md`
