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

        [Header("Aiming Mode Settings (RE2 / Max Payne Style)")]
        public float aimFOV = 50f;
        public float aimDistance = 1.35f;
        public float aimShoulderX = 0.55f;
        public float aimShoulderY = 0.05f;

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

        [Header("Procedural Aim Breathing Sway")]
        public bool enableAimSway = true;
        public float swayFrequency = 1.8f;
        public float swayAmplitude = 0.015f;

        [Header("Transition Speed")]
        [Range(1f, 25f)] public float transitionSpeed = 10f;

        private PlayerController _playerController;
        private PlayerInputHandler _inputHandler;
        private float _currentShoulderSide = 1.0f;
        private float _recoilPitch;
        private float _recoilYaw;

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
                _inputHandler.OnAttackPressed += () => ApplyRecoil(1.2f, Random.Range(-0.3f, 0.3f));
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

            bool isAiming = _playerController.IsAiming;
            bool isCrouching = _playerController.IsCrouching;

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

            // 4. Procedural Aim Breathing Sway
            if (enableAimSway && isAiming && cameraFollowTarget != null)
            {
                float swayX = Mathf.Sin(Time.time * swayFrequency) * swayAmplitude;
                float swayY = Mathf.Cos(Time.time * swayFrequency * 1.5f) * (swayAmplitude * 0.7f);
                targetOffset += new Vector3(swayX, swayY, 0f);
            }

            // 5. Apply Recoil Impulse Recovery
            _recoilPitch = Mathf.Lerp(_recoilPitch, 0f, 12f * Time.deltaTime);
            _recoilYaw = Mathf.Lerp(_recoilYaw, 0f, 12f * Time.deltaTime);

            // 6. Crouch Target Height Adjustment
            if (cameraFollowTarget != null)
            {
                float targetY = isCrouching ? crouchTargetHeight : standingTargetHeight;
                Vector3 localPos = cameraFollowTarget.localPosition;
                localPos.y = Mathf.Lerp(localPos.y, targetY, 8f * Time.deltaTime);
                cameraFollowTarget.localPosition = localPos;
            }

            // 7. Apply to Cinemachine Components
            float t = Time.deltaTime * transitionSpeed;
            cinemachineCamera.Lens.FieldOfView = Mathf.Lerp(cinemachineCamera.Lens.FieldOfView, targetFOV, t);
            thirdPersonFollow.CameraDistance = Mathf.Lerp(thirdPersonFollow.CameraDistance, targetDistance, t);
            thirdPersonFollow.ShoulderOffset = Vector3.Lerp(thirdPersonFollow.ShoulderOffset, targetOffset, t);
            thirdPersonFollow.CameraSide = (_currentShoulderSide + 1f) * 0.5f; // Map -1..1 to 0..1 for Cinemachine
        }

        public void ToggleShoulder()
        {
            shoulderSide = (shoulderSide > 0f) ? -1.0f : 1.0f;
        }

        public void ApplyRecoil(float pitchKick, float yawKick)
        {
            _recoilPitch += pitchKick;
            _recoilYaw += yawKick;

            if (cameraFollowTarget != null)
            {
                cameraFollowTarget.rotation *= Quaternion.Euler(-pitchKick, yawKick, 0f);
            }
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
