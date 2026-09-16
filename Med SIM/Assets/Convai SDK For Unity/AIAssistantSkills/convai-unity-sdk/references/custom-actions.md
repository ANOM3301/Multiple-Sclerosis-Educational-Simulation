# Custom actions

Diagnose before repair. Upsert definitions without deleting unrelated actions or targets.

For built-in movement/body actions, configure the feature first and use returned executor IDs in `Convai.ConfigureActions`. For custom behavior:

1. Create an `IConvaiActionExecutor` script with Unity script tools.
2. Return `Unhandled` for unsupported commands, `Failed` for handled failures, and `Succeeded` only after work finishes.
3. Observe cancellation and guarantee completion for every path, including exceptions and timeouts.
4. Attach component with Unity tools; capture its instance ID.
5. Preview/apply `ConfigureActions` with `executorInstanceId`.
6. In Edit Mode, validate using `SimulateAction`. Enter Play Mode explicitly for real dispatch.

UnityEvent placeholders are incomplete until persistent events are wired.
