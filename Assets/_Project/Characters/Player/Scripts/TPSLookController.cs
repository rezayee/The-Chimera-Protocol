using UnityEngine;
using RootMotion.FinalIK;

namespace TheChimeraProtocol.Player
{
    [System.Serializable]
    public struct MovementLookProfile
    {
        [Tooltip("Weight applied to the head bone solver.")]
        [Range(0f, 1f)] public float headWeight;

        [Tooltip("Maximum weight applied to the upper body/spine bones.")]
        [Range(0f, 1f)] public float maxBodyWeight;

        [Tooltip("Maximum horizontal look deviation angle in degrees relative to character forward.")]
        [Range(10f, 180f)] public float maxLookAngle;

        public MovementLookProfile(float head, float maxBody, float maxAngle)
        {
            headWeight = head;
            maxBodyWeight = maxBody;
            maxLookAngle = maxAngle;
        }
    }

    public enum MovementLookState
    {
        Idle,
        Walk,
        Jog,
        Sprint,
        CrouchIdle,
        CrouchMove,
        AimIdle,
        AimMove
    }

    /// <summary>
    /// Coordinates camera-driven natural gaze for TPS Exploration and Aiming using Final IK 2.3 LookAtIK.
    /// Manages the virtual LookTarget, calculates horizontal/vertical angles, smoothly modulates
    /// Head and Spine weights across 8 movement states, and signals PlayerController for Turn-In-Place upon camera orbit limits.
    /// </summary>
    public class TPSLookController : MonoBehaviour
    {
        [Header("Core References")]
        [Tooltip("The camera transform to follow.")]
        [SerializeField] private Transform cameraTransform;

        [Tooltip("The explicit LookTarget Transform in the scene/character hierarchy.")]
        [SerializeField] private Transform lookTarget;

        [Tooltip("The Final IK LookAtIK component on the character model.")]
        [SerializeField] private LookAtIK lookAtIK;

        [Tooltip("The root PlayerController governing locomotion and Turn-In-Place.")]
        [SerializeField] private PlayerController playerController;

        [Header("Look Target Distance & Smoothing")]
        [Tooltip("Distance in front of the camera where LookTarget will float.")]
        public float lookDistance = 6.0f;

        [Tooltip("Smooth time (seconds) for LookTarget positional dampening. Prevents snappy camera moves from jerking the neck.")]
        [Range(0.02f, 0.4f)] public float lookTargetSmoothTime = 0.10f;

        [Header("Layered Angle Thresholds (Degrees)")]
        [Tooltip("Within this angle, rotation is purely Head dominant.")]
        public float headOnlyAngle = 25f;

        [Tooltip("Beyond this angle, Neck begins contributing to the gaze.")]
        public float neckEngageAngle = 45f;

        [Tooltip("Beyond this angle, Chest and Upper Spine contribute subtly.")]
        public float chestEngageAngle = 65f;

        [Header("Turn-In-Place Intent & Hysteresis")]
        [Tooltip("Horizontal angle threshold where a body turn request begins accumulating.")]
        public float turnStartAngle = 70f;

        [Tooltip("Horizontal angle threshold where body turn request is released.")]
        public float turnStopAngle = 45f;

        [Tooltip("Time the camera must stay beyond turnStartAngle before triggering Turn-In-Place.")]
        public float turnRequestDelay = 0.20f;

        [Header("Movement-Aware Look Profiles")]
        public MovementLookProfile idleProfile = new MovementLookProfile(0.75f, 0.25f, 65f);
        public MovementLookProfile walkProfile = new MovementLookProfile(0.65f, 0.15f, 50f);
        public MovementLookProfile jogProfile = new MovementLookProfile(0.50f, 0.08f, 35f);
        public MovementLookProfile sprintProfile = new MovementLookProfile(0.30f, 0.00f, 20f);
        public MovementLookProfile crouchIdleProfile = new MovementLookProfile(0.70f, 0.15f, 60f);
        public MovementLookProfile crouchMoveProfile = new MovementLookProfile(0.55f, 0.10f, 40f);
        public MovementLookProfile aimIdleProfile = new MovementLookProfile(0.15f, 0.00f, 180f);
        public MovementLookProfile aimMoveProfile = new MovementLookProfile(0.10f, 0.00f, 180f);

        [Header("Weight Smoothing")]
        [Tooltip("Smooth time (seconds) for weight transitions.")]
        public float weightSmoothTime = 0.12f;

        [Header("Turn-In-Place Look Suppression")]
        [Tooltip("Target Head Look weight during Turn-In-Place.")]
        [Range(0f, 1f)] public float turnHeadWeight = 0.10f;

        [Tooltip("Target Body Look weight during Turn-In-Place.")]
        [Range(0f, 1f)] public float turnBodyWeight = 0.00f;

