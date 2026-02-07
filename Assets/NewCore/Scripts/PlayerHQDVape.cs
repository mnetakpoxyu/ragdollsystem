using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Затяжка электронной сигареты (HQD): удержание кнопки V — появление перед лицом, отпускание — плавное исчезновение и пар.
/// Максимум затяжки — 5 секунд, количество пара зависит от времени удержания.
/// Вешать на объект с камерой (например Main Camera). HQD будет дочерним к камере.
/// </summary>
[AddComponentMenu("NewCore/Player HQD Vape")]
public class PlayerHQDVape : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Камера игрока (обычно это сам объект, на котором висит скрипт).")]
    [SerializeField] Transform playerCamera;
    [Tooltip("Префаб HQD (модель электронной сигареты). Если не задан — ищем дочерний объект с именем содержащим 'HQD' или 'hqd'.")]
    [SerializeField] GameObject hqdPrefab;
    [Tooltip("Input Action Asset с картой Player и действием Vape (по умолчанию клавиша V).")]
    [SerializeField] InputActionAsset inputActionAsset;
    [Tooltip("Партикл пара при выдохе. Если не задан — создаётся простой пар при старте.")]
    [SerializeField] ParticleSystem vaporParticle;
    [Tooltip("Родитель пара в мире (например тело игрока). Если задан — пар появляется в мире и его видят все. Если пусто — пар под камерой (только у тебя).")]
    [SerializeField] Transform vaporWorldParent;

    [Header("Видимость HQD")]
    [Tooltip("Слой, на который вешается HQD — только твоя камера должна его рендерить. Создай слой типа FirstPersonOnly, добавь его в Culling Mask своей камеры; камеры других игроков его не рендерят — HQD видят только ты.")]
    [SerializeField] int localOnlyLayer = 0;

    [Header("Коллизия пара")]
    [Tooltip("Слои, сквозь которые пар проходит без столкновения. Добавь слой модели игрока — пар не будет застревать и ломаться, когда бежишь в него. Edit → Project Settings → Tags and Layers: создай слой Player и назначь его телу персонажа.")]
    [SerializeField] LayerMask vaporIgnoreCollisionLayers;

    [Header("Лимит затяжки")]
    [Tooltip("Максимальное время затяжки в секундах. После этого затяжка автоматически прекращается.")]
    [SerializeField] float maxHoldTime = 5f;

    [Header("Позиции HQD (локальные относительно камеры)")]
    [Tooltip("Позиция/поворот/масштаб HQD перед лицом (rotation — твой подобранный угол для «во рту»).")]
    [SerializeField] Vector3 visibleLocalPosition = new Vector3(0.2f, -0.12f, 0.4f);
    [Tooltip("Rotation HQD перед камерой (например 285, 161, 165).")]
    [SerializeField] Vector3 visibleLocalEuler = new Vector3(285f, 161.37f, 164.97f);
    [SerializeField] float visibleScale = 1f;
    [Tooltip("Позиция «в кармане» — уезжает вправо-вниз, чтобы не проходить сквозь модель персонажа.")]
    [SerializeField] Vector3 hiddenLocalPosition = new Vector3(0.65f, -0.5f, -0.22f);
    [SerializeField] Vector3 hiddenLocalEuler = new Vector3(88f, 28f, -42f);
    [SerializeField] float hiddenScale = 0.85f;

    [Header("Анимация")]
    [Tooltip("Длительность появления HQD (сек).")]
    [SerializeField] float showDuration = 0.25f;
    [Tooltip("Длительность исчезновения HQD (сек).")]
    [SerializeField] float hideDuration = 0.35f;
    [Tooltip("Кривая для плавности появления (опционально).")]
    [SerializeField] AnimationCurve showCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Кривая для плавности исчезновения (опционально).")]
    [SerializeField] AnimationCurve hideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Пар (видят все)")]
    [Tooltip("Цвет пара. Меняй в инспекторе — пар будет этого цвета.")]
    [SerializeField] Color vaporColor = new Color(0.95f, 0.95f, 0.98f, 0.55f);
    [Tooltip("Минимальное количество частиц пара при короткой затяжке.")]
    [SerializeField] int vaporMinParticles = 400;
    [Tooltip("Максимальное количество частиц при полной затяжке (огромное облако).")]
    [SerializeField] int vaporMaxParticles = 2800;
    [Tooltip("Длительность испускания пара (сек).")]
    [SerializeField] float vaporEmitDuration = 2.5f;
    [Tooltip("За сколько секунд «выдыхается» пар (имитация выдоха). Частицы появляются постепенно, облако накатывает вперёд.")]
    [SerializeField, Min(0.3f)] float vaporExhaleSpreadDuration = 1.25f;
    [Tooltip("Смещение точки появления пара относительно рта (локально).")]
    [SerializeField] Vector3 vaporLocalOffset = new Vector3(0f, 0f, 0.05f);
    [Tooltip("Размер частиц при короткой затяжке — облако меньше.")]
    [SerializeField] float vaporMinStartSize = 0.65f;
    [Tooltip("Размер частиц при полной затяжке — облако намного больше.")]
    [SerializeField] float vaporMaxStartSize = 2.2f;
    [Tooltip("Случайный разброс размера облака при каждом выдохе (0 = одинаково, 0.25 = ±25%).")]
    [SerializeField, Range(0f, 0.5f)] float vaporSizeRandomness = 0.22f;
    [Tooltip("Случайный разброс количества частиц при каждом выдохе (0 = одинаково, 0.2 = ±20%).")]
    [SerializeField, Range(0f, 0.4f)] float vaporCountRandomness = 0.18f;

    [Header("Трюк: колечко (O‑ring)")]
    [Tooltip("Шанс при выдохе выдуть пар кольцом вместо облака (0 = никогда, 1 = всегда).")]
    [SerializeField, Range(0f, 1f)] float ringTrickChance = 0.3f;
    [Tooltip("Начальный радиус кольца (м) — размер «дырки» в момент выдоха.")]
    [SerializeField] float ringStartRadius = 0.035f;
    [Tooltip("Толщина кольца: разброс радиуса частиц (0 = тонкая линия, 0.02 = пухлое кольцо).")]
    [SerializeField, Min(0f)] float ringThickness = 0.008f;
    [Tooltip("Скорость полёта кольца вперёд по направлению взгляда (м/с).")]
    [SerializeField] float ringForwardSpeed = 1.4f;
    [Tooltip("Скорость расширения кольца — как быстро «дырка» увеличивается (м/с).")]
    [SerializeField] float ringExpandSpeed = 0.7f;
    [Tooltip("Замедление кольца со временем (0 = не замедляется, 0.5 = к концу жизни ~50% скорости).")]
    [SerializeField, Range(0f, 0.95f)] float ringDrag = 0.35f;
    [Tooltip("Количество частиц по контуру кольца (меньше = меньше нагрузка, 48–64 обычно хватает).")]
    [SerializeField, Min(24)] int ringParticleCount = 52;
    [Tooltip("Размер одной частицы — при малом количестве частиц можно увеличить для плотного кольца.")]
    [SerializeField, Min(0.01f)] float ringParticleSize = 0.55f;
    [Tooltip("Время жизни кольца (сек), затем плавно исчезает.")]
    [SerializeField, Min(0.5f)] float ringLifetime = 2.2f;

    [Header("Звуки")]
    [Tooltip("Звук работающего испарителя. Циклится, пока удерживаешь V; плавно стартует и останавливается при отпускании.")]
    [SerializeField] AudioClip evaporatorSound;
    [Tooltip("Громкость звука затяжки (испарителя). 0–1; меньше значение — тише.")]
    [Range(0f, 1f)] [SerializeField] float evaporatorVolume = 0.35f;
    [Tooltip("Звук выдоха пара. Воспроизводится один раз в момент отпускания V.")]
    [SerializeField] AudioClip exhaleSound;
    [Tooltip("Время плавного появления звука испарителя (сек). 0 — без фейда.")]
    [SerializeField] float evaporatorFadeInDuration = 0.15f;
    [Tooltip("Время плавного затухания звука испарителя при отпускании (сек). 0 — без фейда.")]
    [SerializeField] float evaporatorFadeOutDuration = 0.08f;

    Transform _hqdRoot;
    AudioSource _evaporatorSource;
    AudioSource _exhaleSource;
    InputAction _vapeAction;
    bool _isHolding;
    float _holdStartTime;
    float _animTime;
    float _animDuration;
    bool _animShowing;   // true = показываем, false = прячем
    bool _isAnimating;
    ParticleSystem _vaporInstance;
    bool _inputBlocked => PlayerInputManager.Instance != null && PlayerInputManager.Instance.IsInputLocked;

    const string HqdChildName = "HQD";

    void Start()
    {
        if (playerCamera == null)
            playerCamera = transform;

        EnsureHQD();
        EnsureVaporParticle();
        EnsureEvaporatorAudioSource();
        SetupInput();
    }

    const string EvaporatorAudioChildName = "HQDVape_EvaporatorAudio";
    const string ExhaleAudioChildName = "HQDVape_ExhaleAudio";

    void EnsureEvaporatorAudioSource()
    {
        if (_evaporatorSource != null && _exhaleSource != null) return;

        Transform t = transform;
        Transform evapChild = null;
        Transform exhaleChild = null;
        for (int i = 0; i < t.childCount; i++)
        {
            var c = t.GetChild(i);
            if (c.name == EvaporatorAudioChildName) evapChild = c;
            else if (c.name == ExhaleAudioChildName) exhaleChild = c;
        }

        if (evapChild == null)
        {
            var go = new GameObject(EvaporatorAudioChildName);
            go.transform.SetParent(t, false);
            go.transform.localPosition = Vector3.zero;
            _evaporatorSource = go.AddComponent<AudioSource>();
        }
        else
            _evaporatorSource = evapChild.GetComponent<AudioSource>() ?? evapChild.gameObject.AddComponent<AudioSource>();

        if (exhaleChild == null)
        {
            var go = new GameObject(ExhaleAudioChildName);
            go.transform.SetParent(t, false);
            go.transform.localPosition = Vector3.zero;
            _exhaleSource = go.AddComponent<AudioSource>();
        }
        else
            _exhaleSource = exhaleChild.GetComponent<AudioSource>() ?? exhaleChild.gameObject.AddComponent<AudioSource>();

        _evaporatorSource.playOnAwake = false;
        _evaporatorSource.loop = true;
        _evaporatorSource.spatialBlend = 0f;
        _evaporatorSource.Stop();

        _exhaleSource.playOnAwake = false;
        _exhaleSource.loop = false;
        _exhaleSource.spatialBlend = 0f;
    }

    void EnsureHQD()
    {
        if (_hqdRoot != null) return;

        if (hqdPrefab != null)
        {
            GameObject go = Instantiate(hqdPrefab, playerCamera);
            go.name = HqdChildName;
            _hqdRoot = go.transform;
        }
        else
        {
            foreach (Transform child in playerCamera)
            {
                if (child.name.IndexOf("HQD", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    child.name.IndexOf("hqd", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _hqdRoot = child;
                    break;
                }
            }
        }

        if (_hqdRoot == null)
        {
            Debug.LogWarning("PlayerHQDVape: HQD не найден. Назначьте префаб или добавьте дочерний объект с именем HQD.");
            return;
        }

        _hqdRoot.localPosition = hiddenLocalPosition;
        _hqdRoot.localRotation = Quaternion.Euler(hiddenLocalEuler);
        _hqdRoot.localScale = Vector3.one * hiddenScale;
        // По умолчанию скрыт: виден только когда куришь (у рта)
        _hqdRoot.gameObject.SetActive(false);

        if (localOnlyLayer >= 0 && localOnlyLayer <= 31)
            SetLayerRecursive(_hqdRoot.gameObject, localOnlyLayer);
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        for (int i = 0; i < go.transform.childCount; i++)
            SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
    }

    void EnsureVaporParticle()
    {
        Transform vaporParent = vaporWorldParent != null ? vaporWorldParent : playerCamera;
        if (vaporParticle != null)
        {
            _vaporInstance = vaporParticle;
            if (_vaporInstance.transform.parent != vaporParent)
            {
                var go = Instantiate(vaporParticle.gameObject, vaporParent);
                _vaporInstance = go.GetComponent<ParticleSystem>();
            }
            _vaporInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ApplyVaporColor(_vaporInstance, vaporColor);
            _vaporInstance.gameObject.SetActive(false);
            return;
        }

        GameObject vaporGo = new GameObject("VaporTemplate");
        vaporGo.transform.SetParent(vaporParent);
        vaporGo.transform.localPosition = Vector3.zero;
        vaporGo.transform.localRotation = Quaternion.identity;
        vaporGo.transform.localScale = Vector3.one;
        vaporGo.SetActive(false);

        _vaporInstance = vaporGo.AddComponent<ParticleSystem>();
        var main = _vaporInstance.main;
        main.duration = vaporEmitDuration;
        main.loop = false;
        main.startLifetime = 3.2f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        main.startSize = vaporMaxStartSize;
        main.startColor = vaporColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 4500;
        main.gravityModifier = 0.01f;

        var emission = _vaporInstance.emission;
        emission.enabled = false;

        var shape = _vaporInstance.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.04f;
        shape.rotation = new Vector3(0f, 0f, 0f);

        var velocityOverLifetime = _vaporInstance.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(1.2f, 2f);
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

        var noise = _vaporInstance.noise;
        noise.enabled = true;
        noise.strength = 0.12f;
        noise.frequency = 0.4f;
        noise.scrollSpeed = 0.15f;
        noise.damping = true;
        noise.octaveCount = 2;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        var colorOverLifetime = _vaporInstance.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Color c = vaporColor;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
            new[] { new GradientAlphaKey(c.a * 0.92f, 0f), new GradientAlphaKey(c.a * 0.65f, 0.35f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = grad;

        var sizeOverLifetime = _vaporInstance.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.5f), new Keyframe(0.12f, 1.15f), new Keyframe(0.35f, 1.5f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f)));

        var renderer = vaporGo.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            VaporParticleSetup.SetupVaporRenderer(renderer, vaporColor);
        }
        ParticlesCollisionSetup.SetupCollisionAndSplash(_vaporInstance, vaporColor, addSplash: false, vaporIgnoreCollisionLayers);
    }

    void ApplyVaporColor(ParticleSystem ps, Color color)
    {
        if (ps == null) return;
        var main = ps.main;
        main.startColor = color;
    }

    void SetupInput()
    {
        if (inputActionAsset == null) return;
        var map = inputActionAsset.FindActionMap("Player");
        _vapeAction = map?.FindAction("Vape");
        if (_vapeAction != null)
            _vapeAction.Enable();
    }

    void OnDestroy()
    {
        _vapeAction?.Disable();
    }

    void Update()
    {
        if (_hqdRoot == null) return;

        ReadVapeInput();
        if (_isHolding && (Time.time - _holdStartTime) >= maxHoldTime)
            ReleaseVape();

        UpdateAnimation();
    }

    void ReadVapeInput()
    {
        if (_vapeAction == null || _inputBlocked) return;

        if (_vapeAction.WasPressedThisFrame())
        {
            if (!_isHolding && !_isAnimating)
                StartVape();
        }
        else if (_vapeAction.WasReleasedThisFrame())
        {
            if (_isHolding)
                ReleaseVape();
        }
    }

    void StartVape()
    {
        _isHolding = true;
        _holdStartTime = Time.time;
        _animTime = 0f;
        _animDuration = showDuration;
        _animShowing = true;
        _isAnimating = true;

        // Показываем потик только когда курим — сразу у рта (анимация доведёт до visible)
        _hqdRoot.gameObject.SetActive(true);
        _hqdRoot.localPosition = hiddenLocalPosition;
        _hqdRoot.localRotation = Quaternion.Euler(hiddenLocalEuler);
        _hqdRoot.localScale = Vector3.one * hiddenScale;

        if (evaporatorSound != null && _evaporatorSource != null)
        {
            _evaporatorSource.clip = evaporatorSound;
            _evaporatorSource.loop = true;
            if (evaporatorFadeInDuration <= 0f)
            {
                _evaporatorSource.volume = evaporatorVolume;
                _evaporatorSource.Play();
            }
            else
            {
                _evaporatorSource.volume = 0f;
                _evaporatorSource.Play();
                StartCoroutine(FadeEvaporatorVolume(evaporatorVolume, evaporatorFadeInDuration));
            }
        }
    }

    void ReleaseVape()
    {
        if (!_isHolding && !_isAnimating) return;

        float holdDuration = Mathf.Clamp(Time.time - _holdStartTime, 0f, maxHoldTime);
        _isHolding = false;

        if (evaporatorSound != null && _evaporatorSource != null && _evaporatorSource.isPlaying)
        {
            if (evaporatorFadeOutDuration <= 0f)
                _evaporatorSource.Stop();
            else
                StartCoroutine(FadeOutAndStopEvaporator());
        }

        if (exhaleSound != null && _exhaleSource != null)
            _exhaleSource.PlayOneShot(exhaleSound);

        _animTime = 0f;
        _animDuration = hideDuration;
        _animShowing = false;
        _isAnimating = true;

        StartCoroutine(PlayVaporAfterHide(holdDuration));
    }

    IEnumerator FadeEvaporatorVolume(float targetVolume, float duration)
    {
        float start = _evaporatorSource != null ? _evaporatorSource.volume : 0f;
        float t = 0f;
        while (t < duration && _evaporatorSource != null)
        {
            t += Time.deltaTime;
            _evaporatorSource.volume = Mathf.Lerp(start, targetVolume, t / duration);
            yield return null;
        }
        if (_evaporatorSource != null)
            _evaporatorSource.volume = targetVolume;
    }

    IEnumerator FadeOutAndStopEvaporator()
    {
        float startVolume = _evaporatorSource != null ? _evaporatorSource.volume : 1f;
        float t = 0f;
        while (t < evaporatorFadeOutDuration && _evaporatorSource != null)
        {
            t += Time.deltaTime;
            _evaporatorSource.volume = Mathf.Lerp(startVolume, 0f, t / evaporatorFadeOutDuration);
            yield return null;
        }
        if (_evaporatorSource != null)
        {
            _evaporatorSource.volume = 0f;
            _evaporatorSource.Stop();
        }
    }

    IEnumerator PlayVaporAfterHide(float holdDuration)
    {
        yield return new WaitForSeconds(hideDuration);
        bool doRingTrick = ringTrickChance > 0f && Random.value < ringTrickChance;
        if (doRingTrick)
            PlayVaporRing(holdDuration);
        else
            PlayVapor(holdDuration);
    }

    void UpdateAnimation()
    {
        if (!_isAnimating || _hqdRoot == null) return;

        _animTime += Time.deltaTime;
        float t = Mathf.Clamp01(_animTime / _animDuration);
        float curveT = _animShowing ? showCurve.Evaluate(t) : hideCurve.Evaluate(t);

        if (_animShowing)
        {
            _hqdRoot.localPosition = Vector3.Lerp(hiddenLocalPosition, visibleLocalPosition, curveT);
            _hqdRoot.localRotation = Quaternion.Slerp(Quaternion.Euler(hiddenLocalEuler), Quaternion.Euler(visibleLocalEuler), curveT);
            _hqdRoot.localScale = Vector3.one * Mathf.Lerp(hiddenScale, visibleScale, curveT);
        }
        else
        {
            _hqdRoot.localPosition = Vector3.Lerp(visibleLocalPosition, hiddenLocalPosition, curveT);
            _hqdRoot.localRotation = Quaternion.Slerp(Quaternion.Euler(visibleLocalEuler), Quaternion.Euler(hiddenLocalEuler), curveT);
            _hqdRoot.localScale = Vector3.one * Mathf.Lerp(visibleScale, hiddenScale, curveT);
        }

        if (t >= 1f)
        {
            _isAnimating = false;
            // После окончания анимации «убрать» — полностью скрываем потик (не виден, пока снова не нажмёшь V)
            if (!_animShowing)
                _hqdRoot.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Выпустить пар из заданной точки рта (для NPC кальяна). Та же логика и вид, что у игрока.
    /// Родитель = null, чтобы не наследовать scale от стола (иначе огромные багованные партиклы).
    /// </summary>
    public static GameObject EmitVaporAtMouth(Transform mouthPoint)
    {
        var vape = Object.FindFirstObjectByType<PlayerHQDVape>();
        return vape != null ? vape.EmitVaporAtMouthInstance(mouthPoint) : null;
    }

    GameObject EmitVaporAtMouthInstance(Transform mouthPoint)
    {
        if (_vaporInstance == null || mouthPoint == null) return null;

        float normalizedHold = 0.5f;
        float countMul = 1f + Random.Range(-vaporCountRandomness, vaporCountRandomness);
        int count = Mathf.RoundToInt(Mathf.Lerp(vaporMinParticles, vaporMaxParticles, normalizedHold) * countMul);
        if (count <= 0) count = 800;

        Vector3 worldMouth = mouthPoint.position;
        Quaternion forwardRotation = mouthPoint.rotation;

        // Без родителя — иначе scale стола даёт огромные партиклы
        GameObject clone = Instantiate(_vaporInstance.gameObject, null);
        clone.SetActive(false);
        ParticleSystem ps = clone.GetComponent<ParticleSystem>();
        if (ps == null) { Object.Destroy(clone); return null; }
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        clone.transform.position = worldMouth;
        clone.transform.rotation = forwardRotation;
        clone.transform.localScale = Vector3.one;

        ComputerSpot.TryExtinguishAny(worldMouth, forwardRotation * Vector3.forward);

        ApplyVaporForwardExhale(ps);
        ApplyVaporExhaleDirection(ps, mouthPoint.forward);
        ApplyVaporSoftMaterial(ps);
        ApplyVaporColor(ps, vaporColor);
        ParticlesCollisionSetup.SetupCollisionAndSplash(ps, vaporColor, addSplash: false, vaporIgnoreCollisionLayers, enableCollision: false);

        float sizeBase = Mathf.Lerp(vaporMinStartSize, vaporMaxStartSize, normalizedHold);
        float sizeMul = 1f + Random.Range(-vaporSizeRandomness, vaporSizeRandomness);
        float sizeMin = sizeBase * sizeMul * 0.9f;
        float sizeMax = sizeBase * sizeMul * 1.6f;

        float exhaleDuration = Mathf.Min(vaporExhaleSpreadDuration, vaporEmitDuration);
        float rate = count / Mathf.Max(0.01f, exhaleDuration);

        var main = ps.main;
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.duration = exhaleDuration;
        main.loop = false;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = rate;
        emission.SetBursts(new ParticleSystem.Burst[0]);

        clone.SetActive(true);
        ps.Play();
        Object.Destroy(clone, 5.5f);
        return clone;
    }

    void PlayVapor(float holdDuration)
    {
        if (_vaporInstance == null) return;

        float normalizedHold = Mathf.Clamp01(holdDuration / maxHoldTime);
        float countMul = 1f + Random.Range(-vaporCountRandomness, vaporCountRandomness);
        int count = Mathf.RoundToInt(Mathf.Lerp(vaporMinParticles, vaporMaxParticles, normalizedHold) * countMul);
        if (count <= 0) return;

        Vector3 worldMouth = playerCamera.TransformPoint(visibleLocalPosition + vaporLocalOffset);
        Quaternion forwardRotation = playerCamera.rotation;

        // Обычное облако — с родителем, следует за игроком
        Transform vaporParent = vaporWorldParent != null ? vaporWorldParent : playerCamera;
        GameObject clone = Instantiate(_vaporInstance.gameObject, vaporParent);
        clone.SetActive(false);
        ParticleSystem ps = clone.GetComponent<ParticleSystem>();
        if (ps == null) return;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        clone.transform.position = worldMouth;
        clone.transform.rotation = forwardRotation;

        ComputerSpot.TryExtinguishAny(worldMouth, forwardRotation * Vector3.forward);

        ApplyVaporForwardExhale(ps);
        ApplyVaporSoftMaterial(ps);
        ApplyVaporColor(ps, vaporColor);
        if (!ps.collision.enabled)
            ParticlesCollisionSetup.SetupCollisionAndSplash(ps, vaporColor, addSplash: false, vaporIgnoreCollisionLayers);

        float sizeBase = Mathf.Lerp(vaporMinStartSize, vaporMaxStartSize, normalizedHold);
        float sizeMul = 1f + Random.Range(-vaporSizeRandomness, vaporSizeRandomness);
        float sizeMin = sizeBase * sizeMul * 0.9f;
        float sizeMax = sizeBase * sizeMul * 1.6f;

        float exhaleDuration = Mathf.Min(vaporExhaleSpreadDuration, vaporEmitDuration);
        float rate = count / Mathf.Max(0.01f, exhaleDuration);

        var main = ps.main;
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.duration = exhaleDuration;
        main.loop = false;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = rate;
        emission.SetBursts(new ParticleSystem.Burst[0]);

        clone.SetActive(true);
        ps.Play();
        Destroy(clone, 5.5f);
    }

    void PlayVaporRing(float holdDuration)
    {
        Vector3 worldMouth = playerCamera.TransformPoint(visibleLocalPosition + vaporLocalOffset);
        Quaternion forwardRotation = playerCamera.rotation;
        Vector3 worldForward = forwardRotation * Vector3.forward;

        ComputerSpot.TryExtinguishAny(worldMouth, worldForward);

        // Объект в мире без родителя — кольцо летит вперёд по взгляду и расширяется
        GameObject ringGo = new GameObject("VaporRing");
        ringGo.SetActive(false);
        ringGo.transform.SetParent(null);
        ringGo.transform.position = worldMouth;
        ringGo.transform.rotation = forwardRotation;

        ParticleSystem ps = ringGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.duration = 1f;
        main.loop = false;
        main.startLifetime = ringLifetime;
        main.startSpeed = 0f;           // скорость задаём вручную у каждой частицы
        main.startSize = ringParticleSize;
        main.startColor = vaporColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = ringParticleCount;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.enabled = false;

        var shape = ps.shape;
        shape.enabled = false;

        // Замедление вперёд по времени (имитация сопротивления воздуха) — все в Curve mode для Unity
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        var flatZero = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f));
        var zDragCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(1f, -ringForwardSpeed * ringDrag));
        vel.x = new ParticleSystem.MinMaxCurve(1f, flatZero);
        vel.y = new ParticleSystem.MinMaxCurve(1f, flatZero);
        vel.z = new ParticleSystem.MinMaxCurve(1f, zDragCurve);

        // Размер: появление → лёгкий рост → плавное исчезновение
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.3f),
            new Keyframe(0.08f, 1f),
            new Keyframe(0.5f, 1.1f),
            new Keyframe(0.85f, 0.5f),
            new Keyframe(1f, 0f)));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Color c = vaporColor;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
            new[] {
                new GradientAlphaKey(c.a * 0.95f, 0f),
                new GradientAlphaKey(c.a * 0.7f, 0.25f),
                new GradientAlphaKey(c.a * 0.4f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = grad;

        // Лёгкий шум, чтобы кольцо не выглядело как жёсткий диск
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.06f;
        noise.frequency = 0.4f;
        noise.scrollSpeed = 0.08f;
        noise.damping = true;
        noise.octaveCount = 2;
        noise.quality = ParticleSystemNoiseQuality.Low;

        ParticlesCollisionSetup.SetupCollisionAndSplash(ps, vaporColor, addSplash: false, vaporIgnoreCollisionLayers);

        var renderer = ringGo.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
            VaporParticleSetup.SetupVaporRenderer(renderer, vaporColor);

        ringGo.SetActive(true);

        // Расставляем частицы по окружности (ограничено для отсутствия лага при появлении)
        int count = Mathf.Clamp(ringParticleCount, 24, 100);
        var particles = new ParticleSystem.Particle[count];
        float invCount = 1f / count;

        for (int i = 0; i < count; i++)
        {
            float t = i * invCount * Mathf.PI * 2f;
            float angle = t + Random.Range(-0.015f, 0.015f);
            float r = ringStartRadius + Random.Range(-ringThickness, ringThickness);
            r = Mathf.Max(0.001f, r);

            // Локальная позиция на кольце: круг в плоскости XY (вперёд — Z)
            Vector3 localPos = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
            Vector3 worldPos = worldMouth + forwardRotation * localPos;

            // Направление «наружу» от центра кольца в плоскости кольца
            Vector3 localOutward = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 worldOutward = forwardRotation * localOutward;

            // Скорость: вперёд по взгляду + расширение кольца наружу
            Vector3 velocity = worldForward * ringForwardSpeed + worldOutward * ringExpandSpeed;

            var p = particles[i];
            p.position = worldPos;
            p.velocity = velocity;
            p.startLifetime = ringLifetime;
            p.remainingLifetime = ringLifetime;
            p.startSize = ringParticleSize * Random.Range(0.85f, 1.15f);
            p.startColor = vaporColor;
            p.rotation = Random.Range(0f, Mathf.PI * 2f);
            p.randomSeed = (uint)(i + 1);
            particles[i] = p;
        }

        ps.SetParticles(particles, count);
        ps.Play();
        Destroy(ringGo, ringLifetime + 1.5f);
    }

    void ApplyVaporForwardExhale(ParticleSystem ps)
    {
        if (ps == null) return;
        var main = ps.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.04f;
        shape.rotation = Vector3.zero;
        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(1.2f, 2f);
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
    }

    /// <summary> Для NPC: задать направление выдоха в мировых координатах — пар уверенно выдувается вперёд. </summary>
    void ApplyVaporExhaleDirection(ParticleSystem ps, Vector3 blowDirection)
    {
        if (ps == null) return;
        Vector3 dir = blowDirection.sqrMagnitude >= 0.01f ? blowDirection.normalized : Vector3.forward;
        float speed = 2.8f;
        float spread = 0.25f;
        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.World;
        vol.x = new ParticleSystem.MinMaxCurve(dir.x * speed - spread, dir.x * speed + spread);
        vol.y = new ParticleSystem.MinMaxCurve(dir.y * speed - spread, dir.y * speed + spread);
        vol.z = new ParticleSystem.MinMaxCurve(dir.z * speed - spread, dir.z * speed + spread);
    }

    void ApplyVaporSoftMaterial(ParticleSystem ps)
    {
        if (ps == null) return;
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer == null) return;
        VaporParticleSetup.SetupVaporRenderer(renderer, Color.white);
    }
}
