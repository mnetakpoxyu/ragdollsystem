using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Луч из центра экрана: при наведении — подсказка «Взаимодействовать [E]» и подсветка объекта.
/// Вешать на Main Camera. Нужен Input Action Asset (InputSystem_Actions) в поле ввода.
/// </summary>
[RequireComponent(typeof(Camera))]
[AddComponentMenu("NewCore/Player Interact")]
public class PlayerInteract : MonoBehaviour
{
    [Header("Луч")]
    [Tooltip("Максимальная дистанция взаимодействия (в упор).")]
    [SerializeField] float maxDistance = 2.5f;
    [SerializeField] LayerMask interactLayers = ~0;

    [Header("Ввод")]
    [Tooltip("InputSystem_Actions или другой asset с картой Player и действием Interact (E).")]
    [SerializeField] InputActionAsset inputActionAsset;

    [Header("Подсказка (опционально)")]
    [Tooltip("Текст подсказки в UI. Если не задан — создаётся автоматически.")]
    [SerializeField] Text hintText;
    [Tooltip("Шрифт подсказки. Если задан HintSettings в Resources — используется он; иначе этот шрифт.")]
    [SerializeField] Font hintFont;

    [Header("Активная задача")]
    [Tooltip("Текст активной задачи (например «Клиент хочет поиграть…»). Если не задан — создаётся автоматически.")]
    [SerializeField] Text taskText;

    [Header("Запись голоса")]
    [Tooltip("UI для записи голоса клиента (создаётся автоматически если не задан). Используется только VoiceRecorder — окно не показывается.")]
    [SerializeField] VoiceRecordingUI voiceRecordingUI;
    [Tooltip("Максимальная длительность записи голоса для клиента (сек).")]
    [SerializeField] float maxVoiceRecordSeconds = 5f;
    [Tooltip("Клавиша прослушать запись перед подтверждением.")]
    [SerializeField] KeyCode keyPreviewVoice = KeyCode.Q;

    Camera _cam;
    InputAction _interactAction;
    InteractableDoor _currentDoor;
    ClientNPC _currentClient;
    ComputerSpot _currentSpot;
    BalanceDisplayTarget _currentBalanceTarget;
    ClientNPC _clientWaitingToSeat;
    SurveillanceMonitor _currentMonitor;
    BoomboxInteractable _currentBoombox;
    VoiceRecorder _voiceRecorder;
    AudioSource _previewAudioSource;

    enum VoiceRecordState { None, Idle, Recording, HasRecording }
    VoiceRecordState _voiceRecordState;
    float _recordStartTime;
    AudioClip _pendingRecordedClip;

    // Ручное отслеживание нажатий для магнитолы (isPressed), чтобы R/F/G не терялись после E
    Dictionary<KeyCode, bool> _keyPrevPressed = new Dictionary<KeyCode, bool>();

    const string HintMessage = "Взаимодействовать  [E]";
    const string HintFree = "Свободен";
    const string HintOccupied = "Занят";
    const string HintSeatClient = "Посадить клиента  [E]";

