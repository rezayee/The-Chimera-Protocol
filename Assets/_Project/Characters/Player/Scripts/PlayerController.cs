using UnityEngine;

namespace TheChimeraProtocol.Player
{
    public enum TurnType
    {
        None = 0,
        Normal = 1,
        Large = 2,
        Half = 3
    }

    public enum TurnDirection
    {
        Left = -1,
        Right = 1
    }

    [System.Serializable]
    public class TurnProfileData
    {
        public string profileName;
        public float duration = 1.0f;
        public float nativeYaw = 90f;
        public AnimationCurve rotationProfile;

        public TurnProfileData() { }

        public TurnProfileData(string name, float dur, float native, AnimationCurve curve)
        {
            profileName = name;
            duration = dur;
            nativeYaw = native;
            rotationProfile = curve;
        }
    }

    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Locomotion Speeds (m/s)")]
        public float walkSpeed = 1.8f;
        public float jogSpeed = 3.8f;
        public float sprintSpeed = 5.8f;
        public float crouchSpeed = 1.2f;

        [Header("Weight and Inertia (Max Payne Style)")]
        public float acceleration = 10f;
        public float deceleration = 14f;
        public float turnSmoothTime = 0.08f;

        [Header("Turn-In-Place Settings")]
        [Tooltip("Enable or disable Idle body Turn-In-Place behavior when rotating the camera")]
        [SerializeField] private bool enableTurnInPlace = true;
        [Tooltip("Initial window of time (seconds) during turn animation where movement input will not immediately cancel")]
        public float turnProtectedWindow = 0.15f;
        [Tooltip("Input stick magnitude squared required to interrupt a turn after the protected window")]
        public float turnInterruptMoveThreshold = 0.25f;

        [Header("Turn Classification Thresholds (Configurable)")]
        [Tooltip("Minimum angle difference to trigger exploration Turn-In-Place")]
        public float turnTriggerThreshold = 70f;
        [Tooltip("Maximum angle for Normal Right Turn (Native: +95.6°)")]
        public float normalMaxAngleRight = 95f;
        [Tooltip("Maximum angle for Normal Left Turn (Native: -122.6°, scaled for Normal)")]
        public float normalMaxAngleLeft = 95f;
        [Tooltip("Maximum angle for Large Right Turn (Native: +122.6°)")]
        public float largeMaxAngleRight = 135f;
        [Tooltip("Maximum angle for Large Left Turn (Native: -122.6°)")]
        public float largeMaxAngleLeft = 135f;

        [Header("Turn Classification Profiles")]
        public TurnProfileData normalRightProfile;
        public TurnProfileData normalLeftProfile;
        public TurnProfileData largeRightProfile;
        public TurnProfileData largeLeftProfile;
        public TurnProfileData halfRightProfile;
        public TurnProfileData halfLeftProfile;

        [Header("Crouch Settings")]
        public float standingHeight = 1.85f;
        public float crouchHeight = 1.25f;
        public Vector3 standingCenter = new Vector3(0f, 0.95f, 0f);
        public Vector3 crouchCenter = new Vector3(0f, 0.62f, 0f);

        [Header("Grounding and Gravity")]
        public float gravity = 20f;
        public float groundStickForce = 4.0f;
        public LayerMask groundLayers = ~0;
        public float groundCheckOffset = 0.15f;
        public float groundCheckRadius = 0.28f;

        [Header("Visual Root Decoupling (Turn-In-Place)")]
        [Tooltip("Container holding the visual meshes (child of Player)")]
        [SerializeField] private Transform visualModelRoot;
        [Tooltip("Duration in seconds for smooth convergence of Visual Root to Gameplay Root after turn animation")]
        [SerializeField] private float turnEndAlignDuration = 0.20f;
        [Tooltip("Display runtime debug HUD overlay for turn-in-place telemetry")]
        [SerializeField] private bool showTurnDebugHUD = false;

        [Header("References")]
        [SerializeField] private Transform mainCameraTransform;
        [SerializeField] private Animator characterAnimator;

