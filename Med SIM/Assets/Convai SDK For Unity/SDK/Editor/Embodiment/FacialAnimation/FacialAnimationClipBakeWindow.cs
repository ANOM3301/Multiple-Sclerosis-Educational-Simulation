#if UNITY_EDITOR
using System.Collections.Generic;
using Convai.Modules.FacialAnimation.Profiles;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.Embodiment.FacialAnimation
{
    /// <summary>
    ///     Extracts SkinnedMeshRenderer blendshape curves from an AnimationClip and imports
    ///     them into a ConvaiFacialAnimationProfile for ConvaiFacialClipPlayer playback.
    /// </summary>
    public sealed class FacialAnimationClipBakeWindow : EditorWindow
    {
        private const string DefaultMouthPatterns = "Mouth;Lip;Tongue;Jaw";
        private const string WindowTitle = "Facial Clip Baker";

        private AnimationClip _sourceClip;
        private ConvaiFacialAnimationProfile _targetProfile;
        private bool _loop = true;
        private string _mouthPatterns = DefaultMouthPatterns;
        private Vector2 _scrollPosition;
        private List<DiscoveredFacialCurveBinding> _discoveredBindings = new();
        private bool _analyzed;

        /// <summary>
        ///     Not exposed via the menu bar. Call from internal tooling in this assembly or add
        ///     <c>CONVAI_INTERNAL_FACIAL_CLIP_BAKE_WINDOW</c> to scripting defines for a developer menu.
        /// </summary>
        internal static void ShowWindow()
        {
            FacialAnimationClipBakeWindow window =
                GetWindow<FacialAnimationClipBakeWindow>(false, WindowTitle, true);
            window.minSize = new Vector2(500f, 430f);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Facial Clip Bake Tool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Extracts SkinnedMeshRenderer blendshape curves from an AnimationClip and imports them into a ConvaiFacialAnimationProfile. " +
                "Curves preserve the source clip's seconds, weights, tangents, and wrap modes for ConvaiFacialClipPlayer playback.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _sourceClip = (AnimationClip)EditorGUILayout.ObjectField(
                "Source Animation Clip",
                _sourceClip,
                typeof(AnimationClip),
                false);

            EditorGUILayout.BeginHorizontal();
            _targetProfile = (ConvaiFacialAnimationProfile)EditorGUILayout.ObjectField(
                "Target Profile",
                _targetProfile,
                typeof(ConvaiFacialAnimationProfile),
                false);

            using (new EditorGUI.DisabledGroupScope(_sourceClip == null))
            {
                if (GUILayout.Button("Create", GUILayout.Width(72f)))
                    CreateTargetProfile();
            }

            EditorGUILayout.EndHorizontal();

            _loop = EditorGUILayout.Toggle("Loop", _loop);
            _mouthPatterns = EditorGUILayout.TextField("Mouth Patterns", _mouthPatterns);
            if (EditorGUI.EndChangeCheck())
                _analyzed = false;

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledGroupScope(_sourceClip == null))
            {
                if (GUILayout.Button("Analyze Clip", GUILayout.Height(28f)))
                    AnalyzeClip();
            }

            if (_analyzed)
                DrawBindingsList();

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledGroupScope(!CanBake))
            {
                if (GUILayout.Button("Bake to Profile", GUILayout.Height(32f)))
                    BakeToProfile();
            }
        }

        private bool CanBake =>
            _sourceClip != null &&
            _targetProfile != null &&
            _analyzed &&
            _discoveredBindings != null &&
            _discoveredBindings.Count > 0;

        private void AnalyzeClip()
        {
            _discoveredBindings = FacialAnimationClipBakeUtility.Analyze(_sourceClip, _mouthPatterns);
            _analyzed = true;
        }

        private void DrawBindingsList()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField($"Discovered Bindings: {_discoveredBindings.Count}", EditorStyles.boldLabel);

            if (_discoveredBindings.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No SkinnedMeshRenderer blendshape bindings were found in the selected AnimationClip.",
                    MessageType.Warning);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.MaxHeight(300f));

            for (int i = 0; i < _discoveredBindings.Count; i++)
            {
                DiscoveredFacialCurveBinding binding = _discoveredBindings[i];
                EditorGUILayout.BeginHorizontal();

                binding.Include = EditorGUILayout.Toggle(binding.Include, GUILayout.Width(20f));
                EditorGUILayout.LabelField(FormatBindingLabel(binding), GUILayout.MinWidth(240f));

                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.LabelField(binding.IsMouth ? "Mouth" : "General", GUILayout.Width(58f));

                binding.IsMouth = EditorGUILayout.ToggleLeft("Mouth", binding.IsMouth, GUILayout.Width(75f));

                EditorGUILayout.EndHorizontal();
                _discoveredBindings[i] = binding;
            }

            EditorGUILayout.EndScrollView();
        }

        private void BakeToProfile()
        {
            if (!CanBake)
                return;

            List<ConvaiFacialAnimationProfile.CurveBinding> bindings =
                FacialAnimationClipBakeUtility.CreateProfileBindings(_discoveredBindings, _sourceClip.length);

            Undo.RecordObject(_targetProfile, "Bake Facial Animation Profile");
            _targetProfile.SetBindings(bindings, _sourceClip.length, _loop);
            EditorUtility.SetDirty(_targetProfile);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[ConvaiFacialClipBaker] Baked {bindings.Count} blendshape curves from '{_sourceClip.name}' " +
                $"into ConvaiFacialAnimationProfile '{_targetProfile.name}' (duration: {_sourceClip.length:F3}s, loop: {_loop}).");
        }

        private void CreateTargetProfile()
        {
            string defaultName = _sourceClip != null
                ? $"{_sourceClip.name}_ConvaiFacialAnimationProfile.asset"
                : "ConvaiFacialAnimationProfile.asset";

            string path = EditorUtility.SaveFilePanelInProject(
                "Create Facial Animation Profile",
                defaultName,
                "asset",
                "Choose where the baked ConvaiFacialAnimationProfile asset should be saved.");

            if (string.IsNullOrEmpty(path))
                return;

            var profile = CreateInstance<ConvaiFacialAnimationProfile>();
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            _targetProfile = profile;
            Selection.activeObject = profile;
        }

        private static string FormatBindingLabel(DiscoveredFacialCurveBinding binding)
        {
            return string.IsNullOrEmpty(binding.RelativePath)
                ? binding.BlendshapeName
                : $"{binding.RelativePath} / {binding.BlendshapeName}";
        }
    }

#if CONVAI_INTERNAL_FACIAL_CLIP_BAKE_WINDOW
    internal static class FacialAnimationClipBakeWindowMenu
    {
        [MenuItem("Convai/Developer/Facial Clip Bake Tool", priority = 220)]
        private static void OpenFromMenu() => FacialAnimationClipBakeWindow.ShowWindow();
    }
#endif
}
#endif
