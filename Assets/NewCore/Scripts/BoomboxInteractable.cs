using UnityEngine;

/// <summary>
/// Магнитофон: массив треков, громкость, следующий трек, выключение.
/// Звук 3D — слышен на расстоянии. Клавиши настраиваются в инспекторе.
/// Текущий трек циклится с плавным затуханием в конце и появлением в начале.
/// Вешать на объект магнитолы (на нём или на дочернем должен быть Collider для луча).
/// </summary>
[RequireComponent(typeof(AudioSource))]
[AddComponentMenu("NewCore/Boombox Interactable")]
public class BoomboxInteractable : MonoBehaviour
{
    [Header("Треки")]
    [Tooltip("Список треков. При включении играет первый, далее переключение по кнопке.")]
    [SerializeField] AudioClip[] tracks = new AudioClip[0];

    [Header("Громкость")]
    [Tooltip("Начальная громкость при включении (0.5 = 50% от макс.). Остаётся такой, пока не нажмёте «Громче».")]
    [SerializeField, Range(0f, 1f)] float initialVolume = 0.5f;
    [SerializeField, Range(0.05f, 0.5f)] float volumeStep = 0.15f;
    [SerializeField, Range(0f, 1f)] float minVolume = 0f;
    [SerializeField, Range(0f, 1f)] float maxVolume = 1f;

    [Header("Звук в пространстве (3D)")]
    [Tooltip("Включить 3D-звук: громче вблизи, тише вдали. 0 = везде одинаково, 1 = полная 3D.")]
    [SerializeField, Range(0f, 1f)] float spatialBlend = 1f;
    [Tooltip("Дистанция, на которой громкость максимальна (метры).")]
    [SerializeField, Min(0.1f)] float minDistance = 2f;
    [Tooltip("Дистанция, на которой звук затухает до тишины (метры).")]
    [SerializeField, Min(1f)] float maxDistance = 25f;

    [Header("Клавиши управления")]
    [SerializeField] KeyCode keyTurnOnOrNext = KeyCode.E;
    [SerializeField] KeyCode keyTurnOff = KeyCode.R;
    [SerializeField] KeyCode keyVolumeUp = KeyCode.F;
    [SerializeField] KeyCode keyVolumeDown = KeyCode.G;

    [Header("Цикл трека")]
    [Tooltip("Время плавного затухания в конце и появления в начале (сек).")]
    [SerializeField, Min(0.2f)] float loopFadeTime = 1.5f;
    [Tooltip("Время плавного появления звука при включении магнитолы (сек). Чем больше — тем мягче старт.")]
    [SerializeField, Min(0.5f)] float turnOnFadeTime = 4f;

    AudioSource _source;
    float _targetVolume;
    bool _isOn;
    int _currentTrackIndex;

    enum LoopPhase { None, FadingOut, FadingIn, FadingInOnStart }
    LoopPhase _loopPhase;
    float _loopPhaseTime;