        // Public Properties for Camera and FX
        public bool IsGrounded { get; private set; }
        public bool IsCrouching => _inputHandler != null && _inputHandler.CrouchToggled;
        public bool IsSprinting => _inputHandler != null && _inputHandler.SprintHeld && !IsCrouching && _inputHandler.MoveInput.y > 0.1f;
        public float CurrentSpeed => _horizontalVelocity.magnitude;
        public Vector3 Velocity => _characterController != null ? _characterController.velocity : Vector3.zero;
        public float TurnAngleDelta => _turnAngleDelta;
        public float CurrentTurnAngle => _turnAngleToTarget;
        public bool IsTurningInPlace => _isTurningInPlace;
        public TurnType ActiveTurnType => _activeTurnType;
        public Transform VisualModelRoot
        {
            get
            {
                if (visualModelRoot == null) visualModelRoot = transform.Find("VisualModel_Root");
                return visualModelRoot;
            }
        }
        public float TurnEndAlignDuration => turnEndAlignDuration;
        public bool IsAligningVisual => _isAligningVisual;
        public float AlignTimer => _alignTimer;
        public float CurrentVisualYaw => _currentVisualYaw;
        public bool ShowTurnDebugHUD { get => showTurnDebugHUD; set => showTurnDebugHUD = value; }
        public bool EnableTurnInPlace { get => enableTurnInPlace; set => enableTurnInPlace = value; }

        private CharacterController _characterController;
        private PlayerInputHandler _inputHandler;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity = -4.0f;
        private float _turnSmoothVelocity;
        private float _turnAngleDelta;
        private float _turnAngleToTarget;
        private float _previousYaw;
        private bool _isTurningInPlace;
        private TurnType _activeTurnType = TurnType.None;
        private TurnDirection _activeTurnDirection = TurnDirection.Right;
        private float _targetTurnYaw;
        private float _turnStartYaw;
        private float _turnTimer;
        private float _currentTurnDuration;
        private AnimationCurve _currentTurnCurve;
        private float _currentTurnNativeYaw;
        private float _visualStartYaw;
        private float _currentVisualYaw;
        private bool _isAligningVisual;
        private float _alignTimer;
        private float _alignStartVisualYaw;

        // Animator Parameter Hashes
        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimIsGrounded = Animator.StringToHash("IsGrounded");
        private static readonly int AnimIsCrouching = Animator.StringToHash("IsCrouching");
        private static readonly int AnimTurn = Animator.StringToHash("Turn");
        private static readonly int AnimTurnAngle = Animator.StringToHash("TurnAngle");
        private static readonly int AnimIsTurningInPlace = Animator.StringToHash("IsTurningInPlace");
        private static readonly int AnimTurnType = Animator.StringToHash("TurnType");
        private static readonly int AnimTurnDirection = Animator.StringToHash("TurnDirection");

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _inputHandler = GetComponent<PlayerInputHandler>();

            if (mainCameraTransform == null && Camera.main != null)
            {
                mainCameraTransform = Camera.main.transform;
            }

            if (characterAnimator == null)
            {
                characterAnimator = GetComponentInChildren<Animator>();
            }

            // CRITICAL: Ensure Animator Root Motion is strictly disabled.
            // PlayerController is the sole owner of root position, rotation, and capsule collision.
            if (characterAnimator != null)
            {
                characterAnimator.applyRootMotion = false;
            }

            if (visualModelRoot == null)
            {
                visualModelRoot = transform.Find("VisualModel_Root");
            }
            _visualStartYaw = transform.eulerAngles.y;
            _currentVisualYaw = _visualStartYaw;

            InitializeTurnCurves();

