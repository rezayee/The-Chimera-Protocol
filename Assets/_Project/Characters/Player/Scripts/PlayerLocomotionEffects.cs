using UnityEngine;

namespace TheChimeraProtocol.Player
{
    /// <summary>
    /// Implements procedural locomotion polish:
    /// - Procedural banking/leaning into turns (ALS style)
    /// - Camera follow target pitch & yaw orbit control
    /// - Movement acceleration tilt
    /// </summary>
    public class PlayerLocomotionEffects : MonoBehaviour
    {
        [Header("Camera Target Orbit")]
        [Tooltip("The Transform that Cinemachine tracks and orbits around (chest/head height)")]
        [SerializeField] private Transform cameraFollowTarget;
        [Range(-80f, 0f)] public float bottomClamp = -40f;
        [Range(0f, 85f)] public float topClamp = 65f;

        [Header("Procedural Leaning (ALS Style)")]
        [Tooltip("Container holding the visual meshes (child of Player)")]
        [SerializeField] private Transform visualModelRoot;
        [Range(0f, 15f)] public float maxLeanAngle = 6.0f;
        [Range(0.01f, 1f)] public float leanSensitivity = 0.05f;
        [Range(1f, 20f)] public float leanReturnSpeed = 8.0f;

        [Header("Acceleration Pitch Tilt")]
        [Range(0f, 8f)] public float accelerationTiltAngle = 2.5f;
        [Range(1f, 20f)] public float tiltDamping = 6.0f;

        private PlayerController _playerController;
        private PlayerInputHandler _inputHandler;

        private float _cinemachineTargetPitch;
        private float _cinemachineTargetYaw;
        private float _currentLeanRoll;
        private float _currentPitchTilt;
        private float _previousSpeed;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _inputHandler = GetComponent<PlayerInputHandler>();

            if (cameraFollowTarget != null)
            {
                // Ensure camera follow target starts cleanly aligned with character forward
                cameraFollowTarget.localRotation = Quaternion.identity;
                _cinemachineTargetYaw = transform.eulerAngles.y;
                _cinemachineTargetPitch = 0.0f;
            }
        }

        private void LateUpdate()
        {
            HandleCameraTargetOrbit();
            HandleProceduralLeaning();
        }

        private void HandleCameraTargetOrbit()
        {
            if (cameraFollowTarget == null || _inputHandler == null) return;

            Vector2 look = _inputHandler.LookInput;

            // Invert Pitch for natural FPS/TPS mouse control
            _cinemachineTargetPitch += look.y * (_inputHandler.invertY ? 1f : -1f);
            _cinemachineTargetYaw += look.x;

            // Clamp vertical pitch angle
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, bottomClamp, topClamp);

            // Apply rotation to the camera follow target
            cameraFollowTarget.rotation = Quaternion.Euler(_cinemachineTargetPitch, _cinemachineTargetYaw, 0.0f);
        }

        private void HandleProceduralLeaning()
        {
            if (visualModelRoot == null || _playerController == null) return;

            // 1. Roll Leaning into corners (ALS feature)
            float turnRate = _playerController.TurnAngleDelta;
            float targetRoll = Mathf.Clamp(-turnRate * leanSensitivity, -maxLeanAngle, maxLeanAngle);

            // Scale down lean if moving very slowly
            if (_playerController.CurrentSpeed < 1.0f)
            {
                targetRoll = 0f;
            }

            _currentLeanRoll = Mathf.Lerp(_currentLeanRoll, targetRoll, leanReturnSpeed * Time.deltaTime);

            // 2. Pitch Tilt (leaning forward during acceleration, backward on braking)
            float speedDelta = (_playerController.CurrentSpeed - _previousSpeed) / Mathf.Max(Time.deltaTime, 0.001f);
            _previousSpeed = _playerController.CurrentSpeed;

            float targetPitch = Mathf.Clamp(speedDelta * 0.2f, -accelerationTiltAngle, accelerationTiltAngle);
            _currentPitchTilt = Mathf.Lerp(_currentPitchTilt, targetPitch, tiltDamping * Time.deltaTime);

            // Apply procedural rotations to visual container (preserving visual yaw for decoupled turn-in-place)
            visualModelRoot.localRotation = Quaternion.Euler(_currentPitchTilt, visualModelRoot.localEulerAngles.y, _currentLeanRoll);
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        public void SetVisualModelRoot(Transform root)
        {
            visualModelRoot = root;
        }

        public void SetCameraFollowTarget(Transform target)
        {
            cameraFollowTarget = target;
        }
    }
}
