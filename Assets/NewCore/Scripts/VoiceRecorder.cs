using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// Система записи голоса с микрофона. Записывает AudioClip и сохраняет его.
/// </summary>
public class VoiceRecorder : MonoBehaviour
{
    [Header("Настройки записи")]
    [Tooltip("Максимальная длительность записи в секундах.")]
    [SerializeField] int maxRecordingTime = 10;
    [Tooltip("Частота дискретизации записи.")]
    [SerializeField] int sampleRate = 44100;

    AudioClip _recordedClip;
    string _microphoneDevice;
    bool _isRecording;
    bool _isMicrophoneReady;
    int _recordingStartSample; // Позиция начала реальной записи

    public bool IsRecording => _isRecording;
    public bool IsMicrophoneReady => _isMicrophoneReady;
    public AudioClip RecordedClip => _recordedClip;

    // События для UI
    public event Action OnMicrophoneReady;

    void Start()
    {
        // Получаем первый доступный микрофон
        if (Microphone.devices.Length > 0)
        {
            _microphoneDevice = Microphone.devices[0];
            Debug.Log($"VoiceRecorder: Используется микрофон '{_microphoneDevice}'");
        }
        else
        {
            Debug.LogError("VoiceRecorder: Микрофон не найден!");
        }
    }

    /// <summary>
    /// Начать запись с микрофона. Запись реально начнётся после инициализации микрофона.
    /// </summary>
    public void StartRecording()
    {
        if (string.IsNullOrEmpty(_microphoneDevice))
        {
            Debug.LogError("VoiceRecorder: Микрофон не доступен!");
            return;
        }

        if (_isRecording)
        {
            Debug.LogWarning("VoiceRecorder: Запись уже идёт!");
            return;
        }

        _isMicrophoneReady = false;
        _recordedClip = Microphone.Start(_microphoneDevice, false, maxRecordingTime, sampleRate);
        _isRecording = true;

        // Ждём инициализации микрофона в корутине
        StartCoroutine(WaitForMicrophoneReady());
    }

    /// <summary>
    /// Ожидание готовности микрофона к записи.
    /// </summary>
    IEnumerator WaitForMicrophoneReady()
    {
        // Ждём пока микрофон начнёт записывать (позиция > 0)
        float timeout = 2f;
        float elapsed = 0f;

        while (Microphone.GetPosition(_microphoneDevice) <= 0 && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (elapsed >= timeout)
        {
            Debug.LogWarning("VoiceRecorder: Таймаут ожидания микрофона!");
        }

        // Запоминаем позицию начала реальной записи
        _recordingStartSample = Microphone.GetPosition(_microphoneDevice);
        _isMicrophoneReady = true;

        Debug.Log($"VoiceRecorder: Микрофон готов! Начальная позиция: {_recordingStartSample}");
        OnMicrophoneReady?.Invoke();
    }

    /// <summary>
    /// Остановить запись и вернуть AudioClip с записанным голосом.
    /// </summary>
    public AudioClip StopRecording()
    {
        if (!_isRecording)
        {
            Debug.LogWarning("VoiceRecorder: Запись не была начата!");
            return null;
        }

        int endPosition = Microphone.GetPosition(_microphoneDevice);
        Microphone.End(_microphoneDevice);
        _isRecording = false;
        _isMicrophoneReady = false;

        if (_recordedClip == null || endPosition == 0)
        {
            Debug.LogWarning("VoiceRecorder: Не удалось записать аудио.");
            return null;
        }

        // Вычисляем реальную длину записи (от момента готовности микрофона до остановки)
        int startSample = _recordingStartSample;
        int sampleCount = endPosition - startSample;

        if (sampleCount <= 0)
        {
            Debug.LogWarning("VoiceRecorder: Запись слишком короткая!");
            return null;
        }

        // Получаем ВСЕ данные из записи
        int totalSamples = _recordedClip.samples;
        int channels = _recordedClip.channels;
        float[] allSamples = new float[totalSamples * channels];
        _recordedClip.GetData(allSamples, 0);

        // Копируем только нужную часть (от startSample до endPosition)
        float[] trimmedSamples = new float[sampleCount * channels];
        Array.Copy(allSamples, startSample * channels, trimmedSamples, 0, sampleCount * channels);

        // Создаём новый AudioClip с правильной длиной
        AudioClip trimmedClip = AudioClip.Create("RecordedVoice", sampleCount, channels, _recordedClip.frequency, false);
        trimmedClip.SetData(trimmedSamples, 0);

        _recordedClip = trimmedClip;
        Debug.Log($"VoiceRecorder: Запись остановлена. Длительность: {_recordedClip.length:F2} сек. (сэмплы: {startSample} -> {endPosition})");

        return _recordedClip;
    }

    /// <summary>
    /// Отменить текущую запись.
    /// </summary>
    public void CancelRecording()
    {
        if (_isRecording)
        {
            Microphone.End(_microphoneDevice);
            _isRecording = false;
            _recordedClip = null;
            Debug.Log("VoiceRecorder: Запись отменена.");
        }
    }

    void OnDestroy()
    {
        if (_isRecording)
        {
            Microphone.End(_microphoneDevice);
        }
    }
}
