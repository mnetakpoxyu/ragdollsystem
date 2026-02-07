using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Проигрывает зацикленные видео на экране монитора, пока место занято и компьютер в порядке.
/// Видео идёт постоянно (даже если клиент встал: за едой, водой, в туалет, за кальяном).
/// Останавливается только когда: комп сломан, загорелся, клиента выгнали или сессия закончилась.
/// Добавь на объект с Renderer (экран монитора). ComputerSpot подтянется из родителя.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class MonitorScreenVideo : MonoBehaviour
{
    [Tooltip("Видеоклипы для воспроизведения (2–5 штук). При посадке клиента выбирается случайный и зацикливается.")]
    [SerializeField] VideoClip[] clips;

    [Tooltip("Разрешение текстуры, в которую рендерится видео. Влияет на качество и нагрузку.")]
    [SerializeField] Vector2Int renderTextureSize = new Vector2Int(512, 512);

    [Tooltip("Компьютерное место этого монитора. Пусто — ищется среди родительских объектов.")]
    [SerializeField] ComputerSpot computerSpot;

    [Tooltip("Включи, чтобы видео проигрывалось без звука.")]
    [SerializeField] bool disableSound = true;

    Renderer _renderer;
    VideoPlayer _videoPlayer;
    RenderTexture _renderTexture;
    static Texture2D _blackTexture;
    bool _wasPlaying;
    Material _screenMaterialInstance;

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();

        if (computerSpot == null)
            computerSpot = GetComponentInParent<ComputerSpot>();
        if (computerSpot == null)
            Debug.LogWarning("[MonitorScreenVideo] ComputerSpot не найден. Перетащи место в инспектор или помести объект в иерархию под ComputerSpot.", this);

        _renderTexture = new RenderTexture(renderTextureSize.x, renderTextureSize.y, 0, RenderTextureFormat.ARGB32);
        _renderTexture.Create();

        _videoPlayer = gameObject.AddComponent<VideoPlayer>();
        _videoPlayer.playOnAwake = false;
        _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _videoPlayer.targetTexture = _renderTexture;
        _videoPlayer.aspectRatio = VideoAspectRatio.Stretch; // растянуть на весь объект, без чёрных полос
        _videoPlayer.isLooping = true;
        _videoPlayer.source = VideoSource.VideoClip;
        if (disableSound)
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;

        if (_blackTexture == null)
        {
            _blackTexture = new Texture2D(1, 1);
            _blackTexture.SetPixel(0, 0, Color.black);
            _blackTexture.Apply();
        }

        if (_renderer != null && _renderer.material != null)
        {
            _screenMaterialInstance = _renderer.material;
            SetScreenTexture(_blackTexture);
        }
    }

    void SetScreenTexture(Texture tex)
    {
        if (_screenMaterialInstance == null) return;
        _screenMaterialInstance.mainTexture = tex;
        if (_screenMaterialInstance.HasProperty("_BaseMap"))
            _screenMaterialInstance.SetTexture("_BaseMap", tex);
        if (_screenMaterialInstance.HasProperty("_MainTex"))
            _screenMaterialInstance.SetTexture("_MainTex", tex);
        // Эмиссия — чтобы видео было видно при любом освещении (Lit-шейдеры)
        if (_screenMaterialInstance.HasProperty("_EmissionMap"))
        {
            _screenMaterialInstance.SetTexture("_EmissionMap", tex);
            if (_screenMaterialInstance.HasProperty("_EmissionColor"))
                _screenMaterialInstance.SetColor("_EmissionColor", Color.white);
            _screenMaterialInstance.EnableKeyword("_EMISSION");
        }
    }

    void Update()
    {
        if (computerSpot == null || clips == null || clips.Length == 0 || _renderer == null)
            return;

        // Играет, пока место занято и комп не сломан/не горит. Не зависит от того, сидит клиент или ушёл (еда, туалет, кальян).
        bool shouldPlay = computerSpot.IsOccupied && !computerSpot.IsBroken && !computerSpot.IsOnFire;

        if (shouldPlay)
        {
            if (!_videoPlayer.isPlaying)
            {
                _videoPlayer.clip = clips[Random.Range(0, clips.Length)];
                _videoPlayer.Play();
                SetScreenTexture(_renderTexture);
            }
            _wasPlaying = true;
        }
        else
        {
            if (_wasPlaying || _videoPlayer.isPlaying)
            {
                _videoPlayer.Stop();
                SetScreenTexture(_blackTexture);
            }
            _wasPlaying = false;
        }
    }

    void OnDestroy()
    {
        if (_renderTexture != null && _renderTexture.IsCreated())
            _renderTexture.Release();
    }
}
