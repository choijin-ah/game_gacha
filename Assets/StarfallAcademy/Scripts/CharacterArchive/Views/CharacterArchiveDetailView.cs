using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    public sealed class CharacterArchiveDetailView
    {
        readonly LobbyUiFactory ui;
        readonly Action<string> showToast;
        readonly Image art;
        readonly Image skillIcon;
        readonly Text initial;
        readonly Text lockLabel;
        readonly Text skillFallback;
        readonly Text indexLabel;
        readonly Text nameLabel;
        readonly Text rarityLabel;
        readonly Text ownershipLabel;
        readonly Text affiliationLabel;
        readonly Text roleLabel;
        readonly Text attackLabel;
        readonly Text levelLabel;
        readonly Text powerLabel;
        readonly Text descriptionLabel;
        readonly Text walletLabel;
        readonly Text levelGrowthLabel;
        readonly Text skillGrowthLabel;
        readonly Text levelButtonLabel;
        readonly Text skillButtonLabel;
        readonly Text equipmentPowerLabel;
        readonly Text recommendedEquipmentLabel;
        readonly Text awakeningLabel;
        readonly Button levelButton;
        readonly Button skillButton;
        readonly Button recommendedEquipmentButton;
        readonly Button awakeningButton;
        readonly Text[] equipmentSlotLabels = new Text[4];
        readonly Button[] equipmentSlotButtons = new Button[4];
        readonly EquipmentSlot[] equipmentSlots =
        {
            EquipmentSlot.Weapon,
            EquipmentSlot.Armor,
            EquipmentSlot.Accessory,
            EquipmentSlot.AuxiliaryDevice
        };
        CharacterData selected;

        public CharacterArchiveDetailView(RectTransform workspace, LobbyUiFactory ui, Action<string> showToast)
        {
            this.ui = ui;
            this.showToast = showToast;
            RectTransform panel = ui.CreateImage("Character Detail Panel", workspace, UrbanFantasyStyle.Panel,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(270, -32),
                new Vector2(1170, 760)).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, panel);

            RectTransform artPanel = ui.CreateImage("Character Art Panel", panel,
                new Color(.005f, .005f, .008f, .42f), new Vector2(0, .5f), new Vector2(0, .5f),
                new Vector2(310, 0), new Vector2(590, 720)).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, artPanel);
            ui.CreateText("Art Watermark", "✦", artPanel, 180, FontStyle.Normal,
                new Color(1, 1, 1, .035f), Vector2.zero, Vector2.one, Vector2.zero,
                Vector2.zero, TextAnchor.MiddleCenter);
            art = ui.CreateImage("Character Art", artPanel, Color.clear, Vector2.zero, Vector2.one,
                Vector2.zero, new Vector2(-24, -24));
            art.type = Image.Type.Simple;
            art.preserveAspect = true;
            initial = ui.CreateText("Character Initial", "?", artPanel, 120, FontStyle.Normal,
                new Color(1, 1, 1, .28f), Vector2.zero, Vector2.one, Vector2.zero,
                Vector2.zero, TextAnchor.MiddleCenter);
            lockLabel = ui.CreateText("Locked Character", "◆\n\n미 획 득\nRECRUIT TO UNLOCK", artPanel, 18,
                FontStyle.Normal, new Color(.86f, .86f, .9f, .72f), Vector2.zero, Vector2.one,
                Vector2.zero, new Vector2(-50, -50), TextAnchor.MiddleCenter);

            RectTransform equipment = ui.CreateImage("Equipment Panel", artPanel,
                new Color(.008f, .008f, .012f, .92f), new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(0, 98), new Vector2(548, 176)).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, equipment);
            ui.CreateText("Equipment Title", "E Q U I P M E N T", equipment, 11, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(0, 1), new Vector2(.5f, 1),
                new Vector2(16, -16), new Vector2(-8, 22), TextAnchor.MiddleLeft);
            equipmentPowerLabel = ui.CreateText("Equipment Power", string.Empty, equipment, 11,
                FontStyle.Normal, UrbanFantasyStyle.Gold, new Vector2(.5f, 1), new Vector2(1, 1),
                new Vector2(-16, -16), new Vector2(-8, 22), TextAnchor.MiddleRight);

            for (int i = 0; i < equipmentSlots.Length; i++)
            {
                int slotIndex = i;
                EquipmentSlot slot = equipmentSlots[i];
                float x = -198 + i * 132;
                GameObject slotObject = ui.CreateButton("Equipment " + slot, equipment,
                    new Vector2(.5f, 1), new Vector2(x, -67), new Vector2(122, 64),
                    string.Empty, 11, new Color(.13f, .13f, .16f, .98f),
                    () => UpgradeEquipment(equipmentSlots[slotIndex]));
                UrbanFantasyStyle.AddBorder(ui, slotObject.GetComponent<RectTransform>());
                equipmentSlotButtons[i] = slotObject.GetComponent<Button>();
                equipmentSlotLabels[i] = slotObject.GetComponentInChildren<Text>();
                equipmentSlotLabels[i].lineSpacing = .9f;
            }

            GameObject recommendEquipment = ui.CreateButton("Recommended Equipment", equipment,
                new Vector2(.5f, 0), new Vector2(-132, 22), new Vector2(226, 36), string.Empty, 12,
                new Color(.18f, .16f, .10f, .98f), EquipRecommended);
            UrbanFantasyStyle.AddBorder(ui, recommendEquipment.GetComponent<RectTransform>());
            recommendedEquipmentButton = recommendEquipment.GetComponent<Button>();
            recommendedEquipmentLabel = recommendEquipment.GetComponentInChildren<Text>();

            GameObject awaken = ui.CreateButton("Awaken Character", equipment,
                new Vector2(.5f, 0), new Vector2(132, 22), new Vector2(226, 36), string.Empty, 12,
                new Color(.18f, .11f, .19f, .98f), Awaken);
            UrbanFantasyStyle.AddBorder(ui, awaken.GetComponent<RectTransform>());
            awakeningButton = awaken.GetComponent<Button>();
            awakeningLabel = awaken.GetComponentInChildren<Text>();

            RectTransform info = ui.CreateImage("Character Information", panel,
                new Color(.01f, .01f, .015f, .54f), new Vector2(1, .5f), new Vector2(1, .5f),
                new Vector2(-287, 0), new Vector2(540, 720)).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, info);
            indexLabel = ui.CreateText("Character Number", "NO. ---", info, 12, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -34), new Vector2(-54, 22), TextAnchor.MiddleLeft);
            nameLabel = ui.CreateText("Character Name", "캐릭터를 선택하세요", info, 34, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -78), new Vector2(-54, 48), TextAnchor.MiddleLeft);
            rarityLabel = ui.CreateText("Character Rarity", string.Empty, info, 16, FontStyle.Normal,
                UrbanFantasyStyle.Gold, new Vector2(0, 1), new Vector2(.65f, 1),
                new Vector2(0, -119), new Vector2(-27, 26), TextAnchor.MiddleLeft);
            ownershipLabel = ui.CreateText("Ownership", string.Empty, info, 11, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(.65f, 1), new Vector2(1, 1),
                new Vector2(0, -119), new Vector2(-27, 26), TextAnchor.MiddleRight);
            ui.CreateImage("Name Divider", info, UrbanFantasyStyle.Line,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -148), new Vector2(-54, 1));

            affiliationLabel = CreateField(ui, info, "소속", -185);
            roleLabel = CreateField(ui, info, "역할", -227);
            attackLabel = CreateField(ui, info, "공격 타입", -269);
            levelLabel = CreateField(ui, info, "레벨", -311);
            powerLabel = CreateField(ui, info, "전투력", -353);

            ui.CreateText("Description Title", "P R O F I L E", info, 11, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -394), new Vector2(-54, 22), TextAnchor.MiddleLeft);
            ui.CreateImage("Description Divider", info, UrbanFantasyStyle.Line,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -418), new Vector2(-54, 1));
            descriptionLabel = ui.CreateText("Description", string.Empty, info, 15, FontStyle.Normal,
                new Color(.88f, .88f, .91f, .78f), new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -467), new Vector2(-54, 78), TextAnchor.UpperLeft);
            descriptionLabel.lineSpacing = 1.2f;

            RectTransform growth = ui.CreateImage("Growth Panel", info, UrbanFantasyStyle.PanelSoft,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 94), new Vector2(-54, 174)).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, growth);
            walletLabel = ui.CreateText("Growth Wallet", string.Empty, growth, 11, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -17), new Vector2(-24, 22), TextAnchor.MiddleRight);
            ui.CreateImage("Growth Divider", growth, UrbanFantasyStyle.Line,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -36), new Vector2(-20, 1));

            levelGrowthLabel = ui.CreateText("Level Growth", string.Empty, growth, 15, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(143, -70), new Vector2(230, 34), TextAnchor.MiddleLeft);
            GameObject levelUp = ui.CreateButton("Level Up", growth, new Vector2(1, 1),
                new Vector2(-90, -70), new Vector2(156, 48), string.Empty, 14,
                new Color(.17f, .17f, .20f, .98f), LevelUp);
            UrbanFantasyStyle.AddBorder(ui, levelUp.GetComponent<RectTransform>());
            levelButton = levelUp.GetComponent<Button>();
            levelButtonLabel = levelUp.GetComponentInChildren<Text>();

            skillIcon = ui.CreateImage("Skill Icon", growth, new Color(.12f, .12f, .15f, 1),
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(39, 39), new Vector2(52, 52));
            skillIcon.type = Image.Type.Simple;
            skillIcon.preserveAspect = true;
            UrbanFantasyStyle.AddBorder(ui, skillIcon.rectTransform);
            skillFallback = ui.CreateText("Skill Fallback", "✦", skillIcon.transform, 24, FontStyle.Normal,
                UrbanFantasyStyle.Silver, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                TextAnchor.MiddleCenter);
            skillGrowthLabel = ui.CreateText("Skill Growth", string.Empty, growth, 14, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(185, 39), new Vector2(250, 42), TextAnchor.MiddleLeft);
            GameObject skillUp = ui.CreateButton("Skill Level Up", growth, new Vector2(1, 0),
                new Vector2(-90, 39), new Vector2(156, 48), string.Empty, 14,
                new Color(.17f, .17f, .20f, .98f), SkillUp);
            UrbanFantasyStyle.AddBorder(ui, skillUp.GetComponent<RectTransform>());
            skillButton = skillUp.GetComponent<Button>();
            skillButtonLabel = skillUp.GetComponentInChildren<Text>();
            SetCharacter(null, -1);
        }

        public void SetCharacter(CharacterData character, int index)
        {
            selected = character;
            if (character == null)
            {
                art.sprite = null;
                art.material = null;
                art.color = Color.clear;
                initial.text = "?";
                initial.gameObject.SetActive(true);
                lockLabel.gameObject.SetActive(false);
                indexLabel.text = "NO. ---";
                nameLabel.text = "캐릭터를 선택하세요";
                rarityLabel.text = ownershipLabel.text = affiliationLabel.text = roleLabel.text =
                    attackLabel.text = levelLabel.text = powerLabel.text = descriptionLabel.text = string.Empty;
                skillIcon.sprite = null;
                skillFallback.gameObject.SetActive(true);
                RefreshGrowth();
                return;
            }

            bool owned = CharacterProgressionService.IsOwned(character);
            Sprite displayArt = character.GachaArt;
            art.material = null;
            if (displayArt != null)
            {
                art.sprite = displayArt;
                art.color = owned ? Color.white : new Color(.28f, .28f, .31f, .72f);
                if (!owned) UrbanFantasyStyle.ApplyMonochrome(art);
                initial.gameObject.SetActive(false);
            }
            else
            {
                art.sprite = null;
                art.color = new Color(character.AccentColor.r, character.AccentColor.g,
                    character.AccentColor.b, owned ? .12f : .045f);
                initial.text = character.DisplayName.Length > 0 ? character.DisplayName.Substring(0, 1) : "?";
                initial.gameObject.SetActive(true);
            }
            lockLabel.gameObject.SetActive(!owned);
            indexLabel.text = "NO. " + (index + 1).ToString("000") + "   /   " + character.Id;
            nameLabel.text = character.DisplayName;
            rarityLabel.text = new string('★', Mathf.Min(6, character.Rarity));
            ownershipLabel.text = owned ? "O W N E D" : "N O T   O W N E D";
            ownershipLabel.color = owned ? UrbanFantasyStyle.Silver : new Color(.75f, .12f, .10f, .9f);
            affiliationLabel.text = character.Affiliation;
            roleLabel.text = RoleLabel(character.Role);
            attackLabel.text = AttackLabel(character.AttackType);
            descriptionLabel.text = string.IsNullOrWhiteSpace(character.Description)
                ? "등록된 프로필 설명이 없습니다." : character.Description;

            Sprite icon = SkillIconLibrary.Get(character);
            skillIcon.sprite = icon;
            skillIcon.color = icon != null
                ? (owned ? Color.white : new Color(.35f, .35f, .38f, .7f))
                : new Color(.12f, .12f, .15f, 1);
            skillFallback.gameObject.SetActive(icon == null);
            RefreshGrowth();
        }

        void LevelUp()
        {
            if (selected == null) return;
            CharacterProgressionService.TryLevelUp(selected, out string message);
            showToast?.Invoke(message);
            RefreshGrowth();
        }

        void SkillUp()
        {
            if (selected == null) return;
            CharacterProgressionService.TrySkillUp(selected, out string message);
            showToast?.Invoke(message);
            RefreshGrowth();
        }

        void EquipRecommended()
        {
            if (selected == null) return;
            EquipmentDatabase database = Resources.Load<EquipmentDatabase>("Data/EquipmentDatabase");
            IReadOnlyList<EquipmentInstance> items = EquipmentInventoryService.Default.GetAll();
            if (database != null && items.Count > 0)
            {
                int equipped = 0;
                for (int slotIndex = 0; slotIndex < equipmentSlots.Length; slotIndex++)
                {
                    EquipmentSlot slot = equipmentSlots[slotIndex];
                    EquipmentInstance best = null;
                    int bestPower = -1;
                    for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
                    {
                        EquipmentInstance item = items[itemIndex];
                        EquipmentDefinition definition = item == null
                            ? null : database.FindEquipment(item.equipmentId);
                        if (definition == null || definition.Slot != slot
                            || (!string.IsNullOrEmpty(item.equippedCharacterId)
                                && item.equippedCharacterId != selected.Id)) continue;
                        int power = definition.EstimateCombatPower(item.level);
                        if (power <= bestPower) continue;
                        best = item;
                        bestPower = power;
                    }
                    if (best != null && EquipmentInventoryService.Default.Equip(best.instanceId,
                        selected, database, out _)) equipped++;
                }
                showToast?.Invoke(equipped > 0 ? "추천 장비를 장착했습니다."
                    : "장착할 수 있는 장비가 없습니다.");
                RefreshGrowth();
                return;
            }
            EquipmentService.TryEquipRecommended(selected, out string message);
            showToast?.Invoke(message);
            RefreshGrowth();
        }

        void UpgradeEquipment(EquipmentSlot slot)
        {
            if (selected == null) return;
            if (TryGetEquippedInstance(slot, out EquipmentInstance instance,
                out _, out EquipmentDatabase database))
            {
                var enhancement = new EquipmentEnhancementService(PlayerPrefsMetaStorage.Shared);
                enhancement.TryEnhance(instance.instanceId, database, out string enhancementMessage);
                showToast?.Invoke(enhancementMessage);
                RefreshGrowth();
                return;
            }
            EquipmentService.TryUpgradeSlot(selected, slot, out string message);
            showToast?.Invoke(message);
            RefreshGrowth();
        }

        void Awaken()
        {
            if (selected == null) return;
            CharacterAwakeningService.Default.TryAwaken(selected, out string message);
            showToast?.Invoke(message);
            RefreshGrowth();
        }

        void RefreshGrowth()
        {
            if (selected == null)
            {
                levelGrowthLabel.text = skillGrowthLabel.text = walletLabel.text = string.Empty;
                levelButtonLabel.text = skillButtonLabel.text = "—";
                levelButton.interactable = skillButton.interactable = false;
                awakeningLabel.text = "A W A K E N";
                awakeningButton.interactable = false;
                RefreshEquipment(false);
                return;
            }

            bool owned = CharacterProgressionService.IsOwned(selected);
            int level = CharacterProgressionService.GetLevel(selected);
            int skillLevel = CharacterProgressionService.GetSkillLevel(selected);
            levelLabel.text = owned ? "LV. " + level + " / " + selected.MaxLevel : "—";
            powerLabel.text = owned ? CharacterProgressionService.GetCombatPower(selected).ToString("N0") : "—";
            walletLabel.text = "보유   ● " + PlayerWallet.Credits.ToString("N0") +
                "     ◇ " + PlayerWallet.SkillMaterials.ToString("N0") + " " +
                PlayerWallet.SkillMaterialDisplayName;
            levelGrowthLabel.text = "캐릭터 레벨   " + (owned ? "LV. " + level : "LOCKED");
            skillGrowthLabel.text = selected.SkillName + "\n" + (owned
                ? "SKILL LV. " + skillLevel + " / " + selected.SkillMaxLevel : "LOCKED");

            bool levelMax = level >= selected.MaxLevel;
            bool skillMax = skillLevel >= selected.SkillMaxLevel;
            levelButtonLabel.text = levelMax ? "MAX" : "레벨업   ● " +
                CharacterProgressionService.GetLevelUpCost(selected).ToString("N0");
            skillButtonLabel.text = skillMax ? "MAX" : "스킬 강화   ◇ " +
                CharacterProgressionService.GetSkillUpCost(selected).ToString("N0");
            levelButton.interactable = owned && !levelMax;
            skillButton.interactable = owned && !skillMax;
            int awakeningStage = CharacterAwakeningService.Default.GetStage(selected);
            int fragments = CharacterAwakeningService.Default.GetFragments(selected);
            if (awakeningStage >= selected.AwakeningStages.Count)
                awakeningLabel.text = "AWAKEN " + awakeningStage + "  ·  MAX";
            else
                awakeningLabel.text = "AWAKEN " + awakeningStage + " → " + (awakeningStage + 1)
                    + "  ·  " + fragments + "/"
                    + selected.AwakeningStages[awakeningStage].RequiredFragments;
            awakeningButton.interactable = owned
                && CharacterAwakeningService.Default.CanAwaken(selected, out _);
            RefreshEquipment(owned);
        }

        void RefreshEquipment(bool owned)
        {
            if (selected == null)
            {
                equipmentPowerLabel.text = string.Empty;
                recommendedEquipmentLabel.text = "추천 일괄 장착";
                recommendedEquipmentButton.interactable = false;
                for (int i = 0; i < equipmentSlots.Length; i++)
                {
                    equipmentSlotLabels[i].text = EquipmentService.GetSlotDisplayName(equipmentSlots[i]) +
                        "\n—";
                    equipmentSlotButtons[i].interactable = false;
                }
                return;
            }

            if (!EquipmentService.IsUnlocked)
            {
                int requiredLevel = PlayerProfileService.GetRequiredLevel(AccountFeature.Equipment);
                equipmentPowerLabel.text = "장비  ·  계정 LV." + requiredLevel + " 해금";
                recommendedEquipmentLabel.text = "계정 LV." + requiredLevel + " 해금";
                recommendedEquipmentButton.interactable = false;
                for (int i = 0; i < equipmentSlots.Length; i++)
                {
                    equipmentSlotLabels[i].text = EquipmentService.GetSlotDisplayName(equipmentSlots[i])
                        + "\nLOCKED";
                    equipmentSlotButtons[i].interactable = false;
                }
                return;
            }

            bool hasEmptySlot = false;
            equipmentPowerLabel.text = owned ? "장비 전투력  +" +
                EquipmentService.GetCombatPowerBonus(selected).ToString("N0") : "장비 전투력  —";
            for (int i = 0; i < equipmentSlots.Length; i++)
            {
                EquipmentSlot slot = equipmentSlots[i];
                if (TryGetEquippedInstance(slot, out EquipmentInstance instance,
                    out EquipmentDefinition definition, out _))
                {
                    bool instanceMax = instance.level >= definition.MaximumLevel;
                    equipmentSlotLabels[i].text = definition.DisplayName + "  LV." + instance.level + "\n"
                        + (instanceMax ? "MAX" : "강화  "
                            + definition.GetEnhancementCost(instance.level).ToString("N0"));
                    equipmentSlotButtons[i].interactable = owned && !instanceMax;
                    continue;
                }
                int equipmentLevel = EquipmentService.GetLevel(selected, slot);
                string slotName = EquipmentService.GetSlotDisplayName(slot);
                if (!owned)
                {
                    equipmentSlotLabels[i].text = slotName + "\nLOCKED";
                    equipmentSlotButtons[i].interactable = false;
                    continue;
                }
                if (equipmentLevel < EquipmentService.DefaultEquipmentLevel)
                {
                    equipmentSlotLabels[i].text = slotName + "\n미장착";
                    equipmentSlotButtons[i].interactable = false;
                    hasEmptySlot = true;
                    continue;
                }

                bool isMax = equipmentLevel >= EquipmentService.MaxEquipmentLevel;
                equipmentSlotLabels[i].text = slotName + "  LV." + equipmentLevel + "\n" +
                    (isMax ? "MAX" : "강화  ● " +
                        EquipmentService.GetUpgradeCost(selected, slot).ToString("N0"));
                equipmentSlotButtons[i].interactable = !isMax;
            }

            recommendedEquipmentLabel.text = hasEmptySlot ? "추천 일괄 장착" : "장착 완료";
            recommendedEquipmentButton.interactable = owned && hasEmptySlot;
        }

        bool TryGetEquippedInstance(EquipmentSlot slot, out EquipmentInstance instance,
            out EquipmentDefinition definition, out EquipmentDatabase database)
        {
            instance = null;
            definition = null;
            database = Resources.Load<EquipmentDatabase>("Data/EquipmentDatabase");
            if (selected == null || database == null) return false;
            IReadOnlyList<EquipmentInstance> equipped = EquipmentInventoryService.Default.GetEquipped(selected);
            for (int i = 0; i < equipped.Count; i++)
            {
                EquipmentDefinition candidate = database.FindEquipment(equipped[i].equipmentId);
                if (candidate == null || candidate.Slot != slot) continue;
                instance = equipped[i];
                definition = candidate;
                return true;
            }
            return false;
        }

        static Text CreateField(LobbyUiFactory ui, RectTransform parent, string label, float y)
        {
            ui.CreateText(label + " Label", label, parent, 13, FontStyle.Normal, UrbanFantasyStyle.Muted,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(82, y),
                new Vector2(110, 26), TextAnchor.MiddleLeft);
            return ui.CreateText(label + " Value", string.Empty, parent, 16, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(72, y), new Vector2(-210, 28), TextAnchor.MiddleLeft);
        }

        static string RoleLabel(CharacterRole role)
        {
            switch (role)
            {
                case CharacterRole.Striker: return "스트라이커";
                case CharacterRole.Support: return "서포터";
                case CharacterRole.Tank: return "탱커";
                case CharacterRole.Healer: return "힐러";
                default: return "스페셜";
            }
        }

        static string AttackLabel(AttackType attack)
        {
            switch (attack)
            {
                case AttackType.Piercing: return "관통";
                case AttackType.Mystic: return "신비";
                case AttackType.Sonic: return "음파";
                default: return "일반";
            }
        }
    }
}
