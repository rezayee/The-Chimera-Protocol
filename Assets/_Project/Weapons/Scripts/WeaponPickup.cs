using UnityEngine;

namespace TheChimeraProtocol.Weapons
{
    /// <summary>
    /// Represents a weapon placed or dropped in the world that the player can pick up.
    /// Preserves ammunition counts and weapon data across drop and pickup events.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WeaponPickup : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private WeaponData weaponData;
        [SerializeField] private int ammoInMag = 30;
        [SerializeField] private int reserveAmmo = 120;

        [Header("Interaction Settings")]
        [SerializeField] private float interactRadius = 2.2f;
        [SerializeField] private string promptText = "Press [E] to pick up";

        [Header("Visual Settling")]
        [SerializeField] private bool idleFloatAndSpin = false;
        [SerializeField] private float spinSpeed = 45f;
        [SerializeField] private float floatAmplitude = 0.05f;
        [SerializeField] private float floatFrequency = 2.0f;

        public WeaponData Data => weaponData;
        public int AmmoInMag => ammoInMag;
        public int ReserveAmmo => reserveAmmo;
        public float InteractRadius => interactRadius;
        public string PromptText => $"{promptText} {(weaponData != null ? weaponData.weaponName : "Weapon")}";

        private Vector3 _initialLocalPos;
        private Collider _collider;
        private Rigidbody _rigidbody;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            if (_collider == null)
            {
                _collider = gameObject.AddComponent<BoxCollider>();
            }
            _collider.isTrigger = true;
            _rigidbody = GetComponent<Rigidbody>();
            _initialLocalPos = transform.position;

            if (weaponData != null && ammoInMag <= 0)
            {
                ammoInMag = weaponData.magazineCapacity;
                reserveAmmo = weaponData.defaultStartingReserve;
            }
        }

        private void Update()
        {
            if (idleFloatAndSpin)
            {
                transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
                float yOffset = Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
                transform.position = _initialLocalPos + Vector3.up * yOffset;
            }
        }

        public void Setup(WeaponData data, int currentMag, int currentReserve, bool throwPhysics = false, Vector3 throwVelocity = default)
        {
            weaponData = data;
            ammoInMag = currentMag;
            reserveAmmo = currentReserve;

            if (_collider == null) _collider = GetComponent<Collider>();

            if (throwPhysics)
            {
                idleFloatAndSpin = false;
                if (_rigidbody == null) _rigidbody = gameObject.AddComponent<Rigidbody>();
                _rigidbody.isKinematic = false;
                _rigidbody.linearVelocity = throwVelocity;
                _rigidbody.angularVelocity = Random.insideUnitSphere * 5f;
                _collider.isTrigger = false;

                // Settle after 1 second back to trigger
                Invoke(nameof(SettleToTrigger), 1.2f);
            }
        }

        private void SettleToTrigger()
        {
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
            }
            if (_collider != null)
            {
                _collider.isTrigger = true;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactRadius);
        }
    }
}
