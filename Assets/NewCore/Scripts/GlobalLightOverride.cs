using UnityEngine;
using UnityEngine.Rendering;

[AddComponentMenu("NewCore/Global Light Override")]
public class GlobalLightOverride : MonoBehaviour
{
    [Header("AMBIENT OVERRIDE")]
    [Tooltip("Включить глобальную подсветку сцены.")]
    public bool enableAmbientOverride = true;
    [Tooltip("Цвет глобального окружающего света.")]
    public Color ambientColor = new Color(1f, 0.98f, 0.92f);
    [Tooltip("Интенсивность окружающего света (0.5–1.5 обычно достаточно).")]
    [Range(0f, 2f)] public float ambientIntensity = 1.1f;

    [Header("GLOBAL DIRECTIONAL LIGHT")]
    [Tooltip("Добавить направленный свет, чтобы осветить всю сцену.")]
    public bool enableDirectionalLight = true;
    [Range(0f, 3f)] public float directionalIntensity = 0.9f;
    public Color directionalColor = new Color(1f, 0.98f, 0.9f);
    public bool directionalShadows = false;
    [Tooltip("Поворот света (как солнце).")]
    public Vector3 directionalRotation = new Vector3(50f, 30f, 0f);

    private Light _directionalLight;
    private AmbientMode _prevAmbientMode;
    private Color _prevAmbientColor;
    private float _prevAmbientIntensity;

    private void OnEnable()
    {
        CacheRenderSettings();
        ApplyAmbient();
        SetupDirectional();
    }

    private void OnDisable()
    {
        RestoreRenderSettings();
        CleanupDirectional();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        ApplyAmbient();
        ApplyDirectional();
    }

    private void CacheRenderSettings()
    {
        _prevAmbientMode = RenderSettings.ambientMode;
        _prevAmbientColor = RenderSettings.ambientLight;
        _prevAmbientIntensity = RenderSettings.ambientIntensity;
    }

    private void ApplyAmbient()
    {
        if (!enableAmbientOverride) return;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
        RenderSettings.ambientIntensity = ambientIntensity;
    }

    private void RestoreRenderSettings()
    {
        if (!enableAmbientOverride) return;
        RenderSettings.ambientMode = _prevAmbientMode;
        RenderSettings.ambientLight = _prevAmbientColor;
        RenderSettings.ambientIntensity = _prevAmbientIntensity;
    }

    private void SetupDirectional()
    {
        if (!enableDirectionalLight) return;
        if (_directionalLight != null) return;

        var lightObject = new GameObject("GlobalDirectionalLight");
        lightObject.transform.SetParent(transform, false);
        _directionalLight = lightObject.AddComponent<Light>();
        _directionalLight.type = LightType.Directional;
        ApplyDirectional();
    }

    private void ApplyDirectional()
    {
        if (_directionalLight == null) return;

        _directionalLight.intensity = directionalIntensity;
        _directionalLight.color = directionalColor;
        _directionalLight.shadows = directionalShadows ? LightShadows.Soft : LightShadows.None;
        _directionalLight.transform.localRotation = Quaternion.Euler(directionalRotation);
    }

    private void CleanupDirectional()
    {
        if (_directionalLight == null) return;
        Destroy(_directionalLight.gameObject);
        _directionalLight = null;
    }
}
