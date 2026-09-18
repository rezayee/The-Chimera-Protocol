using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheChimeraProtocol.Player
{
    /// <summary>
    /// Handles player inputs via Unity's New Input System.
    /// Provides smoothed input values, event dispatching, and future-proof rebinding support.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("Input Action Asset")]
        [Tooltip("Reference to the PlayerInputActions asset.")]
        [SerializeField] private InputActionAsset inputAsset;

        [Header("Sensitivity and Smoothing")]
        [Range(0.1f, 10f)] public float mouseSensitivity = 1.0f;
        [Range(0.1f, 10f)] public float gamepadSensitivity = 2.5f;
        [Tooltip("Smooth damping on look input")]
        [Range(0f, 0.2f)] public float lookDamping = 0.03f;
        public bool invertY = false;

        // Continuous Input States
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool AimHeld { get; private set; }
        public bool AttackHeld { get; private set; }
        public bool CrouchToggled { get; set; }

        // Action Events
        public event Action OnInteractPressed;
        public event Action OnAttackPressed;
        public event Action OnAttackReleased;
        public event Action OnFlashlightTogglePressed;
        public event Action OnShoulderSwitchPressed;
        public event Action OnCrouchTogglePressed;

        // Weapon Events
        public event Action OnSlot1Pressed;
        public event Action OnSlot2Pressed;
        public event Action OnHolsterPressed;
        public event Action OnReloadPressed;
        public event Action OnNextWeaponPressed;
        public event Action OnPrevWeaponPressed;

        // Private Actions
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _sprintAction;
        private InputAction _aimAction;
        private InputAction _interactAction;
        private InputAction _attackAction;
        private InputAction _crouchAction;
        private InputAction _flashlightAction;
        private InputAction _shoulderSwitchAction;
        private InputAction _slot1Action;
        private InputAction _slot2Action;
        private InputAction _holsterAction;
        private InputAction _reloadAction;
        private InputAction _nextWeaponAction;
        private InputAction _prevWeaponAction;

        private Vector2 _currentLookVelocity;
        private Vector2 _rawLookInput;
        private const string ActionMapName = "Player";

        public InputActionAsset InputAsset
        {
            get => inputAsset;
            set
            {
                inputAsset = value;
                InitializeInputs();
            }
        }

        private void Awake()
        {
            InitializeInputs();
        }

        private void InitializeInputs()
        {
            if (inputAsset == null)
            {
                inputAsset = Resources.Load<InputActionAsset>("PlayerInputActions");
            }

            if (inputAsset == null)
            {
                return;
            }

            var map = inputAsset.FindActionMap(ActionMapName);
            if (map == null)
            {
                Debug.LogError($"[PlayerInputHandler] Action map '{ActionMapName}' not found in InputActionAsset!");
                return;
            }

            _moveAction = map.FindAction("Move");
            _lookAction = map.FindAction("Look");
            _sprintAction = map.FindAction("Sprint");
            _aimAction = map.FindAction("Aim");
            _interactAction = map.FindAction("Interact");
            _attackAction = map.FindAction("Attack");
            _crouchAction = map.FindAction("Crouch");
            _flashlightAction = map.FindAction("Flashlight");
            _shoulderSwitchAction = map.FindAction("ShoulderSwitch");
            _slot1Action = map.FindAction("Slot1");
            _slot2Action = map.FindAction("Slot2");
            _holsterAction = map.FindAction("Holster");
            _reloadAction = map.FindAction("Reload");
            _nextWeaponAction = map.FindAction("NextWeapon");
            _prevWeaponAction = map.FindAction("PrevWeapon");

            // Event callbacks
            if (_interactAction != null)
                _interactAction.performed += _ => OnInteractPressed?.Invoke();

            if (_attackAction != null)
            {
                _attackAction.performed += _ => OnAttackPressed?.Invoke();
                _attackAction.canceled += _ => OnAttackReleased?.Invoke();
            }

            if (_flashlightAction != null)
                _flashlightAction.performed += _ => OnFlashlightTogglePressed?.Invoke();

            if (_shoulderSwitchAction != null)
                _shoulderSwitchAction.performed += _ => OnShoulderSwitchPressed?.Invoke();

            if (_crouchAction != null)
            {
                _crouchAction.performed += _ =>
                {
                    CrouchToggled = !CrouchToggled;
                    OnCrouchTogglePressed?.Invoke();
                };
            }

            if (_slot1Action != null) _slot1Action.performed += _ => OnSlot1Pressed?.Invoke();
            if (_slot2Action != null) _slot2Action.performed += _ => OnSlot2Pressed?.Invoke();
            if (_holsterAction != null) _holsterAction.performed += _ => OnHolsterPressed?.Invoke();
            if (_reloadAction != null) _reloadAction.performed += _ => OnReloadPressed?.Invoke();
            if (_nextWeaponAction != null) _nextWeaponAction.performed += _ => OnNextWeaponPressed?.Invoke();
            if (_prevWeaponAction != null) _prevWeaponAction.performed += _ => OnPrevWeaponPressed?.Invoke();
        }

        private void OnEnable()
        {
            inputAsset?.FindActionMap(ActionMapName)?.Enable();
        }

        private void OnDisable()
        {
            inputAsset?.FindActionMap(ActionMapName)?.Disable();
        }

        private void Update()
        {
            ReadContinuousInputs();
        }

        private void ReadContinuousInputs()
        {
            if (_moveAction != null)
            {
                MoveInput = _moveAction.ReadValue<Vector2>();
            }

            if (_lookAction != null)
            {
                _rawLookInput = _lookAction.ReadValue<Vector2>();

                float sensitivity = IsGamepadActive() ? gamepadSensitivity * 50f : mouseSensitivity;
                Vector2 targetLook = _rawLookInput * sensitivity;

                if (invertY)
                    targetLook.y = -targetLook.y;

                LookInput = Vector2.SmoothDamp(LookInput, targetLook, ref _currentLookVelocity, lookDamping);
            }

            if (_sprintAction != null)
                SprintHeld = _sprintAction.IsPressed();

            if (_aimAction != null)
                AimHeld = _aimAction.IsPressed();

            if (_attackAction != null)
                AttackHeld = _attackAction.IsPressed();
        }

        public bool IsGamepadActive()
        {
            return Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame;
        }

        public void StartRebinding(string actionName, int bindingIndex, Action onComplete)
        {
            var action = inputAsset?.FindActionMap(ActionMapName)?.FindAction(actionName);
            if (action == null) return;

            action.Disable();
            action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(op =>
                {
                    action.Enable();
                    op.Dispose();
                    onComplete?.Invoke();
                })
                .Start();
        }

        public string SaveCustomBindings()
        {
            return inputAsset != null ? inputAsset.SaveBindingOverridesAsJson() : string.Empty;
        }

        public void LoadCustomBindings(string json)
        {
            if (inputAsset != null && !string.IsNullOrEmpty(json))
            {
                inputAsset.LoadBindingOverridesFromJson(json);
            }
        }
    }
}
