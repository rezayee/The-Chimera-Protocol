using System;
using System.Collections;
using UnityEngine;
using TheChimeraProtocol.Player;

namespace TheChimeraProtocol.Weapons
{
    public enum ActiveWeaponSlot
    {
        None = 0,
        Primary = 1,
        Secondary = 2
    }

    /// <summary>
    /// Coordinates weapon inventory, equipping, ground pickups, weapon dropping,
    /// hip-fire shooting with realistic spread, reload states, camera recoil,
    /// and UpperBody Mecanim animation integration.
    /// Fully decoupled from IK and 100% compatible with CharacterModelSwapper.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerWeaponController : MonoBehaviour
    {
        [Header("Equipped Weapon Slots")]
        [SerializeField] private WeaponInstance primaryWeapon;
        [SerializeField] private WeaponInstance secondaryWeapon;
        [SerializeField] private ActiveWeaponSlot activeSlot = ActiveWeaponSlot.None;
        [SerializeField] private bool isHolstered = false;

        [Header("Starting Weapon (Optional)")]
        [SerializeField] private WeaponData startingWeaponData;

        [Header("Socket Configuration")]
        [SerializeField] private Transform rightHandSocket;
        [SerializeField] private Transform holsterSpineSocket;
        [SerializeField] private Transform holsterHipsSocket;

        [Header("Default Socket Offsets (Preserved Across Models)")]
        [SerializeField] private Vector3 handSocketLocalPos = new Vector3(0.0150f, -0.0020f, 0.0020f);
        [SerializeField] private Vector3 handSocketLocalRot = new Vector3(12.1928f, 101.8517f, 76.6811f);
        [SerializeField] private Vector3 spineHolsterLocalPos = new Vector3(0.18f, 0.10f, -0.15f);
        [SerializeField] private Vector3 spineHolsterLocalRot = new Vector3(0f, 180f, 45f);
        [SerializeField] private Vector3 hipsHolsterLocalPos = new Vector3(0.16f, -0.05f, 0.05f);
        [SerializeField] private Vector3 hipsHolsterLocalRot = new Vector3(0f, 90f, 0f);

        [Header("Raycast & Hit Detection")]
        [SerializeField] private LayerMask hitLayers = ~0;
        [SerializeField] private float maxPickupDistance = 3.0f;
        [Tooltip("Minimum cosine angle between camera forward and pickup direction for look-at targeting (0.85 = ~31 degrees)")]
        [SerializeField] private float pickupLookDotThreshold = 0.85f;

        [Header("Drop Weapon Settings")]
        [SerializeField] private KeyCode dropKey = KeyCode.G;
        [SerializeField] private float dropThrowForce = 2.5f;

        [Header("Aiming Settings")]
        [SerializeField] private float aimTurnSpeed = 14f;
        [SerializeField] private float aimSpreadMultiplier = 0.25f;

        // References & State
        private ActiveWeaponSlot _lastActiveSlot = ActiveWeaponSlot.Primary;
        private PlayerInputHandler _inputHandler;
        private PlayerController _playerController;
        private ThirdPersonCameraController _cameraController;
        private CharacterModelSwapper _modelSwapper;
        private Animator _animator;
        private Camera _mainCamera;

        // Animator Hashes
        private static readonly int HashIsWeaponEquipped = Animator.StringToHash("IsWeaponEquipped");
        private static readonly int HashWeaponType = Animator.StringToHash("WeaponType");
        private static readonly int HashShootTrigger = Animator.StringToHash("ShootTrigger");
        private static readonly int HashReloadTrigger = Animator.StringToHash("ReloadTrigger");
        public static readonly int HashIsAiming = Animator.StringToHash("IsAiming");

        public WeaponInstance ActiveWeapon => isHolstered ? null : (activeSlot == ActiveWeaponSlot.Primary ? primaryWeapon : (activeSlot == ActiveWeaponSlot.Secondary ? secondaryWeapon : null));
        public ActiveWeaponSlot ActiveSlot => activeSlot;
        public bool IsHolstered => isHolstered;
        public bool IsAiming { get; private set; }
        public WeaponPickup CurrentTargetPickup { get; private set; }
        public WeaponInstance PrimaryWeapon => primaryWeapon;
        public WeaponInstance SecondaryWeapon => secondaryWeapon;
        public Transform RightHandSocket => rightHandSocket;
        public Transform HolsterSpineSocket => holsterSpineSocket;
        public Transform HolsterHipsSocket => holsterHipsSocket;

        // Events
        public event Action<WeaponInstance> OnWeaponEquipped;
        public event Action<WeaponInstance> OnWeaponFired;
        public event Action<WeaponInstance> OnWeaponReloadStarted;
        public event Action OnWeaponHolstered;

        private void Awake()
        {
            _inputHandler = GetComponent<PlayerInputHandler>();
            _playerController = GetComponent<PlayerController>();
            _cameraController = GetComponent<ThirdPersonCameraController>();
            _modelSwapper = GetComponent<CharacterModelSwapper>();
            _animator = GetComponentInChildren<Animator>();
            _mainCamera = Camera.main;

            AutoResolveSockets();
            SubscribeInputs();

            if (_modelSwapper != null)
            {
                _modelSwapper.OnModelSwapped += HandleModelSwapped;
            }
        }

        private void Start()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;

            if (startingWeaponData != null && primaryWeapon == null)
            {
                EquipNewWeapon(startingWeaponData, WeaponSlotType.Primary);
            }
        }

