using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using TheChimeraProtocol.Player;
using TheChimeraProtocol.Weapons;

namespace TheChimeraProtocol.Weapons.Editor
{
    public static class WeaponSystemSetupEditor
    {
        [MenuItem("Tools/The Chimera Protocol/Setup Weapon System")]
        public static void SetupWeaponSystem()
        {
            Debug.Log("[WeaponSystemSetup] Starting Weapon System Setup...");

            // 1. Create or load WeaponData asset
            string dataFolder = "Assets/_Project/Weapons/Data";
            if (!AssetDatabase.IsValidFolder(dataFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Weapons", "Data");
            }

            string dataPath = $"{dataFolder}/WeaponData_AR_W.asset";
            WeaponData weaponData = AssetDatabase.LoadAssetAtPath<WeaponData>(dataPath);
            if (weaponData == null)
            {
                weaponData = ScriptableObject.CreateInstance<WeaponData>();
                weaponData.weaponId = "ar_rifle_01";
                weaponData.weaponName = "AR-W Assault Rifle";
                weaponData.slotType = WeaponSlotType.Primary;
                weaponData.weaponType = WeaponType.Rifle;
                weaponData.fireMode = FireMode.FullAuto;
                weaponData.roundsPerMinute = 600f;
                weaponData.damage = 25f;
                weaponData.maxRange = 100f;
                weaponData.pelletsPerShot = 1;
                weaponData.baseSpreadAngle = 2.2f;
                weaponData.movingSpreadMultiplier = 1.6f;
                weaponData.sprintingSpreadMultiplier = 2.2f;
                weaponData.crouchSpreadMultiplier = 0.65f;
                weaponData.magazineCapacity = 30;
                weaponData.defaultStartingReserve = 120;
                weaponData.maxReserveCapacity = 240;
                weaponData.reloadDuration = 2.2f;
                weaponData.cameraKickVertical = 0.75f;
                weaponData.cameraKickHorizontal = 0.3f;

                // Hand grip offsets to fit human hand
                weaponData.gripPositionOffset = new Vector3(-0.02f, 0.05f, 0.12f);
                weaponData.gripRotationOffset = new Vector3(0f, 0f, 0f);

                AssetDatabase.CreateAsset(weaponData, dataPath);
                Debug.Log($"[WeaponSystemSetup] Created WeaponData asset at {dataPath}");
            }

            // 2. Setup gun in scene and update Prefab
            GameObject sceneGun = GameObject.Find("Gun_AR_W") ?? GameObject.Find("gun AR w");
            string prefabFolder = "Assets/_Project/Weapons/Prefabs";
            if (!AssetDatabase.IsValidFolder(prefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Weapons", "Prefabs");
            }

            string prefabPath = $"{prefabFolder}/Gun_AR_W.prefab";
            GameObject gunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (sceneGun != null)
            {
                // Remove legacy pink MuzzleFlashFX if present
                var oldFlash = sceneGun.transform.Find("MuzzleFlashFX") ?? (sceneGun.transform.Find("MuzzlePoint") != null ? sceneGun.transform.Find("MuzzlePoint/MuzzleFlashFX") : null);
                if (oldFlash != null) Object.DestroyImmediate(oldFlash.gameObject);

                var box = sceneGun.GetComponent<BoxCollider>();
                if (box == null)
                {
                    box = sceneGun.AddComponent<BoxCollider>();
                    box.size = new Vector3(0.15f, 0.35f, 0.85f);
                    box.center = new Vector3(0f, 0.05f, 0.1f);
                    box.isTrigger = true;
                }

                var inst = sceneGun.GetComponent<WeaponInstance>();
                if (inst == null) inst = sceneGun.AddComponent<WeaponInstance>();
                inst.Initialize(weaponData, 30, 120);

                var pickup = sceneGun.GetComponent<WeaponPickup>();
                if (pickup == null) pickup = sceneGun.AddComponent<WeaponPickup>();
                pickup.Setup(weaponData, 30, 120);

                gunPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(sceneGun, prefabPath, InteractionMode.AutomatedAction);
                Debug.Log($"[WeaponSystemSetup] Saved clean Gun_AR_W prefab at {prefabPath}");

                weaponData.weaponPrefab = gunPrefab;
                weaponData.pickupPrefab = gunPrefab;
                EditorUtility.SetDirty(weaponData);
            }

            // 3. Configure Player_Character
            GameObject player = GameObject.Find("Player_Character");
            if (player != null)
            {
                var weaponCtrl = player.GetComponent<PlayerWeaponController>();
                if (weaponCtrl == null) weaponCtrl = player.AddComponent<PlayerWeaponController>();

                var so = new SerializedObject(weaponCtrl);
                so.FindProperty("startingWeaponData").objectReferenceValue = null;
                so.ApplyModifiedProperties();

                var hud = player.GetComponent<PlayerWeaponHUD>();
                if (hud == null) hud = player.AddComponent<PlayerWeaponHUD>();

                weaponCtrl.AutoResolveSockets();
                EditorUtility.SetDirty(player);
                Debug.Log("[WeaponSystemSetup] Configured PlayerWeaponController & PlayerWeaponHUD on Player_Character");
            }

            // 4. Configure AC_Player_Locomotion AnimatorController
            string animControllerPath = "Assets/_Project/Characters/Player/Animations/AC_Player_Locomotion.controller";
            AnimatorController animCtrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(animControllerPath);
            string maskPath = "Assets/_Project/Characters/Player/Animations/Mask_UpperBody.mask";
            AvatarMask upperBodyMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);

            // Find Animation Clips
            string holdClipPath = "Assets/Kevin Iglesias/Human Animations/Animations/Masked Poses/HumanM@WeaponHold_AssaultRifle01.fbx";
            string shootClipPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/AssaultRifle/HumanM@AssaultRifle_Aim01_Shoot01.fbx";
            string reloadClipPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/AssaultRifle/HumanM@AssaultRifle_Reload01.fbx";

            AnimationClip holdClip = LoadAnimationClip(holdClipPath);
            AnimationClip shootClip = LoadAnimationClip(shootClipPath);
            AnimationClip reloadClip = LoadAnimationClip(reloadClipPath);

            string aimClipPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/AssaultRifle/HumanM@AssaultRifle_Aim01.fbx";
            AnimationClip aimClip = LoadAnimationClip(aimClipPath);

            if (animCtrl != null)
            {
                // Ensure parameters exist
                EnsureParameter(animCtrl, "IsWeaponEquipped", AnimatorControllerParameterType.Bool);
                EnsureParameter(animCtrl, "WeaponType", AnimatorControllerParameterType.Int);
                EnsureParameter(animCtrl, "ShootTrigger", AnimatorControllerParameterType.Trigger);
                EnsureParameter(animCtrl, "ReloadTrigger", AnimatorControllerParameterType.Trigger);
                EnsureParameter(animCtrl, "IsAiming", AnimatorControllerParameterType.Bool);

                // Find or create UpperBody layer
                int upperBodyLayerIdx = -1;
                for (int i = 0; i < animCtrl.layers.Length; i++)
                {
                    if (animCtrl.layers[i].name == "UpperBody")
                    {
                        upperBodyLayerIdx = i;
                        break;
                    }
                }

                AnimatorControllerLayer upperBodyLayer;
                if (upperBodyLayerIdx == -1)
                {
                    upperBodyLayer = new AnimatorControllerLayer
                    {
                        name = "UpperBody",
                        defaultWeight = 1.0f,
                        blendingMode = AnimatorLayerBlendingMode.Override,
                        avatarMask = upperBodyMask,
                        stateMachine = new AnimatorStateMachine { name = "UpperBody" }
                    };
                    AssetDatabase.AddObjectToAsset(upperBodyLayer.stateMachine, animCtrl);
                    animCtrl.AddLayer(upperBodyLayer);
                    upperBodyLayer = animCtrl.layers[animCtrl.layers.Length - 1];
                }
                else
                {
                    var layers = animCtrl.layers;
                    layers[upperBodyLayerIdx].defaultWeight = 1.0f;
                    layers[upperBodyLayerIdx].blendingMode = AnimatorLayerBlendingMode.Override;
                    layers[upperBodyLayerIdx].avatarMask = upperBodyMask;
                    animCtrl.layers = layers;
                    upperBodyLayer = animCtrl.layers[upperBodyLayerIdx];
                }

                // Configure States in UpperBody
                var sm = upperBodyLayer.stateMachine;
                AnimatorState emptyState = null;
                AnimatorState holdState = null;
                AnimatorState shootState = null;
                AnimatorState reloadState = null;
                AnimatorState aimState = null;
                AnimatorState aimShootState = null;

                foreach (var childState in sm.states)
                {
                    if (childState.state.name == "Empty") emptyState = childState.state;
                    else if (childState.state.name == "Rifle_Hold") holdState = childState.state;
                    else if (childState.state.name == "Rifle_Shoot") shootState = childState.state;
                    else if (childState.state.name == "Rifle_Reload") reloadState = childState.state;
                    else if (childState.state.name == "Rifle_Aim") aimState = childState.state;
                    else if (childState.state.name == "Rifle_Aim_Shoot") aimShootState = childState.state;
                }

                if (emptyState == null)
                {
                    emptyState = sm.AddState("Empty", new Vector3(250, 0, 0));
                    sm.defaultState = emptyState;
                }
                if (holdState == null)
                {
                    holdState = sm.AddState("Rifle_Hold", new Vector3(250, 100, 0));
                }
                if (shootState == null)
                {
                    shootState = sm.AddState("Rifle_Shoot", new Vector3(100, 200, 0));
                }
                if (reloadState == null)
                {
                    reloadState = sm.AddState("Rifle_Reload", new Vector3(400, 200, 0));
                }
                if (aimState == null)
                {
                    aimState = sm.AddState("Rifle_Aim", new Vector3(250, 220, 0));
                }
                if (aimShootState == null)
                {
                    aimShootState = sm.AddState("Rifle_Aim_Shoot", new Vector3(250, 320, 0));
                }

                if (holdClip != null) holdState.motion = holdClip;
                if (shootClip != null) shootState.motion = shootClip;
                if (reloadClip != null) reloadState.motion = reloadClip;
                if (aimClip != null) aimState.motion = aimClip;
                if (shootClip != null) aimShootState.motion = shootClip;

                // Clear existing transitions to rebuild cleanly
                emptyState.transitions = new AnimatorStateTransition[0];
                holdState.transitions = new AnimatorStateTransition[0];
                shootState.transitions = new AnimatorStateTransition[0];
                reloadState.transitions = new AnimatorStateTransition[0];
                aimState.transitions = new AnimatorStateTransition[0];
                aimShootState.transitions = new AnimatorStateTransition[0];

                // Empty -> Rifle_Hold
                var transEmptyToHold = emptyState.AddTransition(holdState);
                transEmptyToHold.hasExitTime = false;
                transEmptyToHold.duration = 0.15f;
                transEmptyToHold.AddCondition(AnimatorConditionMode.If, 0, "IsWeaponEquipped");

                // Rifle_Hold -> Empty
                var transHoldToEmpty = holdState.AddTransition(emptyState);
                transHoldToEmpty.hasExitTime = false;
                transHoldToEmpty.duration = 0.2f;
                transHoldToEmpty.AddCondition(AnimatorConditionMode.IfNot, 0, "IsWeaponEquipped");

                // Rifle_Hold -> Rifle_Shoot (Hip-fire)
                var transHoldToShoot = holdState.AddTransition(shootState);
                transHoldToShoot.hasExitTime = false;
                transHoldToShoot.duration = 0.04f;
                transHoldToShoot.AddCondition(AnimatorConditionMode.If, 0, "ShootTrigger");

                // Rifle_Shoot -> Rifle_Hold
                var transShootToHold = shootState.AddTransition(holdState);
                transShootToHold.hasExitTime = true;
                transShootToHold.exitTime = 0.65f;
                transShootToHold.duration = 0.08f;

                // Rifle_Hold -> Rifle_Reload
                var transHoldToReload = holdState.AddTransition(reloadState);
                transHoldToReload.hasExitTime = false;
                transHoldToReload.duration = 0.1f;
                transHoldToReload.AddCondition(AnimatorConditionMode.If, 0, "ReloadTrigger");

                // Rifle_Reload -> Rifle_Hold
                var transReloadToHold = reloadState.AddTransition(holdState);
                transReloadToHold.hasExitTime = true;
                transReloadToHold.exitTime = 0.88f;
                transReloadToHold.duration = 0.15f;

                // Rifle_Hold <-> Rifle_Aim
                var transHoldToAim = holdState.AddTransition(aimState);
                transHoldToAim.hasExitTime = false;
                transHoldToAim.duration = 0.15f;
                transHoldToAim.AddCondition(AnimatorConditionMode.If, 0, "IsAiming");

                var transAimToHold = aimState.AddTransition(holdState);
                transAimToHold.hasExitTime = false;
                transAimToHold.duration = 0.15f;
                transAimToHold.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAiming");

                // Rifle_Aim -> Rifle_Aim_Shoot (ADS / OTS Firing)
                var transAimToShoot = aimState.AddTransition(aimShootState);
                transAimToShoot.hasExitTime = false;
                transAimToShoot.duration = 0.04f;
                transAimToShoot.AddCondition(AnimatorConditionMode.If, 0, "ShootTrigger");

                var transAimShootToAim = aimShootState.AddTransition(aimState);
                transAimShootToAim.hasExitTime = true;
                transAimShootToAim.exitTime = 0.65f;
                transAimShootToAim.duration = 0.08f;

                // Rifle_Aim -> Rifle_Reload
                var transAimToReload = aimState.AddTransition(reloadState);
                transAimToReload.hasExitTime = false;
                transAimToReload.duration = 0.1f;
                transAimToReload.AddCondition(AnimatorConditionMode.If, 0, "ReloadTrigger");

                // Rifle_Aim -> Empty (if unequipped while aiming)
                var transAimToEmpty = aimState.AddTransition(emptyState);
                transAimToEmpty.hasExitTime = false;
                transAimToEmpty.duration = 0.2f;
                transAimToEmpty.AddCondition(AnimatorConditionMode.IfNot, 0, "IsWeaponEquipped");

                EditorUtility.SetDirty(animCtrl);
                Debug.Log("[WeaponSystemSetup] UpperBody Animator Layer configured successfully!");
            }

            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[WeaponSystemSetup] Weapon System Setup COMPLETE!");
        }

        [MenuItem("Tools/The Chimera Protocol/Validate Weapon System")]
        public static void ValidateWeaponSystem()
        {
            Debug.Log("=== WEAPON SYSTEM DIAGNOSTIC REPORT ===");

            // 1. WeaponData
            var weaponData = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Project/Weapons/Data/WeaponData_AR_W.asset");
            Debug.Log($"[Validation] WeaponData AR-W: {(weaponData != null ? "FOUND (" + weaponData.weaponName + ", RPM: " + weaponData.roundsPerMinute + ", Mag: " + weaponData.magazineCapacity + ")" : "MISSING!")}");

            // 2. Gun Prefab
            var gunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Weapons/Prefabs/Gun_AR_W.prefab");
            Debug.Log($"[Validation] Gun Prefab: {(gunPrefab != null ? "FOUND (" + gunPrefab.name + ")" : "MISSING!")}");

            // 3. Player Character & Components
            var player = GameObject.Find("Player_Character");
            if (player != null)
            {
                var weaponCtrl = player.GetComponent<PlayerWeaponController>();
                var inputHandler = player.GetComponent<PlayerInputHandler>();
                var playerCtrl = player.GetComponent<PlayerController>();
                var camCtrl = player.GetComponent<ThirdPersonCameraController>();
                var swapper = player.GetComponent<CharacterModelSwapper>();

                Debug.Log($"[Validation] PlayerWeaponController: {(weaponCtrl != null ? "OK" : "MISSING")}");
                Debug.Log($"[Validation] PlayerInputHandler: {(inputHandler != null ? "OK" : "MISSING")}");
                Debug.Log($"[Validation] PlayerController: {(playerCtrl != null ? "OK" : "MISSING")}");
                Debug.Log($"[Validation] ThirdPersonCameraController: {(camCtrl != null ? "OK" : "MISSING")}");
                Debug.Log($"[Validation] CharacterModelSwapper: {(swapper != null ? "OK" : "MISSING")}");

                var anim = player.GetComponentInChildren<Animator>();
                if (anim != null && anim.isHuman)
                {
                    var rHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
                    var rSocket = rHand != null ? rHand.Find("RightHand_WeaponSocket") : null;
                    Debug.Log($"[Validation] Right Hand Weapon Socket: {(rSocket != null ? "FOUND under " + rHand.name : "MISSING")}");

                    var spine = anim.GetBoneTransform(HumanBodyBones.Chest) ?? anim.GetBoneTransform(HumanBodyBones.Spine);
                    var holster = spine != null ? spine.Find("Spine_WeaponHolster") : null;
                    Debug.Log($"[Validation] Spine Weapon Holster: {(holster != null ? "FOUND under " + spine.name : "MISSING")}");
                }
            }
            else
            {
                Debug.LogError("[Validation] Player_Character not found in active scene!");
            }

            // 4. Animator Controller
            var animCtrl = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_Project/Characters/Player/Animations/AC_Player_Locomotion.controller");
            if (animCtrl != null)
            {
                bool hasUpperBody = false;
                foreach (var l in animCtrl.layers)
                {
                    if (l.name == "UpperBody")
                    {
                        hasUpperBody = true;
                        Debug.Log($"[Validation] UpperBody Layer: FOUND (Weight: {l.defaultWeight}, Blending: {l.blendingMode}, Mask: {(l.avatarMask != null ? l.avatarMask.name : "None")})");
                        foreach (var s in l.stateMachine.states)
                        {
                            Debug.Log($"   -> State: {s.state.name} (Motion: {(s.state.motion != null ? s.state.motion.name : "None")})");
                        }
                    }
                }
                if (!hasUpperBody) Debug.LogWarning("[Validation] UpperBody Layer NOT found in AC_Player_Locomotion!");
            }

            Debug.Log("=== END DIAGNOSTIC REPORT ===");
        }

        private static AnimationClip LoadAnimationClip(string path)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null) return clip;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip c && !c.name.StartsWith("__preview__"))
                {
                    return c;
                }
            }
            return null;
        }

        private static void EnsureParameter(AnimatorController controller, string paramName, AnimatorControllerParameterType type)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name == paramName) return;
            }
            controller.AddParameter(paramName, type);
        }
    }
}
