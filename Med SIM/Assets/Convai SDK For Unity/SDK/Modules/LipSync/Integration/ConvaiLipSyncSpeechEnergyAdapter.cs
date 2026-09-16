using Convai.Runtime.Animation;
using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Modules.LipSync
{
    /// <summary>
    ///     Speech energy provider adapter that bridges <see cref="ConvaiLipSyncComponent" />
    ///     to the <see cref="ISpeechEnergyProvider" /> contract used by embodiment modules.
    ///     Automatically registers itself with the parent <see cref="EmbodimentContext" />
    ///     during <see cref="OnEnable" /> to eliminate the need for manual component scanning.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class ConvaiLipSyncSpeechEnergyAdapter : MonoBehaviour, IConfigurableSpeechEnergyProvider
    {
        private ConvaiLipSyncComponent _lipSync;
        private LipSyncSpeechEnergyProvider _provider;
        private EmbodimentContext _context;

        public float Current => _provider?.Current ?? 0f;

        private void Awake()
        {
            hideFlags |= HideFlags.HideInInspector;
            EnsureProvider();
        }

        private void OnEnable()
        {
            RegisterWithContext();
        }

        private void OnDisable()
        {
            UnregisterFromContext();
        }

        public void Configure(float windowSeconds)
        {
            EnsureProvider();
            _provider?.Configure(windowSeconds);
        }

        public void Sample(float deltaTime)
        {
            EnsureProvider();
            _provider?.Sample(deltaTime);
        }

        private bool EnsureProvider()
        {
            if (_provider != null) return true;

            _lipSync = GetComponentInParent<ConvaiLipSyncComponent>(true);
            if (_lipSync == null)
                _lipSync = GetComponentInChildren<ConvaiLipSyncComponent>(true);
            if (_lipSync == null) return false;

            _provider = new LipSyncSpeechEnergyProvider(_lipSync);
            return true;
        }

        private void RegisterWithContext()
        {
            if (_context != null) return;
            if (!EnsureProvider()) return;
            if (!EmbodimentContext.TryResolve(this, out _context)) return;

            _context.RegisterSpeechEnergyProvider(this);
        }

        private void UnregisterFromContext()
        {
            if (_context == null) return;

            _context.UnregisterSpeechEnergyProvider(this);
            _context = null;
        }
    }
}
