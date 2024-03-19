using DifficultClimbingVRM.Extensions;
using DifficultClimbingVRM.PoseSyncing;
using System;
using System.Linq;
using UnityEngine;

namespace DifficultClimbingVRM
{
    /// <summary>
    /// Class for synchronizing a pose between two characters.
    /// </summary>
    internal partial class PoseSynchronizer : MonoBehaviour
    {
        // Bones that are always locked to origin animators bone positions
        private HumanBodyBones[] lockedBones = new HumanBodyBones[0];

        // Animators
        public Animator OriginAnimator { get => originAnimator; set { originAnimator = value; reinitialize = true; } }
        public Animator TargetAnimator { get => targetAnimator; set { targetAnimator = value; reinitialize = true; } }

        [SerializeField] private Animator originAnimator;
        [SerializeField] private Animator targetAnimator;

        // Posing
        private HumanPoseHandler originPoseHandler;
        private HumanPoseHandler targetPoseHandler;

        // Way to not have to find target characters bone every frame
        private BoneMap[] targetCharacter;

        // Used to call start again if anything changed any of the animators
        private bool reinitialize = false;

        private void Start()
        {
            // We'd rather not run into NullReferenceException's
            if (originAnimator == null || targetAnimator == null)
                return;

            // Dispose pose handlers if set
            originPoseHandler?.Dispose();
            targetPoseHandler?.Dispose();

            // This is the main method we're using for mimicking the pose
            originPoseHandler = new HumanPoseHandler(originAnimator.avatar, originAnimator.avatarRoot);
            targetPoseHandler = new HumanPoseHandler(targetAnimator.avatar, targetAnimator.transform);

            // Calculate new bone mappings if none are applied
            if (targetCharacter == null)
                targetCharacter = BoneMap.GetBoneMappingFromAnimator(targetAnimator).ToArray();

            reinitialize = false;
        }

        private void LateUpdate()
        {
            // If we've requested a reinitialization call start again
            if (reinitialize)
                Start();

            // Copy pose from original humanoid to target
            ApplyPoseFromOriginToTarget(originPoseHandler, targetPoseHandler, targetAnimator, originAnimator, targetCharacter, lockedBones, AlignmentOptions.AlignShoulder);
        }

        private void OnEnable()
        {
            if (originAnimator == null || targetAnimator == null)
                return;

            reinitialize = true;
        }

        private void OnDisable()
        {
            originPoseHandler.Dispose();
            targetPoseHandler.Dispose();
        }

        /// <summary>
        /// Applies the pose from the origin character to the target character.
        /// </summary>
        /// <param name="originPoseHandler">The <see cref="HumanPoseHandler"/> of the origin character.</param>
        /// <param name="targetPoseHandler">The <see cref="HumanPoseHandler"/> of the target character.</param>
        /// <param name="targetAnimator">The <see cref="Animator"/> component of the target character.</param>
        /// <param name="originAnimator">The <see cref="Animator"/> component of the origin character.</param>
        /// <param name="targetCharacter">A <see cref="BoneMap"/> array representing the bones of the target character.</param>
        /// <param name="positionBones">An array of <see cref="HumanBodyBones"/> specifying which bones should have its positions set.</param>
        private void ApplyPoseFromOriginToTarget(HumanPoseHandler originPoseHandler, HumanPoseHandler targetPoseHandler, Animator targetAnimator, Animator originAnimator, BoneMap[] targetCharacter, HumanBodyBones[] positionBones, AlignmentOptions alignment)
        {
            if (originPoseHandler == null || targetPoseHandler == null)
                return;

            if (targetAnimator == null || originAnimator == null)
                return;

            targetAnimator.transform.position = originAnimator.transform.position;
            targetAnimator.transform.rotation = originAnimator.transform.rotation;

            HumanPose originPose = new HumanPose();
            originPoseHandler.GetHumanPose(ref originPose);
            targetPoseHandler.SetHumanPose(ref originPose);

            Vector3 heightAdjustment = CalculateHeightAdjustments(targetAnimator, originAnimator, alignment);

            foreach (BoneMap boneMap in targetCharacter)
            {
                Transform targetBone = boneMap.Transform;
                Transform originBone = originAnimator.GetBoneTransform(boneMap.Bone);

                if (targetBone != null && originBone != null)
                {
                    if (boneMap.Bone == HumanBodyBones.Hips)
                    {
                        // Adjust hip position to match target height
                        targetBone.position += heightAdjustment;
                    }
                    else if (boneMap.Bone.IsOneOf(positionBones))
                    {
                        targetBone.position = originBone.position;
                    }
                }
            }
        }

