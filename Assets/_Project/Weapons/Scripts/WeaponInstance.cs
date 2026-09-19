using System.Collections;
using UnityEngine;

namespace TheChimeraProtocol.Weapons
{
    /// <summary>
    /// Represents the physical equipped weapon instance.
    /// Manages ammunition, fire rate cooldown, audio playback with procedural gunfire synthesis fallback,
    /// muzzle flash, and visual tracer line.
    /// </summary>
    public class WeaponInstance : MonoBehaviour
    {
        [Header("Weapon Data Configuration")]
        [SerializeField] private WeaponData weaponData;

        [Header("Weapon Hierarchy Nodes")]
        [SerializeField] private Transform muzzlePoint;
        [SerializeField] private ParticleSystem muzzleFlashParticles;
        [SerializeField] private AudioSource audioSource;

        [Header("Runtime Ammo State")]
        [SerializeField] private int currentAmmoInMag;
        [SerializeField] private int currentReserveAmmo;
        [SerializeField] private bool isReloading;

        public WeaponData Data => weaponData;
        public int CurrentAmmoInMag => currentAmmoInMag;
        public int CurrentReserveAmmo => currentReserveAmmo;
        public bool IsReloading => isReloading;
        public Transform MuzzlePoint => muzzlePoint;

        private float _nextFireTime;
        private Coroutine _reloadCoroutine;
        private AudioClip _proceduralFireClip;
        private AudioClip _proceduralDryFireClip;
        private AudioClip _proceduralReloadClip;

        private void Awake()
        {
            InitializeComponents();
        }

        public void Initialize(WeaponData data, int startingMag = -1, int startingReserve = -1)
        {
            weaponData = data;
            InitializeComponents();

            if (data != null)
            {
                currentAmmoInMag = (startingMag >= 0) ? startingMag : data.magazineCapacity;
                currentReserveAmmo = (startingReserve >= 0) ? startingReserve : data.defaultStartingReserve;
            }
        }

        private void InitializeComponents()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1.0f; // 3D sound
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 45f;

            // Auto-locate muzzle point if not assigned
            if (muzzlePoint == null)
            {
                muzzlePoint = FindMuzzleTransform();
            }

            // Build or find muzzle flash
            if (muzzleFlashParticles == null && muzzlePoint != null)
            {
                muzzleFlashParticles = muzzlePoint.GetComponentInChildren<ParticleSystem>();
                if (muzzleFlashParticles == null)
                {
                    muzzleFlashParticles = CreateProceduralMuzzleFlash(muzzlePoint);
                }
            }

            GenerateProceduralAudioClips();
        }

        private Transform FindMuzzleTransform()
        {
            Transform found = transform.Find("MuzzlePoint");
            if (found != null) return found;

            found = transform.Find("Muzzle");
            if (found != null) return found;

            // Look for barrel child
            Transform barrel = transform.Find("AR_W_Barrel");
            if (barrel != null)
            {
                // Create MuzzlePoint at the tip of the barrel
                var tip = new GameObject("MuzzlePoint");
                tip.transform.SetParent(barrel);
                tip.transform.localPosition = new Vector3(0f, 0f, 0.45f);
                tip.transform.localRotation = Quaternion.identity;
                return tip.transform;
            }

            // Fallback to front of weapon bounds
            var fallback = new GameObject("MuzzlePoint");
            fallback.transform.SetParent(transform);
            fallback.transform.localPosition = new Vector3(0f, 0.05f, 0.5f);
            fallback.transform.localRotation = Quaternion.identity;
            return fallback.transform;
        }

