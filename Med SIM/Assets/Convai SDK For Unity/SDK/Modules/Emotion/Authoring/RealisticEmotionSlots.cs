using System.Collections.Generic;
using Convai.Domain.Embodiment.Semantics;
using Convai.Modules.Emotion.Outputs;

namespace Convai.Modules.Emotion.Authoring
{
    /// <summary>
    ///     Factory for realistic, FACS-inspired emotion-to-blendshape slot lists. Produces
    ///     a ready-to-consume <see cref="EmotionSlotBinding" /> list for the supplied rig
    ///     convention (ARKit, CC3, CC4 Extended, MetaHuman).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Slot selection follows Ekman's six basic emotions augmented with Plutchik's
    ///         <em>trust</em> and <em>anticipation</em> channels so the output set covers
    ///         the default Convai taxonomy. Per-shape weights are tuned against a standing
    ///         adult reference:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>Joy -> Duchenne smile (mouth smile + cheek raise + subtle eye squint).</item>
    ///         <item>Sadness -> oblique frown + pulled-up inner brow + softly closed eyes.</item>
    ///         <item>Anger -> dropped brow + nose sneer + lip press + eye squint.</item>
    ///         <item>Fear -> eyes wide + inner & outer brow raise + lip stretch.</item>
    ///         <item>Surprise -> eyes wide + outer brow raise + jaw drop (mouth layer).</item>
    ///         <item>Disgust -> nose sneer + upper-lip raise + brow drop.</item>
    ///         <item>Trust -> subtle smile + soft eye.</item>
    ///         <item>Anticipation -> subtle smile + outer brow raise.</item>
    ///     </list>
    ///     <para>
    ///         Mouth-region slots are flagged with <c>IsMouthShape = true</c> so the
    ///         blendshape compositor routes them onto the LipSync-cross-faded mouth layer.
    ///     </para>
    ///     <para>
    ///         The factory returns an empty list for <see cref="RigConvention.Unknown" />
    ///         and <see cref="RigConvention.MetaHuman" /> since those rigs rely on
    ///         user-authored mappings or control-board DNA assets respectively.
    ///     </para>
    /// </remarks>
    public static class RealisticEmotionSlots
    {
        /// <summary>
        ///     Builds the realistic slot list for <paramref name="rig" />. Callers typically
        ///     feed the result into <c>BlendshapeEmotionBinding.SetSlots</c>.
        /// </summary>
        public static IReadOnlyList<EmotionSlotBinding> Build(RigConvention rig)
        {
            return rig switch
            {
                RigConvention.ReallusionCC4Extended => BuildCC4Extended(),
                RigConvention.ReallusionCC3 => BuildCC3(),
                RigConvention.ARKit => BuildARKit(),
                _ => BuildCC3()
            };
        }

