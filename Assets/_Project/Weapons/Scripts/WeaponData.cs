using UnityEngine;

namespace TheChimeraProtocol.Weapons
{
    public enum WeaponSlotType
    {
        Primary = 0,
        Secondary = 1,
        Sidearm = 2
    }

    public enum WeaponType
    {
        Unarmed = 0,
        Rifle = 1,
        Pistol = 2,
        Shotgun = 3
    }

    public enum FireMode
    {
        FullAuto = 0,
        SemiAuto = 1,
        Burst = 2
    }

    [CreateAssetMenu(fileName = "NewWeaponData", menuName = "The Chimera Protocol/Weapons/Weapon Data", order = 10)]
    public class WeaponData : ScriptableObject
    {
        [Header("Identity")]
        public string weaponId = "ar_rifle_01";
        public string weaponName = "Assault Rifle";
        public WeaponSlotType slotType = WeaponSlotType.Primary;
        public WeaponType weaponType = WeaponType.Rifle;

        [Header("Fire Mechanics")]
        public FireMode fireMode = FireMode.FullAuto;
        [Tooltip("Rounds per minute (e.g. 600 RPM = 10 shots/sec)")]
        public float roundsPerMinute = 600f;
        public float damage = 25f;
        public float maxRange = 100f;
        public int pelletsPerShot = 1;

        [Header("Hip-Fire Spread & Accuracy")]
        [Tooltip("Base angular spread cone in degrees when hip-firing while stationary")]
        public float baseSpreadAngle = 2.5f;
        [Tooltip("Spread multiplier applied when player is moving")]
        public float movingSpreadMultiplier = 1.6f;
        [Tooltip("Spread multiplier applied when player is sprinting")]
        public float sprintingSpreadMultiplier = 2.2f;
        [Tooltip("Spread multiplier applied when player is crouching")]
        public float crouchSpreadMultiplier = 0.7f;

        [Header("Ammo & Reload")]
        public int magazineCapacity = 30;
        public int defaultStartingReserve = 120;
        public int maxReserveCapacity = 240;
        [Tooltip("Time in seconds to perform a reload")]
        public float reloadDuration = 2.2f;

        [Header("Recoil & Camera Kick")]
        [Tooltip("Upward camera kick in degrees per shot")]
        public float cameraKickVertical = 0.8f;
        [Tooltip("Random horizontal camera kick in degrees per shot")]
        public float cameraKickHorizontal = 0.35f;

        [Header("Hand Grip Alignment")]
        [Tooltip("Local position offset when socketed into the character's right hand")]
        public Vector3 gripPositionOffset = Vector3.zero;
        [Tooltip("Local euler rotation offset when socketed into the character's right hand")]
        public Vector3 gripRotationOffset = Vector3.zero;

        [Header("Visual Prefabs & Effects")]
        [Tooltip("Prefab instantiated or enabled when equipped")]
        public GameObject weaponPrefab;
        [Tooltip("Prefab dropped into the world when discarded")]
        public GameObject pickupPrefab;
        [Tooltip("Optional muzzle flash particle prefab")]
        public GameObject muzzleFlashPrefab;
        [Tooltip("Optional bullet tracer line or particle prefab")]
        public GameObject tracerPrefab;
        [Tooltip("Material for URP bullet hole decal projector")]
        public Material impactDecalMaterial;

        [Header("Audio")]
        public AudioClip[] fireAudioClips;
        public AudioClip dryFireAudioClip;
        public AudioClip reloadAudioClip;

        public float FireInterval => 60f / Mathf.Max(roundsPerMinute, 1f);
    }
}
