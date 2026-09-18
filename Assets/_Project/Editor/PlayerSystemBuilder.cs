using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using TheChimeraProtocol.Player;

namespace TheChimeraProtocol.Editor
{
    public static class PlayerSystemBuilder
    {
        private const string AnimPath = "Assets/_Project/Characters/Player/Animations";
        private const string PrefabPath = "Assets/_Project/Characters/Player/Prefabs/Player_Character.prefab";
        private const string InputActionsPath = "Assets/_Project/Settings/PlayerInputActions.inputactions";
        private const string ModelFbxPath = "Assets/_Project/Models/Player.fbx";
        private const string UpperBodyMaskPath = "Assets/_Project/Characters/Player/Animations/Mask_UpperBody.mask";

        [MenuItem("The Chimera Protocol/Build Player & Camera System", false, 10)]
        public static void BuildCompleteSystem()
        {
            EnsurePlayerLayer();
            EnsureDirectories();
            var mask = CreateUpperBodyMask();
            var controller = CreateAnimatorAndClips(mask);
            var prefab = CreatePlayerPrefab(controller);
            SetupSceneWithPlayerAndCamera(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[PlayerSystemBuilder]</color> Successfully built ALS Locomotion with Free Orbit, Turn_L/Turn_R, and Balanced Look-At!");
        }

        private static void EnsurePlayerLayer()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            bool exists = false;
            for (int i = 6; i < layers.arraySize; i++)
            {
                var element = layers.GetArrayElementAtIndex(i);
                if (element.stringValue == "Player")
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                for (int i = 6; i < layers.arraySize; i++)
                {
                    var element = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(element.stringValue))
                    {
                        element.stringValue = "Player";
                        tagManager.ApplyModifiedProperties();
                        Debug.Log($"[PlayerSystemBuilder] Registered 'Player' layer at index {i}.");
                        break;
                    }
                }
            }
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Characters/Player"))
                AssetDatabase.CreateFolder("Assets/_Project/Characters", "Player");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Characters/Player/Animations"))
                AssetDatabase.CreateFolder("Assets/_Project/Characters/Player", "Animations");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Characters/Player/Prefabs"))
                AssetDatabase.CreateFolder("Assets/_Project/Characters/Player", "Prefabs");
        }

        private static AvatarMask CreateUpperBodyMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                mask.name = "Mask_UpperBody";