        private ParticleSystem CreateProceduralMuzzleFlash(Transform parent)
        {
            var go = new GameObject("MuzzleFlashFX");
            go.transform.SetParent(parent, false);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.duration = 0.05f;
            main.loop = false;
            main.startLifetime = 0.04f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.25f);
            main.startColor = new Color(1.0f, 0.88f, 0.5f, 1.0f);
            main.stopAction = ParticleSystemStopAction.None;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.02f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            // Assign URP Particle Material to prevent pink rendering
            Shader urpParticleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (urpParticleShader != null)
            {
                var pMat = new Material(urpParticleShader);
                pMat.name = "M_Runtime_MuzzleFlash";
                if (pMat.HasProperty("_BaseColor")) pMat.SetColor("_BaseColor", new Color(1.0f, 0.88f, 0.45f, 1.0f));
                renderer.material = pMat;
            }

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // Light flash
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.4f);
            light.range = 5f;
            light.intensity = 3f;
            light.enabled = false;

            return ps;
        }

        public bool CanFire()
        {
            if (isReloading) return false;
            if (Time.time < _nextFireTime) return false;
            return true;
        }

        public bool TryFire(out bool fired)
        {
            fired = false;
            if (!CanFire()) return false;

            if (weaponData == null) return false;

            _nextFireTime = Time.time + weaponData.FireInterval;

            if (currentAmmoInMag <= 0)
            {
                PlayDryFireSound();
                return false;
            }

            // Consume ammo
            currentAmmoInMag--;
            fired = true;

            // Play Muzzle Flash
            PlayMuzzleFlash();

            // Play Gunshot Audio
            PlayGunshotAudio();

            return true;
        }

        private void PlayMuzzleFlash()
        {
            if (muzzleFlashParticles != null)
            {
                muzzleFlashParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                muzzleFlashParticles.Play();

                Light light = muzzleFlashParticles.GetComponent<Light>();
                if (light != null)
                {
                    StartCoroutine(FlashLightRoutine(light));
                }
            }
        }

        private IEnumerator FlashLightRoutine(Light flashLight)
        {
            flashLight.enabled = true;
            yield return new WaitForSeconds(0.04f);
            flashLight.enabled = false;
        }

        private void PlayGunshotAudio()
        {
            if (audioSource == null) return;

            if (weaponData != null && weaponData.fireAudioClips != null && weaponData.fireAudioClips.Length > 0)
            {
                AudioClip clip = weaponData.fireAudioClips[Random.Range(0, weaponData.fireAudioClips.Length)];
                if (clip != null)
                {
                    audioSource.pitch = Random.Range(0.95f, 1.05f);
                    audioSource.PlayOneShot(clip);
                    return;
                }
            }

            // Procedural Gunshot Fallback
            if (_proceduralFireClip != null)
            {
                audioSource.pitch = Random.Range(0.92f, 1.08f);
                audioSource.PlayOneShot(_proceduralFireClip, 0.9f);
            }
        }

        private void PlayDryFireSound()
        {
            if (audioSource == null) return;

            if (weaponData != null && weaponData.dryFireAudioClip != null)
            {
                audioSource.pitch = 1.0f;
                audioSource.PlayOneShot(weaponData.dryFireAudioClip);
                return;
            }

            if (_proceduralDryFireClip != null)
            {
                audioSource.pitch = 1.2f;
                audioSource.PlayOneShot(_proceduralDryFireClip, 0.4f);
            }
        }

        public bool CanReload()
        {
            if (isReloading) return false;
            if (weaponData == null) return false;
            if (currentAmmoInMag >= weaponData.magazineCapacity) return false;
            if (currentReserveAmmo <= 0) return false;
            return true;
        }

        public void StartReload()
        {
            if (!CanReload()) return;

            if (_reloadCoroutine != null)
            {
                StopCoroutine(_reloadCoroutine);
            }

            _reloadCoroutine = StartCoroutine(ReloadRoutine());
        }

        private IEnumerator ReloadRoutine()
        {
            isReloading = true;

            // Play Reload Audio
            if (weaponData != null && weaponData.reloadAudioClip != null)
            {
                audioSource.PlayOneShot(weaponData.reloadAudioClip);
            }
            else if (_proceduralReloadClip != null)
            {
                audioSource.PlayOneShot(_proceduralReloadClip, 0.6f);
            }

            float duration = (weaponData != null) ? weaponData.reloadDuration : 2.2f;
            yield return new WaitForSeconds(duration);

            CompleteReload();
        }

        public void CompleteReload()
        {
            if (!isReloading || weaponData == null) return;

            int needed = weaponData.magazineCapacity - currentAmmoInMag;
            int toLoad = Mathf.Min(needed, currentReserveAmmo);

            currentAmmoInMag += toLoad;
            currentReserveAmmo -= toLoad;

            isReloading = false;
            _reloadCoroutine = null;
        }

        public void CancelReload()
        {
            if (!isReloading) return;

            if (_reloadCoroutine != null)
            {
                StopCoroutine(_reloadCoroutine);
                _reloadCoroutine = null;
            }
            isReloading = false;
        }

        public void AddReserveAmmo(int amount)
        {
            if (weaponData == null) return;
            currentReserveAmmo = Mathf.Clamp(currentReserveAmmo + amount, 0, weaponData.maxReserveCapacity);
        }

        #region Procedural Audio Generation
        private void GenerateProceduralAudioClips()
        {
            if (_proceduralFireClip == null)
            {
                _proceduralFireClip = CreateProceduralGunshotClip();
            }
            if (_proceduralDryFireClip == null)
            {
                _proceduralDryFireClip = CreateProceduralClickClip(0.03f, 1800f);
            }
            if (_proceduralReloadClip == null)
            {
                _proceduralReloadClip = CreateProceduralReloadSequenceClip();
            }
        }

        private AudioClip CreateProceduralGunshotClip()
        {
            int sampleRate = 44100;
            float duration = 0.28f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;

                // Sharp attack transient (click)
                float transient = Mathf.Sin(2f * Mathf.PI * 120f * t) * Mathf.Exp(-t * 80f);

                // Body boom (low-frequency resonance)
                float body = Mathf.Sin(2f * Mathf.PI * 75f * (1f - t * 1.5f) * t) * Mathf.Exp(-t * 22f);

                // Noise burst (powder crack)
                float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 28f);

                samples[i] = Mathf.Clamp((transient * 0.4f + body * 0.6f + noise * 0.5f), -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Procedural_Gunshot", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralClickClip(float duration, float frequency)
        {
            int sampleRate = 44100;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-t * 120f);
            }

            AudioClip clip = AudioClip.Create("Procedural_Click", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralReloadSequenceClip()
        {
            int sampleRate = 44100;
            float duration = 1.2f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            // Click 1 (mag out at 0.15s)
            int magOutSample = (int)(0.15f * sampleRate);
            // Click 2 (mag in at 0.70s)
            int magInSample = (int)(0.70f * sampleRate);
            // Click 3 (bolt slide at 1.00s)
            int boltSample = (int)(1.00f * sampleRate);

            for (int i = 0; i < totalSamples; i++)
            {
                float val = 0f;
                if (i >= magOutSample && i < magOutSample + 2000)
                {
                    float t = (float)(i - magOutSample) / sampleRate;
                    val += (Random.value * 0.4f + Mathf.Sin(2f * Mathf.PI * 600f * t)) * Mathf.Exp(-t * 100f);
                }
                if (i >= magInSample && i < magInSample + 2500)
                {
                    float t = (float)(i - magInSample) / sampleRate;
                    val += (Random.value * 0.5f + Mathf.Sin(2f * Mathf.PI * 800f * t)) * Mathf.Exp(-t * 90f);
                }
                if (i >= boltSample && i < boltSample + 3000)
                {
                    float t = (float)(i - boltSample) / sampleRate;
                    val += (Random.value * 0.6f + Mathf.Sin(2f * Mathf.PI * 1200f * t)) * Mathf.Exp(-t * 80f);
                }
                samples[i] = Mathf.Clamp(val, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Procedural_Reload", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        #endregion
    }
}