        [Tooltip("Time (seconds) to smoothly ramp down weights when entering Turn-In-Place.")]
        public float turnEnterSmoothTime = 0.05f;

        [Tooltip("Time (seconds) to smoothly recover weights after Turn-In-Place completes.")]
        public float turnExitRecoveryTime = 0.15f;

        [Header("Debug Visualizer")]
        public bool enableDebug = true;

        // Runtime State
        private MovementLookState _currentLookState;
        private MovementLookProfile _activeProfile;
        private Vector3 _targetVelocity;
        private float _currentHeadWeight;
        private float _currentBodyWeight;
        private float _headWeightVel;
        private float _bodyWeightVel;
        private float _horizontalAngle;
        private float _turnIntentTimer;
        private bool _isTurnRequested;
        private bool _isTurnLookSuppressed;
        private float _turnExitTimer;
        private bool _isAxesConfigured;

        // Public Accessors for telemetry & testing
        public MovementLookState CurrentLookState => _currentLookState;
        public MovementLookProfile ActiveProfile => _activeProfile;
        public float CurrentHorizontalAngle => _horizontalAngle;
        public float CurrentHeadWeight => _currentHeadWeight;
        public float CurrentBodyWeight => _currentBodyWeight;
        public bool IsTurnRequested => _isTurnRequested;
        public bool IsTurnLookSuppressed => _isTurnLookSuppressed;
        public float TurnExitTimer => _turnExitTimer;

        private void Awake()
        {
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
                if (playerController == null)
                {
                    playerController = GetComponentInParent<PlayerController>();
                }
            }

            if (lookAtIK == null)
            {
                lookAtIK = GetComponentInChildren<LookAtIK>();
            }

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        private void Start()
        {
            _currentLookState = EvaluateMovementState();
            _activeProfile = GetProfileForState(_currentLookState);
            _currentHeadWeight = _activeProfile.headWeight;
            _currentBodyWeight = 0f;

            // Ensure LookAtIK is cleanly configured on start
            if (lookAtIK != null)
            {
                // Safety: eyes are not present in Mixamo rigs
                if (lookAtIK.solver.eyes != null && lookAtIK.solver.eyes.Length > 0)
                {
                    lookAtIK.solver.eyes = new IKSolverLookAt.LookAtBone[0];
                }

                ConfigureLookAtIKAxes();

                // Initial weights
                lookAtIK.solver.IKPositionWeight = 1.0f;
                lookAtIK.solver.headWeight = _currentHeadWeight;
                lookAtIK.solver.bodyWeight = _currentBodyWeight;
                lookAtIK.solver.clampWeight = 0.5f;
                lookAtIK.solver.clampWeightHead = 0.55f;
                lookAtIK.solver.clampSmoothing = 2;
                lookAtIK.solver.target = lookTarget;
            }

            if (lookTarget != null && cameraTransform != null)
            {
                Vector3 gazeOrigin = (lookAtIK != null && lookAtIK.solver.head != null && lookAtIK.solver.head.transform != null)
                    ? lookAtIK.solver.head.transform.position
                    : transform.position + Vector3.up * 1.55f;
                lookTarget.position = gazeOrigin + cameraTransform.forward * lookDistance;
            }
        }

        private void LateUpdate()
        {
            // Dynamic fallback for camera reference
            if (cameraTransform == null)
            {
                if (Camera.main != null)
                {
                    cameraTransform = Camera.main.transform;
                }
                else
                {
                    return;
                }
            }

            if (!_isAxesConfigured && lookAtIK != null)
            {
                ConfigureLookAtIKAxes();
                _isAxesConfigured = true;
            }

            // 1. Calculate Horizontal Signed Angle between Character Forward and Camera Forward
            Vector3 charForward = transform.forward;
            Vector3 camForwardFlat = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            if (camForwardFlat.sqrMagnitude > 0.001f)
            {
                _horizontalAngle = Vector3.SignedAngle(charForward, camForwardFlat, Vector3.up);
            }

            // 2. Evaluate Movement State & Active Profile
            _currentLookState = EvaluateMovementState();
            _activeProfile = GetProfileForState(_currentLookState);

            // 3. Smoothly update LookTarget position (respecting state look limits)
            UpdateLookTarget(_activeProfile);

            // 4. Modulate IK Weights based on State & Angle
            UpdateIKWeights(_activeProfile);

            // 5. Evaluate Turn-In-Place Intent
            UpdateTurnIntent();
        }