                // Disable legs and root
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);

                // Enable torso, head, arms, hand IKs
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);

                AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
            }
            return mask;
        }

        private static AnimatorController CreateAnimatorAndClips(AvatarMask upperBodyMask)
        {
            string ctrlPath = $"{AnimPath}/AC_Player_Locomotion.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            }

            // Ensure Parameters
            EnsureParam(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParam(controller, "MoveX", AnimatorControllerParameterType.Float);
            EnsureParam(controller, "MoveZ", AnimatorControllerParameterType.Float);
            EnsureParam(controller, "IsAiming", AnimatorControllerParameterType.Bool);
            EnsureParam(controller, "IsGrounded", AnimatorControllerParameterType.Bool);
            EnsureParam(controller, "IsCrouching", AnimatorControllerParameterType.Bool);
            EnsureParam(controller, "IsTurningInPlace", AnimatorControllerParameterType.Bool);
            EnsureParam(controller, "Turn", AnimatorControllerParameterType.Float);
            EnsureParam(controller, "TurnAngle", AnimatorControllerParameterType.Float);
            EnsureParam(controller, "Shoot", AnimatorControllerParameterType.Trigger);
            EnsureParam(controller, "Reload", AnimatorControllerParameterType.Trigger);

            // Placeholder Clips
            var idleClip = GetOrCreateClip("A_Placeholder_Idle");
            var walkClip = GetOrCreateClip("A_Placeholder_Walk");
            var jogClip = GetOrCreateClip("A_Placeholder_Jog");
            var sprintClip = GetOrCreateClip("A_Placeholder_Sprint");
            var crouchClip = GetOrCreateClip("A_Placeholder_Crouch");

            // Dedicated Left and Right Turn-In-Place Clips
            var turnLeftClip = GetOrCreateClip("A_Placeholder_Turn_L");
            var turnRightClip = GetOrCreateClip("A_Placeholder_Turn_R");

            var aimIdleClip = GetOrCreateClip("A_Placeholder_Aim_Idle");
            var aimFwdClip = GetOrCreateClip("A_Placeholder_Aim_Forward");
            var aimBwdClip = GetOrCreateClip("A_Placeholder_Aim_Backward");
            var aimLeftClip = GetOrCreateClip("A_Placeholder_Aim_Left");
            var aimRightClip = GetOrCreateClip("A_Placeholder_Aim_Right");
            var shootClip = GetOrCreateClip("A_Placeholder_Shoot");
            var reloadClip = GetOrCreateClip("A_Placeholder_Reload");

            // ==========================================
            // LAYER 0: BASE LAYER (Locomotion & Legs)
            // ==========================================
            var baseStateMachine = controller.layers[0].stateMachine;
            foreach (var s in baseStateMachine.states)
            {
                baseStateMachine.RemoveState(s.state);
            }

            // 1. Normal Locomotion 1D Blend Tree
            var normalState = baseStateMachine.AddState("Locomotion_Normal");
            var normalTree = new BlendTree
            {
                name = "Normal_Locomotion_Tree",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(normalTree, controller);
            normalTree.AddChild(idleClip, 0.0f);
            normalTree.AddChild(walkClip, 1.8f);
            normalTree.AddChild(jogClip, 3.8f);
            normalTree.AddChild(sprintClip, 5.8f);
            normalState.motion = normalTree;

            // 2. Crouch State
            var crouchState = baseStateMachine.AddState("Locomotion_Crouch");
            crouchState.motion = crouchClip;

            // 3. ALS Turn-In-Place: Dedicated Left & Right States
            var turnLeftState = baseStateMachine.AddState("Turn_In_Place_L");
            turnLeftState.motion = turnLeftClip;

            var turnRightState = baseStateMachine.AddState("Turn_In_Place_R");
            turnRightState.motion = turnRightClip;

            // 4. Aim Strafe 2D Blend Tree
            var aimState = baseStateMachine.AddState("Locomotion_Aiming");
            var aimTree = new BlendTree
            {
                name = "Aim_Strafe_Tree",
                blendType = BlendTreeType.SimpleDirectional2D,
                blendParameter = "MoveX",
                blendParameterY = "MoveZ",
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(aimTree, controller);
            aimTree.AddChild(aimIdleClip, new Vector2(0f, 0f));
            aimTree.AddChild(aimFwdClip, new Vector2(0f, 1f));
            aimTree.AddChild(aimBwdClip, new Vector2(0f, -1f));
            aimTree.AddChild(aimLeftClip, new Vector2(-1f, 0f));
            aimTree.AddChild(aimRightClip, new Vector2(1f, 0f));
            aimState.motion = aimTree;

            // Base Layer Transitions
            // Normal <-> Aim
            var toAim = normalState.AddTransition(aimState);
            toAim.hasExitTime = false;
            toAim.duration = 0.2f;
            toAim.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming");

            var fromAim = aimState.AddTransition(normalState);
            fromAim.hasExitTime = false;
            fromAim.duration = 0.2f;
            fromAim.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming");

            // Normal <-> Crouch
            var toCrouch = normalState.AddTransition(crouchState);
            toCrouch.hasExitTime = false;
            toCrouch.duration = 0.2f;
            toCrouch.AddCondition(AnimatorConditionMode.If, 0f, "IsCrouching");

            var fromCrouch = crouchState.AddTransition(normalState);
            fromCrouch.hasExitTime = false;
            fromCrouch.duration = 0.2f;
            fromCrouch.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrouching");

            // Turn In Place Left Transition (TurnAngle < -40)
            var toTurnLeft = normalState.AddTransition(turnLeftState);
            toTurnLeft.hasExitTime = false;
            toTurnLeft.duration = 0.1f;
            toTurnLeft.AddCondition(AnimatorConditionMode.If, 0f, "IsTurningInPlace");
            toTurnLeft.AddCondition(AnimatorConditionMode.Less, -40f, "TurnAngle");

            var fromTurnLeft = turnLeftState.AddTransition(normalState);
            fromTurnLeft.hasExitTime = false;
            fromTurnLeft.duration = 0.15f;
            fromTurnLeft.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsTurningInPlace");

            // Turn In Place Right Transition (TurnAngle > 40)
            var toTurnRight = normalState.AddTransition(turnRightState);
            toTurnRight.hasExitTime = false;
            toTurnRight.duration = 0.1f;
            toTurnRight.AddCondition(AnimatorConditionMode.If, 0f, "IsTurningInPlace");
            toTurnRight.AddCondition(AnimatorConditionMode.Greater, 40f, "TurnAngle");

            var fromTurnRight = turnRightState.AddTransition(normalState);
            fromTurnRight.hasExitTime = false;
            fromTurnRight.duration = 0.15f;
            fromTurnRight.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsTurningInPlace");

            baseStateMachine.defaultState = normalState;

            // ==========================================
            // LAYER 1: UPPERBODY LAYER (Masked)
            // ==========================================
            AnimatorControllerLayer upperLayer = null;
            if (controller.layers.Length > 1)
            {
                upperLayer = controller.layers[1];
            }
            else
            {
                controller.AddLayer("UpperBody");
                var layers = controller.layers;
                upperLayer = layers[1];
            }

            upperLayer.avatarMask = upperBodyMask;
            upperLayer.defaultWeight = 1.0f;
            upperLayer.blendingMode = AnimatorLayerBlendingMode.Override;

            var upperSm = upperLayer.stateMachine;
            foreach (var s in upperSm.states)
            {
                upperSm.RemoveState(s.state);
            }

            var passThroughState = upperSm.AddState("UpperBody_Passthrough");
            passThroughState.motion = null;

            var upperAimState = upperSm.AddState("UpperBody_Aim");
            upperAimState.motion = aimIdleClip;

            var upperShootState = upperSm.AddState("UpperBody_Shoot");
            upperShootState.motion = shootClip;

            var upperReloadState = upperSm.AddState("UpperBody_Reload");
            upperReloadState.motion = reloadClip;

            // Upper Layer Transitions
            var toUpperAim = passThroughState.AddTransition(upperAimState);
            toUpperAim.hasExitTime = false;
            toUpperAim.duration = 0.15f;
            toUpperAim.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming");

            var fromUpperAim = upperAimState.AddTransition(passThroughState);
            fromUpperAim.hasExitTime = false;
            fromUpperAim.duration = 0.2f;
            fromUpperAim.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming");

            // Shoot Trigger
            var shootFromAim = upperAimState.AddTransition(upperShootState);
            shootFromAim.hasExitTime = false;
            shootFromAim.duration = 0.05f;
            shootFromAim.AddCondition(AnimatorConditionMode.If, 0f, "Shoot");

            var shootReturnAim = upperShootState.AddTransition(upperAimState);
            shootReturnAim.hasExitTime = true;
            shootReturnAim.exitTime = 0.75f;
            shootReturnAim.duration = 0.15f;

            // Reload Trigger
            var reloadFromAim = upperAimState.AddTransition(upperReloadState);
            reloadFromAim.hasExitTime = false;
            reloadFromAim.duration = 0.1f;
            reloadFromAim.AddCondition(AnimatorConditionMode.If, 0f, "Reload");

            var reloadReturnAim = upperReloadState.AddTransition(upperAimState);
            reloadReturnAim.hasExitTime = true;
            reloadReturnAim.exitTime = 0.9f;
            reloadReturnAim.duration = 0.15f;

            upperSm.defaultState = passThroughState;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void EnsureParam(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name == name) return;
            }
            controller.AddParameter(name, type);
        }

        private static AnimationClip GetOrCreateClip(string clipName)
        {
            string path = $"{AnimPath}/{clipName}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = clipName };
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AssetDatabase.CreateAsset(clip, path);
            }
            return clip;
        }

        private static GameObject CreatePlayerPrefab(AnimatorController animCtrl)
        {
            var root = new GameObject("Player_Character");
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer != -1) root.layer = playerLayer;

            // 1. CharacterController
            var cc = root.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0.95f, 0f);
            cc.radius = 0.35f;
            cc.height = 1.85f;
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 45f;
            cc.minMoveDistance = 0.001f;

            // 2. Input Handler
            var inputHandler = root.AddComponent<PlayerInputHandler>();
            var actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actionsAsset != null)
            {
                inputHandler.InputAsset = actionsAsset;
            }

            // 3. Player Controller
            var controller = root.AddComponent<PlayerController>();
            controller.walkSpeed = 1.8f;
            controller.jogSpeed = 3.8f;
            controller.sprintSpeed = 5.8f;
            controller.crouchSpeed = 1.2f;
            controller.acceleration = 10f;
            controller.deceleration = 14f;
            controller.turnSmoothTime = 0.08f;
            controller.gravity = 20f;
            controller.groundStickForce = 4.0f;

            // 4. Camera Follow Target
            var camTarget = new GameObject("CameraFollowTarget");
            camTarget.transform.SetParent(root.transform);
            camTarget.transform.localPosition = new Vector3(0f, 1.45f, 0f);

            // 5. Visual Model Container (Swappable!)
            var visualRoot = new GameObject("VisualModel_Root");
            visualRoot.transform.SetParent(root.transform);
            visualRoot.transform.localPosition = Vector3.zero;

            // Instantiate Model FBX as child
            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFbxPath);
            if (modelPrefab != null)
            {
                var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, visualRoot.transform);
                modelInstance.name = "Player_Model";
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;

                var animator = modelInstance.GetComponent<Animator>();
                if (animator == null) animator = modelInstance.AddComponent<Animator>();
                animator.runtimeAnimatorController = animCtrl;

                controller.SetAnimator(animator);
            }

            // 6. Locomotion Effects
            var effects = root.AddComponent<PlayerLocomotionEffects>();
            effects.SetCameraFollowTarget(camTarget.transform);
            effects.SetVisualModelRoot(visualRoot.transform);
            effects.maxLeanAngle = 6.0f;

            // 7. Third Person Camera Controller
            root.AddComponent<ThirdPersonCameraController>();

            // 8. Footstep Audio System
            var footstep = root.AddComponent<FootstepSystem>();
            footstep.walkStepDistance = 1.6f;
            footstep.jogStepDistance = 1.9f;
            footstep.sprintStepDistance = 2.4f;

            // Save Prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void SetupSceneWithPlayerAndCamera(GameObject playerPrefab)
        {
            var existingPlayer = GameObject.Find("Player_Character");
            if (existingPlayer != null)
            {
                Object.DestroyImmediate(existingPlayer);
            }

            var playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            playerInstance.name = "Player_Character";
            playerInstance.transform.position = new Vector3(0f, 0.05f, 3.5f);
            playerInstance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var camTarget = playerInstance.transform.Find("CameraFollowTarget");

            // Setup Main Camera with CinemachineBrain
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                var brain = mainCam.GetComponent<CinemachineBrain>();
                if (brain == null)
                {
                    brain = mainCam.gameObject.AddComponent<CinemachineBrain>();
                }
            }

            // Setup CM_ThirdPersonCamera (Cinemachine 3.x)
            var existingCmCam = GameObject.Find("CM_ThirdPersonCamera");
            CinemachineCamera cmCamera = null;
            if (existingCmCam != null)
            {
                cmCamera = existingCmCam.GetComponent<CinemachineCamera>();
            }
            else
            {
                var cmObj = new GameObject("CM_ThirdPersonCamera");
                cmCamera = cmObj.AddComponent<CinemachineCamera>();
            }

            if (cmCamera != null)
            {
                cmCamera.Target.TrackingTarget = camTarget;
                cmCamera.Lens.FieldOfView = 65f;
                cmCamera.Lens.NearClipPlane = 0.1f;

                var follow = cmCamera.GetComponent<CinemachineThirdPersonFollow>();
                if (follow == null)
                {
                    follow = cmCamera.gameObject.AddComponent<CinemachineThirdPersonFollow>();
                }

                follow.CameraDistance = 2.2f;
                follow.ShoulderOffset = new Vector3(0.45f, 0.1f, 0.0f);
                follow.VerticalArmLength = 0.0f;
                follow.CameraSide = 1.0f;
                follow.Damping = new Vector3(0.08f, 0.08f, 0.08f);

                follow.AvoidObstacles.Enabled = true;
                follow.AvoidObstacles.CameraRadius = 0.25f;
                follow.AvoidObstacles.DampingIntoCollision = 0.05f;
                follow.AvoidObstacles.DampingFromCollision = 0.4f;

                int playerLayer = LayerMask.NameToLayer("Player");
                if (playerLayer != -1)
                {
                    follow.AvoidObstacles.CollisionFilter = ~(1 << playerLayer | 1 << LayerMask.NameToLayer("Ignore Raycast"));
                }

                var camCtrl = playerInstance.GetComponent<ThirdPersonCameraController>();
                if (camCtrl != null)
                {
                    camCtrl.SetCinemachineCamera(cmCamera);
                }

                var pCtrl = playerInstance.GetComponent<PlayerController>();
                if (pCtrl != null && mainCam != null)
                {
                    pCtrl.SetCameraTransform(mainCam.transform);
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        }
    }
}