        // -----------------------------------------------------------------
        // CC4 Extended  -  richest set. Uses per-side press/stretch/upper-up
        // shapes that only exist in the Extended facial profile.
        // -----------------------------------------------------------------
        private static List<EmotionSlotBinding> BuildCC4Extended()
        {
            var list = new List<EmotionSlotBinding>
            {
                // Joy  -  Duchenne smile + cheek raise + subtle eye narrowing.
                Slot("joy", "Mouth_Smile_L,Mouth_Smile_R", 1.0f, 85f, isMouthShape: true),
                Slot("joy", "Cheek_Raise_L,Cheek_Raise_R", 0.9f, 70f, isMouthShape: false),
                Slot("joy", "Eye_Squint_L,Eye_Squint_R", 0.4f, 35f, isMouthShape: false),

                // Sadness  -  oblique frown + inner brow raise + droopy eye.
                Slot("sadness", "Mouth_Frown_L,Mouth_Frown_R", 1.0f, 75f, isMouthShape: true),
                Slot("sadness", "Brow_Raise_Inner_L,Brow_Raise_Inner_R", 1.0f, 80f, isMouthShape: false),
                Slot("sadness", "Eye_Blink_L,Eye_Blink_R", 0.3f, 25f, isMouthShape: false),

                // Anger  -  dropped brow, nose sneer, lip press, eye squint.
                Slot("anger", "Brow_Drop_L,Brow_Drop_R", 1.0f, 85f, isMouthShape: false),
                Slot("anger", "Nose_Sneer_L,Nose_Sneer_R", 0.75f, 55f, isMouthShape: false),
                Slot("anger", "Mouth_Press_L,Mouth_Press_R", 0.9f, 75f, isMouthShape: true),
                Slot("anger", "Eye_Squint_L,Eye_Squint_R", 0.5f, 45f, isMouthShape: false),

                // Fear  -  eyes wide, inner+outer brow raise, lip stretch.
                Slot("fear", "Eye_Wide_L,Eye_Wide_R", 1.0f, 80f, isMouthShape: false),
                Slot("fear", "Brow_Raise_Inner_L,Brow_Raise_Inner_R", 1.0f, 75f, isMouthShape: false),
                Slot("fear", "Brow_Raise_Outer_L,Brow_Raise_Outer_R", 0.8f, 65f, isMouthShape: false),
                Slot("fear", "Mouth_Stretch_L,Mouth_Stretch_R", 0.85f, 70f, isMouthShape: true),

                // Surprise  -  eyes wide, outer brow raise, jaw drop.
                Slot("surprise", "Eye_Wide_L,Eye_Wide_R", 1.0f, 90f, isMouthShape: false),
                Slot("surprise", "Brow_Raise_Outer_L,Brow_Raise_Outer_R", 1.0f, 80f, isMouthShape: false),
                Slot("surprise", "V_Open", 0.6f, 55f, isMouthShape: true),

                // Disgust  -  nose sneer + upper-lip raise + mild brow drop.
                Slot("disgust", "Nose_Sneer_L,Nose_Sneer_R", 1.0f, 80f, isMouthShape: false),
                Slot("disgust", "Mouth_Up_Upper_L,Mouth_Up_Upper_R", 0.85f, 65f, isMouthShape: true),
                Slot("disgust", "Brow_Drop_L,Brow_Drop_R", 0.5f, 45f, isMouthShape: false),

                // Trust  -  warm, subtle smile.
                Slot("trust", "Mouth_Smile_L,Mouth_Smile_R", 0.6f, 45f, isMouthShape: true),
                Slot("trust", "Cheek_Raise_L,Cheek_Raise_R", 0.4f, 30f, isMouthShape: false),

                // Anticipation  -  subtle lift + outer brow raise.
                Slot("anticipation", "Mouth_Smile_L,Mouth_Smile_R", 0.5f, 40f, isMouthShape: true),
                Slot("anticipation", "Brow_Raise_Outer_L,Brow_Raise_Outer_R", 0.55f, 40f, isMouthShape: false),
            };
            return list;
        }

        // -----------------------------------------------------------------
        // CC3 base  -  same semantic intent but drops shapes that only exist
        // in the Extended profile (press, upper-up, stretch per side).
        // -----------------------------------------------------------------
        private static List<EmotionSlotBinding> BuildCC3()
        {
            var list = new List<EmotionSlotBinding>
            {
                Slot("joy", "Mouth_Smile_L,Mouth_Smile_R", 1.0f, 80f, isMouthShape: true),
                Slot("joy", "Cheek_Raise_L,Cheek_Raise_R", 0.85f, 65f, isMouthShape: false),
                Slot("joy", "Eye_Squint_L,Eye_Squint_R", 0.35f, 30f, isMouthShape: false),

                Slot("sadness", "Mouth_Frown_L,Mouth_Frown_R", 1.0f, 70f, isMouthShape: true),
                Slot("sadness", "Brow_Raise_Inner_L,Brow_Raise_Inner_R", 1.0f, 75f, isMouthShape: false),
                Slot("sadness", "Eye_Blink_L,Eye_Blink_R", 0.25f, 22f, isMouthShape: false),

                Slot("anger", "Brow_Drop_L,Brow_Drop_R", 1.0f, 80f, isMouthShape: false),
                Slot("anger", "Nose_Sneer_L,Nose_Sneer_R", 0.7f, 55f, isMouthShape: false),
                Slot("anger", "Mouth_Dimple_L,Mouth_Dimple_R", 0.75f, 55f, isMouthShape: true),
                Slot("anger", "Eye_Squint_L,Eye_Squint_R", 0.5f, 45f, isMouthShape: false),

                Slot("fear", "Eye_Wide_L,Eye_Wide_R", 1.0f, 80f, isMouthShape: false),
                Slot("fear", "Brow_Raise_Inner_L,Brow_Raise_Inner_R", 1.0f, 70f, isMouthShape: false),
                Slot("fear", "Brow_Raise_Outer_L,Brow_Raise_Outer_R", 0.75f, 60f, isMouthShape: false),
                Slot("fear", "Mouth_Stretch_L,Mouth_Stretch_R", 0.8f, 65f, isMouthShape: true),

                Slot("surprise", "Eye_Wide_L,Eye_Wide_R", 1.0f, 90f, isMouthShape: false),
                Slot("surprise", "Brow_Raise_Outer_L,Brow_Raise_Outer_R", 1.0f, 80f, isMouthShape: false),
                Slot("surprise", "V_Open", 0.55f, 50f, isMouthShape: true),

                Slot("disgust", "Nose_Sneer_L,Nose_Sneer_R", 1.0f, 75f, isMouthShape: false),
                Slot("disgust", "Brow_Drop_L,Brow_Drop_R", 0.5f, 45f, isMouthShape: false),

                Slot("trust", "Mouth_Smile_L,Mouth_Smile_R", 0.55f, 40f, isMouthShape: true),
                Slot("trust", "Cheek_Raise_L,Cheek_Raise_R", 0.35f, 28f, isMouthShape: false),

                Slot("anticipation", "Mouth_Smile_L,Mouth_Smile_R", 0.45f, 35f, isMouthShape: true),
                Slot("anticipation", "Brow_Raise_Outer_L,Brow_Raise_Outer_R", 0.55f, 38f, isMouthShape: false),
            };
            return list;
        }