        /// <summary>
        /// Calculates the distance the hips need to be adjusted by based on the selected alignment option.
        /// </summary>
        /// <param name="targetAnimator">The animator of the character the pose gets applied to</param>
        /// <param name="originAnimator">The animator of the original character the pose gets copied from</param>
        /// <param name="alignment">The vertical alignment used for the 3D model</param>
        /// <returns>The distance the hips need to be adjusted to align correctly</returns>
        private Vector3 CalculateHeightAdjustments(Animator targetAnimator, Animator originAnimator, AlignmentOptions alignment)
        {
            if (targetAnimator == null || originAnimator == null)
                return Vector3.zero;

            if (alignment.HasFlag(AlignmentOptions.AlignShoulder))
            {
                // Find the shoulder bones for origin and target
                Transform originLeftShoulder = originAnimator.GetBoneTransform(HumanBodyBones.LeftShoulder);
                Transform originRightShoulder = originAnimator.GetBoneTransform(HumanBodyBones.RightShoulder);
                Transform targetLeftShoulder = targetAnimator.GetBoneTransform(HumanBodyBones.LeftShoulder);
                Transform targetRightShoulder = targetAnimator.GetBoneTransform(HumanBodyBones.RightShoulder);

                return CalculateAdjustment(originLeftShoulder, originRightShoulder, targetLeftShoulder, targetRightShoulder);
            }
            else if (alignment.HasFlag(AlignmentOptions.AlignFeet))
            {
                Transform originLeftFoot = originAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform originRightFoot = originAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
                Transform targetLeftFoot = targetAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform targetRightFoot = targetAnimator.GetBoneTransform(HumanBodyBones.RightFoot);

                return CalculateAdjustment(originLeftFoot, originRightFoot, targetLeftFoot, targetRightFoot);
            }

            // Hips are already in the correct spot so no need to calculate anything here

            return Vector3.zero;
        }

        /// <summary>
        /// Calculates the difference between two center points.
        /// </summary>
        /// <returns>The Difference between the two centers</returns>
        private static Vector3 CalculateAdjustment(Transform originLeft, Transform originRight, Transform targetLeft, Transform targetRight)
        {
            Vector3 offset = Vector3.zero;
            if (originLeft != null && originRight != null && targetLeft != null && targetRight != null)
            {
                // Calculate the average height difference between left and right shoulders for origin and target
                Vector3 originLeftShoulderPos = originLeft.position;
                Vector3 originRightShoulderPos = originRight.position;
                Vector3 targetLeftShoulderPos = targetLeft.position;
                Vector3 targetRightShoulderPos = targetRight.position;

                Vector3 originShoulderAvgPos = (originLeftShoulderPos + originRightShoulderPos) / 2f;
                Vector3 targetShoulderAvgPos = (targetLeftShoulderPos + targetRightShoulderPos) / 2f;

                offset = -(targetShoulderAvgPos - originShoulderAvgPos);
            }

            return offset;
        }
    }

    /// <summary>
    /// Constructor like static methods for <see cref="PoseSynchronizer"/>
    /// </summary>
    /// <remarks>
    /// This class is a bit more bloated than is needed for this project so that it can more easily be used in other projects.
    /// </remarks>
    internal partial class PoseSynchronizer
    {
        /// <summary>
        /// Adds a <see cref="PoseSynchronizer"/> component to the specified <paramref name="gameObject"/>.
        /// </summary>
        /// <param name="gameObject">The GameObject to attach the PoseSynchronizer component to.</param>
        /// <param name="originAnimator">The Animator component representing the origin character's animator.</param>
        /// <param name="targetAnimator">The Animator component representing the target character's animator.</param>
        /// <param name="targetCharacter">The bone mapping array representing the target character.</param>
        /// <param name="lockedBones">The bones that should always be locked to the original bones.</param>
        /// <returns>The created PoseSynchronizer component.</returns>
        public static PoseSynchronizer CreateComponent(GameObject gameObject, Animator originAnimator, Animator targetAnimator, BoneMap[] targetCharacter, HumanBodyBones[] lockedBones)
        {
            PoseSynchronizer poseSync = gameObject.AddComponent<PoseSynchronizer>();
            poseSync.lockedBones = lockedBones;

            poseSync.originAnimator = originAnimator;
            poseSync.targetAnimator = targetAnimator;

            poseSync.targetCharacter = targetCharacter;

            return poseSync;
        }

