using UnityEngine;

/// <summary>
/// Дверь: при наведении весь объект плавно моргает белым проблесковым светом (emission). Открытие по E.
/// Вешай на каждый створ двери (для двойных дверей — два объекта с этим скриптом).
/// </summary>
[RequireComponent(typeof(Collider))]
public class InteractableDoor : MonoBehaviour
{
    [Header("Открытие")]
    [Tooltip("Угол открытия в градусах (вокруг оси Y локально или hingeAxis).")]
    [SerializeField] float openAngle = 90f;
    [Tooltip("Ось вращения (локальная). Например (0,1,0) — влево/вправо.")]
    [SerializeField] Vector3 hingeAxis = Vector3.up;
    [Tooltip("Скорость открытия/закрытия.")]
    [SerializeField] float openSpeed = 180f;

    [Header("Проблесковый свет при наведении")]
    [Tooltip("Скорость моргания (чем больше — тем частый пульс).")]
    [SerializeField] float pulseSpeed = 4f;
    [Tooltip("Минимальная яркость (0 = совсем гаснет).")]
    [SerializeField, Range(0f, 0.5f)] float pulseMin = 0.15f;
    [Tooltip("Максимальная яркость проблеска.")]
    [SerializeField, Range(0.3f, 1.5f)] float pulseMax = 0.85f;
    [Tooltip("Длительность плавного затухания при уводе прицела (сек).")]
    [SerializeField] float fadeOutDuration = 0.35f;

    Renderer[] _renderers;
    Material[] _materials;
    Vector3 _closedEuler;
    float _currentAngle;
    bool _isOpen;
    bool _highlighted;
    float _highlightStartTime;
    bool _fadingOut;
    float _fadeOutStartTime;
    float _fadeOutStartIntensity;

    void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _materials = new Material[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
                _materials[i] = _renderers[i].material;
        }
        _closedEuler = transform.localEulerAngles;
    }

    void Update()
    {
        float targetAngle = _isOpen ? openAngle : 0f;
        _currentAngle = Mathf.MoveTowards(_currentAngle, targetAngle, openSpeed * Time.deltaTime);
        transform.localRotation = Quaternion.Euler(_closedEuler) * Quaternion.AngleAxis(_currentAngle, hingeAxis);

        if (_highlighted && _materials != null)
        {
            float localT = Time.time - _highlightStartTime;
            float wave = 0.5f + 0.5f * Mathf.Sin(localT * pulseSpeed - Mathf.PI * 0.5f);
            float intensity = Mathf.Lerp(pulseMin, pulseMax, wave);
            ApplyEmission(intensity);
        }
        else if (_fadingOut && _materials != null)
        {
            float t = (Time.time - _fadeOutStartTime) / fadeOutDuration;
            if (t >= 1f)
            {
                ApplyEmission(0f);
                _fadingOut = false;
            }
            else
            {
                float intensity = Mathf.Lerp(_fadeOutStartIntensity, 0f, t);
                ApplyEmission(intensity);
            }
        }
    }

    void ApplyEmission(float intensity)
    {
        if (_materials == null) return;
        Color emission = intensity <= 0f ? Color.black : Color.white * intensity;
        for (int i = 0; i < _materials.Length; i++)
        {
            if (_materials[i] == null) continue;
            _materials[i].EnableKeyword("_EMISSION");
            _materials[i].SetColor("_EmissionColor", emission);
        }
    }

    public void SetHighlight(bool on)
    {
        if (on)
        {
            _highlighted = true;
            _fadingOut = false;
            _highlightStartTime = Time.time;
        }
        else
        {
            _highlighted = false;
            if (_materials != null)
            {
                float localT = Time.time - _highlightStartTime;
                float wave = 0.5f + 0.5f * Mathf.Sin(localT * pulseSpeed - Mathf.PI * 0.5f);
                _fadeOutStartIntensity = Mathf.Lerp(pulseMin, pulseMax, wave);
                _fadeOutStartTime = Time.time;
                _fadingOut = true;
            }
        }
    }

    public void Open()
    {
        _isOpen = !_isOpen;
    }

    public bool IsOpen => _isOpen;
}