        // -----------------------------------------------------------------
        // ARKit  -  Apple's 52-blendshape convention. Naming is camelCase.
        // -----------------------------------------------------------------
        private static List<EmotionSlotBinding> BuildARKit()
        {
            var list = new List<EmotionSlotBinding>
            {
                Slot("joy", "mouthSmileLeft,mouthSmileRight", 1.0f, 85f, isMouthShape: true),
                Slot("joy", "cheekSquintLeft,cheekSquintRight", 0.85f, 65f, isMouthShape: false),
                Slot("joy", "eyeSquintLeft,eyeSquintRight", 0.4f, 35f, isMouthShape: false),

                Slot("sadness", "mouthFrownLeft,mouthFrownRight", 1.0f, 75f, isMouthShape: true),
                Slot("sadness", "browInnerUp", 1.0f, 80f, isMouthShape: false),
                Slot("sadness", "eyeBlinkLeft,eyeBlinkRight", 0.3f, 25f, isMouthShape: false),

                Slot("anger", "browDownLeft,browDownRight", 1.0f, 85f, isMouthShape: false),
                Slot("anger", "noseSneerLeft,noseSneerRight", 0.75f, 55f, isMouthShape: false),
                Slot("anger", "mouthPressLeft,mouthPressRight", 0.9f, 75f, isMouthShape: true),
                Slot("anger", "eyeSquintLeft,eyeSquintRight", 0.5f, 45f, isMouthShape: false),

                Slot("fear", "eyeWideLeft,eyeWideRight", 1.0f, 80f, isMouthShape: false),
                Slot("fear", "browInnerUp", 1.0f, 75f, isMouthShape: false),
                Slot("fear", "browOuterUpLeft,browOuterUpRight", 0.8f, 65f, isMouthShape: false),
                Slot("fear", "mouthStretchLeft,mouthStretchRight", 0.85f, 70f, isMouthShape: true),

                Slot("surprise", "eyeWideLeft,eyeWideRight", 1.0f, 90f, isMouthShape: false),
                Slot("surprise", "browOuterUpLeft,browOuterUpRight", 1.0f, 80f, isMouthShape: false),
                Slot("surprise", "jawOpen", 0.6f, 55f, isMouthShape: true),

                Slot("disgust", "noseSneerLeft,noseSneerRight", 1.0f, 80f, isMouthShape: false),
                Slot("disgust", "mouthUpperUpLeft,mouthUpperUpRight", 0.85f, 65f, isMouthShape: true),
                Slot("disgust", "browDownLeft,browDownRight", 0.5f, 45f, isMouthShape: false),

                Slot("trust", "mouthSmileLeft,mouthSmileRight", 0.6f, 45f, isMouthShape: true),
                Slot("trust", "cheekSquintLeft,cheekSquintRight", 0.4f, 30f, isMouthShape: false),

                Slot("anticipation", "mouthSmileLeft,mouthSmileRight", 0.5f, 40f, isMouthShape: true),
                Slot("anticipation", "browOuterUpLeft,browOuterUpRight", 0.55f, 40f, isMouthShape: false),
            };
            return list;
        }

        private static EmotionSlotBinding Slot(
            string label,
            string blendshapes,
            float weightMultiplier,
            float fullWeight,
            bool isMouthShape)
        {
            return new EmotionSlotBinding(
                emotionLabel: label,
                animatorParameterName: string.Empty,
                blendshapeNames: blendshapes,
                weightMultiplier: weightMultiplier,
                fullBlendshapeWeight: fullWeight,
                isMouthShape: isMouthShape);
        }
    }
}
