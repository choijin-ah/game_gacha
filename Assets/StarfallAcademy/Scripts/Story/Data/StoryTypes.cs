using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public enum StoryCategory
    {
        Main,
        Event,
        Character,
        Side
    }

    public enum StorySpeakerPosition
    {
        Narrator,
        Left,
        Center,
        Right
    }

    public enum StoryTransition
    {
        None,
        Cut,
        CrossFade,
        FadeToBlack,
        FadeToWhite,
        SlideLeft,
        SlideRight
    }

    [Flags]
    public enum StoryScreenEffect
    {
        None = 0,
        Shake = 1 << 0,
        FlashWhite = 1 << 1,
        FlashBlack = 1 << 2,
        FadeIn = 1 << 3,
        FadeOut = 1 << 4,
        Vignette = 1 << 5
    }

    [Serializable]
    public sealed class StoryCharacterDisplay
    {
        [SerializeField] CharacterData character;
        [SerializeField] Sprite expressionSprite;
        [SerializeField] string expressionKey;
        [SerializeField] bool visible;
        [SerializeField] bool flipX;
        [SerializeField] Color tint = Color.white;
        [SerializeField] Vector2 offset;

        public CharacterData Character { get => character; set => character = value; }
        public Sprite ExpressionSprite { get => expressionSprite; set => expressionSprite = value; }
        public string ExpressionKey { get => expressionKey; set => expressionKey = value ?? string.Empty; }
        public bool Visible { get => visible; set => visible = value; }
        public bool FlipX { get => flipX; set => flipX = value; }
        public Color Tint { get => tint; set => tint = value; }
        public Vector2 Offset { get => offset; set => offset = value; }

        public Sprite ResolvedSprite
        {
            get
            {
                if (expressionSprite != null) return expressionSprite;
                if (character != null && !string.IsNullOrWhiteSpace(expressionKey))
                {
                    Sprite keyed = Resources.Load<Sprite>("Story/Expressions/" + character.Id
                        + "/" + expressionKey);
                    if (keyed == null)
                        keyed = Resources.Load<Sprite>("Story/Expressions/" + expressionKey);
                    if (keyed != null) return keyed;
                }
                return character != null ? character.Portrait : null;
            }
        }
    }

    [Serializable]
    public sealed class StoryChoice
    {
        [SerializeField] string text;
        [SerializeField] string nextEpisodeId;
        [SerializeField] string nextLineId;
        [SerializeField] string conditionKey;

        public string Text { get => text; set => text = value ?? string.Empty; }
        public string NextEpisodeId { get => nextEpisodeId; set => nextEpisodeId = value ?? string.Empty; }
        public string NextLineId { get => nextLineId; set => nextLineId = value ?? string.Empty; }
        public string ConditionKey { get => conditionKey; set => conditionKey = value ?? string.Empty; }
    }

    [Serializable]
    public sealed class StoryLine
    {
        [SerializeField] string lineId;

        [Header("Dialogue")]
        [SerializeField] CharacterData speaker;
        [SerializeField] string speakerNameOverride;
        [SerializeField] StorySpeakerPosition speakerPosition = StorySpeakerPosition.Narrator;
        [SerializeField, TextArea(3, 8)] string text;

        [Header("Characters")]
        [SerializeField] StoryCharacterDisplay left = new StoryCharacterDisplay();
        [SerializeField] StoryCharacterDisplay center = new StoryCharacterDisplay();
        [SerializeField] StoryCharacterDisplay right = new StoryCharacterDisplay();

        [Header("Scene")]
        [SerializeField] Sprite background;
        [SerializeField] Sprite cg;
        [SerializeField] AudioClip bgm;
        [SerializeField] AudioClip sfx;

        [Header("Direction")]
        [SerializeField] StoryTransition transition = StoryTransition.CrossFade;
        [SerializeField] StoryScreenEffect effects;
        [SerializeField, Min(0f)] float shakeStrength = 8f;
        [SerializeField, Min(0f)] float effectDuration = .35f;

        [Header("Playback")]
        [SerializeField, Min(0f)] float textSpeed = .035f;
        [SerializeField, Min(0f)] float autoDuration = 2f;
        [SerializeField] List<StoryChoice> choices = new List<StoryChoice>();

        public string Id { get => lineId; set => lineId = value ?? string.Empty; }
        public CharacterData Speaker { get => speaker; set => speaker = value; }
        public string SpeakerName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(speakerNameOverride)) return speakerNameOverride;
                return speaker != null ? speaker.DisplayName : string.Empty;
            }
            set => speakerNameOverride = value ?? string.Empty;
        }
        public string SpeakerNameOverride { get => speakerNameOverride; set => speakerNameOverride = value ?? string.Empty; }
        public StorySpeakerPosition SpeakerPosition { get => speakerPosition; set => speakerPosition = value; }
        public string Text { get => text; set => text = value ?? string.Empty; }
        public StoryCharacterDisplay Left { get => left ?? (left = new StoryCharacterDisplay()); set => left = value ?? new StoryCharacterDisplay(); }
        public StoryCharacterDisplay Center { get => center ?? (center = new StoryCharacterDisplay()); set => center = value ?? new StoryCharacterDisplay(); }
        public StoryCharacterDisplay Right { get => right ?? (right = new StoryCharacterDisplay()); set => right = value ?? new StoryCharacterDisplay(); }
        public Sprite Background { get => background; set => background = value; }
        public Sprite Cg { get => cg; set => cg = value; }
        public AudioClip Bgm { get => bgm; set => bgm = value; }
        public AudioClip Sfx { get => sfx; set => sfx = value; }
        public StoryTransition Transition { get => transition; set => transition = value; }
        public StoryScreenEffect Effects { get => effects; set => effects = value; }
        public float ShakeStrength { get => Mathf.Max(0f, shakeStrength); set => shakeStrength = Mathf.Max(0f, value); }
        public float EffectDuration { get => Mathf.Max(0f, effectDuration); set => effectDuration = Mathf.Max(0f, value); }
        public float TextSpeed { get => Mathf.Max(0f, textSpeed); set => textSpeed = Mathf.Max(0f, value); }
        public float AutoDuration { get => Mathf.Max(0f, autoDuration); set => autoDuration = Mathf.Max(0f, value); }
        public IReadOnlyList<StoryChoice> Choices => choices;
        public bool HasChoices => choices != null && choices.Count > 0;

        public StoryLine()
        {
            lineId = "line_001";
        }

        public void ReplaceChoices(IEnumerable<StoryChoice> newChoices)
        {
            choices = newChoices != null ? new List<StoryChoice>(newChoices) : new List<StoryChoice>();
        }

        public StoryLine DeepCopy()
        {
            var copy = new StoryLine
            {
                lineId = lineId,
                speaker = speaker,
                speakerNameOverride = speakerNameOverride,
                speakerPosition = speakerPosition,
                text = text,
                left = CopyDisplay(Left),
                center = CopyDisplay(Center),
                right = CopyDisplay(Right),
                background = background,
                cg = cg,
                bgm = bgm,
                sfx = sfx,
                transition = transition,
                effects = effects,
                shakeStrength = shakeStrength,
                effectDuration = effectDuration,
                textSpeed = textSpeed,
                autoDuration = autoDuration,
                choices = new List<StoryChoice>()
            };

            if (choices != null)
            {
                foreach (StoryChoice choice in choices)
                {
                    if (choice == null) continue;
                    copy.choices.Add(new StoryChoice
                    {
                        Text = choice.Text,
                        NextEpisodeId = choice.NextEpisodeId,
                        NextLineId = choice.NextLineId,
                        ConditionKey = choice.ConditionKey
                    });
                }
            }
            return copy;
        }

        static StoryCharacterDisplay CopyDisplay(StoryCharacterDisplay source)
        {
            return new StoryCharacterDisplay
            {
                Character = source.Character,
                ExpressionSprite = source.ExpressionSprite,
                ExpressionKey = source.ExpressionKey,
                Visible = source.Visible,
                FlipX = source.FlipX,
                Tint = source.Tint,
                Offset = source.Offset
            };
        }
    }
}
