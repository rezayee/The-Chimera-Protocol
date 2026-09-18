using UnityEngine;

namespace TheChimeraProtocol.Player
{
    /// <summary>
    /// [OBSOLETE] Deprecated in favor of TPSLookController and Final IK LookAtIK.
    /// Inactive by design to ensure ONLY ONE LOOK SOLVER is active on the character.
    /// </summary>
    [System.Obsolete("Deprecated in favor of TPSLookController and Final IK LookAtIK.", false)]
    public class PlayerLookAtIK : MonoBehaviour
    {
        [Header("Status")]
        [Tooltip("This component is deprecated and disabled in favor of TPSLookController and Final IK LookAtIK.")]
        public bool isDeprecated = true;

        private void OnAnimatorIK(int layerIndex)
        {
            // Fully deactivated: Final IK LookAtIK is the sole Look solver.
        }

        public void SetCameraTransform(Transform cam) { }
        public void SetPointOfInterest(Transform poi) { }
    }
}