        private void UpdateLookTarget(MovementLookProfile profile)
        {
            if (lookTarget == null || cameraTransform == null) return;

            Vector3 targetDirection = cameraTransform.forward;

            // Constrain horizontal gaze angle if profile defines a limit (< 179 deg)
            if (profile.maxLookAngle < 179f && Mathf.Abs(_horizontalAngle) > profile.maxLookAngle)
            {
                float clampedAngle = Mathf.Sign(_horizontalAngle) * profile.maxLookAngle;
                Vector3 clampedHorizontal = Quaternion.AngleAxis(clampedAngle, Vector3.up) * transform.forward;
                targetDirection = (clampedHorizontal + Vector3.up * cameraTransform.forward.y).normalized;
            }

            Vector3 gazeOrigin = (lookAtIK != null && lookAtIK.solver.head != null && lookAtIK.solver.head.transform != null)
                ? lookAtIK.solver.head.transform.position
                : transform.position + Vector3.up * 1.55f;

            Vector3 rawTargetPos = gazeOrigin + targetDirection * lookDistance;

            // Smooth position dampening
            lookTarget.position = Vector3.SmoothDamp(
                lookTarget.position,
                rawTargetPos,
                ref _targetVelocity,
                lookTargetSmoothTime
            );
        }

        private void UpdateIKWeights(MovementLookProfile profile)
        {
            if (lookAtIK == null) return;

            float targetHeadWeight = profile.headWeight;
            float targetBodyWeight = 0f;

            if (profile.maxBodyWeight > 0.001f)
            {
                float absAngle = Mathf.Abs(_horizontalAngle);

                if (absAngle <= headOnlyAngle)
                {
                    // Within headOnlyAngle: pure head glance, body stays still
                    targetBodyWeight = 0f;
                }
                else if (absAngle <= chestEngageAngle)
                {
                    // Neck and chest smoothly ramp in
                    float t = (absAngle - headOnlyAngle) / Mathf.Max(chestEngageAngle - headOnlyAngle, 0.001f);
                    targetBodyWeight = Mathf.Lerp(0f, profile.maxBodyWeight, t);
                }
                else
                {
                    // Beyond chestEngageAngle: full allowed body weight for this state
                    targetBodyWeight = profile.maxBodyWeight;
                }
            }

            // Turn-In-Place Look Suppression Override
            bool isTurning = playerController != null && playerController.IsTurningInPlace;
            bool isAimOrMove = playerController != null && (playerController.IsAiming || playerController.CurrentSpeed > 0.1f);

            float currentSmoothTime = weightSmoothTime;

            if (isAimOrMove)
            {
                // Aim and Locomotion profiles have absolute priority over Turn suppression
                _isTurnLookSuppressed = false;
                _turnExitTimer = 0f;
                currentSmoothTime = weightSmoothTime;
            }
            else if (isTurning)
            {
                // Turn Active: override weights towards turnHeadWeight and turnBodyWeight with smooth enter ramp
                _isTurnLookSuppressed = true;
                _turnExitTimer = turnExitRecoveryTime;

                targetHeadWeight = turnHeadWeight;
                targetBodyWeight = turnBodyWeight;
                currentSmoothTime = turnEnterSmoothTime;
            }
            else if (_turnExitTimer > 0f)
            {
                // Turn Exit Transition: smoothly recovering back to Idle profile targets
                _turnExitTimer -= Time.deltaTime;
                _isTurnLookSuppressed = true;
                currentSmoothTime = turnExitRecoveryTime;
            }
            else
            {
                _isTurnLookSuppressed = false;
                currentSmoothTime = weightSmoothTime;
            }

            // Smooth weight transitions
            _currentHeadWeight = Mathf.SmoothDamp(_currentHeadWeight, targetHeadWeight, ref _headWeightVel, currentSmoothTime);
            _currentBodyWeight = Mathf.SmoothDamp(_currentBodyWeight, targetBodyWeight, ref _bodyWeightVel, currentSmoothTime);

            // Apply directly to Final IK Solver
            lookAtIK.solver.IKPositionWeight = 1.0f;
            lookAtIK.solver.headWeight = _currentHeadWeight;
            lookAtIK.solver.bodyWeight = _currentBodyWeight;
            lookAtIK.solver.target = lookTarget;
        }

        private MovementLookState EvaluateMovementState()
        {
            if (playerController == null) return MovementLookState.Idle;

            bool isAiming = playerController.IsAiming;
            bool isCrouching = playerController.IsCrouching;
            bool isSprinting = playerController.IsSprinting;
            float speed = playerController.CurrentSpeed;
            bool isMoving = speed > 0.1f;

            if (isAiming)
            {
                return isMoving ? MovementLookState.AimMove : MovementLookState.AimIdle;
            }

            if (isCrouching)
            {
                return isMoving ? MovementLookState.CrouchMove : MovementLookState.CrouchIdle;
            }

            if (isSprinting)
            {
                return MovementLookState.Sprint;
            }

            if (!isMoving)
            {
                return MovementLookState.Idle;
            }

            // Distinguish walk vs jog using walkSpeed threshold
            if (speed <= playerController.walkSpeed + 0.4f)
            {
                return MovementLookState.Walk;
            }

            return MovementLookState.Jog;
        }

