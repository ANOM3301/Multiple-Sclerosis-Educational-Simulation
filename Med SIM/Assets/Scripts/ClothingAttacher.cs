using UnityEngine;

public class ClothingAttacher : MonoBehaviour
{
    // Drag your character's main SkinnedMeshRenderer (the body) here
    public SkinnedMeshRenderer targetBodyRenderer;
    // Drag your T-Shirt's SkinnedMeshRenderer here
    public SkinnedMeshRenderer clothingRenderer;

    void Start()
    {
        if (targetBodyRenderer == null || clothingRenderer == null) return;

        // 1. Match the Root Bone
        clothingRenderer.rootBone = targetBodyRenderer.rootBone;

        // 2. Remap the hidden internal bones array to match the main rig
        Transform[] currentBones = targetBodyRenderer.bones;
        Transform[] newBones = new Transform[clothingRenderer.bones.Length];

        for (int i = 0; i < clothingRenderer.bones.Length; i++)
        {
            // Find the matching bone by name in the target body rig
            newBones[i] = FindBoneByName(currentBones, clothingRenderer.bones[i].name);
        }

        clothingRenderer.bones = newBones;
    }

    private Transform FindBoneByName(Transform[] bodyBones, string boneName)
    {
        foreach (var bone in bodyBones)
        {
            if (bone.name == boneName) return bone;
        }
        return null;
    }
}