            _previousYaw = transform.eulerAngles.y;
            _verticalVelocity = -groundStickForce;
        }

        private void OnValidate()
        {
            InitializeTurnCurves();
        }

        private void InitializeTurnCurves()
        {
            // 1. Normal Right (+95.6°, 1.000s, Ch17 Right Turn FBX)
            if (normalRightProfile == null || normalRightProfile.rotationProfile == null || normalRightProfile.rotationProfile.length == 0)
            {
                normalRightProfile = new TurnProfileData("Normal Right", 1.000f, 95.6f, new AnimationCurve(
                    new Keyframe(0.0f, 0.000f, 1.47f, 1.47f),
                    new Keyframe(0.1f, 0.147f, 1.25f, 1.25f),
                    new Keyframe(0.2f, 0.251f, 0.92f, 0.92f),
                    new Keyframe(0.3f, 0.332f, 0.74f, 0.74f),
                    new Keyframe(0.4f, 0.399f, 0.67f, 0.67f),
                    new Keyframe(0.5f, 0.467f, 0.74f, 0.74f),
                    new Keyframe(0.6f, 0.548f, 0.88f, 0.88f),
                    new Keyframe(0.7f, 0.644f, 1.02f, 1.02f),
                    new Keyframe(0.8f, 0.753f, 1.13f, 1.13f),
                    new Keyframe(0.9f, 0.870f, 1.22f, 1.22f),
                    new Keyframe(1.0f, 1.000f, 1.15f, 1.15f)
                ));
            }

            // 2. Normal Left (-122.6°, 0.933s, Ch17 Left Turn FBX)
            if (normalLeftProfile == null || normalLeftProfile.rotationProfile == null || normalLeftProfile.rotationProfile.length == 0)
            {
                normalLeftProfile = new TurnProfileData("Normal Left", 0.933f, -122.6f, CreateCh17LeftCurve());
            }

            // 3. Large Right (+122.6°, 0.933s, Mirrored Ch17 Left Turn FBX)
            if (largeRightProfile == null || largeRightProfile.rotationProfile == null || largeRightProfile.rotationProfile.length == 0)
            {
                largeRightProfile = new TurnProfileData("Large Right", 0.933f, 122.6f, CreateCh17LeftCurve());
            }

            // 4. Large Left (-122.6°, 0.933s, Ch17 Left Turn FBX)
            if (largeLeftProfile == null || largeLeftProfile.rotationProfile == null || largeLeftProfile.rotationProfile.length == 0)
            {
                largeLeftProfile = new TurnProfileData("Large Left", 0.933f, -122.6f, CreateCh17LeftCurve());
            }

            // 5. Half Right (+177.7°, 1.233s, Stand Half Turn Right FBX)
            if (halfRightProfile == null || halfRightProfile.rotationProfile == null || halfRightProfile.rotationProfile.length == 0)
            {
                halfRightProfile = new TurnProfileData("Half Right", 1.233f, 177.7f, CreateHalfTurnCurve());
            }

            // 6. Half Left (-171.0°, 1.233s, Stand Half Turn Left FBX)
            if (halfLeftProfile == null || halfLeftProfile.rotationProfile == null || halfLeftProfile.rotationProfile.length == 0)
            {
                halfLeftProfile = new TurnProfileData("Half Left", 1.233f, -171.0f, CreateHalfTurnCurve());
            }
        }

        private static AnimationCurve CreateCh17LeftCurve()
        {
            return new AnimationCurve(
                new Keyframe(0.0f, 0.000f, 1.00f, 1.00f),
                new Keyframe(0.1f, 0.100f, 0.95f, 0.95f),
                new Keyframe(0.2f, 0.189f, 0.79f, 0.79f),
                new Keyframe(0.3f, 0.258f, 0.65f, 0.65f),
                new Keyframe(0.4f, 0.318f, 0.58f, 0.58f),
                new Keyframe(0.5f, 0.373f, 0.65f, 0.65f),
                new Keyframe(0.6f, 0.448f, 0.88f, 0.88f),
                new Keyframe(0.7f, 0.548f, 1.12f, 1.12f),
                new Keyframe(0.8f, 0.672f, 1.37f, 1.37f),
                new Keyframe(0.9f, 0.823f, 1.62f, 1.62f),
                new Keyframe(1.0f, 1.000f, 1.50f, 1.50f)
            );
        }

        private static AnimationCurve CreateHalfTurnCurve()
        {
            return new AnimationCurve(
                new Keyframe(0.0f, 0.000f),
                new Keyframe(0.1f, 0.035f),
                new Keyframe(0.2f, 0.144f),
                new Keyframe(0.3f, 0.280f),
                new Keyframe(0.4f, 0.425f),
                new Keyframe(0.5f, 0.583f),
                new Keyframe(0.6f, 0.723f),
                new Keyframe(0.7f, 0.822f),
                new Keyframe(0.8f, 0.899f),
                new Keyframe(0.9f, 0.957f),
                new Keyframe(1.0f, 1.000f)
            );
        }

        private void Update()
        {
            CheckGrounded();
            HandleCrouchDimensions();
            HandleLocomotion();
            HandleRotation();
            UpdateAnimator();
            CalculateAngularVelocity();
        }

        private void CheckGrounded()
        {
            // 1. Exclude the Player layer so the character controller NEVER detects itself!
            int playerLayer = gameObject.layer;
            LayerMask mask = (playerLayer != -1) ? (groundLayers & ~(1 << playerLayer)) : groundLayers;

            // 2. Downward SphereCast from above the capsule base
            float castRadius = _characterController.radius * 0.85f;
            Vector3 castOrigin = transform.position + Vector3.up * (_characterController.radius + 0.05f);
            float castDistance = groundCheckOffset + 0.12f;

            bool sphereCastHit = Physics.SphereCast(castOrigin, castRadius, Vector3.down, out RaycastHit hit, castDistance, mask, QueryTriggerInteraction.Ignore);

            // 3. Slope check
            bool onValidSlope = true;
            if (sphereCastHit)
            {
                float slopeAngle = Vector3.Angle(Vector3.up, hit.normal);
                if (slopeAngle > _characterController.slopeLimit)
                {
                    onValidSlope = false;
                    // Slide down steep slopes
                    Vector3 slideDir = Vector3.ProjectOnPlane(Vector3.down, hit.normal).normalized;
                    _horizontalVelocity += slideDir * (gravity * Time.deltaTime);
                }
            }

            // 4. Grounded state
            IsGrounded = onValidSlope && (sphereCastHit || _characterController.isGrounded);

            // 5. Apply Gravity or Ground Stick
            if (IsGrounded)
            {
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = -groundStickForce;
                }
            }
            else
            {
                // In Air: Full acceleration under gravity!
                _verticalVelocity -= gravity * Time.deltaTime;
                _verticalVelocity = Mathf.Max(_verticalVelocity, -50f);
            }

            // 6. Ceiling collision: cancel upward momentum
            if ((_characterController.collisionFlags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
            {
                _verticalVelocity = 0f;
            }
        }

        private void HandleCrouchDimensions()
        {
            float targetHeight = IsCrouching ? crouchHeight : standingHeight;
            Vector3 targetCenter = IsCrouching ? crouchCenter : standingCenter;

            _characterController.height = Mathf.Lerp(_characterController.height, targetHeight, 10f * Time.deltaTime);
            _characterController.center = Vector3.Lerp(_characterController.center, targetCenter, 10f * Time.deltaTime);
        }

        private void HandleLocomotion()
        {
            Vector2 input = _inputHandler.MoveInput;
            float inputMagnitude = Mathf.Clamp01(input.magnitude);

            // Determine Target Speed
            float targetSpeed = 0f;
            if (inputMagnitude > 0.01f)
            {
                if (IsCrouching)
                {
                    targetSpeed = crouchSpeed;
                }
                else if (IsSprinting)
                {
                    targetSpeed = sprintSpeed;
                }
                else if (inputMagnitude > 0.7f)
                {
                    targetSpeed = jogSpeed;
                }
                else
                {
                    targetSpeed = walkSpeed;
                }
            }

            // Calculate camera-relative movement direction
            Vector3 moveDir = Vector3.zero;
            if (mainCameraTransform != null)
            {
                Vector3 camForward = Vector3.ProjectOnPlane(mainCameraTransform.forward, Vector3.up).normalized;
                Vector3 camRight = Vector3.ProjectOnPlane(mainCameraTransform.right, Vector3.up).normalized;
                moveDir = (camForward * input.y + camRight * input.x).normalized;
            }
            else
            {
                moveDir = new Vector3(input.x, 0f, input.y).normalized;
            }

            Vector3 targetVelocity = moveDir * (targetSpeed * inputMagnitude);

            // Acceleration / Deceleration
            float rate = (targetSpeed > 0.01f) ? acceleration : deceleration;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, targetVelocity, rate * Time.deltaTime);

            // Move CharacterController with horizontal and vertical velocity
            Vector3 finalMove = _horizontalVelocity + Vector3.up * _verticalVelocity;
            _characterController.Move(finalMove * Time.deltaTime);
        }

        private void HandleRotation()
        {
            Vector2 input = _inputHandler != null ? _inputHandler.MoveInput : Vector2.zero;

            if (input.sqrMagnitude > 0.01f && _horizontalVelocity.sqrMagnitude > 0.05f)
            {
                // In Exploration Moving: Smoothly turn towards velocity vector
                if (_isTurningInPlace)
                {
                    CancelExplorationTurnInPlace();
                }

                float targetAngle = Mathf.Atan2(_horizontalVelocity.x, _horizontalVelocity.z) * Mathf.Rad2Deg;
                float smoothAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _turnSmoothVelocity, turnSmoothTime);
                transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);
            }
            else
            {
                // In Exploration Idle: Complete 360 degree orbit freedom, with Turn-In-Place when camera orbits past limit
                if (_isTurningInPlace)
                {
                    // Movement Interruption:
                    // After the initial protected window, meaningful stick input cleanly cancels the turn into locomotion
                    if (_turnTimer > turnProtectedWindow && input.sqrMagnitude > turnInterruptMoveThreshold)
                    {
                        CancelExplorationTurnInPlace();
                        return;
                    }

                    // Progress the synchronized turn
                    _turnTimer += Time.deltaTime;
                    float duration = _currentTurnDuration > 0f ? _currentTurnDuration : 1.0f;
                    float normalizedTime = Mathf.Clamp01(_turnTimer / duration);

                    float rotProgress = (_currentTurnCurve != null && _currentTurnCurve.length > 0)
                        ? _currentTurnCurve.Evaluate(normalizedTime)
                        : normalizedTime;

                    // 1. Gameplay Root: rotates toward target angle via curve
                    float newYaw = Mathf.LerpAngle(_turnStartYaw, _targetTurnYaw, rotProgress);
                    transform.rotation = Quaternion.Euler(0f, newYaw, 0f);

                    // 2. Visual Model Root: decoupled from capsule, progresses along native animation yaw
                    _currentVisualYaw = _visualStartYaw + rotProgress * _currentTurnNativeYaw;

                    // Turn completion: full animation duration elapsed (not an artificial angle threshold)
                    if (_turnTimer >= duration)
                    {
                        transform.rotation = Quaternion.Euler(0f, _targetTurnYaw, 0f);
                        CancelExplorationTurnInPlace(startAlignment: true);
                    }
                }
                else
                {
                    _turnAngleToTarget = 0f;
                }
            }
        }

        private void CalculateAngularVelocity()
        {
            float currentYaw = transform.eulerAngles.y;
            _turnAngleDelta = Mathf.DeltaAngle(_previousYaw, currentYaw) / Mathf.Max(Time.deltaTime, 0.001f);
            _previousYaw = currentYaw;
        }

        private void UpdateAnimator()
        {
            if (characterAnimator == null) return;

            characterAnimator.SetFloat(AnimSpeed, CurrentSpeed);
            characterAnimator.SetBool(AnimIsGrounded, IsGrounded);
            characterAnimator.SetBool(AnimIsCrouching, IsCrouching);
            characterAnimator.SetBool(AnimIsTurningInPlace, _isTurningInPlace);
            characterAnimator.SetInteger(AnimTurnType, (int)_activeTurnType);
            characterAnimator.SetInteger(AnimTurnDirection, (int)_activeTurnDirection);
            characterAnimator.SetFloat(AnimTurn, _turnAngleDelta / 100f);
            characterAnimator.SetFloat(AnimTurnAngle, _turnAngleToTarget);
        }

        public void SetAnimator(Animator newAnimator)
        {
            characterAnimator = newAnimator;
            if (characterAnimator != null)
            {
                characterAnimator.applyRootMotion = false;
            }
        }

        public void SetCameraTransform(Transform camTransform)
        {
            mainCameraTransform = camTransform;
        }

        public bool ClassifyTurn(float signedAngle, out TurnType turnType, out TurnDirection direction, out TurnProfileData profile)
        {
            float absAngle = Mathf.Abs(signedAngle);
            direction = signedAngle < 0f ? TurnDirection.Left : TurnDirection.Right;

            if (absAngle < turnTriggerThreshold)
            {
                turnType = TurnType.None;
                profile = null;
                return false;
            }

            if (direction == TurnDirection.Right)
            {
                if (absAngle <= normalMaxAngleRight)
                {
                    turnType = TurnType.Normal;
                    profile = normalRightProfile;
                }
                else if (absAngle <= largeMaxAngleRight)
                {
                    turnType = TurnType.Large;
                    profile = largeRightProfile;
                }
                else
                {
                    turnType = TurnType.Half;
                    profile = halfRightProfile;
                }
            }
            else
            {
                if (absAngle <= normalMaxAngleLeft)
                {
                    turnType = TurnType.Normal;
                    profile = normalLeftProfile;
                }
                else if (absAngle <= largeMaxAngleLeft)
                {
                    turnType = TurnType.Large;
                    profile = largeLeftProfile;
                }
                else
                {
                    turnType = TurnType.Half;
                    profile = halfLeftProfile;
                }
            }

            return true;
        }

        public void RequestExplorationTurnInPlace(float targetYaw, float signedAngle)
        {
            if (!enableTurnInPlace || IsCrouching || CurrentSpeed > 0.1f || _isTurningInPlace) return;

            if (!ClassifyTurn(signedAngle, out TurnType turnType, out TurnDirection direction, out TurnProfileData profile))
            {
                return;
            }

            _isTurningInPlace = true;
            _activeTurnType = turnType;
            _activeTurnDirection = direction;
            _turnTimer = 0f;
            _turnStartYaw = transform.eulerAngles.y;
            _targetTurnYaw = targetYaw;
            _turnAngleToTarget = signedAngle;

            _currentTurnDuration = (profile != null && profile.duration > 0f) ? profile.duration : 1.0f;
            _currentTurnCurve = (profile != null && profile.rotationProfile != null && profile.rotationProfile.length > 0)
                ? profile.rotationProfile
                : null;
            _currentTurnNativeYaw = (profile != null) ? profile.nativeYaw : signedAngle;

            // Initialize Visual Root Decoupling
            _visualStartYaw = (visualModelRoot != null) ? visualModelRoot.eulerAngles.y : _turnStartYaw;
            _currentVisualYaw = _visualStartYaw;
            _isAligningVisual = false;
            _alignTimer = 0f;

            if (characterAnimator != null)
            {
                characterAnimator.SetBool(AnimIsTurningInPlace, true);
                characterAnimator.SetInteger(AnimTurnType, (int)_activeTurnType);
                characterAnimator.SetInteger(AnimTurnDirection, (int)_activeTurnDirection);
                characterAnimator.SetFloat(AnimTurnAngle, _turnAngleToTarget);
            }
        }

        public void CancelExplorationTurnInPlace(bool startAlignment = false)
        {
            _isTurningInPlace = false;
            _activeTurnType = TurnType.None;
            _turnAngleToTarget = 0f;
            _currentTurnCurve = null;

            if (characterAnimator != null)
            {
                characterAnimator.SetBool(AnimIsTurningInPlace, false);
                characterAnimator.SetInteger(AnimTurnType, 0);
            }

            if (startAlignment && visualModelRoot != null)
            {
                float deltaAngle = Mathf.DeltaAngle(_currentVisualYaw, transform.eulerAngles.y);
                if (Mathf.Abs(deltaAngle) > 0.05f)
                {
                    _isAligningVisual = true;
                    _alignTimer = 0f;
                    _alignStartVisualYaw = _currentVisualYaw;
                }
                else
                {
                    _isAligningVisual = false;
                    visualModelRoot.localRotation = Quaternion.identity;
                }
            }
            else
            {
                _isAligningVisual = false;
                if (visualModelRoot != null)
                {
                    visualModelRoot.localRotation = Quaternion.identity;
                }
            }
        }

        private void LateUpdate()
        {
            HandleVisualRootOrientation();
        }

        private void HandleVisualRootOrientation()
        {
            if (visualModelRoot == null) visualModelRoot = transform.Find("VisualModel_Root");
            if (visualModelRoot == null) return;

            if (_isTurningInPlace)
            {
                // Active Turn: decouple visual root rotation in world space
                visualModelRoot.rotation = Quaternion.Euler(0f, _currentVisualYaw, 0f);
            }
            else if (_isAligningVisual)
            {
                // Alignment Phase: smoothly converge to Gameplay Root over turnEndAlignDuration (0.20s)
                _alignTimer += Time.deltaTime;
                float tau = Mathf.Clamp01(_alignTimer / Mathf.Max(turnEndAlignDuration, 0.01f));
                float s = Mathf.SmoothStep(0f, 1f, tau);

                _currentVisualYaw = Mathf.LerpAngle(_alignStartVisualYaw, transform.eulerAngles.y, s);
                visualModelRoot.rotation = Quaternion.Euler(0f, _currentVisualYaw, 0f);

                if (tau >= 1.0f || Mathf.Abs(Mathf.DeltaAngle(_currentVisualYaw, transform.eulerAngles.y)) < 0.05f)
                {
                    _isAligningVisual = false;
                    visualModelRoot.localRotation = Quaternion.identity;
                }
            }
        }

        private void OnGUI()
        {
            if (!showTurnDebugHUD) return;

            float gpYaw = transform.eulerAngles.y;
            float visYaw = (visualModelRoot != null) ? visualModelRoot.eulerAngles.y : gpYaw;
            float diff = Mathf.DeltaAngle(visYaw, gpYaw);

            string stateStr = _isTurningInPlace ? $"Turn ({_activeTurnType} {_activeTurnDirection})" : (_isAligningVisual ? "Aligning" : "Idle/Move");
            string alignPct = _isAligningVisual ? $"{Mathf.Clamp01(_alignTimer / Mathf.Max(turnEndAlignDuration, 0.01f)) * 100f:F0}%" : "N/A";

            GUI.Box(new Rect(10, 10, 230, 130), "Turn Visual Decoupling HUD");
            GUI.Label(new Rect(20, 35, 210, 20), $"Gameplay Yaw: {gpYaw:F1}°");
            GUI.Label(new Rect(20, 55, 210, 20), $"Visual Yaw:   {visYaw:F1}°");
            GUI.Label(new Rect(20, 75, 210, 20), $"Yaw Delta:    {diff:F2}°");
            GUI.Label(new Rect(20, 95, 210, 20), $"State:        {stateStr}");
            GUI.Label(new Rect(20, 115, 210, 20), $"Align Prog:   {alignPct}");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            if (_characterController != null)
            {
                Vector3 castOrigin = transform.position + Vector3.up * (_characterController.radius + 0.05f);
                Gizmos.DrawWireSphere(castOrigin, _characterController.radius * 0.85f);
                Gizmos.DrawLine(castOrigin, castOrigin + Vector3.down * (groundCheckOffset + 0.12f));
            }
        }
    }
}
