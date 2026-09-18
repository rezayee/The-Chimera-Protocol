using UnityEngine;

public class EmergencyAlarmLight : MonoBehaviour
{
    [Header("Rotation")]
    public Transform rotatingHead;
    public float rotationSpeed = 360f; // degrees per sec

    [Header("Light & Pulse")]
    public Light alarmLight;
    public float pulseSpeed = 4f;
    public float minIntensity = 0.5f;
    public float maxIntensity = 3.0f;

    [Header("Emissive Indicator")]
    public Renderer beaconRenderer;
    [ColorUsage(true, true)] public Color emergencyColor = new Color(1.0f, 0.05f, 0.05f) * 4f;

    private MaterialPropertyBlock propBlock;

    void Awake()
    {
        propBlock = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (rotatingHead != null)
        {
            rotatingHead.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
        }

        if (alarmLight != null)
        {
            float wave = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            alarmLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, wave);

            if (beaconRenderer != null)
            {
                beaconRenderer.GetPropertyBlock(propBlock);
                propBlock.SetColor("_EmissionColor", emergencyColor * wave);
                beaconRenderer.SetPropertyBlock(propBlock);
            }
        }
    }
}
