using System.Collections.Generic;
using System.Reflection;
using Convai.Modules.DialogueAnimation.Core;
using Convai.Modules.DialogueAnimation.Runtime.Driver;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Convai.Tests.EditMode.DialogueAnimation
{
    /// <summary>
    ///     Verifies pool partitioning and library-index resolution in
    ///     <see cref="DialogueAnimationClipPicker" />. Pool membership is the only behavior
    ///     the picker controls directly; selection delegates to <see cref="IDialogueVariantSelector" />.
    /// </summary>
    public sealed class DialogueAnimationClipPickerTests
    {
        [Test]
        public void RebuildHeadTalkEligibleScratch_KeepsHeadOnlyAndCombined_DropsBodyOnly()
        {
            AnimationClip headOnly = MakeClip("HeadOnly");
            AnimationClip combined = MakeClip("Combined");
            AnimationClip bodyOnly = MakeClip("BodyOnly");
            AnimationClip invalid = null;

            var entries = new List<DialogueClipEntry>
            {
                MakeEntry(headOnly, DialogueTalkBodyCoverage.HeadOnly),
                MakeEntry(bodyOnly, DialogueTalkBodyCoverage.BodyOnly),
                MakeEntry(combined, DialogueTalkBodyCoverage.BodyAndHead),
                MakeEntry(invalid, DialogueTalkBodyCoverage.HeadOnly),
            };

            DialogueAnimationClipPicker picker = new();
            try
            {
                picker.RebuildHeadTalkEligibleScratch(entries);

                Assert.AreEqual(2, picker.HeadTalkEligibleCount);
                Assert.AreEqual(0, picker.ResolveHeadTalkLibraryIndex(0));
                Assert.AreEqual(2, picker.ResolveHeadTalkLibraryIndex(1));
                Assert.AreEqual(-1, picker.ResolveHeadTalkLibraryIndex(2));
            }
            finally
            {
                Object.DestroyImmediate(headOnly);
                Object.DestroyImmediate(combined);
                Object.DestroyImmediate(bodyOnly);
            }
        }

        [Test]
        public void RebuildBodyOnlyTalkScratch_KeepsOnlyBodyOnly()
        {
            AnimationClip headOnly = MakeClip("HeadOnly");
            AnimationClip bodyA = MakeClip("BodyA");
            AnimationClip bodyB = MakeClip("BodyB");

            var entries = new List<DialogueClipEntry>
            {
                MakeEntry(headOnly, DialogueTalkBodyCoverage.HeadOnly),
                MakeEntry(bodyA, DialogueTalkBodyCoverage.BodyOnly),
                MakeEntry(bodyB, DialogueTalkBodyCoverage.BodyOnly),
            };

            DialogueAnimationClipPicker picker = new();
            try
            {
                picker.RebuildBodyOnlyTalkScratch(entries);

                Assert.AreEqual(2, picker.BodyOnlyTalkCount);
                Assert.AreEqual(1, picker.ResolveBodyOnlyLibraryIndex(0));
                Assert.AreEqual(2, picker.ResolveBodyOnlyLibraryIndex(1));
            }
            finally
            {
                Object.DestroyImmediate(headOnly);
                Object.DestroyImmediate(bodyA);
                Object.DestroyImmediate(bodyB);
            }
        }

        [Test]
        public void ResetPickIndices_ClearsBothPoolHistories()
        {
            DialogueAnimationClipPicker picker = new();
            picker.LastHeadTalkEligiblePickIndex = 5;
            picker.LastBodyOnlyPickIndex = 7;

            picker.ResetPickIndices();

            Assert.AreEqual(-1, picker.LastHeadTalkEligiblePickIndex);
            Assert.AreEqual(-1, picker.LastBodyOnlyPickIndex);
        }

        [Test]
        public void Rebuild_NullPool_LeavesEmptyScratch()
        {
            DialogueAnimationClipPicker picker = new();
            picker.RebuildHeadTalkEligibleScratch(null);
            picker.RebuildBodyOnlyTalkScratch(null);

            Assert.AreEqual(0, picker.HeadTalkEligibleCount);
            Assert.AreEqual(0, picker.BodyOnlyTalkCount);
        }

        private static AnimationClip MakeClip(string name) => new() { name = name };

        private static DialogueClipEntry MakeEntry(AnimationClip clip, DialogueTalkBodyCoverage coverage)
        {
            object boxed = new DialogueClipEntry();
            SetField(boxed, "_clip", clip);
            SetField(boxed, "_talkBodyCoverage", coverage);
            SetField(boxed, "_selectionWeight", 1f);
            return (DialogueClipEntry)boxed;
        }

        private static void SetField(object boxed, string name, object value)
        {
            FieldInfo field = typeof(DialogueClipEntry)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"DialogueClipEntry.{name} not found via reflection.");
            field.SetValue(boxed, value);
        }
    }
}