        private void OnDestroy()
        {
            UnsubscribeInputs();
            if (_modelSwapper != null)
            {
                _modelSwapper.OnModelSwapped -= HandleModelSwapped;
            }
        }

        private void Update()
        {
            UpdateAimState();
            UpdateNearbyPickupCheck();
            HandleShootingInput();
            HandleDropInput();
        }

        private void UpdateAimState()
        {
            bool wantsAim = _inputHandler != null && _inputHandler.AimHeld && ActiveWeapon != null && !isHolstered;
            if (IsAiming != wantsAim)
            {
                IsAiming = wantsAim;
                UpdateAnimatorState();
            }

            if (IsAiming && _mainCamera != null)
            {
                Vector3 camFwd = _mainCamera.transform.forward;
                camFwd.y = 0f;
                if (camFwd.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(camFwd);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * aimTurnSpeed);
                }
            }
        }

        private void UpdateNearbyPickupCheck()
        {
            CurrentTargetPickup = FindBestLookAtPickup();
        }

        /// <summary>
        /// Finds the closest ground pickup that the player is actively looking at within range.
        /// </summary>
        public WeaponPickup FindBestLookAtPickup()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;

            Vector3 checkCenter = transform.position + Vector3.up * 1.0f;
            Collider[] colliders = Physics.OverlapSphere(checkCenter, maxPickupDistance, hitLayers, QueryTriggerInteraction.Collide);

            WeaponPickup bestPickup = null;
            float bestScore = -1f;

            Vector3 camPos = _mainCamera != null ? _mainCamera.transform.position : checkCenter;
            Vector3 camForward = _mainCamera != null ? _mainCamera.transform.forward : transform.forward;

            foreach (var col in colliders)
            {
                if (col == null) continue;
                // Never consider colliders that are attached to the player or player's equipped weapons
                if (col.transform.IsChildOf(transform)) continue;

                var pickup = col.GetComponentInParent<WeaponPickup>();
                if (pickup == null || !pickup.enabled || !pickup.gameObject.activeInHierarchy || pickup.Data == null) continue;
                if (pickup.transform.IsChildOf(transform)) continue;

                Vector3 pickupCenter = pickup.transform.position + Vector3.up * 0.12f;
                float distToPlayer = Vector3.Distance(checkCenter, pickupCenter);
                if (distToPlayer > maxPickupDistance) continue;

                Vector3 dirFromCam = (pickupCenter - camPos).normalized;
                float lookDot = Vector3.Dot(camForward, dirFromCam);

                // Look-at filter: Player must be looking towards the weapon within the view cone
                if (lookDot < pickupLookDotThreshold) continue;

                // Line of sight check from character eye level to ensure walls/obstacles don't block interaction
                Vector3 eyePos = transform.position + Vector3.up * 1.4f;
                if (Physics.Linecast(eyePos, pickupCenter, out RaycastHit hit, hitLayers, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform != pickup.transform && !hit.transform.IsChildOf(pickup.transform) && !hit.transform.IsChildOf(transform))
                    {
                        continue; // Blocked by wall/obstacle
                    }
                }

                if (lookDot > bestScore)
                {
                    bestScore = lookDot;
                    bestPickup = pickup;
                }
            }

            return bestPickup;
        }

        #region Input Subscriptions
        private void SubscribeInputs()
        {
            if (_inputHandler == null) return;

            _inputHandler.OnAttackPressed += HandleAttackPressed;
            _inputHandler.OnReloadPressed += HandleReloadPressed;
            _inputHandler.OnSlot1Pressed += HandleSlot1Pressed;
            _inputHandler.OnSlot2Pressed += HandleSlot2Pressed;
            _inputHandler.OnHolsterPressed += HandleHolsterPressed;
            _inputHandler.OnNextWeaponPressed += HandleNextWeaponPressed;
            _inputHandler.OnPrevWeaponPressed += HandlePrevWeaponPressed;
            _inputHandler.OnInteractPressed += HandleInteractPressed;
        }

        private void UnsubscribeInputs()
        {
            if (_inputHandler == null) return;

            _inputHandler.OnAttackPressed -= HandleAttackPressed;
            _inputHandler.OnReloadPressed -= HandleReloadPressed;
            _inputHandler.OnSlot1Pressed -= HandleSlot1Pressed;
            _inputHandler.OnSlot2Pressed -= HandleSlot2Pressed;
            _inputHandler.OnHolsterPressed -= HandleHolsterPressed;
            _inputHandler.OnNextWeaponPressed -= HandleNextWeaponPressed;
            _inputHandler.OnPrevWeaponPressed -= HandlePrevWeaponPressed;
            _inputHandler.OnInteractPressed -= HandleInteractPressed;
        }
        #endregion

        #region Socket & Swapper Management
        public void AutoResolveSockets()
        {
            _animator = GetComponentInChildren<Animator>();
            if (_animator == null || !_animator.isHuman) return;

            // 1. Right Hand Socket
            Transform rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (rightHand != null)
            {
                Transform existingSocket = rightHand.Find("RightHand_WeaponSocket");
                if (existingSocket != null)
                {
                    rightHandSocket = existingSocket;
                    handSocketLocalPos = existingSocket.localPosition;
                    handSocketLocalRot = existingSocket.localEulerAngles;
                }
                else
                {
                    var newSocket = new GameObject("RightHand_WeaponSocket");
                    newSocket.transform.SetParent(rightHand, false);
                    newSocket.transform.localPosition = handSocketLocalPos;
                    newSocket.transform.localRotation = Quaternion.Euler(handSocketLocalRot);
                    rightHandSocket = newSocket.transform;
                }
            }

            // 2. Spine / Chest Holster Socket (Primary / Long Guns)
            Transform spine = _animator.GetBoneTransform(HumanBodyBones.Chest) ?? _animator.GetBoneTransform(HumanBodyBones.Spine);
            if (spine != null)
            {
                Transform existingHolster = spine.Find("Spine_WeaponHolster") ?? spine.Find("Spine_PrimaryHolster");
                if (existingHolster != null)
                {
                    holsterSpineSocket = existingHolster;
                    spineHolsterLocalPos = existingHolster.localPosition;
                    spineHolsterLocalRot = existingHolster.localEulerAngles;
                }
                else
                {
                    var newHolster = new GameObject("Spine_WeaponHolster");
                    newHolster.transform.SetParent(spine, false);
                    newHolster.transform.localPosition = spineHolsterLocalPos;
                    newHolster.transform.localRotation = Quaternion.Euler(spineHolsterLocalRot);
                    holsterSpineSocket = newHolster.transform;
                }
            }

            // 3. Hips / Thigh Holster Socket (Secondary / Sidearms)
            Transform hips = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg) ?? _animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null)
            {
                Transform existingHolster = hips.Find("Hips_SecondaryHolster");
                if (existingHolster != null)
                {
                    holsterHipsSocket = existingHolster;
                    hipsHolsterLocalPos = existingHolster.localPosition;
                    hipsHolsterLocalRot = existingHolster.localEulerAngles;
                }
                else
                {
                    var newHolster = new GameObject("Hips_SecondaryHolster");
                    newHolster.transform.SetParent(hips, false);
                    newHolster.transform.localPosition = hipsHolsterLocalPos;
                    newHolster.transform.localRotation = Quaternion.Euler(hipsHolsterLocalRot);
                    holsterHipsSocket = newHolster.transform;
                }
            }
        }

        public void HandleModelSwapped(Animator newAnimator)
        {
            _animator = newAnimator;
            AutoResolveSockets();
            UpdateAllWeaponSockets();
            UpdateAnimatorState();
        }

        public void UpdateAllWeaponSockets()
        {
            if (primaryWeapon != null)
            {
                bool isHeld = (activeSlot == ActiveWeaponSlot.Primary && !isHolstered);
                AttachWeaponToSocket(primaryWeapon, isHeld ? rightHandSocket : holsterSpineSocket, isHeld);
            }

            if (secondaryWeapon != null)
            {
                bool isHeld = (activeSlot == ActiveWeaponSlot.Secondary && !isHolstered);
                AttachWeaponToSocket(secondaryWeapon, isHeld ? rightHandSocket : holsterHipsSocket, isHeld);
            }
        }

        private void AttachWeaponToSocket(WeaponInstance weapon, Transform targetSocket, bool isHeldInHand)
        {
            if (weapon == null || targetSocket == null) return;

            StripPickupComponents(weapon.gameObject);

            weapon.transform.SetParent(targetSocket, false);
            if (isHeldInHand && weapon.Data != null)
            {
                weapon.transform.localPosition = weapon.Data.gripPositionOffset;
                weapon.transform.localRotation = Quaternion.Euler(weapon.Data.gripRotationOffset);
            }
            else
            {
                weapon.transform.localPosition = Vector3.zero;
                weapon.transform.localRotation = Quaternion.identity;
            }
            weapon.gameObject.SetActive(true);
        }

        private void StripPickupComponents(GameObject weaponObj)
        {
            if (weaponObj == null) return;

            var pickups = weaponObj.GetComponentsInChildren<WeaponPickup>(true);
            foreach (var p in pickups)
            {
                p.enabled = false;
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(p);
                else Destroy(p);
#else
                Destroy(p);
#endif
            }

            var colliders = weaponObj.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                col.enabled = false;
            }

            var rb = weaponObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }
        }
        #endregion

        #region Weapon Switching & Equipping
        public void EquipNewWeapon(WeaponData data, WeaponSlotType slotType, int startingMag = -1, int startingReserve = -1)
        {
            if (data == null) return;

            // Instantiate or create weapon instance
            GameObject prefab = data.weaponPrefab;
            GameObject instanceObj = (prefab != null) ? Instantiate(prefab) : new GameObject(data.weaponName);

            StripPickupComponents(instanceObj);

            WeaponInstance instance = instanceObj.GetComponent<WeaponInstance>();
            if (instance == null)
            {
                instance = instanceObj.AddComponent<WeaponInstance>();
            }
            instance.Initialize(data, startingMag, startingReserve);

            RegisterEquippedWeapon(instance, slotType);
        }

        public void RegisterEquippedWeapon(WeaponInstance weapon, WeaponSlotType slotType)
        {
            if (weapon == null) return;

            if (slotType == WeaponSlotType.Primary)
            {
                if (primaryWeapon != null && primaryWeapon != weapon)
                {
                    DropWeapon(primaryWeapon);
                }
                primaryWeapon = weapon;
                SwitchToSlot(ActiveWeaponSlot.Primary);
            }
            else
            {
                if (secondaryWeapon != null && secondaryWeapon != weapon)
                {
                    DropWeapon(secondaryWeapon);
                }
                secondaryWeapon = weapon;
                SwitchToSlot(ActiveWeaponSlot.Secondary);
            }
        }

        public void SwitchToSlot(ActiveWeaponSlot slot)
        {
            if (slot == ActiveWeaponSlot.None)
            {
                HolsterActiveWeapon();
                return;
            }

            WeaponInstance targetWeapon = (slot == ActiveWeaponSlot.Primary) ? primaryWeapon : secondaryWeapon;
            if (targetWeapon == null) return;

            activeSlot = slot;
            _lastActiveSlot = slot;
            isHolstered = false;

            UpdateAllWeaponSockets();
            UpdateAnimatorState();
            OnWeaponEquipped?.Invoke(targetWeapon);
        }

        public void HolsterActiveWeapon()
        {
            if (activeSlot == ActiveWeaponSlot.None && isHolstered) return;

            isHolstered = true;
            UpdateAllWeaponSockets();
            UpdateAnimatorState();
            OnWeaponHolstered?.Invoke();
        }

        public void UnholsterActiveWeapon()
        {
            if (activeSlot == ActiveWeaponSlot.None)
            {
                if (primaryWeapon != null) SwitchToSlot(ActiveWeaponSlot.Primary);
                else if (secondaryWeapon != null) SwitchToSlot(ActiveWeaponSlot.Secondary);
                return;
            }

            isHolstered = false;
            UpdateAllWeaponSockets();
            UpdateAnimatorState();
        }

        private void ToggleHolster()
        {
            if (isHolstered) UnholsterActiveWeapon();
            else HolsterActiveWeapon();
        }
        #endregion

        #region Hip-Fire Shooting
        private void HandleShootingInput()
        {
            if (isHolstered)
            {
                if (_inputHandler != null && _inputHandler.AttackHeld)
                {
                    if (TryQuickDrawOnFire())
                    {
                        WeaponInstance weapon = ActiveWeapon;
                        if (weapon != null && weapon.Data.fireMode == FireMode.FullAuto && weapon.CurrentAmmoInMag > 0)
                        {
                            ExecuteFire(weapon);
                        }
                    }
                }
                return;
            }

            WeaponInstance activeWeapon = ActiveWeapon;
            if (activeWeapon == null) return;

            if (activeWeapon.Data.fireMode == FireMode.FullAuto)
            {
                if (_inputHandler != null && _inputHandler.AttackHeld)
                {
                    ExecuteFire(activeWeapon);
                }
            }
        }

        private void HandleAttackPressed()
        {
            if (isHolstered)
            {
                if (TryQuickDrawOnFire())
                {
                    WeaponInstance weapon = ActiveWeapon;
                    if (weapon != null && weapon.CurrentAmmoInMag > 0)
                    {
                        ExecuteFire(weapon);
                    }
                    else if (weapon != null && weapon.CanReload())
                    {
                        HandleReloadPressed();
                    }
                }
                return;
            }

            WeaponInstance activeWeapon = ActiveWeapon;
            if (activeWeapon == null) return;

            if (activeWeapon.Data.fireMode == FireMode.SemiAuto || activeWeapon.Data.fireMode == FireMode.Burst)
            {
                ExecuteFire(activeWeapon);
            }
        }

        /// <summary>
        /// Automatically draws the best weapon when player fires while holstered:
        /// 1. Last equipped weapon if it has ammo in mag
        /// 2. Alternate weapon if it has ammo in mag
        /// 3. Last equipped weapon if it has reserve ammo
        /// 4. Alternate weapon if it has reserve ammo
        /// 5. Whichever weapon exists
        /// </summary>
        public bool TryQuickDrawOnFire()
        {
            if (!isHolstered) return false;

            ActiveWeaponSlot preferred = (_lastActiveSlot != ActiveWeaponSlot.None) ? _lastActiveSlot : ActiveWeaponSlot.Primary;
            ActiveWeaponSlot alternate = (preferred == ActiveWeaponSlot.Primary) ? ActiveWeaponSlot.Secondary : ActiveWeaponSlot.Primary;

            WeaponInstance prefWeapon = GetWeaponInSlot(preferred);
            WeaponInstance altWeapon = GetWeaponInSlot(alternate);

            ActiveWeaponSlot chosenSlot = ActiveWeaponSlot.None;

            // 1. Preferred weapon with ammo in magazine
            if (prefWeapon != null && prefWeapon.CurrentAmmoInMag > 0)
            {
                chosenSlot = preferred;
            }
            // 2. Alternate weapon with ammo in magazine
            else if (altWeapon != null && altWeapon.CurrentAmmoInMag > 0)
            {
                chosenSlot = alternate;
            }
            // 3. Preferred weapon with reserve ammo
            else if (prefWeapon != null && prefWeapon.CurrentReserveAmmo > 0)
            {
                chosenSlot = preferred;
            }
            // 4. Alternate weapon with reserve ammo
            else if (altWeapon != null && altWeapon.CurrentReserveAmmo > 0)
            {
                chosenSlot = alternate;
            }
            // 5. Preferred weapon exists
            else if (prefWeapon != null)
            {
                chosenSlot = preferred;
            }
            // 6. Alternate weapon exists
            else if (altWeapon != null)
            {
                chosenSlot = alternate;
            }

            if (chosenSlot != ActiveWeaponSlot.None)
            {
                SwitchToSlot(chosenSlot);
                return true;
            }

            return false;
        }

        private WeaponInstance GetWeaponInSlot(ActiveWeaponSlot slot)
        {
            switch (slot)
            {
                case ActiveWeaponSlot.Primary: return primaryWeapon;
                case ActiveWeaponSlot.Secondary: return secondaryWeapon;
                default: return null;
            }
        }

        private void ExecuteFire(WeaponInstance weapon)
        {
            if (!weapon.TryFire(out bool fired)) return;

            if (fired)
            {
                PerformHipFireRaycast(weapon);

                // Recoil Kick
                if (_cameraController != null && weapon.Data != null)
                {
                    _cameraController.ApplyRecoil(weapon.Data.cameraKickVertical, weapon.Data.cameraKickHorizontal);
                }

                // Trigger Animation
                if (_animator != null && _animator.gameObject.activeInHierarchy)
                {
                    _animator.SetTrigger(HashShootTrigger);
                }

                OnWeaponFired?.Invoke(weapon);
            }
        }

        private void PerformHipFireRaycast(WeaponInstance weapon)
        {
            if (_mainCamera == null) _mainCamera = Camera.main;

            WeaponData data = weapon.Data;
            float spread = CalculateCurrentSpread(data);
            if (IsAiming) spread *= aimSpreadMultiplier;

            Vector3 muzzlePos = weapon.MuzzlePoint != null ? weapon.MuzzlePoint.position : transform.position + Vector3.up * 1.3f;
            int pellets = Mathf.Max(data.pelletsPerShot, 1);

            for (int i = 0; i < pellets; i++)
            {
                Vector3 shootDirection;

                if (IsAiming && _mainCamera != null)
                {
                    // 1. AIM MODE: Aim converges on screen center crosshair
                    Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * spread;
                    Quaternion spreadRotation = Quaternion.Euler(-randomCircle.y, randomCircle.x, 0f);
                    Vector3 camShootDir = spreadRotation * _mainCamera.transform.forward;

                    Vector3 rayOrigin = _mainCamera.transform.position;
                    Vector3 targetPoint;

                    if (Physics.Raycast(rayOrigin, camShootDir, out RaycastHit camHit, data.maxRange, hitLayers, QueryTriggerInteraction.Ignore))
                    {
                        targetPoint = camHit.point;
                    }
                    else
                    {
                        targetPoint = rayOrigin + camShootDir * data.maxRange;
                    }

                    shootDirection = (targetPoint - muzzlePos).normalized;
                }
                else
                {
                    // 2. HIP-FIRE MODE: Shoots straight forward in front of the gun from the muzzle!
                    Vector3 forwardDir = transform.forward;
                    if (_mainCamera != null)
                    {
                        forwardDir.y = _mainCamera.transform.forward.y;
                        forwardDir.Normalize();
                    }

                    Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * spread;
                    Quaternion spreadRotation = Quaternion.Euler(-randomCircle.y, randomCircle.x, 0f);
                    shootDirection = Quaternion.LookRotation(forwardDir) * (spreadRotation * Vector3.forward);
                }

                if (Physics.Raycast(muzzlePos, shootDirection, out RaycastHit hit, data.maxRange, hitLayers, QueryTriggerInteraction.Ignore))
                {
                    // Don't hit self
                    if (hit.transform.IsChildOf(transform)) continue;

                    // Spawn impact decal & sparks
                    BulletImpactManager.Instance.SpawnImpact(hit, data.impactDecalMaterial);
                }
            }
        }

        private float CalculateCurrentSpread(WeaponData data)
        {
            if (data == null) return 2.5f;

            float spread = data.baseSpreadAngle;

            if (_playerController != null)
            {
                if (_playerController.IsCrouching)
                {
                    spread *= data.crouchSpreadMultiplier;
                }
                else if (_inputHandler != null && _inputHandler.MoveInput.sqrMagnitude > 0.01f)
                {
                    spread *= _inputHandler.SprintHeld ? data.sprintingSpreadMultiplier : data.movingSpreadMultiplier;
                }
            }

            return spread;
        }
        #endregion

        #region Reloading
        private void HandleReloadPressed()
        {
            WeaponInstance weapon = ActiveWeapon;
            if (weapon == null || isHolstered) return;

            if (weapon.CanReload())
            {
                weapon.StartReload();

                if (_animator != null && _animator.gameObject.activeInHierarchy)
                {
                    _animator.SetTrigger(HashReloadTrigger);
                }

                OnWeaponReloadStarted?.Invoke(weapon);
            }
        }
        #endregion

        #region World Interaction & Pickups
        private void HandleInteractPressed()
        {
            WeaponPickup targetPickup = FindBestLookAtPickup();
            if (targetPickup != null)
            {
                PickupWeapon(targetPickup);
            }
        }

        private void PickupWeapon(WeaponPickup pickup)
        {
            if (pickup == null || pickup.Data == null) return;

            WeaponData data = pickup.Data;
            int mag = pickup.AmmoInMag;
            int reserve = pickup.ReserveAmmo;
            GameObject pickupObj = pickup.gameObject;

            // Immediately deactivate and clear reference so HUD prompt vanishes without any linger
            pickupObj.SetActive(false);
            CurrentTargetPickup = null;

            // Check if slot type is occupied
            WeaponSlotType targetSlot = data.slotType;
            WeaponInstance targetInstance = (targetSlot == WeaponSlotType.Primary) ? primaryWeapon : secondaryWeapon;

            // If same weapon already held, pick up ammo into reserve
            if (targetInstance != null && targetInstance.Data.weaponId == data.weaponId)
            {
                targetInstance.AddReserveAmmo(mag + reserve);
                Destroy(pickupObj);
                return;
            }

            // Destroy ground pickup
            Destroy(pickupObj);

            // Equip new weapon
            EquipNewWeapon(data, targetSlot, mag, reserve);
        }

        private void HandleDropInput()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.gKey.wasPressedThisFrame)
            {
                DropActiveWeapon();
            }
        }

        public void DropActiveWeapon()
        {
            WeaponInstance weapon = ActiveWeapon;
            if (weapon == null) return;

            DropWeapon(weapon);

            if (activeSlot == ActiveWeaponSlot.Primary) primaryWeapon = null;
            else if (activeSlot == ActiveWeaponSlot.Secondary) secondaryWeapon = null;

            activeSlot = ActiveWeaponSlot.None;
            UpdateAllWeaponSockets();
            UpdateAnimatorState();
        }

        private void DropWeapon(WeaponInstance weapon)
        {
            if (weapon == null) return;

            // Cancel any active reload
            weapon.CancelReload();

            // Unparent from character
            weapon.transform.SetParent(null, true);
            weapon.transform.position = transform.position + transform.forward * 0.6f + Vector3.up * 1.0f;
            weapon.transform.rotation = transform.rotation;

            // Re-enable or add colliders for ground pickup
            var cols = weapon.GetComponentsInChildren<Collider>(true);
            if (cols.Length == 0)
            {
                var box = weapon.gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(0.15f, 0.35f, 0.85f);
                box.isTrigger = true;
            }
            else
            {
                foreach (var col in cols)
                {
                    col.enabled = true;
                }
            }

            // Add or setup WeaponPickup
            WeaponPickup pickup = weapon.GetComponent<WeaponPickup>();
            if (pickup == null)
            {
                pickup = weapon.gameObject.AddComponent<WeaponPickup>();
            }
            else
            {
                pickup.enabled = true;
            }

            Vector3 throwVelocity = transform.forward * dropThrowForce + Vector3.up * 1.5f;
            pickup.Setup(weapon.Data, weapon.CurrentAmmoInMag, weapon.CurrentReserveAmmo, true, throwVelocity);
        }
        #endregion

        #region Slot Cycling Input Callbacks
        private void HandleSlot1Pressed()
        {
            if (primaryWeapon != null)
            {
                if (activeSlot == ActiveWeaponSlot.Primary && isHolstered) UnholsterActiveWeapon();
                else SwitchToSlot(ActiveWeaponSlot.Primary);
            }
        }

        private void HandleSlot2Pressed()
        {
            if (secondaryWeapon != null)
            {
                if (activeSlot == ActiveWeaponSlot.Secondary && isHolstered) UnholsterActiveWeapon();
                else SwitchToSlot(ActiveWeaponSlot.Secondary);
            }
        }

        private void HandleHolsterPressed()
        {
            ToggleHolster();
        }

        private void HandleNextWeaponPressed()
        {
            CycleWeapon(1);
        }

        private void HandlePrevWeaponPressed()
        {
            CycleWeapon(-1);
        }

        private void CycleWeapon(int direction)
        {
            if (primaryWeapon == null && secondaryWeapon == null) return;

            if (activeSlot == ActiveWeaponSlot.Primary && secondaryWeapon != null)
            {
                SwitchToSlot(ActiveWeaponSlot.Secondary);
            }
            else if (activeSlot == ActiveWeaponSlot.Secondary && primaryWeapon != null)
            {
                SwitchToSlot(ActiveWeaponSlot.Primary);
            }
            else if (activeSlot == ActiveWeaponSlot.None)
            {
                if (primaryWeapon != null) SwitchToSlot(ActiveWeaponSlot.Primary);
                else if (secondaryWeapon != null) SwitchToSlot(ActiveWeaponSlot.Secondary);
            }
        }
        #endregion

        #region Animator Integration
        private void UpdateAnimatorState()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator == null || !_animator.gameObject.activeInHierarchy) return;

            bool equipped = (ActiveWeapon != null && !isHolstered);
            int type = equipped ? (int)ActiveWeapon.Data.weaponType : 0;

            _animator.SetBool(HashIsWeaponEquipped, equipped);
            _animator.SetInteger(HashWeaponType, type);
            _animator.SetBool(HashIsAiming, IsAiming);
        }
        #endregion
    }
}
