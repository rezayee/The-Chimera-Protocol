using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TheChimeraProtocol.Player
{
    /// <summary>
    /// Automates the workflow of swapping or configuring humanoid character models under VisualModel_Root.
    /// Configures Animator (assigns AC_Player_Locomotion, applyRootMotion = false, cullingMode = CullUpdateTransforms)
    /// and binds the animator reference to PlayerController.
    /// Pure humanoid character model swapping without animation rigging or aim dependencies.
    /// Supports Undo in Unity Editor and marks scenes/prefabs dirty for reliable saving.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class CharacterModelSwapper : MonoBehaviour
    {
        [Header("Root References")]
        [Tooltip("Transform container holding the visual mesh (child of Player_Character)")]
        [SerializeField] private Transform visualModelRoot;

        [Header("Locomotion Controller")]
        [Tooltip("The shared locomotion AnimatorController (defaults to AC_Player_Locomotion)")]
        [SerializeField] private RuntimeAnimatorController locomotionAnimatorController;

        [Header("Components on Player_Character")]
        [SerializeField] private PlayerController playerController;

        [Header("New Model Source")]
        [Tooltip("Prefab or GameObject of the new humanoid character model to swap in")]
        [SerializeField] private GameObject newModelPrefab;

        public Transform VisualModelRoot => visualModelRoot;
        public GameObject NewModelPrefab { get => newModelPrefab; set => newModelPrefab = value; }

        private void Reset()
        {
            AutoResolveReferences();
        }

        private void Awake()
        {
            AutoResolveReferences();
        }

        public void AutoResolveReferences()
        {
            if (visualModelRoot == null)
            {
                visualModelRoot = transform.Find("VisualModel_Root");
            }

            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }

#if UNITY_EDITOR
            if (locomotionAnimatorController == null)
            {
                string[] guids = AssetDatabase.FindAssets("AC_Player_Locomotion t:AnimatorController");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    locomotionAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
                }
            }
#endif
        }

        /// <summary>
        /// Swaps the existing character model under VisualModel_Root with a new humanoid prefab/model.
        /// </summary>
        public bool SwapModel(GameObject sourcePrefabOrObject)
        {
            if (sourcePrefabOrObject == null)
            {
                Debug.LogError("[CharacterModelSwapper] Cannot swap model: sourcePrefabOrObject is null!", this);
                return false;
            }

            AutoResolveReferences();

            if (visualModelRoot == null)
            {
                Debug.LogError("[CharacterModelSwapper] VisualModel_Root transform not found! Cannot place model.", this);
                return false;
            }

#if UNITY_EDITOR
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Swap Character Model");
            int undoGroup = Undo.GetCurrentGroup();
#endif

            // Identify existing model
            Transform existingModel = null;
            for (int i = 0; i < visualModelRoot.childCount; i++)
            {
                Transform child = visualModelRoot.GetChild(i);
                if (child.gameObject.activeSelf)
                {
                    existingModel = child;
                    break;
                }
            }

            // Instantiate or duplicate new model under visualModelRoot
            GameObject newInstance = null;
#if UNITY_EDITOR
            if (PrefabUtility.IsPartOfPrefabAsset(sourcePrefabOrObject))
            {
                newInstance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefabOrObject, visualModelRoot);
                Undo.RegisterCreatedObjectUndo(newInstance, "Instantiate Model Prefab");
            }
            else
#endif
            {
                newInstance = Instantiate(sourcePrefabOrObject, visualModelRoot);
#if UNITY_EDITOR
                Undo.RegisterCreatedObjectUndo(newInstance, "Instantiate New Model");
#endif
            }

            if (newInstance == null)
            {
                Debug.LogError("[CharacterModelSwapper] Failed to instantiate new model!", this);
                return false;
            }

            newInstance.name = sourcePrefabOrObject.name;
            newInstance.transform.localPosition = Vector3.zero;
            newInstance.transform.localRotation = Quaternion.identity;
            newInstance.transform.localScale = Vector3.one;

            // Deactivate old model (safe non-destructive replacement)
            if (existingModel != null && existingModel != newInstance.transform)
            {
#if UNITY_EDITOR
                Undo.RecordObject(existingModel.gameObject, "Deactivate Old Model");
#endif
                existingModel.gameObject.SetActive(false);
            }

            bool success = SetupModel(newInstance);

#if UNITY_EDITOR
            Undo.CollapseUndoOperations(undoGroup);
#endif

            return success;
        }

        /// <summary>
        /// Automatically inspects and configures the active model currently under VisualModel_Root.
        /// Useful when dragging and dropping a model directly in the Hierarchy.
        /// </summary>
        public bool AutoConfigureCurrentModel()
        {
            AutoResolveReferences();

            if (visualModelRoot == null)
            {
                Debug.LogError("[CharacterModelSwapper] VisualModel_Root transform not found!", this);
                return false;
            }

            Transform activeModel = null;
            for (int i = 0; i < visualModelRoot.childCount; i++)
            {
                Transform child = visualModelRoot.GetChild(i);
                if (child.gameObject.activeSelf)
                {
                    activeModel = child;
                    break;
                }
            }

            if (activeModel == null)
            {
                Debug.LogError("[CharacterModelSwapper] No active model found under VisualModel_Root!", this);
                return false;
            }

            return SetupModel(activeModel.gameObject);
        }

        /// <summary>
        /// Configures Animator and binds references for the specified model GameObject.
        /// </summary>
        public bool SetupModel(GameObject modelObject)
        {
            if (modelObject == null) return false;

            AutoResolveReferences();

#if UNITY_EDITOR
            Undo.RecordObject(modelObject, "Configure Character Model");
#endif

            // 1. Configure Animator
            Animator animator = modelObject.GetComponent<Animator>();
            if (animator == null)
            {
#if UNITY_EDITOR
                animator = Undo.AddComponent<Animator>(modelObject);
#else
                animator = modelObject.AddComponent<Animator>();
#endif
            }

            if (locomotionAnimatorController != null)
            {
                animator.runtimeAnimatorController = locomotionAnimatorController;
            }
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            if (animator.avatar != null && !animator.avatar.isHuman)
            {
                Debug.LogWarning("[CharacterModelSwapper] Model Avatar is not marked as Humanoid! Mecanim bone resolution might fail.", modelObject);
            }

            // 2. Rebind parent references on Player_Character
            if (playerController != null)
            {
#if UNITY_EDITOR
                Undo.RecordObject(playerController, "Rebind PlayerController Animator");
#endif
                playerController.SetAnimator(animator);
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(modelObject);
            if (playerController != null) EditorUtility.SetDirty(playerController);
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif

            Debug.Log($"[CharacterModelSwapper] Successfully configured character model: '{modelObject.name}'!", modelObject);
            return true;
        }

        /// <summary>
        /// Diagnostic validation of current model setup.
        /// </summary>
        public bool IsModelConfigured(out string statusMessage)
        {
            AutoResolveReferences();

            if (visualModelRoot == null)
            {
                statusMessage = "VisualModel_Root is missing!";
                return false;
            }

            Animator anim = visualModelRoot.GetComponentInChildren<Animator>();
            if (anim == null)
            {
                statusMessage = "No active Animator found under VisualModel_Root.";
                return false;
            }

            if (anim.runtimeAnimatorController == null)
            {
                statusMessage = "Animator has no RuntimeAnimatorController assigned.";
                return false;
            }

            statusMessage = $"Model '{anim.gameObject.name}' is configured with Animator '{anim.runtimeAnimatorController.name}' and ready!";
            return true;
        }
    }
}
