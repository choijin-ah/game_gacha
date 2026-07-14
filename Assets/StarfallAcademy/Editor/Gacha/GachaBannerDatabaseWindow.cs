using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    public sealed class GachaBannerDatabaseWindow : EditorWindow
    {
        enum BannerFilter
        {
            All,
            Standard,
            Pickup,
            Event
        }

        const float ListWidth = 315f;

        GachaBannerDatabase database;
        CharacterDatabase characterDatabase;
        GachaBannerData selected;
        SerializedObject selectedObject;
        Vector2 listScroll;
        Vector2 detailScroll;
        string search = string.Empty;
        string simulationSummary = string.Empty;
        BannerFilter filter;
        bool hideEnded;

        [MenuItem("Starfall/Data/Gacha Banner Database")]
        public static void Open()
        {
            var window = GetWindow<GachaBannerDatabaseWindow>("Gacha Banner Database");
            window.minSize = new Vector2(980f, 650f);
            window.Show();
        }

        void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            Reload();
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        void OnGUI()
        {
            DrawHeader();
            if (database == null)
            {
                EditorGUILayout.HelpBox("GachaBannerDatabase를 불러오지 못했습니다.",
                    MessageType.Error);
                if (GUILayout.Button("데이터베이스 다시 만들기")) Reload();
                return;
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            DrawBannerList();
            DrawBannerDetail();
            EditorGUILayout.EndHorizontal();
        }

        void DrawHeader()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("STARFALL GACHA BANNER DATABASE", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "여러 모집 배너의 픽업, 절대 확률, 천장 공유 그룹과 UTC 기간을 관리합니다.",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(5f);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("＋ 새 배너", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                CreateBanner();
            using (new EditorGUI.DisabledScope(selected == null))
            {
                if (GUILayout.Button("복제", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                    DuplicateBanner();
                if (GUILayout.Button("삭제", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                    DeleteBanner();
            }
            if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(48f))) SaveAll();
            if (GUILayout.Button("검증", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                ContentValidationWindow.Open(GachaBannerValidation.ProviderId);
            if (GUILayout.Button("Legacy 변환", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                LegacyGachaConfigMigration.MigrateMenu();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("DB 찾기", EditorStyles.toolbarButton, GUILayout.Width(62f)))
                Ping(database);
            EditorGUILayout.EndHorizontal();
        }

        void DrawBannerList()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(ListWidth),
                GUILayout.ExpandHeight(true));
            int count = database.Banners != null ? database.Banners.Count : 0;
            EditorGUILayout.LabelField("배너 목록  " + count, EditorStyles.boldLabel);

            search = EditorGUILayout.TextField(search ?? string.Empty,
                EditorStyles.toolbarSearchField);
            EditorGUILayout.BeginHorizontal();
            filter = (BannerFilter)EditorGUILayout.EnumPopup(filter, GUILayout.Width(135f));
            hideEnded = GUILayout.Toggle(hideEnded, "종료 숨김", GUILayout.Width(90f));
            EditorGUILayout.EndHorizontal();

            listScroll = EditorGUILayout.BeginScrollView(listScroll,
                GUILayout.ExpandHeight(true));
            int visible = 0;
            if (database.Banners != null)
            {
                for (int i = 0; i < database.Banners.Count; i++)
                {
                    GachaBannerData banner = database.Banners[i];
                    if (!IsVisible(banner)) continue;
                    visible++;
                    DrawBannerRow(banner, i);
                }
            }
            if (visible == 0)
                EditorGUILayout.HelpBox(count == 0 ? "등록된 배너가 없습니다."
                    : "현재 검색/필터에 맞는 배너가 없습니다.", MessageType.Info);
            EditorGUILayout.EndScrollView();

            using (new EditorGUI.DisabledScope(selected == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("▲ 위")) MoveSelected(-1);
                if (GUILayout.Button("▼ 아래")) MoveSelected(1);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        void DrawBannerRow(GachaBannerData banner, int index)
        {
            if (banner == null)
            {
                EditorGUILayout.HelpBox((index + 1) + "번 배너 참조가 비어 있습니다.",
                    MessageType.Error);
                return;
            }

            ScheduleState state = banner.GetScheduleState(ContentTime.UtcNow);
            bool active = state == ScheduleState.Active;
            Color previous = GUI.backgroundColor;
            if (banner == selected) GUI.backgroundColor = new Color(.38f, .76f, 1f);
            else if (active) GUI.backgroundColor = new Color(.55f, .88f, .58f);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUI.backgroundColor = previous;
            if (GUILayout.Button((index + 1).ToString("00") + "  " + banner.BannerTitle,
                EditorStyles.boldLabel)) SelectBanner(banner);
            EditorGUILayout.LabelField(banner.Id + "  ·  " + banner.BannerType,
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField("기간  " + StateLabel(state)
                + "  ·  천장 " + banner.PityGroupId, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        void DrawBannerDetail()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (selected == null)
            {
                EditorGUILayout.HelpBox("왼쪽에서 배너를 선택하거나 새 배너를 만드세요.",
                    MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EnsureSelectedObject();
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll,
                GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(selected.BannerTitle, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("현재 상태: "
                + StateLabel(selected.GetScheduleState(ContentTime.UtcNow)),
                EditorStyles.miniBoldLabel, GUILayout.Width(150f));
            EditorGUILayout.EndHorizontal();

            selectedObject.Update();
            DrawSection("기본", "bannerId", "bannerType", "bannerTitle",
                "bannerSubtitle", "bannerImage");
            DrawSection("기간", "schedule");
            DrawSection("절대 확률 (%)", "topRarityRatePercent",
                "featuredSharePercent", "fourStarRatePercent");
            DrawRatePreview();
            DrawSection("천장", "pityGroupId", "hardPity", "softPityStart",
                "softPityBonusPerPullPercent", "guaranteeFeaturedAfterMiss",
                "guaranteeFourStarOnTenPull");
            DrawSection("비용", "ticketItemId", "singlePullCost", "tenPullCost");
            DrawPickupSection();
            bool changed = selectedObject.ApplyModifiedProperties();
            if (changed)
            {
                EditorUtility.SetDirty(selected);
                simulationSummary = string.Empty;
            }

            DrawInlineValidation();
            DrawTools();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawSection(string title, params string[] propertyNames)
        {
            EditorGUILayout.Space(7f);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            for (int i = 0; i < propertyNames.Length; i++)
            {
                SerializedProperty property = selectedObject.FindProperty(propertyNames[i]);
                if (property != null) EditorGUILayout.PropertyField(property, true);
            }
            EditorGUILayout.EndVertical();
        }

        void DrawInlineValidation()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("빠른 검증", EditorStyles.boldLabel);
            int issueCount = 0;

            if (string.IsNullOrWhiteSpace(selected.Id))
            {
                EditorGUILayout.HelpBox("bannerId를 입력하세요.", MessageType.Error);
                issueCount++;
            }
            else if (HasDuplicateBannerId(selected.Id))
            {
                EditorGUILayout.HelpBox("같은 bannerId가 데이터베이스에 이미 있습니다.",
                    MessageType.Error);
                issueCount++;
            }

            if (selected.Schedule != null && !selected.Schedule.IsValid)
            {
                EditorGUILayout.HelpBox("시작/종료 UTC 기간이 올바르지 않습니다.",
                    MessageType.Error);
                issueCount++;
            }
            if (selected.TopRarityRatePercent <= 0f
                || selected.TopRarityRatePercent + selected.FourStarRatePercent > 100.0001f)
            {
                EditorGUILayout.HelpBox("5★와 4★ 절대 확률의 합을 확인하세요.",
                    MessageType.Error);
                issueCount++;
            }

            if (selected.BannerType != GachaBannerType.Standard
                && (selected.PickupCharacters == null || selected.PickupCharacters.Count == 0))
            {
                EditorGUILayout.HelpBox("5★ 이상 픽업 캐릭터를 한 명 이상 등록하세요.",
                    MessageType.Error);
                issueCount++;
            }
            else
            {
                for (int i = 0; i < selected.PickupCharacters.Count; i++)
                {
                    CharacterData pickup = selected.PickupCharacters[i];
                    if (pickup != null && pickup.Rarity >= 5) continue;
                    EditorGUILayout.HelpBox("비어 있거나 5★ 미만인 픽업 참조가 있습니다.",
                        MessageType.Error);
                    issueCount++;
                    break;
                }
            }

            if (characterDatabase == null)
            {
                EditorGUILayout.HelpBox("CharacterDatabase를 찾을 수 없습니다.",
                    MessageType.Warning);
                issueCount++;
            }
            if (issueCount == 0)
                EditorGUILayout.HelpBox("빠른 검증을 통과했습니다.", MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        void DrawRatePreview()
        {
            SerializedProperty top = selectedObject.FindProperty("topRarityRatePercent");
            SerializedProperty featured = selectedObject.FindProperty("featuredSharePercent");
            SerializedProperty four = selectedObject.FindProperty("fourStarRatePercent");
            if (top == null || featured == null || four == null) return;
            float threeRate = Mathf.Max(0f, 100f - top.floatValue - four.floatValue);
            float selectedRate = top.floatValue * featured.floatValue / 100f;
            EditorGUILayout.HelpBox("5★ " + top.floatValue.ToString("0.###")
                + "%  ·  4★ " + four.floatValue.ToString("0.###")
                + "%  ·  3★ " + threeRate.ToString("0.###")
                + "%\n선택 픽업 절대 확률 " + selectedRate.ToString("0.###") + "%",
                MessageType.Info);
        }

        void DrawPickupSection()
        {
            EditorGUILayout.Space(7f);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("픽업 캐릭터", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("5★ 이상 모두 추가", GUILayout.Width(115f)))
                AddAllTopRarityCharacters();
            EditorGUILayout.EndHorizontal();

            SerializedProperty pickups = selectedObject.FindProperty("pickupCharacters");
            if (pickups != null) EditorGUILayout.PropertyField(pickups, true);
            if (selected.PickupCharacters != null)
            {
                for (int i = 0; i < selected.PickupCharacters.Count; i++)
                {
                    CharacterData pickup = selected.PickupCharacters[i];
                    if (pickup == null) continue;
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(pickup.DisplayName + "  " + pickup.Rarity + "★",
                        EditorStyles.miniLabel);
                    if (GUILayout.Button("열기", GUILayout.Width(45f))) Ping(pickup);
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndVertical();
        }

        void DrawTools()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("시뮬레이션 및 검증", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("현재 풀에 맞게 확률 보정")) FixRatesForCurrentPool();
            if (GUILayout.Button("10,000회 시뮬레이션")) Simulate(10000);
            if (GUILayout.Button("100,000회 시뮬레이션")) Simulate(100000);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("전체 배너 검증"))
                ContentValidationWindow.Open(GachaBannerValidation.ProviderId);
            if (GUILayout.Button("Project 창에서 배너 찾기")) Ping(selected);
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrWhiteSpace(simulationSummary))
                EditorGUILayout.HelpBox(simulationSummary, MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        void CreateBanner()
        {
            GachaBannerDatabaseBootstrap.EnsureFolders();
            var banner = ScriptableObject.CreateInstance<GachaBannerData>();
            banner.name = "New Gacha Banner";
            var serialized = new SerializedObject(banner);
            serialized.FindProperty("bannerId").stringValue = NewBannerId();
            serialized.FindProperty("bannerTitle").stringValue = "새 모집 배너";
            serialized.FindProperty("bannerSubtitle").stringValue = "NEW RECRUITMENT";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            string path = AssetDatabase.GenerateUniqueAssetPath(
                GachaBannerDatabaseBootstrap.BannerFolder + "/GachaBanner.asset");
            AssetDatabase.CreateAsset(banner, path);
            Undo.RegisterCreatedObjectUndo(banner, "Create gacha banner");
            Undo.RecordObject(database, "Register gacha banner");
            database.Add(banner);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            SelectBanner(banner);
            Ping(banner);
        }

        void DuplicateBanner()
        {
            if (selected == null) return;
            GachaBannerDatabaseBootstrap.EnsureFolders();
            GachaBannerData duplicate = UnityEngine.Object.Instantiate(selected);
            duplicate.name = selected.name + " Copy";
            var serialized = new SerializedObject(duplicate);
            serialized.FindProperty("bannerId").stringValue = NewBannerId();
            SerializedProperty title = serialized.FindProperty("bannerTitle");
            title.stringValue = title.stringValue + " 복사본";
            SerializedProperty source = serialized.FindProperty("legacySource");
            if (source != null) source.objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            string path = AssetDatabase.GenerateUniqueAssetPath(
                GachaBannerDatabaseBootstrap.BannerFolder + "/GachaBanner.asset");
            AssetDatabase.CreateAsset(duplicate, path);
            Undo.RegisterCreatedObjectUndo(duplicate, "Duplicate gacha banner");
            Undo.RecordObject(database, "Register duplicated gacha banner");
            database.Add(duplicate);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            SelectBanner(duplicate);
            Ping(duplicate);
        }

        void DeleteBanner()
        {
            if (selected == null) return;
            if (!EditorUtility.DisplayDialog("배너 삭제",
                "'" + selected.BannerTitle + "' 배너 에셋을 삭제할까요?\n이 작업은 Undo로 복원할 수 있습니다.",
                "삭제", "취소")) return;

            int index = database.IndexOf(selected);
            GachaBannerData target = selected;
            Undo.RecordObject(database, "Delete gacha banner");
            database.Remove(target);
            EditorUtility.SetDirty(database);
            SelectBanner(null);
            Undo.DestroyObjectImmediate(target);
            AssetDatabase.SaveAssets();
            if (database.Banners.Count > 0)
                SelectBanner(database.Banners[Mathf.Clamp(index, 0, database.Banners.Count - 1)]);
            GUIUtility.ExitGUI();
        }

        void MoveSelected(int direction)
        {
            int from = database.IndexOf(selected);
            int to = from + direction;
            if (from < 0 || to < 0 || to >= database.Banners.Count) return;
            Undo.RecordObject(database, "Reorder gacha banner");
            if (!database.Move(from, to)) return;
            EditorUtility.SetDirty(database);
            Repaint();
        }

        void AddAllTopRarityCharacters()
        {
            if (selected == null || characterDatabase == null
                || characterDatabase.Characters == null) return;
            Undo.RecordObject(selected, "Add top-rarity pickup characters");
            for (int i = 0; i < characterDatabase.Characters.Count; i++)
            {
                CharacterData character = characterDatabase.Characters[i];
                if (character != null && character.Rarity >= 5) selected.AddPickup(character);
            }
            EditorUtility.SetDirty(selected);
            RebuildSelectedObject();
        }

        void FixRatesForCurrentPool()
        {
            if (selected == null || characterDatabase == null)
            {
                EditorUtility.DisplayDialog("확률 보정", "CharacterDatabase가 없습니다.", "확인");
                return;
            }

            GetPoolAvailability(out bool hasTop, out bool hasFour, out bool hasThree);
            if (!hasTop || !hasFour && !hasThree)
            {
                EditorUtility.DisplayDialog("확률 보정",
                    "5★ 풀과 최소 한 종류의 하위 등급 풀이 필요합니다.", "확인");
                return;
            }

            Undo.RecordObject(selected, "Fix gacha banner rates");
            EnsureSelectedObject();
            selectedObject.Update();
            SerializedProperty top = selectedObject.FindProperty("topRarityRatePercent");
            SerializedProperty four = selectedObject.FindProperty("fourStarRatePercent");
            SerializedProperty guarantee =
                selectedObject.FindProperty("guaranteeFourStarOnTenPull");
            if (!hasThree && hasFour) four.floatValue = Mathf.Max(0f, 100f - top.floatValue);
            else if (!hasFour && hasThree)
            {
                four.floatValue = 0f;
                guarantee.boolValue = false;
            }
            else four.floatValue = Mathf.Clamp(four.floatValue, 0f, 100f - top.floatValue);
            selectedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(selected);
            simulationSummary = "현재 캐릭터 등급 풀에 맞춰 절대 확률을 보정했습니다.";
        }

        void Simulate(int samples)
        {
            if (selected == null) return;
            var random = new System.Random(7919 + samples);
            int pity = 0;
            int top = 0;
            int four = 0;
            int three = 0;
            int featured = 0;
            bool featuredGuaranteed = false;
            bool hasFourOrHigherInTen = false;
            for (int i = 0; i < samples; i++)
            {
                int nextPull = pity + 1;
                float topRate = selected.TopRarityRatePercent;
                if (nextPull >= selected.SoftPityStart)
                    topRate += (nextPull - selected.SoftPityStart + 1)
                        * selected.SoftPityBonusPerPullPercent;
                bool forceTop = nextPull >= selected.HardPity;
                bool lastOfTen = i % 10 == 9;
                bool forceFour = lastOfTen && selected.GuaranteeFourStarOnTenPull
                    && !hasFourOrHigherInTen;
                int rarity = forceTop ? 5 : GachaService.SelectRarity(
                    random.NextDouble() * 100d, topRate,
                    selected.FourStarRatePercent, forceFour);
                if (rarity >= 5)
                {
                    top++;
                    pity = 0;
                    bool isFeatured = featuredGuaranteed
                        || random.NextDouble() * 100d < selected.FeaturedSharePercent;
                    if (isFeatured)
                    {
                        featured++;
                        featuredGuaranteed = false;
                    }
                    else if (selected.GuaranteeFeaturedAfterMiss)
                    {
                        featuredGuaranteed = true;
                    }
                }
                else
                {
                    pity++;
                    if (rarity == 4) four++;
                    else three++;
                }
                if (rarity >= 4) hasFourOrHigherInTen = true;
                if (lastOfTen) hasFourOrHigherInTen = false;
            }

            simulationSummary = samples.ToString("N0") + "회 결정적 시뮬레이션\n"
                + "5★ " + top.ToString("N0") + " (" + Percent(top, samples) + ")  ·  "
                + "4★ " + four.ToString("N0") + " (" + Percent(four, samples) + ")  ·  "
                + "3★ " + three.ToString("N0") + " (" + Percent(three, samples) + ")\n"
                + "선택 픽업 " + featured.ToString("N0") + " (전체 "
                + Percent(featured, samples) + ", 5★ 중 " + Percent(featured, top) + ")";
        }

        bool IsVisible(GachaBannerData banner)
        {
            if (banner == null) return true;
            if (filter != BannerFilter.All
                && (int)banner.BannerType != (int)filter - 1) return false;
            if (hideEnded && banner.GetScheduleState(ContentTime.UtcNow) == ScheduleState.Ended)
                return false;
            if (string.IsNullOrWhiteSpace(search)) return true;
            string needle = search.Trim();
            return banner.Id.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
                || banner.BannerTitle.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        bool HasDuplicateBannerId(string bannerId)
        {
            if (database == null || string.IsNullOrWhiteSpace(bannerId)) return false;
            for (int i = 0; i < database.Banners.Count; i++)
            {
                GachaBannerData banner = database.Banners[i];
                if (banner != null && banner != selected && banner.Id == bannerId) return true;
            }
            return false;
        }

        void GetPoolAvailability(out bool hasTop, out bool hasFour, out bool hasThree)
        {
            hasTop = hasFour = hasThree = false;
            if (characterDatabase == null || characterDatabase.Characters == null) return;
            for (int i = 0; i < characterDatabase.Characters.Count; i++)
            {
                CharacterData character = characterDatabase.Characters[i];
                if (character == null) continue;
                if (character.Rarity >= 5) hasTop = true;
                else if (character.Rarity == 4) hasFour = true;
                else if (character.Rarity == 3) hasThree = true;
            }
        }

        void SelectBanner(GachaBannerData banner)
        {
            selected = banner;
            detailScroll = Vector2.zero;
            simulationSummary = string.Empty;
            RebuildSelectedObject();
            Repaint();
        }

        void Reload()
        {
            database = GachaBannerDatabaseBootstrap.LoadOrCreate();
            characterDatabase = GachaBannerDatabaseBootstrap.LoadCharacterDatabase();
            if (database == null)
            {
                selected = null;
                RebuildSelectedObject();
                Repaint();
                return;
            }
            if (selected != null && database.IndexOf(selected) < 0) selected = null;
            if (selected == null && database.Banners != null && database.Banners.Count > 0)
                selected = database.Banners[0];
            RebuildSelectedObject();
            Repaint();
        }

        void SaveAll()
        {
            if (selectedObject != null) selectedObject.ApplyModifiedProperties();
            if (selected != null) EditorUtility.SetDirty(selected);
            if (database != null) EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }

        void OnUndoRedo()
        {
            RebuildSelectedObject();
            Repaint();
        }

        void EnsureSelectedObject()
        {
            if (selectedObject == null || selectedObject.targetObject != selected)
                RebuildSelectedObject();
        }

        void RebuildSelectedObject()
        {
            selectedObject = selected != null ? new SerializedObject(selected) : null;
        }

        static string NewBannerId() =>
            "banner_" + Guid.NewGuid().ToString("N").Substring(0, 12);

        static string Percent(int count, int total) => total <= 0 ? "0%"
            : (count * 100f / total).ToString("0.###") + "%";

        static string StateLabel(ScheduleState state)
        {
            switch (state)
            {
                case ScheduleState.Upcoming: return "예정";
                case ScheduleState.Active: return "진행 중";
                case ScheduleState.Ended: return "종료";
                default: return "기간 오류";
            }
        }

        static void Ping(UnityEngine.Object target)
        {
            if (target == null) return;
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }
    }
}
