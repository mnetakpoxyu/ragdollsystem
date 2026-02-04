using UnityEngine;

/// <summary>
/// Неоновая лампа: повесь на объект — он начнёт светиться розовым неоном и красиво освещаться в 3D.
/// Всё настраивается скриптом: свет + свечение материала. Работает в редакторе и в игре.
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public class NeonLamp : MonoBehaviour
{
    [Header("Цвет неона (розовый)")]
    [SerializeField] Color neonColor = new Color(1f, 0.45f, 0.65f, 1f);

    [Header("Свет — освещает этот объект и рядом")]
    [SerializeField, Range(0.5f, 10f)] float lightIntensity = 2.5f;
    [SerializeField, Range(1f, 15f)] float lightRange = 5f;
    [SerializeField] bool castShadows = false;

    [Header("Свечение материала (сам неон)")]
    [SerializeField, Range(1f, 10f)] float emissionStrength = 4f;
    [SerializeField] bool affectChildren = true;

    Light _light;
    Renderer[] _renderers;
    Material[] _materialInstances;

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");

    void Reset()
    {
        SetupLight();
        SetupEmission();
    }

    void Awake()
    {
        SetupLight();
        SetupEmission();
    }

    void OnEnable()
    {
        SetupLight();
        SetupEmission();
    }

    void Start()
    {
        if (Application.isPlaying)
        {
            SetupLight();
            SetupEmission();
        }
    }

    void SetupLight()
    {
        _light = GetComponent<Light>();
        if (_light == null)
            _light = gameObject.AddComponent<Light>();

        _light.type = LightType.Point;
        _light.color = neonColor;
        _light.intensity = lightIntensity;
        _light.range = lightRange;
        _light.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
        _light.enabled = true;
    }

    void SetupEmission()
    {
        _renderers = affectChildren ? GetComponentsInChildren<Renderer>(true) : GetComponents<Renderer>();
        if (_renderers == null || _renderers.Length == 0) return;

        if (_materialInstances != null && _materialInstances.Length == _renderers.Length)
        {
            for (int i = 0; i < _materialInstances.Length; i++)
                ApplyEmission(_materialInstances[i]);
            return;
        }

        _materialInstances = new Material[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            _materialInstances[i] = _renderers[i].material;
            ApplyEmission(_materialInstances[i]);
        }
    }

    void ApplyEmission(Material mat)
    {
        if (mat == null) return;

        Color emissionColor = neonColor * emissionStrength;

        if (mat.HasProperty(EmissionColorId))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor(EmissionColorId, emissionColor);
            if (mat.HasProperty(EmissionMapId) && mat.GetTexture(EmissionMapId) == null)
                mat.SetTexture(EmissionMapId, Texture2D.whiteTexture);
            return;
        }

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emissionColor);
            if (mat.HasProperty("_EmissionMap") && mat.GetTexture("_EmissionMap") == null)
                mat.SetTexture("_EmissionMap", Texture2D.whiteTexture);
            return;
        }

        if (mat.HasProperty("_EmissiveColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissiveColor", emissionColor);
            return;
        }

        if (mat.HasProperty("_BaseColor"))
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", emissionColor);
        }
    }

    void OnValidate()
    {
        if (!gameObject.activeInHierarchy) return;

        if (_light != null)
        {
            _light.color = neonColor;
            _light.intensity = lightIntensity;
            _light.range = lightRange;
            _light.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
            _light.enabled = true;
        }
        else
            SetupLight();

        if (_materialInstances != null)
        {
            Color emissionColor = neonColor * emissionStrength;
            for (int i = 0; i < _materialInstances.Length; i++)
            {
                if (_materialInstances[i] == null) continue;
                if (_materialInstances[i].HasProperty(EmissionColorId))
                {
                    _materialInstances[i].EnableKeyword("_EMISSION");
                    _materialInstances[i].SetColor(EmissionColorId, emissionColor);
                }
                else if (_materialInstances[i].HasProperty("_EmissionColor"))
                {
                    _materialInstances[i].EnableKeyword("_EMISSION");
                    _materialInstances[i].SetColor("_EmissionColor", emissionColor);
                }
                else if (_materialInstances[i].HasProperty("_EmissiveColor"))
                    _materialInstances[i].SetColor("_EmissiveColor", emissionColor);
            }
        }
        else
            SetupEmission();
    }

    void OnDestroy()
    {
        if (_materialInstances != null)
        {
            for (int i = 0; i < _materialInstances.Length; i++)
            {
                if (_materialInstances[i] != null)
                    Destroy(_materialInstances[i]);
            }
        }
    }
}