    void Start()
    {
        _cam = GetComponent<Camera>();
        if (inputActionAsset != null)
        {
            var map = inputActionAsset.FindActionMap("Player");
            _interactAction = map?.FindAction("Interact");
            if (_interactAction != null)
                _interactAction.Enable();
        }

        // Единый шрифт для всех подсказок: из HintSettings в Resources или из поля в инспекторе
        HintSettings hs = Resources.Load<HintSettings>("HintSettings");
        if (hs != null && hs.defaultHintFont != null)
            hintFont = hs.defaultHintFont;

        if (hintText == null)
            CreateDefaultHint();
        if (hintText != null)
        {
            if (hintFont != null) hintText.font = hintFont;
            hintText.gameObject.SetActive(false);
        }

        if (taskText == null)
            CreateDefaultTask();
        if (taskText != null)
        {
            if (hintFont != null) taskText.font = hintFont;
            taskText.gameObject.SetActive(false);
        }

        // Создаём VoiceRecordingUI если не задан (нужен только VoiceRecorder на нём)
        if (voiceRecordingUI == null)
        {
            var voiceUIObj = new GameObject("VoiceRecordingUI");
            voiceRecordingUI = voiceUIObj.AddComponent<VoiceRecordingUI>();
        }
        if (voiceRecordingUI != null)
            _voiceRecorder = voiceRecordingUI.GetComponent<VoiceRecorder>();
        if (_voiceRecorder == null && voiceRecordingUI != null)
            _voiceRecorder = voiceRecordingUI.gameObject.AddComponent<VoiceRecorder>();
        if (_voiceRecorder != null)
        {
            _previewAudioSource = _voiceRecorder.GetComponent<AudioSource>();
            if (_previewAudioSource == null)
                _previewAudioSource = _voiceRecorder.gameObject.AddComponent<AudioSource>();
            _previewAudioSource.playOnAwake = false;
            _previewAudioSource.spatialBlend = 0f; // 2D для прослушивания в наушниках
        }
    }

