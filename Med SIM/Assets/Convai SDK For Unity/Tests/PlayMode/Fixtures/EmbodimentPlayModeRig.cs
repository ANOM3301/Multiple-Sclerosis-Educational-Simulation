using Convai.Domain.EventSystem;
using Convai.Runtime.Embodiment;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Convai.Tests.PlayMode.Fixtures
{
    /// <summary>
    ///     Owns a single root <see cref="GameObject" /> with an <see cref="EmbodimentContext" />
    ///     for PlayMode integration and acceptance tests. Call <see cref="Destroy" /> in
    ///     <c>[UnityTearDown]</c> to prevent scene leaks.
    /// </summary>
    public sealed class EmbodimentPlayModeRig
    {
        private readonly GameObject _root;

        public EmbodimentContext Context { get; }

        private EmbodimentPlayModeRig(string name, IEventHub eventHub)
        {
            _root = new GameObject(name);
            Context = _root.AddComponent<EmbodimentContext>();
            Context.Populate(eventHub, null);
        }

        public static EmbodimentPlayModeRig Create(string name = "PlayModeRig", IEventHub eventHub = null)
            => new(name, eventHub);

        public T AddComponent<T>() where T : MonoBehaviour => _root.AddComponent<T>();

        public GameObject CreateChildObject(string childName = "child")
        {
            GameObject child = new(childName);
            child.transform.SetParent(_root.transform);
            return child;
        }

        public void Destroy()
        {
            if (_root != null)
                Object.Destroy(_root);
        }
    }
}
