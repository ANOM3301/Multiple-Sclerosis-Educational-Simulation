#if UNITY_EDITOR
using System;
using Convai.Editor.Actions;
using Convai.Editor.Inspectors;
using Convai.Editor.UI;
using Convai.Shared.Actions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Convai.Tests.EditMode.Presentation
{
    public class ConvaiEditorUiDesignSystemTests
    {
        [Test]
        public void SectionStateStore_GetSet_RoundTrips()
        {
            string hostId = $"Host_{Guid.NewGuid():N}";
            string sectionId = "Core Setup";
            string key = ConvaiInspectorSectionStateStore.BuildKey(hostId, sectionId);

            EditorPrefs.DeleteKey(key);
            Assert.IsFalse(ConvaiInspectorSectionStateStore.Get(hostId, sectionId, false));

            ConvaiInspectorSectionStateStore.Set(hostId, sectionId, true);
            Assert.IsTrue(ConvaiInspectorSectionStateStore.Get(hostId, sectionId, false));

            ConvaiInspectorSectionStateStore.Set(hostId, sectionId, false);
            Assert.IsFalse(ConvaiInspectorSectionStateStore.Get(hostId, sectionId, true));

            EditorPrefs.DeleteKey(key);
        }

        [Test]
        public void SectionStateStore_BuildKey_NormalizesWhitespace()
        {
            string key = ConvaiInspectorSectionStateStore.BuildKey("Map Debug Window", "Validation Results");
            Assert.AreEqual("Convai.Editor.MapDebugWindow.ValidationResults.Expanded", key);
        }

        [Test]
        public void StyleCache_EnsureInitialized_ReusesStyleInstances()
        {
            ConvaiInspectorStyleCache.EnsureInitialized();
            GUIStyle firstHeader = ConvaiInspectorStyleCache.SectionHeaderLabelStyle;
            GUIStyle firstIcon = ConvaiInspectorStyleCache.SectionIconStyle;
            GUIStyle firstChevron = ConvaiInspectorStyleCache.SectionChevronStyle;

            ConvaiInspectorStyleCache.EnsureInitialized();

            Assert.AreSame(firstHeader, ConvaiInspectorStyleCache.SectionHeaderLabelStyle);
            Assert.AreSame(firstIcon, ConvaiInspectorStyleCache.SectionIconStyle);
            Assert.AreSame(firstChevron, ConvaiInspectorStyleCache.SectionChevronStyle);
        }

        [Test]
        public void IconProvider_GetConvaiIcon_ReturnsNonNullTexture()
        {
            Texture2D icon = ConvaiBrandedIconProvider.GetConvaiIcon();
            Assert.IsNotNull(icon);
        }

        [Test]
        public void ActionDebugPatchDraft_PreservesOmittedFieldsAndBuildsExplicitClears()
        {
            var draft = new ConvaiActionDebugPatchDraft
            {
                IncludeObjects = true,
                IncludeNestedAttention = true,
                NestedAttention = string.Empty
            };

            ConvaiActionConfigPatch patch = draft.BuildActionConfigPatch();

            Assert.IsNull(patch.Actions);
            Assert.IsEmpty(patch.Objects);
            Assert.IsNull(patch.Characters);
            Assert.AreEqual(string.Empty, patch.CurrentAttentionObject);
            Assert.IsNull(draft.BuildTopLevelAttention());
        }

        [Test]
        public void ActionDebugPatchDraft_ParsesActionLinesAndKeepsTopLevelOverrideIndependent()
        {
            var draft = new ConvaiActionDebugPatchDraft
            {
                IncludeActions = true,
                ActionsText = "  Move To  \n\nPick Up\r\n",
                IncludeTopLevelAttention = true,
                TopLevelAttention = "Cube"
            };

            ConvaiActionConfigPatch patch = draft.BuildActionConfigPatch();

            CollectionAssert.AreEqual(new[] { "Move To", "Pick Up" }, patch.Actions);
            Assert.IsNull(patch.CurrentAttentionObject);
            Assert.AreEqual("Cube", draft.BuildTopLevelAttention());
        }

        [Test]
        public void ActionDebugPatchDraft_LoadClonesConfirmedSnapshotAndBindings()
        {
            var cube = new GameObject("Cube");
            try
            {
                var config = new ConvaiActionConfig
                {
                    Actions = new() { "Move To" },
                    Objects = new()
                    {
                        new ConvaiActionObjectDefinition
                        {
                            Name = "Cube",
                            Description = "Target",
                            GameObjectReference = cube
                        }
                    },
                    CurrentAttentionObject = "Cube"
                };
                var draft = new ConvaiActionDebugPatchDraft();

                draft.Load(config);
                ConvaiActionConfigPatch patch = draft.BuildActionConfigPatch();

                Assert.IsTrue(draft.IncludeActions);
                Assert.IsTrue(draft.IncludeObjects);
                Assert.IsTrue(draft.IncludeCharacters);
                Assert.IsTrue(draft.IncludeNestedAttention);
                Assert.IsFalse(draft.IncludeTopLevelAttention);
                Assert.AreEqual("Move To", patch.Actions[0]);
                Assert.AreNotSame(config.Objects[0], patch.Objects[0]);
                Assert.AreSame(cube, patch.Objects[0].GameObjectReference);
                Assert.AreEqual("Cube", patch.CurrentAttentionObject);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cube);
            }
        }
    }
}
#endif
