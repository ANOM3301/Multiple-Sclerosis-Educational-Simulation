using System;

namespace Convai.Runtime.Embodiment
{
    /// <summary>
    ///     Attaches the branded inspector header (title + subtitle) used by the editor-side
    ///     branded inspector template to an embodiment component. Components that opt into
    ///     branded chrome decorate themselves with this attribute so the inspector template
    ///     can render their header without per-component boilerplate.
    /// </summary>
    /// <remarks>
    ///     Defaults to the standard "Convai Embodiment" subtitle so most components only need
    ///     to provide the title. The attribute lives in the runtime assembly (alongside the
    ///     component types it describes) so editor inspectors can read it via reflection
    ///     without violating the inward-only assembly rule.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class EmbodimentComponentBrandingAttribute : Attribute
    {
        public EmbodimentComponentBrandingAttribute(string title, string subtitle = "Convai Embodiment")
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Branded inspector title cannot be empty.", nameof(title));

            Title = title;
            Subtitle = subtitle ?? string.Empty;
        }

        public string Title { get; }
        public string Subtitle { get; }
    }
}
