using System.Collections.Generic;
using System.IO;
using Convai.Editor.Embodiment.Inspectors;
using Convai.Modules.Attention.Profiles;
using Convai.Modules.Embodiment.Presets;
using Convai.Modules.ConversationFlow.Profiles;
using Convai.Modules.DialogueAnimation.Profiles;
using Convai.Modules.Emotion.Profiles;
using Convai.Modules.Emotion.Taxonomy;
using Convai.Modules.Gaze.Profiles;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Convai.Tests.EditMode.Runtime
{
    public sealed class EmbodimentProfileEditorTests
    {
        [Test]
        public void EmbodimentProfileAssets_CreateConvaiEmbodimentEditors()
        {
            AssertUsesEmbodimentEditor(ScriptableObject.CreateInstance<EmbodimentPresetLibrary>());
            AssertUsesEmbodimentEditor(ScriptableObject.CreateInstance<CharacterEmbodimentPreset>());
            AssertUsesEmbodimentEditor(ScriptableObject.CreateInstance<ConvaiConversationFlowProfile>());
            AssertUsesEmbodimentEditor(ScriptableObject.CreateInstance<ConvaiAttentionProfile>());
            AssertUsesEmbodimentEditor(ScriptableObject.CreateInstance<ConvaiEmotionProfile>());
            AssertUsesEmbodimentEditor(ScriptableObject.CreateInstance<EmotionTaxonomyAsset>());
            AssertUsesEmbodimentEditor(ScriptableObject.CreateInstance<ConvaiGazeCoordinationProfile>());
            AssertUsesEmbodimentEditor(ScriptableObject.CreateInstance<ConvaiGazeEyeProfile>());
            AssertUsesEmbodimentEditor(ScriptableObject.CreateInstance<ConvaiGazeHeadProfile>());
            AssertUsesEmbodimentEditor(ScriptableObject.CreateInstance<ConvaiDialogueAnimationProfile>());
        }

        [Test]
        public void EmbodimentProfileEditors_ExposeStableSectionIds()
        {
            Assert.AreEqual("Presets", EmbodimentPresetLibraryInspector.SectionPresets);
            Assert.AreEqual("Identity", CharacterEmbodimentPresetInspector.SectionIdentity);
            Assert.AreEqual("Commitment", AttentionProfileInspector.SectionCommitment);
            Assert.AreEqual("Taxonomy", EmotionProfileInspector.SectionTaxonomy);
            Assert.AreEqual("StateWeights", ConvaiGazeCoordinationProfileInspector.SectionStateWeights);
            Assert.AreEqual("Tracking", ConvaiGazeEyeProfileInspector.SectionTracking);
            Assert.AreEqual("Range", ConvaiGazeHeadProfileInspector.SectionRange);
            Assert.AreEqual("Content", DialogueAnimationProfileInspector.SectionContent);
        }

        [Test]
        public void EmbodimentProfileEditors_DoNotUseAdHocEditorPrefs()
        {
            string root = Path.GetFullPath(Path.Combine(
                global::UnityEngine.Application.dataPath,
                "..",
                "Packages",
                "com.convai.convai-sdk-for-unity",
                "SDK",
                "Editor",
                "Embodiment",
                "Inspectors"));

            AssertNoEditorPrefs(Path.Combine(root, "EmbodimentProfileEditorBase.cs"));
            AssertNoEditorPrefs(Path.Combine(root, "EmbodimentProfileAssetEditors.cs"));
            AssertNoEditorPrefs(Path.Combine(root, "CharacterEmbodimentPresetInspector.cs"));
        }

        [Test]
        public void PresetSlotDiagnostics_DetectDuplicateEmptyNullAndWrongType()
        {
            var attention = ScriptableObject.CreateInstance<ConvaiAttentionProfile>();
            var emotion = ScriptableObject.CreateInstance<ConvaiEmotionProfile>();
            try
            {
                var slots = new List<EmbodimentProfileSlot>
                {
                    new("convai.attention", attention),
                    new("convai.attention", attention),
                    new(string.Empty, attention),
                    new("convai.emotion", null),
                    new("convai.gaze-head", emotion),
                };

                List<EmbodimentPresetSlotDiagnostic> diagnostics = EmbodimentPresetSlotDiagnostics.Analyze(slots);

                Assert.IsTrue(Contains(diagnostics, "Duplicate module ID 'convai.attention'", EmbodimentPresetSlotSeverity.Error));
                Assert.IsTrue(Contains(diagnostics, "empty module ID", EmbodimentPresetSlotSeverity.Error));
                Assert.IsTrue(Contains(diagnostics, "profile is null", EmbodimentPresetSlotSeverity.Warning));
                Assert.IsTrue(Contains(diagnostics, "expected ConvaiGazeHeadProfile", EmbodimentPresetSlotSeverity.Error));
                Assert.IsTrue(EmbodimentPresetSlotDiagnostics.HasErrors(diagnostics));
            }
            finally
            {
                Object.DestroyImmediate(attention);
                Object.DestroyImmediate(emotion);
            }
        }

        private static void AssertUsesEmbodimentEditor(ScriptableObject target)
        {
            UnityEditor.Editor editor = null;
            try
            {
                editor = UnityEditor.Editor.CreateEditor(target);
                Assert.IsNotNull(editor, $"No editor created for {target.GetType().Name}");
                Assert.AreEqual(
                    "Convai.Editor.Embodiment",
                    editor.GetType().Assembly.GetName().Name,
                    $"{target.GetType().Name} used {editor.GetType().FullName}");
            }
            finally
            {
                if (editor != null)
                    Object.DestroyImmediate(editor);
                if (target != null)
                    Object.DestroyImmediate(target);
            }
        }

        private static void AssertNoEditorPrefs(string path)
        {
            Assert.IsTrue(File.Exists(path), $"Expected {path}");
            string text = File.ReadAllText(path);
            StringAssert.DoesNotContain("EditorPrefs", text);
        }

        private static bool Contains(
            IReadOnlyList<EmbodimentPresetSlotDiagnostic> diagnostics,
            string messageFragment,
            EmbodimentPresetSlotSeverity severity)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                EmbodimentPresetSlotDiagnostic diagnostic = diagnostics[i];
                if (diagnostic.Severity == severity && diagnostic.Message.Contains(messageFragment))
                    return true;
            }
            return false;
        }
    }
}
