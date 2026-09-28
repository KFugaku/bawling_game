using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BowlingAudioFeedback : MonoBehaviour
{
    private const int SampleRate = 44100;
    private const int SourcePoolSize = 8;
    private const float PinImpactInterval = 0.035f;

    private readonly List<AudioSource> sources = new List<AudioSource>();
    private AudioClip throwClip;
    private AudioClip pinImpactClip;
    private AudioClip strikeClip;
    private AudioClip spareClip;
    private int nextSourceIndex;
    private float lastPinImpactTime = -10f;

    public static BowlingAudioFeedback Instance { get; private set; }

    public static BowlingAudioFeedback EnsureCreated()
    {
        if (Instance != null)
        {
            return Instance;
        }

        BowlingAudioFeedback existing = FindFirstObjectByType<BowlingAudioFeedback>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject feedbackObject = new GameObject("Bowling Audio Feedback");
        return feedbackObject.AddComponent<BowlingAudioFeedback>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CreateSourcePool();
        CreateClips();
    }

    public void PlayThrow(float powerRatio)
    {
        PlayOneShot(
            throwClip,
            Mathf.Lerp(0.18f, 0.34f, Mathf.Clamp01(powerRatio)),
            Mathf.Lerp(0.88f, 1.12f, Mathf.Clamp01(powerRatio)));
    }

    public void PlayPinImpact(float impactSpeed)
    {
        if (Time.unscaledTime - lastPinImpactTime < PinImpactInterval)
        {
            return;
        }

        lastPinImpactTime = Time.unscaledTime;
        float strength = Mathf.InverseLerp(0.6f, 8f, impactSpeed);
        PlayOneShot(pinImpactClip, Mathf.Lerp(0.1f, 0.42f, strength), Random.Range(0.9f, 1.1f));
    }

    public void PlayStrike()
    {
        PlayOneShot(strikeClip, 0.58f, 1f);
    }

    public void PlaySpare()
    {
        PlayOneShot(spareClip, 0.5f, 1f);
    }

    private void CreateSourcePool()
    {
        for (int index = 0; index < SourcePoolSize; index++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            sources.Add(source);
        }
    }

    private void CreateClips()
    {
        throwClip = CreateThrowClip();
        pinImpactClip = CreatePinImpactClip();
        strikeClip = CreateFanfareClip("Strike Fanfare", new[] { 523.25f, 659.25f, 783.99f, 1046.5f });
        spareClip = CreateFanfareClip("Spare Fanfare", new[] { 392f, 523.25f, 659.25f });
    }

    private void PlayOneShot(AudioClip clip, float volume, float pitch)
    {
        if (clip == null || sources.Count == 0)
        {
            return;
        }

        AudioSource source = sources[nextSourceIndex];
        nextSourceIndex = (nextSourceIndex + 1) % sources.Count;
        source.Stop();
        source.pitch = pitch;
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private static AudioClip CreateThrowClip()
    {
        const float duration = 0.22f;
        int sampleCount = Mathf.CeilToInt(duration * SampleRate);
        float[] samples = new float[sampleCount];
        for (int index = 0; index < sampleCount; index++)
        {
            float time = index / (float)SampleRate;
            float progress = time / duration;
            float envelope = Mathf.Sin(progress * Mathf.PI);
            float frequency = Mathf.Lerp(90f, 260f, progress);
            float tone = Mathf.Sin(2f * Mathf.PI * frequency * time);
            samples[index] = envelope * (tone * 0.25f + SampleNoise(index) * 0.18f);
        }

        return CreateClip("Ball Release", samples);
    }

    private static AudioClip CreatePinImpactClip()
    {
        const float duration = 0.12f;
        int sampleCount = Mathf.CeilToInt(duration * SampleRate);
        float[] samples = new float[sampleCount];
        for (int index = 0; index < sampleCount; index++)
        {
            float time = index / (float)SampleRate;
            float envelope = Mathf.Exp(-28f * time);
            float bodyTone = Mathf.Sin(2f * Mathf.PI * 730f * time) * 0.42f;
            float clickTone = Mathf.Sin(2f * Mathf.PI * 1180f * time) * 0.2f;
            samples[index] = envelope * (bodyTone + clickTone + SampleNoise(index + 71) * 0.28f);
        }

        return CreateClip("Pin Impact", samples);
    }

    private static AudioClip CreateFanfareClip(string name, float[] frequencies)
    {
        const float noteDuration = 0.13f;
        const float tailDuration = 0.2f;
        float duration = frequencies.Length * noteDuration + tailDuration;
        int sampleCount = Mathf.CeilToInt(duration * SampleRate);
        float[] samples = new float[sampleCount];
        for (int index = 0; index < sampleCount; index++)
        {
            float time = index / (float)SampleRate;
            int noteIndex = Mathf.Min(Mathf.FloorToInt(time / noteDuration), frequencies.Length - 1);
            float noteTime = time - noteIndex * noteDuration;
            float noteEnvelope = Mathf.Sin(Mathf.Clamp01(noteTime / noteDuration) * Mathf.PI);
            float tailEnvelope = time < frequencies.Length * noteDuration
                ? 1f
                : Mathf.Clamp01(1f - (time - frequencies.Length * noteDuration) / tailDuration);
            float frequency = frequencies[noteIndex];
            float fundamental = Mathf.Sin(2f * Mathf.PI * frequency * time);
            float harmonic = Mathf.Sin(2f * Mathf.PI * frequency * 2f * time) * 0.22f;
            samples[index] = (fundamental * 0.44f + harmonic) * noteEnvelope * tailEnvelope;
        }

        return CreateClip(name, samples);
    }

    private static AudioClip CreateClip(string name, float[] samples)
    {
        AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static float SampleNoise(int sampleIndex)
    {
        unchecked
        {
            uint value = (uint)(sampleIndex + 1);
            value = value * 1664525u + 1013904223u;
            return (value & 0xffffu) / 32767.5f - 1f;
        }
    }
}