    void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
        _source.spatialBlend = spatialBlend;
        _source.minDistance = minDistance;
        _source.maxDistance = maxDistance;
        _targetVolume = Mathf.Clamp(initialVolume, minVolume, maxVolume);
        _source.volume = 0f;
    }

    /// <summary> Текст подсказки: при выключенной магнитоле — «Включить», при включённой — все действия со стрелками. </summary>
    public string GetHintText()
    {
        string kOn = KeyCodeToShortString(keyTurnOnOrNext);
        string kOff = KeyCodeToShortString(keyTurnOff);
        string kUp = KeyCodeToShortString(keyVolumeUp);
        string kDown = KeyCodeToShortString(keyVolumeDown);

        if (!_isOn)
            return $"  {kOn}  →  Включить магнитолу  ";

        return $"  {kOn}  →  След. трек   │   {kOff}  →  Выкл   │   {kUp}  →  Громче   │   {kDown}  →  Тише  ";
    }

    static string KeyCodeToShortString(KeyCode k)
    {
        if (k == KeyCode.None) return "?";
        string s = k.ToString();
        if (s.StartsWith("Alpha")) return s.Replace("Alpha", "");
        if (s.StartsWith("Keypad")) return "Num" + s.Replace("Keypad", "");
        return s;
    }

    public KeyCode KeyTurnOnOrNext => keyTurnOnOrNext;
    public KeyCode KeyTurnOff => keyTurnOff;
    public KeyCode KeyVolumeUp => keyVolumeUp;
    public KeyCode KeyVolumeDown => keyVolumeDown;

    void Update()
    {
        if (!_isOn || tracks == null || tracks.Length == 0) return;

        if (_loopPhase == LoopPhase.FadingOut)
        {
            _loopPhaseTime += Time.deltaTime;
            float t = Mathf.Clamp01(_loopPhaseTime / loopFadeTime);
            _source.volume = Mathf.Lerp(_targetVolume, 0f, t);
            if (t >= 1f)
            {
                RestartCurrentTrack();
                _loopPhase = LoopPhase.FadingIn;
                _loopPhaseTime = 0f;
            }
            return;
        }

        if (_loopPhase == LoopPhase.FadingIn)
        {
            _loopPhaseTime += Time.deltaTime;
            float t = Mathf.Clamp01(_loopPhaseTime / loopFadeTime);
            _source.volume = Mathf.Lerp(0f, _targetVolume, t);
            if (t >= 1f)
                _loopPhase = LoopPhase.None;
            return;
        }

        if (_loopPhase == LoopPhase.FadingInOnStart)
        {
            _loopPhaseTime += Time.deltaTime;
            float t = Mathf.Clamp01(_loopPhaseTime / turnOnFadeTime);
            float smoothT = Mathf.SmoothStep(0f, 1f, t); // ещё плавнее: медленный старт и мягкий выход
            _source.volume = Mathf.Lerp(0f, _targetVolume, smoothT);
            if (t >= 1f)
                _loopPhase = LoopPhase.None;
            return;
        }

        if (!_source.isPlaying) return;

        AudioClip clip = _source.clip;
        if (clip == null) return;

        float timeLeft = clip.length - _source.time;
        if (timeLeft <= loopFadeTime && timeLeft > 0f)
        {
            _loopPhase = LoopPhase.FadingOut;
            _loopPhaseTime = 0f;
        }
    }

    void RestartCurrentTrack()
    {
        if (tracks == null || tracks.Length == 0) return;
        _currentTrackIndex = (_currentTrackIndex % tracks.Length + tracks.Length) % tracks.Length;
        AudioClip c = tracks[_currentTrackIndex];
        if (c == null) return;
        _source.Stop();
        _source.clip = c;
        _source.volume = 0f;
        _source.Play();
    }

    /// <summary> Включить следующий трек (или начать воспроизведение, если выключено). </summary>
    public void NextOrStart()
    {
        if (tracks == null || tracks.Length == 0) return;

        if (!_isOn)
        {
            TurnOn();
            return;
        }

        _loopPhase = LoopPhase.None;
        _currentTrackIndex = (_currentTrackIndex + 1) % tracks.Length;
        AudioClip c = tracks[_currentTrackIndex];
        if (c == null) return;
        _source.Stop();
        _source.clip = c;
        _source.volume = _targetVolume;
        _source.Play();
    }

    /// <summary> Включить магнитолу (плавное появление звука, первый трек). </summary>
    public void TurnOn()
    {
        if (tracks == null || tracks.Length == 0) return;
        _isOn = true;
        _currentTrackIndex = 0;
        AudioClip c = tracks[0];
        if (c == null) return;
        _source.Stop();
        _source.clip = c;
        _source.volume = 0f;
        _source.Play();
        _loopPhase = LoopPhase.FadingInOnStart;
        _loopPhaseTime = 0f;
    }

    /// <summary> Выключить магнитолу. </summary>
    public void TurnOff()
    {
        _isOn = false;
        _loopPhase = LoopPhase.None;
        _source.Stop();
        _source.clip = null;
    }

    /// <summary> Громче. </summary>
    public void VolumeUp()
    {
        _targetVolume = Mathf.Clamp(_targetVolume + volumeStep, minVolume, maxVolume);
        if (_loopPhase == LoopPhase.None)
            _source.volume = _targetVolume;
    }

    /// <summary> Тише. </summary>
    public void VolumeDown()
    {
        _targetVolume = Mathf.Clamp(_targetVolume - volumeStep, minVolume, maxVolume);
        if (_loopPhase == LoopPhase.None)
            _source.volume = _targetVolume;
    }

    public bool IsOn => _isOn;
    public int CurrentTrackIndex => _currentTrackIndex;
    public int TrackCount => tracks != null ? tracks.Length : 0;
}