    void CreateDefaultTask()
    {
        if (hintText == null) return;
        var canvas = hintText.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var textObj = new GameObject("TaskText");
        textObj.transform.SetParent(canvas.transform, false);
        taskText = textObj.AddComponent<Text>();
        taskText.text = "";
        taskText.font = hintFont != null ? hintFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        taskText.fontSize = 32;
        taskText.fontStyle = FontStyle.Bold;
        taskText.color = Color.white;
        taskText.alignment = TextAnchor.UpperCenter;
        taskText.raycastTarget = false;
        taskText.horizontalOverflow = HorizontalWrapMode.Overflow;
        taskText.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = textObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);
        var rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(900, 56);
        rect.anchoredPosition = new Vector2(0f, -50f);
    }

    void OnDestroy()
    {
        _interactAction?.Disable();
        if (_currentBalanceTarget != null)
        {
            _currentBalanceTarget.SetAimedAt(false);
            _currentBalanceTarget = null;
        }
    }

    void CreateDefaultHint()
    {
        var canvasObj = new GameObject("InteractHintCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100;
        canvasObj.AddComponent<GraphicRaycaster>();

        var textObj = new GameObject("HintText");
        textObj.transform.SetParent(canvasObj.transform, false);
        hintText = textObj.AddComponent<Text>();
        hintText.text = HintMessage;
        hintText.font = hintFont != null ? hintFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hintText.fontSize = 32;
        hintText.fontStyle = FontStyle.Bold;
        hintText.color = Color.white;
        hintText.alignment = TextAnchor.MiddleCenter;
        hintText.raycastTarget = false;
        hintText.horizontalOverflow = HorizontalWrapMode.Overflow;
        hintText.verticalOverflow = VerticalWrapMode.Overflow;

        var outline = textObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);

        var rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.08f);
        rect.anchorMax = new Vector2(0.5f, 0.08f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(520, 56);
        rect.anchoredPosition = Vector2.zero;
    }

    void Update()
    {
        if (voiceRecordingUI != null && voiceRecordingUI.IsUIActive)
            return;

        InteractableDoor hitDoor = null;
        ClientNPC hitClient = null;
        ComputerSpot hitSpot = null;
        BalanceDisplayTarget hitBalanceTarget = null;
        BoomboxInteractable hitBoombox = null;
        _currentMonitor = null;

        Ray ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactLayers))
        {
            hitDoor = hit.collider.GetComponentInParent<InteractableDoor>();
            hitClient = hit.collider.GetComponentInParent<ClientNPC>();
            hitSpot = hit.collider.GetComponentInParent<ComputerSpot>();
            var monitor = hit.collider.GetComponentInParent<SurveillanceMonitor>();
            hitBalanceTarget = hit.collider.GetComponentInParent<BalanceDisplayTarget>();
            hitBoombox = hit.collider.GetComponentInParent<BoomboxInteractable>();
            _currentMonitor = monitor;
        }

        _currentBoombox = hitBoombox;

        if (hitDoor != _currentDoor)
        {
            if (_currentDoor != null)
                _currentDoor.SetHighlight(false);
            _currentDoor = hitDoor;
            if (_currentDoor != null)
                _currentDoor.SetHighlight(true);
        }

        // Взаимодействовать с клиентом можно только когда он уже стоит у стойки (не на пути к ней и не к компьютеру)
        ClientNPC effectiveClient = (hitClient != null && hitClient.CurrentState == ClientNPC.State.WaitingAtCounter) ? hitClient : null;
        if (effectiveClient != _currentClient)
            _currentClient = effectiveClient;

        if (hitSpot != _currentSpot)
        {
            if (_currentSpot != null)
                _currentSpot.SetHighlight(false);
            _currentSpot = hitSpot;
            if (_currentSpot != null)
                _currentSpot.SetHighlight(true);
        }

        if (hitBalanceTarget != _currentBalanceTarget)
        {
            if (_currentBalanceTarget != null)
                _currentBalanceTarget.SetAimedAt(false);
            _currentBalanceTarget = hitBalanceTarget;
            if (_currentBalanceTarget != null)
                _currentBalanceTarget.SetAimedAt(true);
        }

        // Подсказка при записи голоса клиента (наведён на того же клиента)
        bool showingVoiceHint = _clientWaitingToSeat != null && _currentClient == _clientWaitingToSeat &&
            (_voiceRecordState == VoiceRecordState.Idle || _voiceRecordState == VoiceRecordState.Recording || _voiceRecordState == VoiceRecordState.HasRecording);

        bool showHint = _currentDoor != null || _currentClient != null || _currentSpot != null || _currentMonitor != null || _currentBoombox != null || showingVoiceHint;
        if (hintText != null)
        {
            hintText.gameObject.SetActive(showHint);
            if (showHint)
            {
                if (showingVoiceHint)
                {
                    if (_voiceRecordState == VoiceRecordState.Recording)
                    {
                        float elapsed = Mathf.Min(Time.time - _recordStartTime, maxVoiceRecordSeconds);
                        hintText.text = string.Format("Запись: {0:F1} / {1:F0} сек. Отпустите [R]", elapsed, maxVoiceRecordSeconds);
                    }
                    else if (_voiceRecordState == VoiceRecordState.HasRecording)
                        hintText.text = string.Format("  [{0}] — прослушать   │   [F] — подтвердить   │   [G] — перезаписать   │   [E] — без голоса  ", keyPreviewVoice);
                    else
                        hintText.text = string.Format("  Удерживайте [R] — запись (макс. {0:F0} сек). [E] — отправить без голоса  ", maxVoiceRecordSeconds);
                }
                else if (_currentBoombox != null)
                {
                    hintText.text = _currentBoombox.GetHintText();
                }
                else if (_currentSpot != null)
                {
                    if (!_currentSpot.IsOccupied && _clientWaitingToSeat != null && _clientWaitingToSeat.AssignedSpot == _currentSpot)
                        hintText.text = HintSeatClient;
                    else
                        hintText.text = _currentSpot.IsOccupied ? HintOccupied : HintFree;
                }
                else if (_currentMonitor != null)
                {
                    hintText.text = "Смотреть камеры  [E]";
                }
                else if (_currentClient != null && _currentClient.CurrentState == ClientNPC.State.WaitingAtCounter && !_currentClient.HasOrdered && ComputerSpot.GetFreeSpotsList().Count == 0)
                {
                    hintText.text = "Нет свободных мест";
                }
                else
                    hintText.text = HintMessage;
            }
        }

        // Задача сверху только когда смотришь на этого же клиента — увёл курсор = всё закрыто
        if (taskText != null)
        {
            bool showTask = _clientWaitingToSeat != null && _currentClient == _clientWaitingToSeat;
            taskText.gameObject.SetActive(showTask);
            if (showTask && _clientWaitingToSeat != null && _clientWaitingToSeat.HasOrdered)
            {
                float h = _clientWaitingToSeat.RequestedSessionHours;
                float pay = _clientWaitingToSeat.PaymentAmount;
                taskText.text = string.Format("Клиент: {0:F1} ч., {1:F0} ₽", h, pay);
            }
        }

        if (_currentBoombox != null)
        {
            if (WasKeyPressed(_currentBoombox.KeyTurnOnOrNext))
                _currentBoombox.NextOrStart();
            else if (WasKeyPressed(_currentBoombox.KeyTurnOff))
                _currentBoombox.TurnOff();
            else if (WasKeyPressed(_currentBoombox.KeyVolumeUp))
                _currentBoombox.VolumeUp();
            else if (WasKeyPressed(_currentBoombox.KeyVolumeDown))
                _currentBoombox.VolumeDown();
        }

        // Запись голоса клиента в реальном времени (без окна): наведение на NPC, удержание R, затем F/G/E
        if (_clientWaitingToSeat != null && _currentClient == _clientWaitingToSeat && _voiceRecorder != null)
        {
            if (_voiceRecordState == VoiceRecordState.Idle)
            {
                if (WasKeyPressed(KeyCode.R))
                {
                    _voiceRecorder.StartRecording();
                    _voiceRecordState = VoiceRecordState.Recording;
                    _recordStartTime = Time.time;
                }
            }
            else if (_voiceRecordState == VoiceRecordState.Recording)
            {
                bool rHeld = IsKeyHeld(KeyCode.R);
                bool timeout = (Time.time - _recordStartTime) >= maxVoiceRecordSeconds;
                if (!rHeld || timeout)
                {
                    AudioClip clip = _voiceRecorder.StopRecording();
                    _pendingRecordedClip = clip;
                    _voiceRecordState = VoiceRecordState.HasRecording;
                }
            }
            else if (_voiceRecordState == VoiceRecordState.HasRecording)
            {
                if (WasKeyPressed(keyPreviewVoice) && _pendingRecordedClip != null && _previewAudioSource != null)
                    _previewAudioSource.PlayOneShot(_pendingRecordedClip);
                else if (WasKeyPressed(KeyCode.F))
                {
                    if (_clientWaitingToSeat != null)
                    {
                        if (_pendingRecordedClip != null)
                            _clientWaitingToSeat.SetRecordedPhrase(_pendingRecordedClip);
                        SendClientToFreeSpot(_clientWaitingToSeat);
                    }
                    ClearVoiceState();
                }
                else if (WasKeyPressed(KeyCode.G))
                {
                    _pendingRecordedClip = null;
                    _voiceRecordState = VoiceRecordState.Idle;
                }
            }
            _keyPrevPressed[KeyCode.R] = IsKeyHeld(KeyCode.R);
        }

        if (_interactAction != null && _interactAction.triggered)
        {
            // E при наведении на принятого клиента — отправить без голоса
            if (_currentClient == _clientWaitingToSeat && _clientWaitingToSeat != null &&
                (_voiceRecordState == VoiceRecordState.Idle || _voiceRecordState == VoiceRecordState.HasRecording))
            {
                SendClientWithoutVoice();
                return;
            }

            if (_currentBoombox != null)
                ; // управление магнитолой — по клавишам из компонента магнитолы
            else if (_currentDoor != null)
                _currentDoor.Open();
            else if (_currentClient != null && _currentClient.CurrentState == ClientNPC.State.WaitingAtCounter && !_currentClient.HasOrdered)
            {
                ComputerSpot spot = ComputerSpot.GetFreeSpotWithPriceDistribution();
                if (spot == null) return;
                _currentClient.AssignSpot(spot);
                _currentClient.OnInteract();
                _clientWaitingToSeat = _currentClient;
                _voiceRecordState = VoiceRecordState.Idle;
            }
            else if (_currentMonitor != null)
            {
                _currentMonitor.BeginViewing();
            }
            else if (_currentSpot != null && !_currentSpot.IsOccupied && _clientWaitingToSeat != null && _clientWaitingToSeat.AssignedSpot == _currentSpot)
            {
                if (_currentSpot.SeatClient(_clientWaitingToSeat))
                {
                    _clientWaitingToSeat = null;
                    _voiceRecordState = VoiceRecordState.None;
                    _pendingRecordedClip = null;
                }
            }
        }
    }

    void ClearVoiceState()
    {
        _clientWaitingToSeat = null;
        _voiceRecordState = VoiceRecordState.None;
        _pendingRecordedClip = null;
    }

    void SendClientWithoutVoice()
    {
        SendClientToFreeSpot(_clientWaitingToSeat);
        ClearVoiceState();
    }

    /// <summary>
    /// Отправить клиента к назначенному компьютерному месту. После записи/отмены он сам идёт к столу.
    /// </summary>
    void SendClientToFreeSpot(ClientNPC client)
    {
        if (client == null) return;

        ComputerSpot spot = client.AssignedSpot;
        if (spot != null && !spot.IsOccupied && spot.SeatClient(client))
        {
            _clientWaitingToSeat = null;
            var spawner = FindFirstObjectByType<ClientNPCSpawner>();
            spawner?.OnClientSentToComputer();
            Debug.Log("PlayerInteract: Клиент отправлен к назначенному месту.");
        }
    }

    /// <summary> Нажатие по «переходу»: сейчас нажата и в прошлом кадре не была — не зависит от wasPressedThisFrame. </summary>
    bool WasKeyPressed(KeyCode key)
    {
        if (key == KeyCode.None) return false;
        bool now = IsKeyHeld(key);
        if (!_keyPrevPressed.TryGetValue(key, out bool prev))
            prev = false;
        _keyPrevPressed[key] = now;
        return now && !prev;
    }

    bool IsKeyHeld(KeyCode key)
    {
        if (key == KeyCode.None) return false;
        var kb = Keyboard.current;
        if (kb != null)
        {
            switch (key)
            {
                case KeyCode.R: return kb.rKey.isPressed;
                case KeyCode.F: return kb.fKey.isPressed;
                case KeyCode.G: return kb.gKey.isPressed;
                case KeyCode.E: return kb.eKey.isPressed;
                case KeyCode.Q: return kb.qKey.isPressed;
                default:
                    if (TryConvertKeyCode(key, out var k))
                    {
                        var c = kb[k];
                        return c != null && c.isPressed;
                    }
                    break;
            }
        }
        return Input.GetKey(key);
    }

    static bool TryConvertKeyCode(KeyCode keyCode, out Key key)
    {
        if (System.Enum.TryParse(keyCode.ToString(), true, out key))
            return true;
        switch (keyCode)
        {
            case KeyCode.Escape: key = Key.Escape; return true;
            case KeyCode.E: key = Key.E; return true;
            case KeyCode.R: key = Key.R; return true;
            case KeyCode.F: key = Key.F; return true;
            case KeyCode.G: key = Key.G; return true;
            case KeyCode.Q: key = Key.Q; return true;
            case KeyCode.Alpha0: key = Key.Digit0; return true;
            case KeyCode.Alpha1: key = Key.Digit1; return true;
            case KeyCode.Alpha2: key = Key.Digit2; return true;
            case KeyCode.Alpha3: key = Key.Digit3; return true;
            case KeyCode.Alpha4: key = Key.Digit4; return true;
            case KeyCode.Alpha5: key = Key.Digit5; return true;
            case KeyCode.Alpha6: key = Key.Digit6; return true;
            case KeyCode.Alpha7: key = Key.Digit7; return true;
            case KeyCode.Alpha8: key = Key.Digit8; return true;
            case KeyCode.Alpha9: key = Key.Digit9; return true;
            case KeyCode.Return: key = Key.Enter; return true;
            default: key = Key.None; return false;
        }
    }
}
