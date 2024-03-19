using System;
using System.Collections.Generic;
using UnityEngine;

namespace DifficultClimbingVRM.PoseSyncing
{
    /// <summary>
    /// A simpler way of representing the vrm bonemap tuple.
    /// </summary>
    internal record struct BoneMap
    {
        public Transform Transform { get; set; }
        public HumanBodyBones Bone { get; set; }

        public BoneMap(Transform transform, HumanBodyBones bone)
        {
            Transform = transform;
            Bone = bone;
        }

        public static implicit operator (Transform, HumanBodyBones)(BoneMap value)
        {
            return (value.Transform, value.Bone);
        }

        public static implicit operator BoneMap((Transform, HumanBodyBones) value)
        {
            return new BoneMap(value.Item1, value.Item2);
        }

        public static implicit operator BoneMap(KeyValuePair<Transform, HumanBodyBones> value)
        {
            return new BoneMap(value.Key, value.Value);
        }

        public static IEnumerable<BoneMap> GetBoneMappingFromAnimator(Animator animator)
        {
            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                Transform boneTransform = animator.GetBoneTransform(bone);
                if (boneTransform != null)
                {
                    yield return new BoneMap(boneTransform, bone);
                }
            }
        }
    }
}
