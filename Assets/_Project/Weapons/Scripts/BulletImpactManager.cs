using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TheChimeraProtocol.Weapons
{
    /// <summary>
    /// Spawns and manages pooled bullet impact decals (URP DecalProjector) and spark particle effects.
    /// Fully procedural fallback ensures bullet holes appear even if custom textures/materials are not assigned.
    /// </summary>
    public class BulletImpactManager : MonoBehaviour
    {
        private static BulletImpactManager _instance;
        public static BulletImpactManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[BulletImpactManager]");
                    _instance = go.AddComponent<BulletImpactManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Decal Settings")]
        [SerializeField] private Material defaultDecalMaterial;
        [SerializeField] private Vector3 decalSize = new Vector3(0.12f, 0.12f, 0.25f);
        [SerializeField] private float decalLifetime = 15f;
        [SerializeField] private int maxPooledDecals = 64;

        [Header("Hit Particle Settings")]
        [SerializeField] private int maxPooledSparks = 32;

        private readonly Queue<DecalProjector> _decalPool = new Queue<DecalProjector>();
        private readonly Queue<ParticleSystem> _sparkPool = new Queue<ParticleSystem>();
        private Material _runtimeDecalMaterial;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            EnsureDefaultDecalMaterial();
        }

        private void EnsureDefaultDecalMaterial()
        {
            if (defaultDecalMaterial != null) return;
            if (_runtimeDecalMaterial != null) return;

            // Find URP Decal Shader
            Shader decalShader = Shader.Find("Shader Graphs/Decal") ?? Shader.Find("Universal Render Pipeline/Decal");
            if (decalShader != null)
            {
                _runtimeDecalMaterial = new Material(decalShader);
                _runtimeDecalMaterial.name = "M_Procedural_BulletHole_Decal";

                // Generate procedural bullet hole texture
                Texture2D holeTex = GenerateBulletHoleTexture();
                if (_runtimeDecalMaterial.HasProperty("_BaseColorMap"))
                {
                    _runtimeDecalMaterial.SetTexture("_BaseColorMap", holeTex);
                }
                else if (_runtimeDecalMaterial.HasProperty("_MainTex"))
                {
                    _runtimeDecalMaterial.SetTexture("_MainTex", holeTex);
                }
                _runtimeDecalMaterial.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.12f, 0.95f));
            }
        }

        private Texture2D GenerateBulletHoleTexture()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size * 0.42f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist < radius)
                    {
                        float edgeFactor = Mathf.Clamp01((radius - dist) / 4f);
                        float innerDark = Mathf.Lerp(0.05f, 0.25f, dist / radius);
                        tex.SetPixel(x, y, new Color(innerDark, innerDark, innerDark, edgeFactor));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            return tex;
        }

        public void SpawnImpact(RaycastHit hit, Material overrideMaterial = null)
        {
            SpawnDecal(hit.point, hit.normal, hit.collider.transform, overrideMaterial);
            SpawnSparks(hit.point, hit.normal);
        }

        public void SpawnDecal(Vector3 position, Vector3 normal, Transform parent = null, Material overrideMaterial = null)
        {
            EnsureDefaultDecalMaterial();
            Material matToUse = overrideMaterial != null ? overrideMaterial : (defaultDecalMaterial != null ? defaultDecalMaterial : _runtimeDecalMaterial);
            if (matToUse == null) return;

            DecalProjector projector;
            if (_decalPool.Count < maxPooledDecals)
            {
                var go = new GameObject("Decal_BulletHole");
                projector = go.AddComponent<DecalProjector>();
                go.transform.SetParent(transform);
            }
            else
            {
                projector = _decalPool.Dequeue();
                if (projector == null) return;
            }

            projector.gameObject.SetActive(true);
            projector.material = matToUse;
            projector.size = decalSize;
            projector.fadeFactor = 1.0f;

            // Orient projector pointing into the surface
            projector.transform.position = position + normal * 0.05f;
            projector.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up);

            // Slightly randomize roll for organic look
            projector.transform.Rotate(Vector3.forward, Random.Range(0f, 360f), Space.Self);

            if (parent != null)
            {
                projector.transform.SetParent(parent, true);
            }

            _decalPool.Enqueue(projector);
        }

        public void SpawnSparks(Vector3 position, Vector3 normal)
        {
            ParticleSystem ps;
            if (_sparkPool.Count < maxPooledSparks)
            {
                var go = new GameObject("ImpactSparks");
                ps = go.AddComponent<ParticleSystem>();
                ConfigureSparkParticleSystem(ps);
                go.transform.SetParent(transform);
            }
            else
            {
                ps = _sparkPool.Dequeue();
                if (ps == null) return;
            }

            ps.gameObject.SetActive(true);
            ps.transform.position = position + normal * 0.02f;
            ps.transform.rotation = Quaternion.LookRotation(normal);
            ps.Play();

            _sparkPool.Enqueue(ps);
        }

        private void ConfigureSparkParticleSystem(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration = 0.35f;
            main.loop = false;
            main.startLifetime = 0.25f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.0f, 6.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startColor = new Color(1.0f, 0.85f, 0.4f, 1.0f);
            main.gravityModifier = 2.0f;
            main.stopAction = ParticleSystemStopAction.None;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 6, 12) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.03f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            // Assign URP Particle Material to prevent pink rendering
            Shader urpParticleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (urpParticleShader != null)
            {
                var pMat = new Material(urpParticleShader);
                pMat.name = "M_Runtime_Sparks";
                if (pMat.HasProperty("_BaseColor")) pMat.SetColor("_BaseColor", new Color(1.0f, 0.85f, 0.4f, 1.0f));
                renderer.material = pMat;
            }
        }
    }
}
