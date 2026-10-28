using System;
using UnityEngine;

namespace IVRAR.CybersecurityVR
{
    /// <summary>
    /// Spatial audio feedback manager for the VR Phishing Simulator.
    /// Provides audio feedback for tactile button clicks, threat warning alarms,
    /// safe email confirmation chimes, and ambient cyber-defense SOC room hum.
    /// </summary>
    public class PhishingAudioManager : MonoBehaviour
    {
        public static PhishingAudioManager Instance { get; private set; }

        [Header("Audio Clips")]
        [SerializeField] private AudioClip buttonClickClip;
        [SerializeField] private AudioClip threatAlarmClip;
        [SerializeField] private AudioClip safeChimeClip;
        [SerializeField] private AudioClip ambientHumClip;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource alarmSource;
        [SerializeField] private AudioSource chimeSource;
        [SerializeField] private AudioSource ambientSource;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;

            InitializeAudioSystem();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void InitializeAudioSystem()
        {
            LoadAudioClips();
            ConfigureAudioSources();
            StartAmbientHum();
        }

        private void Start()
        {
            InitializeAudioSystem();
        }

        private void Update()
        {
            if (ambientSource == null || !ambientSource.isPlaying)
            {
                ConfigureAudioSources();
                StartAmbientHum();
            }
        }

        private void LoadAudioClips()
        {
            AudioListener.volume = 1.0f;
            AudioListener.pause = false;

            // Load fresh high-gain audio clips from Resources/Audio so serialized old references never override updated clips
            AudioClip resClick = Resources.Load<AudioClip>("Audio/button_click");
            if (resClick != null) buttonClickClip = resClick;
            AudioClip resAlarm = Resources.Load<AudioClip>("Audio/threat_alarm");
            if (resAlarm != null) threatAlarmClip = resAlarm;
            AudioClip resChime = Resources.Load<AudioClip>("Audio/safe_chime");
            if (resChime != null) safeChimeClip = resChime;
            AudioClip resAmbient = Resources.Load<AudioClip>("Audio/ambient_office_hum");
            if (resAmbient != null) ambientHumClip = resAmbient;

            // Ensure audio data is loaded into memory even if preloadAudioData was false in .meta
            if (buttonClickClip != null && buttonClickClip.loadState != AudioDataLoadState.Loaded)
                buttonClickClip.LoadAudioData();
            if (threatAlarmClip != null && threatAlarmClip.loadState != AudioDataLoadState.Loaded)
                threatAlarmClip.LoadAudioData();
            if (safeChimeClip != null && safeChimeClip.loadState != AudioDataLoadState.Loaded)
                safeChimeClip.LoadAudioData();
            if (ambientHumClip != null && ambientHumClip.loadState != AudioDataLoadState.Loaded)
                ambientHumClip.LoadAudioData();

            // Programmatic procedural synthesis fallback if resource files are missing
            if (buttonClickClip == null)
                buttonClickClip = GenerateProceduralClickClip();
            if (threatAlarmClip == null)
                threatAlarmClip = GenerateProceduralAlarmClip();
            if (safeChimeClip == null)
                safeChimeClip = GenerateProceduralChimeClip();
            if (ambientHumClip == null)
                ambientHumClip = GenerateProceduralAmbientClip();
        }

        private void ConfigureAudioSources()
        {
            EnsureAudioListener();

            if (sfxSource == null)
                sfxSource = CreateAudioSource("SFX AudioSource", new Vector3(0f, 0.82f, 0.90f), false, 1.0f);
            if (alarmSource == null)
                alarmSource = CreateAudioSource("Alarm AudioSource", new Vector3(0f, 1.34f, 1.15f), false, 1.0f);
            if (chimeSource == null)
                chimeSource = CreateAudioSource("Chime AudioSource", new Vector3(0f, 1.34f, 1.15f), false, 1.0f);
            if (ambientSource == null)
                ambientSource = CreateAudioSource("Ambient AudioSource", new Vector3(0f, 1.5f, 1.5f), true, 0.55f);
            else
                ambientSource.volume = 0.55f;
        }

        private void EnsureAudioListener()
        {
            GameObject head = GameObject.Find("CenterEyeAnchor");
            if (head != null)
            {
                AudioListener listener = head.GetComponent<AudioListener>();
                if (listener == null)
                    listener = head.AddComponent<AudioListener>();
                listener.enabled = true;
            }
        }

