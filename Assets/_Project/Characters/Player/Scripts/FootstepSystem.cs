using UnityEngine;

namespace TheChimeraProtocol.Player
{
    public enum SurfaceType
    {
        Marble,
        Metal,
        Tiles,
        Concrete,
        BloodPuddle
    }

    /// <summary>
    /// Implements procedural footstep sound cadence and surface detection.
    /// Distinguishes between Marble, Metal, Tiles, Concrete, and Blood pools.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class FootstepSystem : MonoBehaviour
    {
        [Header("Cadence Settings")]
        public float walkStepDistance = 1.6f;
        public float jogStepDistance = 1.9f;
        public float sprintStepDistance = 2.4f;

        [Header("Audio Clips (Optional - Generates procedural audio if null)")]
        public AudioClip[] marbleSteps;
        public AudioClip[] metalSteps;
        public AudioClip[] bloodSplashes;

        private AudioSource _audioSource;
        private PlayerController _playerController;
        private float _distanceTraveled;
        private Vector3 _lastPosition;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.spatialBlend = 1.0f; // Full 3D spatial sound
            _audioSource.minDistance = 1.5f;
            _audioSource.maxDistance = 25f;
            _audioSource.playOnAwake = false;

            _playerController = GetComponent<PlayerController>();
            _lastPosition = transform.position;
        }

        private void Update()
        {
            if (_playerController == null || !_playerController.IsGrounded) return;

            float speed = _playerController.CurrentSpeed;
            if (speed < 0.2f) return;

            Vector3 delta = transform.position - _lastPosition;
            delta.y = 0f;
            _distanceTraveled += delta.magnitude;
            _lastPosition = transform.position;

            float stepInterval = _playerController.IsSprinting ? sprintStepDistance : (_playerController.IsCrouching ? 1.2f : jogStepDistance);

            if (_distanceTraveled >= stepInterval)
            {
                _distanceTraveled = 0f;
                PlayFootstep();
            }
        }

        private void PlayFootstep()
        {
            SurfaceType surface = DetectSurface();
            float volume = _playerController.IsSprinting ? 0.9f : (_playerController.IsCrouching ? 0.25f : 0.6f);

            _audioSource.pitch = Random.Range(0.92f, 1.08f);

            AudioClip clip = GetClipForSurface(surface);
            if (clip != null)
            {
                _audioSource.PlayOneShot(clip, volume);
            }
            else
            {
                // Procedural synthetic footstep fallback
                PlayProceduralStep(surface, volume);
            }
        }

        private SurfaceType DetectSurface()
        {
            Vector3 origin = transform.position + Vector3.up * 0.2f;
            int playerLayer = LayerMask.NameToLayer("Player");
            int mask = (playerLayer != -1) ? ~(1 << playerLayer) : ~0;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 0.6f, mask, QueryTriggerInteraction.Ignore))
            {
                // Check material or object name
                var rend = hit.collider.GetComponent<Renderer>();
                if (rend != null && rend.sharedMaterial != null)
                {
                    string matName = rend.sharedMaterial.name.ToLower();
                    if (matName.Contains("marble")) return SurfaceType.Marble;
                    if (matName.Contains("metal") || matName.Contains("plate")) return SurfaceType.Metal;
                    if (matName.Contains("tile")) return SurfaceType.Tiles;
                    if (matName.Contains("blood")) return SurfaceType.BloodPuddle;
                }

                // Check for decal blood underneath
                Collider[] cols = Physics.OverlapSphere(hit.point, 0.3f);
                foreach (var c in cols)
                {
                    if (c.name.ToLower().Contains("blood"))
                        return SurfaceType.BloodPuddle;
                }
            }

            return SurfaceType.Marble; // Default for Admin Lobby
        }

        private AudioClip GetClipForSurface(SurfaceType surface)
        {
            AudioClip[] array = surface switch
            {
                SurfaceType.Marble => marbleSteps,
                SurfaceType.Metal => metalSteps,
                SurfaceType.BloodPuddle => bloodSplashes,
                _ => marbleSteps
            };

            if (array != null && array.Length > 0)
            {
                return array[Random.Range(0, array.Length)];
            }
            return null;
        }

        private void PlayProceduralStep(SurfaceType surface, float volume)
        {
            // Synthesize subtle foot-plant impact sound
            int sampleRate = 44100;
            float duration = 0.08f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float baseFreq = surface == SurfaceType.Metal ? 380f : 120f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 45f);
                float noise = (Random.value * 2f - 1f) * 0.3f;
                samples[i] = (Mathf.Sin(2f * Mathf.PI * baseFreq * t) + noise) * decay * volume;
            }

            AudioClip synthClip = AudioClip.Create("SynthFootstep", sampleCount, 1, sampleRate, false);
            synthClip.SetData(samples, 0);
            _audioSource.PlayOneShot(synthClip, volume);
        }
    }
}
