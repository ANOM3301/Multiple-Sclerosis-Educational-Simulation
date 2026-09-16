using System;
using System.Reflection;
using Convai.Runtime.Vision.Sources;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Convai.Tests.EditMode.Runtime
{
    [TestFixture]
    public class VisionSourcePerformanceTests
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName}");
            field.SetValue(target, value);
        }

        private static object GetField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName}");
            return field.GetValue(target);
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
            Assert.That(method, Is.Not.Null, $"Missing method {methodName}");
            return method.Invoke(target, args);
        }

        [Test]
        public void Webcam_TargetFrameRate_ClampsToMinimum()
        {
            var gameObject = new GameObject("WebcamVisionPerfTest");
            try
            {
                var source = gameObject.AddComponent<WebcamVisionFrameSource>();
                var statusProvider = source as IVisionFrameSourceStatusProvider;
                SetField(source, "_requestedFps", 0);

                Assert.That(statusProvider, Is.Not.Null);
                Assert.That(statusProvider.State, Is.EqualTo(VisionSourceState.Idle));
                Assert.That(source.TargetFrameRate, Is.EqualTo(1f));

                SetField(source, "_requestedFps", -10);

                Assert.That(source.TargetFrameRate, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Webcam_OutputDimensions_RespectMaxCap()
        {
            var gameObject = new GameObject("WebcamVisionCapTest");
            try
            {
                var source = gameObject.AddComponent<WebcamVisionFrameSource>();
                SetField(source, "_maxOutputWidth", 640);
                SetField(source, "_maxOutputHeight", 360);

                object dimensions = Invoke(source, "ResolveOutputDimensions", 1920, 1080, 0);

                Assert.That(dimensions.ToString(), Does.Contain("640").And.Contain("360"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Webcam_RenderTexture_IsReusedWhenDimensionsUnchanged()
        {
            var gameObject = new GameObject("WebcamVisionReuseTest");
            try
            {
                var source = gameObject.AddComponent<WebcamVisionFrameSource>();

                Invoke(source, "EnsureRenderTexture", 640, 480, 0);
                RenderTexture first = source.CurrentRenderTexture;

                Invoke(source, "EnsureRenderTexture", 640, 480, 0);

                Assert.That(source.CurrentRenderTexture, Is.SameAs(first));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Webcam_CaptureCadence_UsesClampedTargetFrameRate()
        {
            var gameObject = new GameObject("WebcamVisionCadenceTest");
            try
            {
                var source = gameObject.AddComponent<WebcamVisionFrameSource>();
                SetField(source, "_requestedFps", 0);
                SetField(source, "_nextCaptureTime", 5f);

                Assert.That((bool)Invoke(source, "ShouldCaptureFrameAt", 4.99f), Is.False);
                Assert.That((bool)Invoke(source, "ShouldCaptureFrameAt", 5f), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Webcam_OnValidate_ClampsCaptureAndOutputSettings()
        {
            var gameObject = new GameObject("WebcamVisionValidateTest");
            try
            {
                var source = gameObject.AddComponent<WebcamVisionFrameSource>();
                SetField(source, "_requestedWidth", -1);
                SetField(source, "_requestedHeight", -1);
                SetField(source, "_requestedFps", -1);
                SetField(source, "_maxOutputWidth", -1);
                SetField(source, "_maxOutputHeight", -1);

                Invoke(source, "OnValidate");

                Assert.That(GetField(source, "_requestedWidth"), Is.EqualTo(1));
                Assert.That(GetField(source, "_requestedHeight"), Is.EqualTo(1));
                Assert.That(GetField(source, "_requestedFps"), Is.EqualTo(1));
                Assert.That(GetField(source, "_maxOutputWidth"), Is.EqualTo(0));
                Assert.That(GetField(source, "_maxOutputHeight"), Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Quest_TargetFrameRate_ClampsAndOnValidateClampsOutput()
        {
            var gameObject = new GameObject("QuestVisionValidateTest");
            try
            {
                var source = gameObject.AddComponent<QuestVisionFrameSource>();
                var statusProvider = source as IVisionFrameSourceStatusProvider;
                SetField(source, "_targetFrameRate", 0);
                SetField(source, "_maxOutputWidth", -1);
                SetField(source, "_maxOutputHeight", -1);

                Invoke(source, "OnValidate");

                Assert.That(statusProvider, Is.Not.Null);
                Assert.That(statusProvider.State, Is.EqualTo(VisionSourceState.Idle));
                Assert.That(source.TargetFrameRate, Is.EqualTo(1f));
                Assert.That(GetField(source, "_maxOutputWidth"), Is.EqualTo(0));
                Assert.That(GetField(source, "_maxOutputHeight"), Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Quest_Discovery_IgnoresInactiveAndNotPlayingCandidates()
        {
            var sourceObject = new GameObject("QuestVisionSourceTest");
            var inactiveObject = new GameObject("InactivePassthroughCandidate");
            var notPlayingObject = new GameObject("NotPlayingPassthroughCandidate");
            var playingObject = new GameObject("PlayingPassthroughCandidate");

            try
            {
                var source = sourceObject.AddComponent<QuestVisionFrameSource>();
                inactiveObject.SetActive(false);
                var inactiveCandidate = inactiveObject.AddComponent<PassthroughCameraAccess>();
                inactiveCandidate.IsPlaying = true;

                var notPlayingCandidate = notPlayingObject.AddComponent<PassthroughCameraAccess>();
                notPlayingCandidate.IsPlaying = false;

                var playingCandidate = playingObject.AddComponent<PassthroughCameraAccess>();
                playingCandidate.IsPlaying = true;

                Invoke(source, "BindPassthroughAccess", true);

                Assert.That(GetField(source, "_passthroughCameraAccess"), Is.SameAs(playingCandidate));
            }
            finally
            {
                Object.DestroyImmediate(sourceObject);
                Object.DestroyImmediate(inactiveObject);
                Object.DestroyImmediate(notPlayingObject);
                Object.DestroyImmediate(playingObject);
            }
        }

        [Test]
        public void Quest_CachesReflectionDelegatesWhenBinding()
        {
            var sourceObject = new GameObject("QuestVisionDelegateTest");
            var accessObject = new GameObject("QuestVisionAccessTest");

            try
            {
                var source = sourceObject.AddComponent<QuestVisionFrameSource>();
                var access = accessObject.AddComponent<PassthroughCameraAccess>();
                access.IsPlaying = true;
                SetField(source, "_passthroughCameraAccess", access);

                Invoke(source, "BindPassthroughAccess", false);

                Assert.That(GetField(source, "_getTexture"), Is.Not.Null);
                Assert.That(GetField(source, "_getIsPlaying"), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(sourceObject);
                Object.DestroyImmediate(accessObject);
            }
        }

        [Test]
        public void Quest_BindRetry_IsBoundedDuringCapture()
        {
            var gameObject = new GameObject("QuestVisionRetryTest");
            try
            {
                var source = gameObject.AddComponent<QuestVisionFrameSource>();
                var statusProvider = (IVisionFrameSourceStatusProvider)source;

                source.StartCapture();

                Assert.That(statusProvider.State, Is.EqualTo(VisionSourceState.Failed));
                Assert.That(statusProvider.ErrorKind, Is.EqualTo(VisionSourceErrorKind.InvalidConfiguration));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        public sealed class PassthroughCameraAccess : MonoBehaviour
        {
            public bool IsPlaying { get; set; }

            public Texture GetTexture() => Texture2D.whiteTexture;
        }
    }
}
