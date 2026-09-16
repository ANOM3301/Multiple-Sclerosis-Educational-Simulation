using System;
using Convai.Domain.Embodiment.Interfaces;

namespace Convai.Runtime.Embodiment
{
    /// <summary>
    ///     Holds the optional default <see cref="IConversationFlowSource" /> factory installed by
    ///     the ConversationFlow module so <see cref="EmbodimentContext" /> can lazily
    ///     auto-provision a driver when an embodiment module demands one but no controller has been
    ///     authored on the character. Keeps the cross-module integration out of the context
    ///     itself.
    /// </summary>
    internal static class EmbodimentContextConversationFlowProvisioner
    {
        private static Func<EmbodimentContext, IConversationFlowSource> _defaultFactory;

        /// <summary>
        ///     Registers the default factory invoked by
        ///     <see cref="CreateDefault" /> when no source is registered. Called at module load
        ///     time by the ConversationFlow runtime installer.
        /// </summary>
        public static void RegisterDefaultFactory(
            Func<EmbodimentContext, IConversationFlowSource> factory)
        {
            _defaultFactory = factory;
        }

        /// <summary>
        ///     Invokes the registered factory (if any) to materialize a default source for
        ///     <paramref name="context" />. Returns <c>null</c> when no factory is installed.
        /// </summary>
        public static IConversationFlowSource CreateDefault(EmbodimentContext context) =>
            _defaultFactory?.Invoke(context);
    }
}