        private AudioSource CreateAudioSource(string sourceName, Vector3 localPosition, bool loop, float volume)
        {
            Transform existing = transform.Find(sourceName);
            GameObject obj = existing != null ? existing.gameObject : new GameObject(sourceName);
            if (existing == null)
                obj.transform.SetParent(transform, false);

            obj.transform.localPosition = localPosition;
            AudioSource source = obj.GetComponent<AudioSource>();
            if (source == null)
                source = obj.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = loop;
            source.volume = volume;
            source.spatialBlend = 0.0f; // 2D non-spatialized output guarantees crystal-clear, full-volume playback in Meta XR Simulator and PC speakers
            source.spatialize = false;
            source.ignoreListenerPause = true;
            source.minDistance = 1.0f;
            source.maxDistance = 20.0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            return source;
        }

        public void PlayButtonClick(Vector3? worldPosition = null)
        {
            ConfigureAudioSources();
            if (sfxSource != null && buttonClickClip != null)
            {
                if (worldPosition.HasValue)
                    sfxSource.transform.position = worldPosition.Value;
                sfxSource.pitch = UnityEngine.Random.Range(0.97f, 1.03f);
                sfxSource.PlayOneShot(buttonClickClip, 1.45f);
            }
        }

        public void PlayThreatAlarm(Vector3? worldPosition = null)
        {
            ConfigureAudioSources();
            if (alarmSource != null && threatAlarmClip != null)
            {
                if (worldPosition.HasValue)
                    alarmSource.transform.position = worldPosition.Value;
                alarmSource.pitch = 1.0f;
                alarmSource.PlayOneShot(threatAlarmClip, 1.50f);
            }
        }

        public void PlaySafeChime(Vector3? worldPosition = null)
        {
            ConfigureAudioSources();
            if (chimeSource != null && safeChimeClip != null)
            {
                if (worldPosition.HasValue)
                    chimeSource.transform.position = worldPosition.Value;
                chimeSource.pitch = 1.0f;
                chimeSource.PlayOneShot(safeChimeClip, 1.45f);
            }
        }

        public void StartAmbientHum()
        {
            if (ambientSource != null && ambientHumClip != null && !ambientSource.isPlaying)
            {
                ambientSource.clip = ambientHumClip;
                ambientSource.loop = true;
                ambientSource.Play();
            }
        }

        public void StopAmbientHum()
        {
            if (ambientSource != null && ambientSource.isPlaying)
            {
                ambientSource.Stop();
            }
        }

        // =========================================================================
        // In-Memory Procedural Audio Synthesis Fallbacks (Fail-Safe Architecture)
        // =========================================================================
        private static AudioClip GenerateProceduralClickClip()
        {
            int sampleRate = 44100;
            int numSamples = (int)(0.06f * sampleRate);
            float[] data = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 90.0f);
                data[i] = (Mathf.Sin(2.0f * Mathf.PI * 2400.0f * t) * 0.7f + Mathf.Sin(2.0f * Mathf.PI * 140.0f * t) * 0.3f) * decay;
            }
            AudioClip clip = AudioClip.Create("Procedural_Click", numSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateProceduralAlarmClip()
        {
            int sampleRate = 44100;
            int numSamples = (int)(0.60f * sampleRate);
            float[] data = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = (t % 0.18f < 0.09f) ? 880.0f : 587.0f;
                float envelope = Mathf.Clamp01(1.0f - (t / 0.60f) * 0.4f);
                data[i] = (Mathf.Sin(2.0f * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(4.0f * Mathf.PI * freq * t)) * envelope * 0.75f;
            }
            AudioClip clip = AudioClip.Create("Procedural_Alarm", numSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateProceduralChimeClip()
        {
            int sampleRate = 44100;
            int numSamples = (int)(0.65f * sampleRate);
            float[] data = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float bell = Mathf.Sin(2.0f * Mathf.PI * 523.25f * t) * Mathf.Exp(-t * 4.5f) * 0.5f +
                             Mathf.Sin(2.0f * Mathf.PI * 783.99f * t) * Mathf.Exp(-t * 5.5f) * 0.35f +
                             Mathf.Sin(2.0f * Mathf.PI * 1318.51f * t) * Mathf.Exp(-t * 8.0f) * 0.15f;
                data[i] = bell * Mathf.Min(1.0f, t * 100.0f);
            }
            AudioClip clip = AudioClip.Create("Procedural_Chime", numSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateProceduralAmbientClip()
        {
            int sampleRate = 44100;
            int numSamples = (int)(2.0f * sampleRate);
            float[] data = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                data[i] = (Mathf.Sin(2.0f * Mathf.PI * 60.0f * t) * 0.35f +
                           Mathf.Sin(2.0f * Mathf.PI * 120.0f * t) * 0.20f) * 0.30f;
            }
            AudioClip clip = AudioClip.Create("Procedural_Ambient", numSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
