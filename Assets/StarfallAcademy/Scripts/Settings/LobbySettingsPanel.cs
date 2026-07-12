using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class LobbySettingsPanel : MonoBehaviour
    {
        GameObject layer;
        Text masterValue;
        Text musicValue;
        Text sfxValue;
        Text graphicsValue;
        Text textSpeedValue;
        Text autoValue;
        LobbyUiFactory ui;

        public void Initialize(RectTransform parent, LobbyUiFactory factory)
        {
            ui = factory;
            transform.SetParent(parent, false);
            RectTransform controller = (RectTransform)transform;
            controller.anchorMin = Vector2.zero;
            controller.anchorMax = Vector2.one;
            controller.offsetMin = controller.offsetMax = Vector2.zero;

            layer = new GameObject("Settings Layer", typeof(RectTransform));
            layer.transform.SetParent(transform, false);
            RectTransform layerRect = layer.GetComponent<RectTransform>();
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.offsetMin = layerRect.offsetMax = Vector2.zero;

            Image dim = ui.CreateImage("Settings Backdrop", layer.transform, UrbanFantasyStyle.Backdrop,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            Button backdrop = dim.gameObject.AddComponent<Button>();
            backdrop.transition = Selectable.Transition.None;
            backdrop.onClick.AddListener(Close);

            RectTransform card = ui.CreateImage("Settings Window", layer.transform, UrbanFantasyStyle.PanelStrong,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(760, 720), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, card, UrbanFantasyStyle.StrongLine);
            ui.CreateText("Settings Eyebrow", "G A M E   S E T T I N G S", card, 11, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -34), new Vector2(-72, 22), TextAnchor.MiddleLeft);
            ui.CreateText("Settings Title", "환경 설정", card, 31, FontStyle.Normal, UrbanFantasyStyle.Silver,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -74),
                new Vector2(-72, 42), TextAnchor.MiddleLeft);
            ui.CreateButton("Close Settings", card, new Vector2(1, 1), new Vector2(-34, -34),
                new Vector2(48, 48), "×", 27, UrbanFantasyStyle.PanelSoft, Close);
            ui.CreateImage("Settings Divider", card, UrbanFantasyStyle.Line,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -112), new Vector2(-72, 1));

            masterValue = CreateStepper(card, "마스터 음량", 158, () => AdjustVolume(0, -.1f),
                () => AdjustVolume(0, .1f));
            musicValue = CreateStepper(card, "배경음악", 226, () => AdjustVolume(1, -.1f),
                () => AdjustVolume(1, .1f));
            sfxValue = CreateStepper(card, "효과음", 294, () => AdjustVolume(2, -.1f),
                () => AdjustVolume(2, .1f));
            graphicsValue = CreateStepper(card, "그래픽 품질", 380, () => CycleGraphics(-1),
                () => CycleGraphics(1));
            textSpeedValue = CreateStepper(card, "텍스트 속도", 448, () => CycleText(-1),
                () => CycleText(1));
            autoValue = CreateStepper(card, "자동 전투 기본값", 516, ToggleAuto, ToggleAuto);

            ui.CreateText("Settings Note",
                "자동 전투 기본값은 전투 진입 시 적용됩니다. 설정은 즉시 저장됩니다.",
                card, 13, FontStyle.Normal, UrbanFantasyStyle.Muted,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 94),
                new Vector2(-72, 32), TextAnchor.MiddleCenter);
            GameObject confirm = ui.CreateButton("Confirm Settings", card, new Vector2(.5f, 0),
                new Vector2(0, 44), new Vector2(220, 56), "확인", 18,
                new Color(.17f, .17f, .20f, .98f), Close);
            UrbanFantasyStyle.AddBorder(ui, confirm.GetComponent<RectTransform>(), UrbanFantasyStyle.StrongLine);
            layer.SetActive(false);
        }

        Text CreateStepper(RectTransform card, string label, float y, UnityEngine.Events.UnityAction previous,
            UnityEngine.Events.UnityAction next)
        {
            ui.CreateText(label + " Label", label, card, 17, FontStyle.Normal, UrbanFantasyStyle.Silver,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(150, -y),
                new Vector2(220, 34), TextAnchor.MiddleLeft);
            GameObject left = ui.CreateButton(label + " Previous", card, new Vector2(1, 1),
                new Vector2(-276, -y), new Vector2(50, 44), "‹", 23,
                UrbanFantasyStyle.PanelSoft, () => previous());
            Text value = ui.CreateText(label + " Value", string.Empty, card, 17, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-174, -y), new Vector2(140, 44), TextAnchor.MiddleCenter);
            GameObject right = ui.CreateButton(label + " Next", card, new Vector2(1, 1),
                new Vector2(-72, -y), new Vector2(50, 44), "›", 23,
                UrbanFantasyStyle.PanelSoft, () => next());
            UrbanFantasyStyle.AddBorder(ui, left.GetComponent<RectTransform>());
            UrbanFantasyStyle.AddBorder(ui, right.GetComponent<RectTransform>());
            return value;
        }

        public void Open()
        {
            Refresh();
            layer.SetActive(true);
            layer.transform.SetAsLastSibling();
        }

        public void Close()
        {
            if (layer != null) layer.SetActive(false);
        }

        void AdjustVolume(int type, float amount)
        {
            if (type == 0) GameSettings.MasterVolume += amount;
            else if (type == 1) GameSettings.MusicVolume += amount;
            else GameSettings.SfxVolume += amount;
            Refresh();
        }

        void CycleGraphics(int direction)
        {
            GameSettings.GraphicsQuality = (GameSettings.GraphicsQuality + direction + 3) % 3;
            Refresh();
        }

        void CycleText(int direction)
        {
            int speed = ((int)GameSettings.TextSpeed + direction + 3) % 3;
            GameSettings.TextSpeed = (GameTextSpeed)speed;
            Refresh();
        }

        void ToggleAuto()
        {
            GameSettings.AutoBattle = !GameSettings.AutoBattle;
            Refresh();
        }

        void Refresh()
        {
            masterValue.text = Mathf.RoundToInt(GameSettings.MasterVolume * 100) + "%";
            musicValue.text = Mathf.RoundToInt(GameSettings.MusicVolume * 100) + "%";
            sfxValue.text = Mathf.RoundToInt(GameSettings.SfxVolume * 100) + "%";
            graphicsValue.text = GameSettings.GraphicsQuality == 0 ? "LOW"
                : GameSettings.GraphicsQuality == 1 ? "MEDIUM" : "HIGH";
            textSpeedValue.text = GameSettings.TextSpeed == GameTextSpeed.Slow ? "느림"
                : GameSettings.TextSpeed == GameTextSpeed.Fast ? "빠름" : "보통";
            autoValue.text = GameSettings.AutoBattle ? "ON" : "OFF";
            autoValue.color = GameSettings.AutoBattle ? UrbanFantasyStyle.Gold : UrbanFantasyStyle.Muted;
        }
    }
}