        public MovementLookProfile GetProfileForState(MovementLookState state)
        {
            switch (state)
            {
                case MovementLookState.Idle: return idleProfile;
                case MovementLookState.Walk: return walkProfile;
                case MovementLookState.Jog: return jogProfile;
                case MovementLookState.Sprint: return sprintProfile;
                case MovementLookState.CrouchIdle: return crouchIdleProfile;
                case MovementLookState.CrouchMove: return crouchMoveProfile;
                case MovementLookState.AimIdle: return aimIdleProfile;
                case MovementLookState.AimMove: return aimMoveProfile;
                default: return idleProfile;
            }
        }

        private void UpdateTurnIntent()
        {
            if (playerController == null) return;

            bool isAiming = playerController.IsAiming;
            bool isMoving = playerController.CurrentSpeed > 0.1f;
            bool isCrouching = playerController.IsCrouching;
            bool isTurning = playerController.IsTurningInPlace;

            // Turn-in-place is only requested when stationary in exploration standing mode
            if (!isAiming && !isMoving && !isCrouching && !isTurning)
            {
                float absAngle = Mathf.Abs(_horizontalAngle);

                if (absAngle > turnStartAngle)
                {
                    _turnIntentTimer += Time.deltaTime;
                    if (_turnIntentTimer >= turnRequestDelay)
                    {
                        _isTurnRequested = true;
                        float targetCamYaw = cameraTransform != null ? cameraTransform.eulerAngles.y : transform.eulerAngles.y;
                        playerController.RequestExplorationTurnInPlace(targetCamYaw, _horizontalAngle);
                    }
                }
                else if (absAngle < turnStopAngle)
                {
                    _turnIntentTimer = 0f;
                    _isTurnRequested = false;
                }
            }
            else
            {
                _turnIntentTimer = 0f;
                _isTurnRequested = false;
            }
        }

        public void SetLookTarget(Transform target)
        {
            lookTarget = target;
            if (lookAtIK != null)
            {
                lookAtIK.solver.target = lookTarget;
            }
        }

        public void SetCameraTransform(Transform cam)
        {
            cameraTransform = cam;
        }

        public void SetLookAtIK(LookAtIK ik)
        {
            lookAtIK = ik;
            ConfigureLookAtIKAxes();
        }

        private void ConfigureLookAtIKAxes()
        {
            if (lookAtIK == null) return;

            // mixamorig1:Head is oriented at ~241 deg in Mixamo rig space; local forward is (-0.87, 0.02, 0.49)
            if (lookAtIK.solver.head != null && lookAtIK.solver.head.transform != null)
            {
                lookAtIK.solver.head.axis = new Vector3(-0.36f, 0.02f, 0.93f).normalized;
            }

            if (lookAtIK.solver.spine != null)
            {
                if (lookAtIK.solver.spine.Length > 0 && lookAtIK.solver.spine[0].transform != null)
                    lookAtIK.solver.spine[0].axis = new Vector3(0.00f, -0.08f, 1.00f).normalized;
                if (lookAtIK.solver.spine.Length > 1 && lookAtIK.solver.spine[1].transform != null)
                    lookAtIK.solver.spine[1].axis = new Vector3(0.00f, -0.08f, 1.00f).normalized;
                if (lookAtIK.solver.spine.Length > 2 && lookAtIK.solver.spine[2].transform != null)
                    lookAtIK.solver.spine[2].axis = new Vector3(0.00f, -0.01f, 1.00f).normalized;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!enableDebug) return;

            // Character forward (Blue)
            Gizmos.color = Color.blue;
            Vector3 origin = transform.position + Vector3.up * 1.5f;
            Gizmos.DrawRay(origin, transform.forward * 2.5f);

            // Camera forward horizontal projection (Cyan)
            if (cameraTransform != null)
            {
                Vector3 camForwardFlat = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(origin, camForwardFlat * 2.5f);
            }

            // LookTarget (Yellow sphere and gaze line)
            if (lookTarget != null)
            {
                Gizmos.color = _isTurnRequested ? Color.red : Color.yellow;
                Gizmos.DrawWireSphere(lookTarget.position, 0.25f);
                Gizmos.DrawLine(origin, lookTarget.position);
            }

            // Turn thresholds visualization
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f); // Orange
            Vector3 leftLimit = Quaternion.Euler(0f, -turnStartAngle, 0f) * transform.forward;
            Vector3 rightLimit = Quaternion.Euler(0f, turnStartAngle, 0f) * transform.forward;
            Gizmos.DrawRay(origin, leftLimit * 2.0f);
            Gizmos.DrawRay(origin, rightLimit * 2.0f);
        }
    }
}
