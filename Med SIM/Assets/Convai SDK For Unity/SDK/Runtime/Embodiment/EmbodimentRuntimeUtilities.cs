using UnityEngine;

namespace Convai.Runtime.Embodiment
{
    /// <summary>
    ///     Centralized execution order contract for every Convai embodiment component. Phases
    ///     are grouped into three bands so the Animator pass always sees embodiment state in
    ///     the right order: composition root and lifecycle helpers run first (negative band),
    ///     per-frame cognition modules run during <c>Update</c> (positive band), and
    ///     procedural pose drivers + the facial compositor run after the humanoid pass
    ///     (high positive band).
    /// </summary>
    /// <remarks>
    ///     Adding a new embodiment component? Place its execution order constant here and
    ///     reference it via <c>[DefaultExecutionOrder(EmbodimentExecutionOrders.X)]</c> rather
    ///     than a magic number so the cross-module ordering stays auditable in one file.
    /// </remarks>
    public static class EmbodimentExecutionOrders
    {
        // Composition root + lifecycle band ----------------------------------
        /// <summary>Embodiment context — runs first so modules can resolve it during Awake.</summary>
        public const int Context = -1000;

        /// <summary>Character embodiment binding — applies presets before any module reads its profile.</summary>
        public const int Binding = -500;

        // Cognition band (Update phase) --------------------------------------
        /// <summary>Conversation flow — folds upstream signals into the dialogue state machine.</summary>
        public const int ConversationFlow = 100;

        /// <summary>Attention — selects the focus target consumed by gaze.</summary>
        public const int Attention = 200;

        /// <summary>Emotion — folds reactions into the smoothed emotion reading.</summary>
        public const int Emotion = 300;

        /// <summary>Dialogue animation — picks talk / idle clips for the next animator tick.</summary>
        public const int DialogueAnimation = 400;

        /// <summary>Gaze coordinator — blends authority weights consumed by the eye/head actuators.</summary>
        public const int GazeCoordinator = 500;

        // Late pose + compositor band (LateUpdate phase) ---------------------
        /// <summary>Embodiment tick scheduler — flushes deterministic per-frame ticks before pose actuators.</summary>
        public const int TickScheduler = 18000;

        /// <summary>Animator conductor — single-writer pass over animator layer weights.</summary>
        public const int AnimatorConductor = 19000;

        /// <summary>Procedural body pose adjustments.</summary>
        public const int BodyPose = 19300;

        /// <summary>Head look actuator — runs after Animator pose, before eye gaze.</summary>
        public const int HeadLook = 19400;

        /// <summary>Eye gaze actuator — runs immediately before the facial compositor flushes.</summary>
        public const int EyeGaze = 19500;

        /// <summary>Facial blendshape compositor host — flushes composed weights to the meshes.</summary>
        public const int FacialCompositor = 20000;
    }

    /// <summary>
    ///     Small deterministic random stream used by realtime embodiment components so they
    ///     do not perturb UnityEngine.Random's global state.
    /// </summary>
    public struct DeterministicEmbodimentRandom
    {
        private uint _state;

        public DeterministicEmbodimentRandom(uint seed)
        {
            _state = seed != 0u ? seed : 0x9E3779B9u;
        }

        public float Value => NextUnit();

        public float Range(float minInclusive, float maxInclusive)
        {
            if (maxInclusive <= minInclusive) return minInclusive;
            return Mathf.Lerp(minInclusive, maxInclusive, NextUnit());
        }

        /// <summary>
        ///     Deterministic <c>[0, 1)</c> draw from a single LCG step on <paramref name="seed" />.
        ///     Implements weighted random selection consistent with dialogue variant selection so weighted
        ///     picks stay bit-stable across refactors.
        /// </summary>
        public static float UnitDrawFromLcgSeed(uint seed)
        {
            unchecked
            {
                uint advanced = seed * 1664525u + 1013904223u;
                return Mathf.Clamp01((advanced & 0xFFFFFFu) / (float)0x1000000u);
            }
        }

        public uint NextUInt()
        {
            unchecked
            {
                _state = _state * 1664525u + 1013904223u;
                return _state;
            }
        }

        public static DeterministicEmbodimentRandom Create(Component component, uint salt = 0u) =>
            new(CreateSeed(component, salt));

        public static uint CreateSeed(Component component, uint salt = 0u)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = Mix(hash, salt);
                if (component != null)
                {
                    hash = Mix(hash, component.GetType().FullName);
                    Transform t = component.transform;
                    while (t != null)
                    {
                        hash = Mix(hash, t.name);
                        hash = Mix(hash, (uint)t.GetSiblingIndex());
                        t = t.parent;
                    }
                }
                return hash != 0u ? hash : 0x9E3779B9u;
            }
        }

        public static int StableStringHash(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            unchecked
            {
                uint hash = 2166136261u;
                hash = Mix(hash, value);
                return (int)hash;
            }
        }

        private float NextUnit()
        {
            unchecked
            {
                return (NextUInt() & 0xFFFFFFu) / (float)0x1000000u;
            }
        }

        private static uint Mix(uint hash, uint value)
        {
            unchecked
            {
                hash ^= value;
                return hash * 16777619u;
            }
        }

        private static uint Mix(uint hash, string value)
        {
            if (string.IsNullOrEmpty(value)) return Mix(hash, 0u);
            unchecked
            {
                for (int i = 0; i < value.Length; i++)
                    hash = Mix(hash, value[i]);
                return hash;
            }
        }
    }
}
