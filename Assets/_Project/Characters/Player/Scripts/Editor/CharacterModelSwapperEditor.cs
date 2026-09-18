using UnityEngine;
using UnityEditor;

namespace TheChimeraProtocol.Player
{
    [CustomEditor(typeof(CharacterModelSwapper))]
    public class CharacterModelSwapperEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            CharacterModelSwapper swapper = (CharacterModelSwapper)target;

            // Header Banner
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Character Model Swapper", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Easily swap the active player character model or auto-configure any humanoid model under VisualModel_Root.\n" +
                "Automatically configures Animator (AC_Player_Locomotion) and binds references to PlayerController.",
                MessageType.Info);

            EditorGUILayout.Space(8);

            // Default serialized properties
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(12);

            // Status Check Box
            bool isReady = swapper.IsModelConfigured(out string statusMsg);
            if (isReady)
            {
                EditorGUILayout.HelpBox("✔ " + statusMsg, MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("⚠ " + statusMsg, MessageType.Warning);
            }

            EditorGUILayout.Space(10);

            // Action Buttons
            GUI.backgroundColor = new Color(0.35f, 0.75f, 0.35f);
            if (GUILayout.Button("Swap Character Model (From Prefab/Source)", GUILayout.Height(36)))
            {
                if (swapper.NewModelPrefab == null)
                {
                    EditorUtility.DisplayDialog("Model Missing", "Please assign a humanoid model Prefab or GameObject in the 'New Model Prefab' slot first.", "OK");
                }
                else
                {
                    if (EditorUtility.DisplayDialog("Confirm Model Swap", 
                        $"Are you sure you want to swap the player model with '{swapper.NewModelPrefab.name}'?", "Yes, Swap Model", "Cancel"))
                    {
                        swapper.SwapModel(swapper.NewModelPrefab);
                    }
                }
            }

            GUI.backgroundColor = new Color(0.35f, 0.65f, 0.95f);
            EditorGUILayout.Space(4);
            if (GUILayout.Button("Auto-Configure Active Model Under VisualModel_Root", GUILayout.Height(32)))
            {
                swapper.AutoConfigureCurrentModel();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(8);
        }
    }
}
