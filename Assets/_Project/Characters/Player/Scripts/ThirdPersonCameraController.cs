using UnityEngine;
using Unity.Cinemachine;

namespace TheChimeraProtocol.Player
{
    /// <summary>
    /// Coordinates the Cinemachine 3.x ThirdPersonCamera.
    /// Features:
    /// - Over-The-Shoulder Exploration vs Aiming transition
    /// - Left / Right Shoulder Switch (RE2 & Max Payne style)
    /// - Smart Wall Avoidance on flanks
    /// - Aiming procedural breathing sway & shooting recoil impulse
    /// - Crouch height compensation
    /// </summary>
    public class ThirdPersonCameraController : MonoBehaviour
    {
        [Header("Cinemachine References")]
        [SerializeField] private CinemachineCamera cinemachineCamera;
        [SerializeField] private CinemachineThirdPersonFollow thirdPersonFollow;
        [SerializeField] private Transform cameraFollowTarget;

        [Header("Exploration Mode Settings")]
        public float normalFOV = 65f;
        public float normalDistance = 2.2f;
        public float normalShoulderX = 0.45f;
        public float normalShoulderY = 0.10f;

        [Header("Aim OTS Mode Settings")]
        public float aimFOV = 48f;
        public float aimDistance = 1.45f;
        public float aimShoulderX = 0.55f;
        public float aimShoulderY = 0.14f;

        [Header("Shoulder Switching")]
        [Tooltip("1.0 for right shoulder, -1.0 for left shoulder")]
        public float shoulderSide = 1.0f;
        public float shoulderSwitchSpeed = 8.0f;

        [Header("Smart Flank Wall Avoidance")]
        public bool enableSmartWallAvoidance = true;
        public LayerMask wallLayers = ~0;
        public float wallCheckRadius = 0.22f;

        [Header("Crouch Target Heights")]
        public float standingTargetHeight = 1.45f;
        public float crouchTargetHeight = 0.95f;

        [Header("Transition Speed")]
        [Range(1f, 25f)] public float transitionSpeed = 10f;

        private PlayerController _playerController;
        private PlayerInputHandler _inputHandler;
        private TheChimeraProtocol.Weapons.PlayerWeaponController _weaponController;
        private float _currentShoulderSide = 1.0f;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _inputHandler = GetComponent<PlayerInputHandler>();

            if (cinemachineCamera == null)
            {
                cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();
            }

            if (cinemachineCamera != null && thirdPersonFollow == null)
            {
                thirdPersonFollow = cinemachineCamera.GetComponent<CinemachineThirdPersonFollow>();
            }

            if (cameraFollowTarget == null)
            {
                var targetChild = transform.Find("CameraFollowTarget");
                if (targetChild != null) cameraFollowTarget = targetChild;
            }

            if (_inputHandler != null)
            {
                _inputHandler.OnShoulderSwitchPressed += ToggleShoulder;
            }
        }

        private void OnDestroy()
        {
            if (_inputHandler != null)
            {
                _inputHandler.OnShoulderSwitchPressed -= ToggleShoulder;
            }
        }

        private void LateUpdate()
        {
            if (cinemachineCamera == null || thirdPersonFollow == null || _playerController == null)
                return;

            bool isCrouching = _playerController.IsCrouching;
            if (_weaponController == null) _weaponController = GetComponent<TheChimeraProtocol.Weapons.PlayerWeaponController>();
            bool isAiming = _weaponController != null && _weaponController.IsAiming;

            // 1. Smooth Shoulder Switch Interpolation (Right <-> Left)
            _currentShoulderSide = Mathf.Lerp(_currentShoulderSide, shoulderSide, Time.deltaTime * shoulderSwitchSpeed);

            // 2. Base Shoulder Offsets
            float baseX = isAiming ? aimShoulderX : normalShoulderX;
            float baseY = isAiming ? aimShoulderY : normalShoulderY;
            float targetDistance = isAiming ? aimDistance : normalDistance;
            float targetFOV = isAiming ? aimFOV : normalFOV;

            Vector3 targetOffset = new Vector3(baseX * _currentShoulderSide, baseY, 0f);

            // 3. Smart Flank Wall Avoidance
            if (enableSmartWallAvoidance && cameraFollowTarget != null)
            {
                int playerLayer = LayerMask.NameToLayer("Player");
                LayerMask mask = (playerLayer != -1) ? (wallLayers & ~(1 << playerLayer)) : wallLayers;

                Vector3 checkOrigin = cameraFollowTarget.position;
                Vector3 checkDir = cameraFollowTarget.right * (_currentShoulderSide > 0 ? 1f : -1f);
                float checkDist = Mathf.Abs(targetOffset.x) + wallCheckRadius;

                if (Physics.SphereCast(checkOrigin, wallCheckRadius, checkDir, out RaycastHit hit, checkDist, mask, QueryTriggerInteraction.Ignore))
                {
                    // Nudge shoulder closer to center to avoid wall obstruction
                    float safeDist = Mathf.Max(hit.distance - wallCheckRadius, 0.12f);
                    targetOffset.x = safeDist * Mathf.Sign(_currentShoulderSide);
                }
            }

            // 4. Crouch Target Height Adjustment
            if (cameraFollowTarget != null)
            {
                float targetY = isCrouching ? crouchTargetHeight : standingTargetHeight;
                Vector3 localPos = cameraFollowTarget.localPosition;
                localPos.y = Mathf.Lerp(localPos.y, targetY, 8f * Time.deltaTime);
                cameraFollowTarget.localPosition = localPos;
            }

            // 5. Apply to Cinemachine Components
            float t = Time.deltaTime * transitionSpeed;
            cinemachineCamera.Lens.FieldOfView = Mathf.Lerp(cinemachineCamera.Lens.FieldOfView, targetFOV, t);
            thirdPersonFollow.CameraDistance = Mathf.Lerp(thirdPersonFollow.CameraDistance, targetDistance, t);
            _currentRecoilOffset = Vector3.Lerp(_currentRecoilOffset, Vector3.zero, Time.deltaTime * recoilRecoverySpeed);
            thirdPersonFollow.ShoulderOffset = Vector3.Lerp(thirdPersonFollow.ShoulderOffset, targetOffset + _currentRecoilOffset, t);
            thirdPersonFollow.CameraSide = (_currentShoulderSide + 1f) * 0.5f; // Map -1..1 to 0..1 for Cinemachine
        }

        [Header("Procedural Recoil Shake")]
        [SerializeField] private float recoilRecoverySpeed = 12f;
        private Vector3 _currentRecoilOffset;

        public void ApplyRecoil(float verticalKick, float horizontalKick)
        {
            float randomX = Random.Range(-horizontalKick, horizontalKick);
            _currentRecoilOffset += new Vector3(randomX * 0.05f, verticalKick * 0.06f, -verticalKick * 0.04f);
        }

        public void ToggleShoulder()
        {
            shoulderSide = (shoulderSide > 0f) ? -1.0f : 1.0f;
        }

        public void SetCinemachineCamera(CinemachineCamera cam)
        {
            cinemachineCamera = cam;
            if (cam != null)
            {
                thirdPersonFollow = cam.GetComponent<CinemachineThirdPersonFollow>();
            }
        }
    }
}
