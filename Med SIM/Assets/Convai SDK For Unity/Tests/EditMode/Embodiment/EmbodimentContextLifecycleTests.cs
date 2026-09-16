using Convai.Runtime.Embodiment;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Convai.Tests.EditMode.Embodiment
{
    public sealed class EmbodimentContextLifecycleTests
    {
        [Test]
        public void TryResolve_WithNullOrigin_ReturnsFalse()
        {
            bool result = EmbodimentContext.TryResolve(null, out EmbodimentContext ctx);
            Assert.IsFalse(result);
            Assert.IsNull(ctx);
        }

        [Test]
        public void TryResolve_OnPlainGameObject_CreatesContextOnSameObject()
        {
            GameObject root = new("LifecycleTest_Create");
            try
            {
                Stub stub = root.AddComponent<Stub>();
                bool resolved = EmbodimentContext.TryResolve(stub, out EmbodimentContext ctx);

                Assert.IsTrue(resolved);
                Assert.IsNotNull(ctx);
                Assert.AreSame(root, ctx.gameObject);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void TryResolve_CalledTwice_ReturnsSameInstance()
        {
            GameObject root = new("LifecycleTest_Idempotent");
            try
            {
                Stub stub = root.AddComponent<Stub>();
                EmbodimentContext.TryResolve(stub, out EmbodimentContext first);
                EmbodimentContext.TryResolve(stub, out EmbodimentContext second);

                Assert.IsNotNull(first);
                Assert.AreSame(first, second);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void TryResolve_ChildComponent_FindsParentContext()
        {
            GameObject parent = new("LifecycleTest_Parent");
            GameObject child = new("LifecycleTest_Child");
            child.transform.SetParent(parent.transform);
            try
            {
                EmbodimentContext parentCtx = parent.AddComponent<EmbodimentContext>();
                Stub childStub = child.AddComponent<Stub>();

                EmbodimentContext.TryResolve(childStub, out EmbodimentContext resolved);

                Assert.AreSame(parentCtx, resolved);
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void CreatedContext_HasHideInInspectorFlag()
        {
            GameObject root = new("LifecycleTest_HideFlags");
            try
            {
                Stub stub = root.AddComponent<Stub>();
                EmbodimentContext.TryResolve(stub, out EmbodimentContext ctx);

                Assert.IsTrue((ctx.hideFlags & HideFlags.HideInInspector) != 0);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Populate_FiresDependenciesPopulated()
        {
            GameObject root = new("LifecycleTest_Populate");
            try
            {
                EmbodimentContext ctx = root.AddComponent<EmbodimentContext>();
                bool fired = false;
                ctx.DependenciesPopulated += () => fired = true;

                ctx.Populate(null, null);

                Assert.IsTrue(fired);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void NotifyEmbodimentConfigurationChanged_FiresEventOnEachCall()
        {
            GameObject root = new("LifecycleTest_ConfigChanged");
            try
            {
                EmbodimentContext ctx = root.AddComponent<EmbodimentContext>();
                int count = 0;
                ctx.EmbodimentConfigurationChanged += () => count++;

                ctx.NotifyEmbodimentConfigurationChanged();
                ctx.NotifyEmbodimentConfigurationChanged();

                Assert.AreEqual(2, count);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void AddedManuallyToGameObject_ContextIsNotNull()
        {
            GameObject root = new("LifecycleTest_ManualAdd");
            try
            {
                EmbodimentContext ctx = root.AddComponent<EmbodimentContext>();
                Assert.IsNotNull(ctx);
                Assert.AreSame(root, ctx.gameObject);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private sealed class Stub : MonoBehaviour { }
    }
}
