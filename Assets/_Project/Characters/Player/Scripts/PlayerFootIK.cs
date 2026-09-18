using UnityEngine;

namespace TheChimeraProtocol.Player
{
    /// <summary>
    /// Implements procedural Humanoid Foot IK & Pelvis height compensation.
    /// Eliminates foot floating and clipping on stairs, ramps, and uneven floors.
    /// Smoothly disables in air so legs hang naturally during falling.
    /// </summary>
    public class PlayerFootIK : MonoBehaviour
    {
        [Header("Foot IK Settings")]
        public bool enableFootIK = true;
        [Range(0f, 1f)] public float globalIKWeight = 1.0f;
        public LayerMask environmentLayers = ~0;
        public float raycastOriginUpOffset = 0.45f;
        public float raycastDistance = 0.95f;
        public float footBottomHeightOffset = 0.11f;

        [Header("Pelvis (Hips) Adjustment")]
        public bool enablePelvisOffset = true;
        public float pelvisDamping = 12.0f;
        public float maxPelvisDrop = -0.45f;

        [Header("Rotation Alignment")]
        public bool alignFootToSurfaceNormal = true;
        public float rotationDamping = 15.0f;

        private Animator _animator;
        private PlayerController _playerController;

        private Vector3 _leftFootIKPos;
        private Vector3 _rightFootIKPos;
        private Quaternion _leftFootIKRot;
        private Quaternion _rightFootIKRot;

        private float _lastLeftFootY;
        private float _lastRightFootY;
        private float _currentPelvisOffset;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            _playerController = GetComponentInParent<PlayerController>();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!enableFootIK || _animator == null) return;

            // In air / falling: smoothly disable Foot IK so legs hang and fall naturally!
            if (_playerController != null && !_playerController.IsGrounded)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
                _animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
                _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
                _animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
                _currentPelvisOffset = Mathf.Lerp(_currentPelvisOffset, 0f, pelvisDamping * Time.deltaTime);
                return;
            }

            // Reduce IK weight during high-speed sprint for looser natural stride
            float targetWeight = globalIKWeight;
            if (_playerController != null && _playerController.IsSprinting)
            {
                targetWeight *= 0.65f;
            }

            // 1. Process Left Foot
            ProcessFootIK(AvatarIKGoal.LeftFoot, ref _leftFootIKPos, ref _leftFootIKRot, ref _lastLeftFootY, targetWeight);

            // 2. Process Right Foot
            ProcessFootIK(AvatarIKGoal.RightFoot, ref _rightFootIKPos, ref _rightFootIKRot, ref _lastRightFootY, targetWeight);

            // 3. Pelvis / Hip Adjustment on Uneven Terrain / Steps
            if (enablePelvisOffset)
            {
                float targetPelvisOffset = Mathf.Clamp(Mathf.Min(_lastLeftFootY, _lastRightFootY), maxPelvisDrop, 0f);
                _currentPelvisOffset = Mathf.Lerp(_currentPelvisOffset, targetPelvisOffset, pelvisDamping * Time.deltaTime);

                Vector3 currentBodyPos = _animator.bodyPosition;
                currentBodyPos.y += _currentPelvisOffset;
                _animator.bodyPosition = currentBodyPos;
            }
        }

        private void ProcessFootIK(AvatarIKGoal foot, ref Vector3 ikPos, ref Quaternion ikRot, ref float footYOffset, float weight)
        {
            Vector3 footPos = _animator.GetIKPosition(foot);
            Vector3 rayOrigin = footPos + Vector3.up * raycastOriginUpOffset;

            // Remove Player layer from collision check
            int playerLayer = LayerMask.NameToLayer("Player");
            LayerMask mask = (playerLayer != -1) ? (environmentLayers & ~(1 << playerLayer)) : environmentLayers;

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDistance, mask, QueryTriggerInteraction.Ignore))
            {
                ikPos = footPos;
                ikPos.y = hit.point.y + footBottomHeightOffset;

                footYOffset = ikPos.y - footPos.y;

                if (alignFootToSurfaceNormal)
                {
                    Quaternion surfaceRot = Quaternion.FromToRotation(Vector3.up, hit.normal) * transform.rotation;
                    ikRot = Quaternion.Slerp(_animator.GetIKRotation(foot), surfaceRot, rotationDamping * Time.deltaTime);
                }
                else
                {
                    ikRot = _animator.GetIKRotation(foot);
                }

                _animator.SetIKPositionWeight(foot, weight);
                _animator.SetIKRotationWeight(foot, weight);
                _animator.SetIKPosition(foot, ikPos);
                _animator.SetIKRotation(foot, ikRot);
            }
            else
            {
                footYOffset = 0f;
                _animator.SetIKPositionWeight(foot, 0f);
                _animator.SetIKRotationWeight(foot, 0f);
            }
        }
    }
}