        /// <summary>
        /// Adds a <see cref="PoseSynchronizer"/> component to the specified <paramref name="gameObject"/>.
        /// </summary>
        /// <param name="gameObject">The GameObject to attach the PoseSynchronizer component to.</param>
        /// <param name="targetAnimator">The Animator component representing the target character's animator.</param>
        /// <param name="targetCharacter">The bone mapping array representing the target character.</param>
        /// <returns>The created PoseSynchronizer component.</returns>
        public static PoseSynchronizer CreateComponent(GameObject gameObject, Animator originAnimator, Animator targetAnimator, BoneMap[] targetCharacter)
        {
            return CreateComponent(gameObject, originAnimator, targetAnimator, targetCharacter, new HumanBodyBones[0]);
        }

        /// <summary>
        /// Adds a <see cref="PoseSynchronizer"/> component to the specified <paramref name="gameObject"/>.
        /// </summary>
        /// <param name="gameObject">The GameObject to attach the PoseSynchronizer component to.</param>
        /// <param name="originAnimator">The Animator component representing the origin character's animator.</param>
        /// <param name="targetAnimator">The Animator component representing the target character's animator.</param>
        /// <param name="targetCharacterAnimator">The Animator component representing the target character's animator.</param>
        /// <param name="lockedBones">The bones that should always be locked to the original bones.</param>
        /// <returns>The created PoseSynchronizer component.</returns>
        public static PoseSynchronizer CreateComponent(GameObject gameObject, Animator originAnimator, Animator targetAnimator, Animator targetCharacterAnimator, HumanBodyBones[] lockedBones)
        {
            BoneMap[] targetBonemap = BoneMap.GetBoneMappingFromAnimator(targetCharacterAnimator).ToArray();
            return CreateComponent(gameObject, originAnimator, targetAnimator, targetBonemap, lockedBones);
        }

        /// <summary>
        /// Adds a <see cref="PoseSynchronizer"/> component to the specified <paramref name="gameObject"/>.
        /// </summary>
        /// <param name="gameObject">The GameObject to attach the PoseSynchronizer component to.</param>
        /// <param name="targetAnimator">The Animator component representing the target character's animator.</param>
        /// <param name="targetCharacterAnimator">The Animator component representing the target character's animator.</param>
        /// <returns>The created PoseSynchronizer component.</returns>
        public static PoseSynchronizer CreateComponent(GameObject gameObject, Animator originAnimator, Animator targetAnimator, Animator targetCharacterAnimator)
        {
            BoneMap[] targetBonemap = BoneMap.GetBoneMappingFromAnimator(targetCharacterAnimator).ToArray();
            return CreateComponent(gameObject, originAnimator, targetAnimator, targetBonemap, new HumanBodyBones[0]);
        }
    }

    /// <summary>
    /// The mode used to vertically align our model.
    /// </summary>
    /// <remarks>
    /// Flags to potentially stretch the model in the future
    /// </remarks>
    [Flags]
    public enum AlignmentOptions
    {
        /// <summary>
        /// No alignment option selected. (Will align hips)
        /// </summary>
        None = 0,

        /// <summary>
        /// Aligns hips.
        /// </summary>
        Hips = 1 << 0,

        /// <summary>
        /// Align the height of the feet.
        /// </summary>
        AlignFeet = 1 << 1,

        /// <summary>
        /// Align the height of the shoulders.
        /// </summary>
        AlignShoulder = 1 << 2,
    }
}
