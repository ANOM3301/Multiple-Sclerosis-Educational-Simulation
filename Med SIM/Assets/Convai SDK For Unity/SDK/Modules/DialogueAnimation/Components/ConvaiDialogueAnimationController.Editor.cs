#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ContractDefaults = Convai.Modules.DialogueAnimation.Runtime.DialogueAnimatorContract.Defaults;

namespace Convai.Modules.DialogueAnimation.Components
{
    /// <summary>
    ///     Editor-only validation and sanitization for
    ///     <see cref="ConvaiDialogueAnimationController" />. Kept in a separate partial so the
    ///     runtime file is free of inspector-time concerns.
    /// </summary>
    public sealed partial class ConvaiDialogueAnimationController
    {
        private void OnValidate()
        {
            if (!UnityEngine.Application.isPlaying && profile != null)
                ApplyProfileValues(profile);

            if (string.IsNullOrWhiteSpace(_baseIdleStateName)) _baseIdleStateName = ContractDefaults.BaseIdleStateName;
            if (string.IsNullOrWhiteSpace(_idleOverlayStateA)) _idleOverlayStateA = ContractDefaults.IdleOverlayStateA;
            if (string.IsNullOrWhiteSpace(_idleOverlayStateB)) _idleOverlayStateB = ContractDefaults.IdleOverlayStateB;
            if (string.IsNullOrWhiteSpace(_bodyTalkStateA)) _bodyTalkStateA = ContractDefaults.BodyTalkStateA;
            if (string.IsNullOrWhiteSpace(_bodyTalkStateB)) _bodyTalkStateB = ContractDefaults.BodyTalkStateB;
            if (string.IsNullOrWhiteSpace(_headTalkStateA)) _headTalkStateA = ContractDefaults.HeadTalkStateA;
            if (string.IsNullOrWhiteSpace(_headTalkStateB)) _headTalkStateB = ContractDefaults.HeadTalkStateB;

            if (string.IsNullOrWhiteSpace(_basePlaceholderName)) _basePlaceholderName = ContractDefaults.BasePlaceholderName;
            if (string.IsNullOrWhiteSpace(_idleOverlayPlaceholderA)) _idleOverlayPlaceholderA = ContractDefaults.IdleOverlayPlaceholderA;
            if (string.IsNullOrWhiteSpace(_idleOverlayPlaceholderB)) _idleOverlayPlaceholderB = ContractDefaults.IdleOverlayPlaceholderB;
            if (string.IsNullOrWhiteSpace(_bodyTalkPlaceholderA)) _bodyTalkPlaceholderA = ContractDefaults.BodyTalkPlaceholderA;
            if (string.IsNullOrWhiteSpace(_bodyTalkPlaceholderB)) _bodyTalkPlaceholderB = ContractDefaults.BodyTalkPlaceholderB;
            if (string.IsNullOrWhiteSpace(_headTalkPlaceholderA)) _headTalkPlaceholderA = ContractDefaults.HeadTalkPlaceholderA;
            if (string.IsNullOrWhiteSpace(_headTalkPlaceholderB)) _headTalkPlaceholderB = ContractDefaults.HeadTalkPlaceholderB;

            SanitizeLayerIndicesForEditor();
            RestoreTransientRuntimeOverrideInEditor();
        }

        /// <summary>
        ///     Keeps the four animator layer indices unique and within range so the contract
        ///     validator and conductor do not collide on duplicate writes.
        /// </summary>
        private void SanitizeLayerIndicesForEditor()
        {
            if (_idleOverlayLayerIndex == _baseIdleLayerIndex)
                _idleOverlayLayerIndex = _baseIdleLayerIndex + 1;

            Animator anim = _animatorOverride != null
                ? _animatorOverride
                : GetComponentInChildren<Animator>(true);

            int lc = anim != null ? anim.layerCount : 0;
            if (lc >= 4)
            {
                _baseIdleLayerIndex = Mathf.Clamp(_baseIdleLayerIndex, 0, lc - 1);
                _idleOverlayLayerIndex = Mathf.Clamp(_idleOverlayLayerIndex, 0, lc - 1);
                _bodyTalkLayerIndex = Mathf.Clamp(_bodyTalkLayerIndex, 0, lc - 1);
                _headTalkLayerIndex = Mathf.Clamp(_headTalkLayerIndex, 0, lc - 1);

                if (_idleOverlayLayerIndex == _baseIdleLayerIndex)
                    _idleOverlayLayerIndex = _baseIdleLayerIndex < lc - 1 ? _baseIdleLayerIndex + 1 : 0;

                if (_bodyTalkLayerIndex == _baseIdleLayerIndex || _bodyTalkLayerIndex == _idleOverlayLayerIndex)
                {
                    for (int i = 0; i < lc; i++)
                    {
                        if (i != _baseIdleLayerIndex && i != _idleOverlayLayerIndex)
                        {
                            _bodyTalkLayerIndex = i;
                            break;
                        }
                    }
                }

                if (_headTalkLayerIndex == _baseIdleLayerIndex
                    || _headTalkLayerIndex == _idleOverlayLayerIndex
                    || _headTalkLayerIndex == _bodyTalkLayerIndex)
                {
                    for (int i = 0; i < lc; i++)
                    {
                        if (i != _baseIdleLayerIndex && i != _idleOverlayLayerIndex && i != _bodyTalkLayerIndex)
                        {
                            _headTalkLayerIndex = i;
                            break;
                        }
                    }
                }

                return;
            }

            if (lc > 0)
            {
                _baseIdleLayerIndex = Mathf.Clamp(_baseIdleLayerIndex, 0, lc - 1);
                _idleOverlayLayerIndex = Mathf.Clamp(_idleOverlayLayerIndex, 0, lc - 1);
                _bodyTalkLayerIndex = Mathf.Clamp(_bodyTalkLayerIndex, 0, lc - 1);
                _headTalkLayerIndex = Mathf.Clamp(_headTalkLayerIndex, 0, lc - 1);
            }
            else
            {
                _idleOverlayLayerIndex = Mathf.Max(_idleOverlayLayerIndex, _baseIdleLayerIndex + 1);
                if (_bodyTalkLayerIndex <= _idleOverlayLayerIndex)
                    _bodyTalkLayerIndex = _idleOverlayLayerIndex + 1;
                if (_headTalkLayerIndex <= _bodyTalkLayerIndex)
                    _headTalkLayerIndex = _bodyTalkLayerIndex + 1;
            }
        }

        private void RestoreTransientRuntimeOverrideInEditor()
        {
            if (UnityEngine.Application.isPlaying) return;

            Animator anim = _animatorOverride != null
                ? _animatorOverride
                : GetComponentInChildren<Animator>(true);

            if (anim == null) return;
            if (anim.runtimeAnimatorController is not AnimatorOverrideController overrideController) return;
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(overrideController))) return;

            RuntimeAnimatorController baseController = overrideController.runtimeAnimatorController;
            if (baseController == null) return;

            Undo.RecordObject(anim, "Restore Convai Runtime Animator Controller");
            anim.runtimeAnimatorController = baseController;
            EditorUtility.SetDirty(anim);
            DestroyImmediate(overrideController);
        }
    }
}
#endif
