using UnityEngine;

public class FlickeringLight : MonoBehaviour
{
    [Header("Light Settings")]
    public Light targetLight;
    public float minIntensity = 0.05f;
    public float maxIntensity = 1.8f;
    
    [Header("Flicker Pattern")]
    public float minFlickerSpeed = 0.03f;
    public float maxFlickerSpeed = 0.25f;
    [Range(0f, 1f)] public float outageChance = 0.15f;
    public float outageDuration = 0.8f;

    [Header("Linked Emissive Mesh")]
    public Renderer emissiveRenderer;
    public int materialIndex = 0;
    [ColorUsage(true, true)] public Color baseEmissionColor = new Color(0.85f, 0.95f, 1.0f) * 2.5f;

    private float timer = 0f;
    private float currentInterval = 0.1f;
    private bool isOutage = false;
    private MaterialPropertyBlock propBlock;

    void Awake()
    {
        if (targetLight == null) targetLight = GetComponent<Light>();
        propBlock = new MaterialPropertyBlock();
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= currentInterval)
        {
            timer = 0f;
            currentInterval = Random.Range(minFlickerSpeed, maxFlickerSpeed);

            if (!isOutage && Random.value < outageChance)
            {
                isOutage = true;
                currentInterval = outageDuration;
                SetLightState(0.01f);
            }
            else
            {
                isOutage = false;
                float targetInt = Random.Range(minIntensity, maxIntensity);
                SetLightState(targetInt);
            }
        }
    }

    void SetLightState(float intensity)
    {
        if (targetLight != null) targetLight.intensity = intensity;

        if (emissiveRenderer != null)
        {
            float ratio = Mathf.Clamp01(intensity / maxIntensity);
            Color finalEmission = baseEmissionColor * ratio;
            emissiveRenderer.GetPropertyBlock(propBlock, materialIndex);
            propBlock.SetColor("_EmissionColor", finalEmission);
            emissiveRenderer.SetPropertyBlock(propBlock, materialIndex);
        }
    }
}
