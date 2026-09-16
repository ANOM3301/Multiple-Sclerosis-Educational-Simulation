namespace Convai.Runtime.Animation
{
    /// <summary>
    ///     Layer identifiers for the facial blendshape compositor.
    ///     Built-in layers have fixed IDs below <see cref="CustomLayerStart" />.
    ///     Register custom layers via <see cref="FacialBlendshapeCompositorHost.RegisterCustomLayer" />.
    /// </summary>
    public static class FacialBlendshapeLayers
    {
        public const int EmotionGeneral = 0;
        public const int EmotionMouth = 1;
        public const int LipSync = 2;
        public const int Eyes = 3;
        public const int HeadLook = 4;
        public const int FacialClipGeneral = 5;
        public const int FacialClipMouth = 6;

        /// <summary>First ID available for user-registered custom layers.</summary>
        public const int CustomLayerStart = 100;

        internal const int BuiltInCount = 7;

        internal static bool IsBuiltIn(int layerId) => layerId >= 0 && layerId < CustomLayerStart;
        internal static bool IsCustom(int layerId) => layerId >= CustomLayerStart;
    }
}
