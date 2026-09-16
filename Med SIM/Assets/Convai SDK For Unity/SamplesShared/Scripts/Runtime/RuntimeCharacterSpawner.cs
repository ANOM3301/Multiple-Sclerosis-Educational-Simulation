using System;
using System.Threading.Tasks;
using Convai.Domain.DomainEvents.Session;
using Convai.Runtime.Components;
using UnityEngine;

namespace Convai.Sample.Runtime
{
    /// <summary>
    ///     Sample component that instantiates a Convai character prefab and starts its conversation at runtime.
    /// </summary>
    public sealed class RuntimeCharacterSpawner : MonoBehaviour
    {
        private const int InjectionTimeoutFrames = 120;

        [SerializeField] private ConvaiCharacter characterPrefab;
        [SerializeField] private Transform spawnPoint;

        private ConvaiCharacter _runtimeCharacter;
        private bool _isSpawning;

        /// <summary>
        ///     Assign this method to a UI Button OnClick event.
        /// </summary>
        public async void SpawnAndConnectCharacter()
        {
            if (_isSpawning) return;

            if (characterPrefab == null)
            {
                Debug.LogError(
                    $"[{nameof(RuntimeCharacterSpawner)}] Assign a ConvaiCharacter prefab before spawning.",
                    this);
                return;
            }

            _isSpawning = true;
            try
            {
                bool reconnectForOwnershipChange = false;
                if (_runtimeCharacter == null)
                {
                    ConvaiManager manager = ConvaiManager.ActiveManager;
                    if (manager == null)
                    {
                        throw new InvalidOperationException(
                            "Cannot register the runtime character because no active ConvaiManager exists.");
                    }

                    Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
                    Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
                    _runtimeCharacter = Instantiate(characterPrefab, position, rotation);

                    reconnectForOwnershipChange = manager.IsConnected;

                    // Explicit ownership registration supports plain ConvaiCharacter prefabs as well
                    // as prefabs whose child modules also use the late-module registration path.
                    manager.SetExplicitCharacters(new[] { _runtimeCharacter });
                    manager.SetExplicitConversationTarget(_runtimeCharacter);
                    await WaitForInjectionAsync(_runtimeCharacter);

                    // Injection precedes late-module startup. Let that startup finish before opening
                    // the conversation so lip-sync transport is active for the first audio response.
                    await Task.Yield();
                }

                ConvaiManager activeManager = ConvaiManager.ActiveManager;
                if (reconnectForOwnershipChange && activeManager != null && activeManager.IsConnected)
                {
                    // Ownership changes made while connected are applied on the next connection.
                    // Disconnect first so StartConversationAsync reconnects with this character selected.
                    await activeManager.DisconnectAsync();
                }

                switch (_runtimeCharacter.SessionState)
                {
                    case SessionState.Disconnected:
                        await _runtimeCharacter.StartConversationAsync();
                        break;

                    case SessionState.Error:
                        await _runtimeCharacter.ResetAndRetryAsync();
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                _isSpawning = false;
            }
        }

        private static async Task WaitForInjectionAsync(ConvaiCharacter character)
        {
            for (int frame = 0; frame < InjectionTimeoutFrames; frame++)
            {
                if (character != null && character.IsInjected) return;
                await Task.Yield();
            }

            throw new InvalidOperationException(
                "The runtime character was not injected. Ensure an active ConvaiManager is in the scene.");
        }
    }
}
