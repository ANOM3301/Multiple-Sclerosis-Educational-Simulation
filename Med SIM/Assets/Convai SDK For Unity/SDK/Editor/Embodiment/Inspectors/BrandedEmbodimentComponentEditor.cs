using System.Reflection;
using Convai.Editor.Inspectors;
using Convai.Runtime.Embodiment;
using UnityEditor;

namespace Convai.Editor.Embodiment.Inspectors
{
    /// <summary>
    ///     Reusable inspector base that renders the branded Convai header for any component
    ///     decorated with <see cref="EmbodimentComponentBrandingAttribute" />. Subclasses only
    ///     need to declare a <see cref="CustomEditor" /> targeting the runtime component
    ///     type; the title and subtitle are read from the attribute on the target class.
    /// </summary>
    /// <remarks>
    ///     Components without the attribute fall back to the default inspector header so
    ///     this template never silently overwrites another editor's chrome. Pair with
    ///     <see cref="ConvaiBrandedInspectorChrome" /> for premium typography and spacing.
    /// </remarks>
    internal abstract class BrandedEmbodimentComponentEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawHeaderFromAttribute();
            DrawDefaultInspector();
        }

        private void DrawHeaderFromAttribute()
        {
            if (target == null) return;

            EmbodimentComponentBrandingAttribute branding = target
                .GetType()
                .GetCustomAttribute<EmbodimentComponentBrandingAttribute>(inherit: false);

            if (branding != null)
            {
                ConvaiBrandedInspectorChrome.DrawHeader(branding.Title, branding.Subtitle);
            }
        }
    }
}
