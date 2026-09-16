using System;
using System.Reflection;
using Convai.Runtime.Animation;
using UnityEngine;

namespace Convai.Runtime.Embodiment
{
    /// <summary>
    ///     Reflection-based fallback bootstrap that integrates <see cref="EmbodimentContext" />
    ///     with the optional LipSync module without taking a hard assembly reference. In typical
    ///     scenarios, <see cref="ConvaiLipSyncSpeechEnergyAdapter" /> self-registers during
    ///     its lifecycle (<c>OnEnable</c>); this bridge serves as a last-resort factory when
    ///     a LipSync component exists but the adapter has not yet been created.
    /// </summary>
    internal static class EmbodimentLipSyncBridge
    {
        private const string LipSyncAssemblyName = "Convai.Modules.LipSync";
        private const string SpeechEnergyAdapterTypeName = "Convai.Modules.LipSync.ConvaiLipSyncSpeechEnergyAdapter";
        private const string LipSyncComponentTypeName = "Convai.Modules.LipSync.ConvaiLipSyncComponent";

        /// <summary>
        ///     Fallback factory that creates and registers a speech energy adapter when the LipSync
        ///     module is present but no adapter has self-registered yet. Returns <c>true</c> when
        ///     an existing adapter is found and registered or a new adapter is successfully created;
        ///     <c>false</c> when LipSync is not present, no LipSync component exists in the hierarchy,
        ///     or adapter creation fails.
        /// </summary>
        /// <remarks>
        ///     Most production scenarios rely on <see cref="ConvaiLipSyncSpeechEnergyAdapter" />
        ///     registering itself during <c>OnEnable</c>. This method only runs when
        ///     <see cref="EmbodimentContext.EnsureSpeechEnergyProvider" /> is called before the
        ///     adapter's lifecycle completes, such as in unit tests or unusual initialization order.
        /// </remarks>
        public static bool TryRegisterSpeechEnergyAdapter(EmbodimentContext context)
        {
            if (context == null) return false;

            Type adapterType = FindLoadedType(SpeechEnergyAdapterTypeName, LipSyncAssemblyName);
            if (adapterType == null || !typeof(MonoBehaviour).IsAssignableFrom(adapterType))
                return false;

            // Check for existing adapter; if found, ensure it's registered.
            // Adapter normally self-registers in OnEnable, but timing edge cases may skip that.
            if (context.GetComponentInChildren(adapterType, true) is ISpeechEnergyProvider existing)
            {
                context.RegisterSpeechEnergyProvider(existing);
                return true;
            }

            // No adapter exists. Check if LipSync component is present to justify creating one.
            Type lipSyncType = FindLoadedType(LipSyncComponentTypeName, LipSyncAssemblyName);
            if (lipSyncType == null || context.GetComponentInChildren(lipSyncType, true) == null)
                return false;

            // Create adapter component; it will self-register in its OnEnable.
            var adapter = context.gameObject.AddComponent(adapterType) as MonoBehaviour;
            if (adapter == null) return false;

            adapter.hideFlags = EmbodimentContext.RuntimeInfrastructureHideFlags();
            if (adapter is not ISpeechEnergyProvider provider) return false;

            // Redundant safety: register immediately in case OnEnable hasn't fired yet.
            context.RegisterSpeechEnergyProvider(provider);
            return true;
        }

        private static Type FindLoadedType(string fullName, string assemblyName)
        {
            Type direct = Type.GetType($"{fullName}, {assemblyName}");
            if (direct != null) return direct;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type candidate = assemblies[i].GetType(fullName, throwOnError: false);
                if (candidate != null) return candidate;
            }

            return null;
        }
    }
}
