using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public enum GameAudioChannelType
    {
        Music,
        Sfx
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class GameAudioChannel : MonoBehaviour
    {
        [SerializeField] GameAudioChannelType channel = GameAudioChannelType.Sfx;
        [SerializeField, Range(0f, 1f)] float baseVolume = 1f;

        AudioSource source;

        void OnEnable()
        {
            source = GetComponent<AudioSource>();
            GameSettings.Changed += Apply;
            Apply();
        }

        void OnDisable() => GameSettings.Changed -= Apply;

        void OnValidate()
        {
            baseVolume = Mathf.Clamp01(baseVolume);
            if (isActiveAndEnabled) Apply();
        }

        void Apply()
        {
            if (source == null) source = GetComponent<AudioSource>();
            float channelVolume = channel == GameAudioChannelType.Music
                ? GameSettings.MusicVolume : GameSettings.SfxVolume;
            source.volume = baseVolume * channelVolume;
        }
    }

}
