using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.EditorTools
{
    public sealed class PlayerDataViewerWindow : EditorWindow
    {
        enum DataTab
        {
            Profile,
            Wallet,
            Characters,
            Equipment,
            Formation,
            Gacha,
            Attendance,
            Mail,
            WeeklyBoss,
            Tower,
            Missions
        }

        static readonly string[] TabNames =
        {
            "Profile", "Wallet", "Characters", "Equipment", "Formation", "Gacha",
            "Attendance", "Mail", "Weekly Boss", "Tower", "Missions"
        };

        const string ProfileLevelKey = "StarfallAcademy.Meta.Profile.AccountLevel";
        const string ProfileExperienceKey = "StarfallAcademy.Meta.Profile.AccountExperience";
        const string PremiumKey = "StarfallAcademy.PremiumCurrency";
        const string CreditsKey = "StarfallAcademy.Credits";
        const string MaterialsKey = "StarfallAcademy.SkillMaterials";
        const string OwnedPrefix = "StarfallAcademy.Character.Owned.";
        const string LevelPrefix = "StarfallAcademy.Character.Level.";
        const string SkillPrefix = "StarfallAcademy.Character.SkillLevel.";
        const string AwakeningPrefix = "StarfallAcademy.Character.AwakeningStage.";
        const string MissionDayKey = "StarfallAcademy.Meta.Mission.UtcDay";
        const string MissionProgressPrefix = "StarfallAcademy.Meta.Mission.Progress.";
        const string MissionClaimedPrefix = "StarfallAcademy.Meta.Mission.Claimed.";
        const string ChangeKeyPrefix = "StarfallAcademy.PlayerDataViewer.LastChanged.";

        DataTab tab;
        Vector2 pageScroll;
        Vector2 jsonScroll;
        string json = string.Empty;
        string status = string.Empty;
        MessageType statusType = MessageType.Info;

        [MenuItem("Starfall/Debug/Player Data Viewer", priority = 300)]
        static void Open()
        {
            PlayerDataViewerWindow window = GetWindow<PlayerDataViewerWindow>("Player Data");
            window.minSize = new Vector2(920f, 620f);
            window.RefreshJson();
        }

        void OnEnable() => RefreshJson();

        void OnGUI()
        {
            DrawTitle();
            DataTab next = (DataTab)GUILayout.Toolbar((int)tab, TabNames,
                GUILayout.Height(25f));
            if (next != tab)
            {
                tab = next;
                pageScroll = jsonScroll = Vector2.zero;
                status = string.Empty;
                RefreshJson();
            }

            pageScroll = EditorGUILayout.BeginScrollView(pageScroll);
            DrawPlayerDataHeader();
            DrawContentReference();
            DrawOverview();
            DrawMetadata();
            DrawValidationWarnings();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("테스트 값 / JSON", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "아래 JSON은 플레이어 저장 데이터 사본입니다. 값을 편집한 뒤 적용하면 테스트 값을 만들 수 있습니다.",
                MessageType.None);
            jsonScroll = EditorGUILayout.BeginScrollView(jsonScroll, GUILayout.MinHeight(190f),
                GUILayout.MaxHeight(360f));
            json = EditorGUILayout.TextArea(json ?? string.Empty, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            DrawFeatureActions();
            if (!string.IsNullOrWhiteSpace(status))
                EditorGUILayout.HelpBox(status, statusType);
            EditorGUILayout.Space(12f);
            DrawWholeSaveActions();
            EditorGUILayout.EndScrollView();
        }

        void DrawTitle()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Starfall Player Data Viewer", new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18
            });
            if (EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Play Mode 중입니다. 변경 시 실행 중인 서비스 캐시와 값이 달라질 수 있습니다.",
                    MessageType.Warning);
        }

        static void DrawPlayerDataHeader()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 34f);
            EditorGUI.DrawRect(rect, new Color(.10f, .25f, .42f, 1f));
            GUI.Label(new Rect(rect.x + 10f, rect.y + 7f, rect.width - 20f, 20f),
                "PLAYER DATA  ·  PlayerPrefs 기반 개인 진행값 (콘텐츠 에셋과 분리)",
                EditorStyles.whiteBoldLabel);
        }

        void DrawContentReference()
        {
            UnityEngine.Object reference = GetContentReference(tab);
            if (reference == null && tab is DataTab.Profile or DataTab.Wallet or DataTab.Mail
                or DataTab.Missions) return;
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, .78f, .28f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = previous;
            EditorGUILayout.LabelField("CONTENT ASSET (읽기 전용 참조)", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField(reference, typeof(UnityEngine.Object), false);
            if (reference == null)
                EditorGUILayout.HelpBox("연결할 콘텐츠 데이터베이스가 없습니다. 저장값 참조 검증이 제한됩니다.",
                    MessageType.Warning);
            EditorGUILayout.EndVertical();
        }

        void DrawOverview()
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("현재 저장값", EditorStyles.boldLabel);
            switch (tab)
            {
                case DataTab.Profile:
                    Label("Account Level", PlayerProfileService.CurrentLevel.ToString());
                    Label("Experience", PlayerProfileService.CurrentExperience.ToString("N0"));
                    break;
                case DataTab.Wallet:
                    Label(PlayerWallet.PremiumCurrencyDisplayName, PlayerWallet.PremiumCurrency.ToString("N0"));
                    Label("Credits", PlayerWallet.Credits.ToString("N0"));
                    Label(PlayerWallet.SkillMaterialDisplayName, PlayerWallet.SkillMaterials.ToString("N0"));
                    break;
                case DataTab.Characters:
                    DrawCharacterOverview();
                    break;
                case DataTab.Equipment:
                    DrawEquipmentOverview();
                    break;
                case DataTab.Formation:
                    DrawFormationOverview();
                    break;
                case DataTab.Gacha:
                    DrawGachaOverview();
                    break;
                case DataTab.Attendance:
                    DrawAttendanceOverview();
                    break;
                case DataTab.Mail:
                    DrawMailOverview();
                    break;
                case DataTab.WeeklyBoss:
                    DrawWeeklyBossOverview();
                    break;
                case DataTab.Tower:
                    DrawTowerOverview();
                    break;
                case DataTab.Missions:
                    DrawMissionOverview();
                    break;
            }
        }

        void DrawMetadata()
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("저장 메타데이터", EditorStyles.boldLabel);
            Label("Feature schema", "1");
            Label("Global save version", PlayerSaveVersion.StoredVersion + " / "
                + PlayerSaveVersion.LatestVersion);
            Label("Last changed", ResolveLastChanged());
            EditorGUILayout.LabelField("Related PlayerPrefs keys", EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel(RelatedKeys(tab), EditorStyles.textArea,
                GUILayout.MinHeight(42f), GUILayout.MaxHeight(86f));
        }

        void DrawFeatureActions()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("현재 값 다시 읽기", GUILayout.Height(26f))) RefreshJson();
            if (GUILayout.Button("JSON 파일 내보내기", GUILayout.Height(26f))) ExportCurrent();
            if (GUILayout.Button("JSON 파일 가져오기", GUILayout.Height(26f))) ImportCurrentFile();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("편집한 JSON 적용", GUILayout.Height(28f))) ApplyEditedJson();
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, .47f, .36f);
            if (GUILayout.Button("이 기능만 초기화", GUILayout.Height(28f))) ResetCurrent();
            GUI.backgroundColor = previous;
            EditorGUILayout.EndHorizontal();
        }

        void DrawWholeSaveActions()
        {
            EditorGUILayout.LabelField("전체 저장 안전 도구", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "11개 기능의 알려진 키와 manifest에 등록된 동적 아이템·보상 거래 키를 함께 백업/초기화합니다. 다른 플러그인 설정을 보호하기 위해 PlayerPrefs.DeleteAll은 사용하지 않습니다.",
                MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("전체 저장 백업", GUILayout.Height(30f)))
            {
                string path = WriteBackup("manual");
                SetStatus("백업 완료: " + path, MessageType.Info);
            }
            if (GUILayout.Button("전체 백업 가져오기", GUILayout.Height(30f))) ImportFullBackup();
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(.95f, .32f, .28f);
            if (GUILayout.Button("전체 저장 초기화", GUILayout.Height(30f))) ResetAllKnownData();
            GUI.backgroundColor = previous;
            EditorGUILayout.EndHorizontal();
        }

        void ApplyEditedJson()
        {
            if (!ConfirmMutation("편집한 JSON을 " + TabNames[(int)tab] + " 저장값에 적용할까요?")) return;
            string previous = CaptureJson(tab);
            if (TryApplyJson(tab, json, out string error))
            {
                MarkChanged(tab);
                RefreshJson();
                SetStatus("JSON을 적용했습니다.", MessageType.Info);
            }
            else
            {
                bool restored = TryApplyJson(tab, previous, out string rollbackError);
                RefreshJson();
                SetStatus("적용 실패: " + error + (restored ? "\n이전 저장값을 복원했습니다."
                    : "\n복원도 실패했습니다: " + rollbackError), MessageType.Error);
            }
        }

        void ResetCurrent()
        {
            if (!ConfirmMutation(TabNames[(int)tab] + " 저장값만 초기화할까요?")) return;
            string previous = CaptureJson(tab);
            try
            {
                ResetTab(tab);
                MarkChanged(tab);
                RefreshJson();
                SetStatus("선택한 기능을 초기화했습니다.", MessageType.Info);
            }
            catch (Exception exception)
            {
                bool restored = TryApplyJson(tab, previous, out string rollbackError);
                RefreshJson();
                SetStatus("초기화 실패: " + exception.Message
                    + (restored ? "\n이전 저장값을 복원했습니다."
                        : "\n복원도 실패했습니다: " + rollbackError), MessageType.Error);
            }
        }

        void ExportCurrent()
        {
            string path = EditorUtility.SaveFilePanel("Export Player Data", string.Empty,
                "starfall-" + TabNames[(int)tab].Replace(" ", "-").ToLowerInvariant() + ".json", "json");
            if (string.IsNullOrWhiteSpace(path)) return;
            File.WriteAllText(path, CaptureJson(tab));
            SetStatus("내보내기 완료: " + path, MessageType.Info);
        }

        void ImportCurrentFile()
        {
            string path = EditorUtility.OpenFilePanel("Import Player Data", string.Empty, "json");
            if (string.IsNullOrWhiteSpace(path)) return;
            json = File.ReadAllText(path);
            ApplyEditedJson();
        }

        void ImportFullBackup()
        {
            string path = EditorUtility.OpenFilePanel("Import Full Player Backup", BackupDirectory(), "json");
            if (string.IsNullOrWhiteSpace(path)) return;
            if (!ConfirmMutation("전체 플레이어 백업을 가져올까요? 현재 값은 자동 백업됩니다.")) return;
            FullBackup previous = CaptureBackup("rollback-before-import");
            try
            {
                FullBackup backup = JsonUtility.FromJson<FullBackup>(File.ReadAllText(path));
                if (backup == null) throw new InvalidOperationException("백업 JSON 형식이 올바르지 않습니다.");
                foreach (DataTab value in Enum.GetValues(typeof(DataTab)))
                {
                    string payload = backup.Get(value);
                    if (!string.IsNullOrWhiteSpace(payload)
                        && !TryApplyJson(value, payload, out string error))
                        throw new InvalidOperationException(TabNames[(int)value] + ": " + error);
                    MarkChanged(value);
                }
                if (backup.version >= 2 && backup.dynamicKeys != null)
                    ApplyDynamicKeys(backup.dynamicKeys);
                PlayerPrefs.SetInt(PlayerSaveVersion.VersionKey,
                    Mathf.Clamp(backup.saveVersion, 0, PlayerSaveVersion.LatestVersion));
                PlayerPrefs.SetString(PlayerSaveVersion.LastMigrationUtcKey,
                    backup.lastMigrationUtc ?? string.Empty);
                PlayerPrefs.Save();
                RefreshJson();
                SetStatus("전체 백업을 가져왔습니다.", MessageType.Info);
            }
            catch (Exception exception)
            {
                bool restored = RestoreBackup(previous, out string rollbackError);
                RefreshJson();
                SetStatus("백업 가져오기 실패: " + exception.Message
                    + (restored ? "\n이전 전체 저장값을 복원했습니다."
                        : "\n전체 복원도 실패했습니다: " + rollbackError), MessageType.Error);
            }
        }

        void ResetAllKnownData()
        {
            if (!ConfirmMutation("관리 중인 모든 플레이어 저장값을 초기화할까요?")) return;
            FullBackup previous = CaptureBackup("rollback-before-reset");
            try
            {
                foreach (DataTab value in Enum.GetValues(typeof(DataTab)))
                {
                    ResetTab(value);
                    MarkChanged(value);
                }
                ResetDynamicKeys();
                PlayerSaveVersion.ResetVersionMarker();
                RefreshJson();
                SetStatus("알려진 플레이어 저장값을 모두 초기화했습니다.", MessageType.Info);
            }
            catch (Exception exception)
            {
                bool restored = RestoreBackup(previous, out string rollbackError);
                RefreshJson();
                SetStatus("전체 초기화 실패: " + exception.Message
                    + (restored ? "\n이전 전체 저장값을 복원했습니다."
                        : "\n전체 복원도 실패했습니다: " + rollbackError), MessageType.Error);
            }
        }

        bool ConfirmMutation(string message)
        {
            if (EditorApplication.isPlaying
                && !EditorUtility.DisplayDialog("Play Mode 변경 경고",
                    "실행 중인 서비스 캐시와 저장값이 달라질 수 있습니다. 계속할까요?", "계속", "취소"))
                return false;
            if (!EditorUtility.DisplayDialog("Player Data 변경 확인", message, "변경", "취소"))
                return false;
            try { WriteBackup("automatic"); }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("자동 백업 실패",
                    "변경을 중단했습니다.\n" + exception.Message, "확인");
                return false;
            }
            return true;
        }

        void RefreshJson()
        {
            try
            {
                json = CaptureJson(tab);
                Repaint();
            }
            catch (Exception exception)
            {
                json = string.Empty;
                SetStatus("저장값을 읽지 못했습니다: " + exception.Message, MessageType.Error);
            }
        }

        string CaptureJson(DataTab value)
        {
            switch (value)
            {
                case DataTab.Profile: return JsonUtility.ToJson(new ProfileSnapshot
                {
                    level = PlayerProfileService.CurrentLevel,
                    experience = PlayerProfileService.CurrentExperience
                }, true);
                case DataTab.Wallet: return JsonUtility.ToJson(new WalletSnapshot
                {
                    premiumCurrency = PlayerWallet.PremiumCurrency,
                    credits = PlayerWallet.Credits,
                    skillMaterials = PlayerWallet.SkillMaterials
                }, true);
                case DataTab.Characters: return CaptureCharacters();
                case DataTab.Equipment: return CaptureEquipment();
                case DataTab.Formation: return FormationPresetService.Default.ExportJson();
                case DataTab.Gacha: return CaptureGacha();
                case DataTab.Attendance:
                    return JsonUtility.ToJson(AttendanceService.Default.CaptureSnapshot(AttendanceDatabase()), true);
                case DataTab.Mail: return JsonUtility.ToJson(MailService.Default.CaptureSnapshot(), true);
                case DataTab.WeeklyBoss:
                    return WeeklyBossService.Default.ExportPlayerDataJson(WeeklyBossDatabase());
                case DataTab.Tower:
                    return TowerProgressService.Default.ExportPlayerDataJson(TowerDatabase());
                case DataTab.Missions: return CaptureMissions();
                default: return "{}";
            }
        }

        bool TryApplyJson(DataTab value, string payload, out string error)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(payload)) throw new InvalidOperationException("JSON이 비어 있습니다.");
                switch (value)
                {
                    case DataTab.Profile: ApplyProfile(JsonUtility.FromJson<ProfileSnapshot>(payload)); break;
                    case DataTab.Wallet: ApplyWallet(JsonUtility.FromJson<WalletSnapshot>(payload)); break;
                    case DataTab.Characters: ApplyCharacters(JsonUtility.FromJson<CharacterSnapshot>(payload)); break;
                    case DataTab.Equipment:
                        return TryApplyEquipment(payload, out error);
                    case DataTab.Formation:
                        return FormationPresetService.Default.TryImportJson(payload, CharacterDatabase(), out error);
                    case DataTab.Gacha: ApplyGacha(JsonUtility.FromJson<GachaSnapshot>(payload)); break;
                    case DataTab.Attendance:
                        if (!AttendanceService.Default.RestoreSnapshot(
                            JsonUtility.FromJson<AttendanceSaveSnapshot>(payload), AttendanceDatabase()))
                            throw new InvalidOperationException("출석 저장값 또는 콘텐츠 참조가 올바르지 않습니다.");
                        break;
                    case DataTab.Mail:
                        if (!MailService.Default.RestoreSnapshot(JsonUtility.FromJson<MailSaveSnapshot>(payload)))
                            throw new InvalidOperationException("우편 저장값이 올바르지 않습니다.");
                        break;
                    case DataTab.WeeklyBoss:
                        return WeeklyBossService.Default.TryImportPlayerDataJson(
                            WeeklyBossDatabase(), payload, out error);
                    case DataTab.Tower:
                        return TowerProgressService.Default.TryImportPlayerDataJson(
                            TowerDatabase(), payload, out error);
                    case DataTab.Missions: ApplyMissions(JsonUtility.FromJson<MissionSnapshot>(payload)); break;
                }
                PlayerPrefs.Save();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        void ResetTab(DataTab value)
        {
            switch (value)
            {
                case DataTab.Profile: PlayerProfileService.Default.Reset(); break;
                case DataTab.Wallet: PlayerWallet.Reset(); break;
                case DataTab.Characters:
                    CharacterProgressionService.ResetAll(CharacterDatabase());
                    break;
                case DataTab.Equipment:
                    EquipmentInventoryService.Default.Reset();
                    CharacterDatabase characters = CharacterDatabase();
                    ResetLegacyEquipment(characters);
                    break;
                case DataTab.Formation: FormationPresetService.Default.Reset(); break;
                case DataTab.Gacha: ResetGacha(); break;
                case DataTab.Attendance: AttendanceService.Default.ResetAll(AttendanceDatabase()); break;
                case DataTab.Mail: MailService.Default.Reset(); break;
                case DataTab.WeeklyBoss: WeeklyBossService.Default.ResetPlayerData(WeeklyBossDatabase()); break;
                case DataTab.Tower: TowerProgressService.Default.ResetPlayerData(TowerDatabase()); break;
                case DataTab.Missions: MissionService.Default.Reset(); break;
            }
            PlayerPrefs.Save();
        }

        string WriteBackup(string reason)
        {
            string directory = BackupDirectory();
            Directory.CreateDirectory(directory);
            FullBackup backup = CaptureBackup(reason);
            string path = Path.Combine(directory, "player-data-"
                + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + reason + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(backup, true));
            return path;
        }

        FullBackup CaptureBackup(string reason)
        {
            var backup = new FullBackup
            {
                capturedAtUtc = DateTime.UtcNow.ToString("O"),
                reason = reason ?? string.Empty,
                saveVersion = PlayerSaveVersion.StoredVersion,
                lastMigrationUtc = PlayerSaveVersion.LastMigrationUtc,
                dynamicKeys = CaptureDynamicKeys()
            };
            foreach (DataTab value in Enum.GetValues(typeof(DataTab)))
                backup.Set(value, CaptureJson(value));
            return backup;
        }

        bool RestoreBackup(FullBackup backup, out string error)
        {
            var failures = new List<string>();
            if (backup == null)
            {
                error = "복원할 백업이 없습니다.";
                return false;
            }
            foreach (DataTab value in Enum.GetValues(typeof(DataTab)))
            {
                string payload = backup.Get(value);
                if (string.IsNullOrWhiteSpace(payload)) continue;
                if (!TryApplyJson(value, payload, out string sectionError))
                    failures.Add(TabNames[(int)value] + ": " + sectionError);
                else MarkChanged(value);
            }
            if (backup.version >= 2 && backup.dynamicKeys != null)
            {
                try { ApplyDynamicKeys(backup.dynamicKeys); }
                catch (Exception exception) { failures.Add("Dynamic keys: " + exception.Message); }
            }
            PlayerPrefs.SetInt(PlayerSaveVersion.VersionKey,
                Mathf.Clamp(backup.saveVersion, 0, PlayerSaveVersion.LatestVersion));
            PlayerPrefs.SetString(PlayerSaveVersion.LastMigrationUtcKey,
                backup.lastMigrationUtc ?? string.Empty);
            PlayerPrefs.Save();
            error = string.Join("\n", failures);
            return failures.Count == 0;
        }

        static string BackupDirectory() => Path.Combine(
            Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath,
            "Library", "StarfallAcademy", "PlayerDataBackups");

        static DynamicKeySnapshot CaptureDynamicKeys()
        {
            var snapshot = new DynamicKeySnapshot();
            var itemKeys = new HashSet<string>(
                PlayerDataKeyManifest.GetItemKeys(PlayerPrefsMetaStorage.Shared),
                StringComparer.Ordinal);
            foreach (string key in KnownItemStorageKeys())
                if (PlayerPrefs.HasKey(key)) itemKeys.Add(key);
            CaptureDynamicGroup(itemKeys, snapshot.itemInventory);
            CaptureDynamicGroup(
                PlayerDataKeyManifest.GetRewardTransactionKeys(PlayerPrefsMetaStorage.Shared),
                snapshot.rewardTransactions);
            return snapshot;
        }

        static void CaptureDynamicGroup(IEnumerable<string> keys, List<DynamicIntEntry> target)
        {
            foreach (string key in keys.Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
            {
                bool exists = PlayerPrefs.HasKey(key);
                target.Add(new DynamicIntEntry
                {
                    key = key,
                    exists = exists,
                    value = exists ? PlayerPrefs.GetInt(key, 0) : 0
                });
            }
        }

        static void ApplyDynamicKeys(DynamicKeySnapshot snapshot)
        {
            if (snapshot == null) throw new InvalidOperationException("동적 키 백업이 없습니다.");
            ValidateDynamicGroup(snapshot.itemInventory,
                PlayerDataKeyManifest.ItemKeyPrefix, false);
            ValidateDynamicGroup(snapshot.rewardTransactions,
                PlayerDataKeyManifest.RewardTransactionKeyPrefix, true);

            var currentItems = new HashSet<string>(
                PlayerDataKeyManifest.GetItemKeys(PlayerPrefsMetaStorage.Shared),
                StringComparer.Ordinal);
            foreach (string key in KnownItemStorageKeys())
                if (PlayerPrefs.HasKey(key)) currentItems.Add(key);
            ReplaceDynamicGroup(currentItems, snapshot.itemInventory);
            ReplaceDynamicGroup(
                PlayerDataKeyManifest.GetRewardTransactionKeys(PlayerPrefsMetaStorage.Shared),
                snapshot.rewardTransactions);
            PlayerDataKeyManifest.ReplaceItemKeys(PlayerPrefsMetaStorage.Shared,
                snapshot.itemInventory.Select(entry => entry.key));
            PlayerDataKeyManifest.ReplaceRewardTransactionKeys(PlayerPrefsMetaStorage.Shared,
                snapshot.rewardTransactions.Select(entry => entry.key));
            PlayerPrefs.Save();
        }

        static void ResetDynamicKeys()
        {
            var itemKeys = new HashSet<string>(
                PlayerDataKeyManifest.GetItemKeys(PlayerPrefsMetaStorage.Shared),
                StringComparer.Ordinal);
            foreach (string key in KnownItemStorageKeys())
                if (PlayerPrefs.HasKey(key)) itemKeys.Add(key);
            foreach (string key in itemKeys) PlayerPrefs.DeleteKey(key);
            foreach (string key in PlayerDataKeyManifest.GetRewardTransactionKeys(
                PlayerPrefsMetaStorage.Shared)) PlayerPrefs.DeleteKey(key);
            PlayerDataKeyManifest.ReplaceItemKeys(PlayerPrefsMetaStorage.Shared,
                Array.Empty<string>());
            PlayerDataKeyManifest.ReplaceRewardTransactionKeys(PlayerPrefsMetaStorage.Shared,
                Array.Empty<string>());
            PlayerPrefs.Save();
        }

        static void ValidateDynamicGroup(List<DynamicIntEntry> entries, string requiredPrefix,
            bool transactionValues)
        {
            if (entries == null) throw new InvalidOperationException("동적 키 목록이 없습니다.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                DynamicIntEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.key)
                    || !entry.key.StartsWith(requiredPrefix, StringComparison.Ordinal)
                    || !keys.Add(entry.key))
                    throw new InvalidOperationException("잘못되거나 중복된 동적 저장 키가 있습니다.");
                if (entry.exists && (entry.value < 0
                    || transactionValues && entry.value != 1))
                    throw new InvalidOperationException("동적 저장 키 값이 올바르지 않습니다: "
                        + entry.key);
            }
        }

        static void ReplaceDynamicGroup(IEnumerable<string> current,
            IEnumerable<DynamicIntEntry> restored)
        {
            var affected = new HashSet<string>(current ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            foreach (DynamicIntEntry entry in restored)
                if (entry != null) affected.Add(entry.key);
            foreach (string key in affected) PlayerPrefs.DeleteKey(key);
            foreach (DynamicIntEntry entry in restored)
                if (entry != null && entry.exists) PlayerPrefs.SetInt(entry.key, entry.value);
        }

        static IEnumerable<string> KnownItemStorageKeys()
        {
            var itemIds = new HashSet<string>(StringComparer.Ordinal)
            {
                "ticket:recruitment", "material:awakening", "material:enhancement"
            };
            CharacterDatabase characters = CharacterDatabase();
            if (characters != null)
                for (int i = 0; i < characters.Characters.Count; i++)
                    if (characters.Characters[i] != null)
                        itemIds.Add(ItemInventoryService.CharacterFragmentId(
                            characters.Characters[i].Id));

            AddRewardAssets<AttendanceCampaignData>(itemIds, campaign =>
            {
                for (int i = 0; i < campaign.Days.Count; i++)
                    AddRewardItemIds(itemIds, campaign.Days[i]?.Reward);
            });
            AddRewardAssets<MailTemplateData>(itemIds,
                template => AddRewardItemIds(itemIds, template.Attachments));
            AddRewardAssets<WeeklyBossDefinition>(itemIds, boss =>
            {
                for (int i = 0; i < boss.RewardTiers.Count; i++)
                    AddRewardItemIds(itemIds, boss.RewardTiers[i]?.Reward);
            });
            AddRewardAssets<TowerFloorData>(itemIds,
                floor => AddRewardItemIds(itemIds, floor.FirstClearReward));
            AddRewardAssets<TowerDatabase>(itemIds, database =>
            {
                for (int i = 0; i < database.CumulativeStarRewards.Count; i++)
                    AddRewardItemIds(itemIds, database.CumulativeStarRewards[i]?.Reward);
            });
            AddRewardAssets<StageData>(itemIds, stage =>
            {
                AddRewardItemIds(itemIds, stage.FirstClearRewardPackage);
                AddRewardItemIds(itemIds, stage.RepeatClearRewardPackage);
            });
            AddRewardAssets<GachaBannerData>(itemIds, banner =>
            {
                if (!string.IsNullOrWhiteSpace(banner.TicketItemId))
                    itemIds.Add(banner.TicketItemId);
            });
            return itemIds.Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(ItemInventoryService.StorageKeyFor);
        }

        static void AddRewardAssets<T>(ISet<string> itemIds, Action<T> visitor)
            where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            for (int i = 0; i < guids.Length; i++)
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (asset != null) visitor(asset);
            }
        }

        static void AddRewardItemIds(ISet<string> itemIds, RewardPackage reward)
        {
            if (reward?.ItemRewards == null) return;
            for (int i = 0; i < reward.ItemRewards.Count; i++)
            {
                ItemReward item = reward.ItemRewards[i];
                if (item != null && !string.IsNullOrWhiteSpace(item.ItemId))
                    itemIds.Add(item.ItemId);
            }
        }

        static void ApplyProfile(ProfileSnapshot value)
        {
            if (value == null) throw new InvalidOperationException("Profile JSON이 올바르지 않습니다.");
            int level = Mathf.Clamp(value.level, 1, PlayerProfileService.Default.MaximumLevel);
            int maximumExperience = level >= PlayerProfileService.Default.MaximumLevel ? 0
                : PlayerProfileService.Default.GetRequiredExperienceForNextLevel(level) - 1;
            PlayerPrefs.SetInt(ProfileLevelKey, level);
            PlayerPrefs.SetInt(ProfileExperienceKey, Mathf.Clamp(value.experience, 0, maximumExperience));
        }

        static void ApplyWallet(WalletSnapshot value)
        {
            if (value == null) throw new InvalidOperationException("Wallet JSON이 올바르지 않습니다.");
            PlayerPrefs.SetInt(PremiumKey, Mathf.Max(0, value.premiumCurrency));
            PlayerPrefs.SetInt(CreditsKey, Mathf.Max(0, value.credits));
            PlayerPrefs.SetInt(MaterialsKey, Mathf.Max(0, value.skillMaterials));
        }

        static string CaptureCharacters()
        {
            var snapshot = new CharacterSnapshot();
            CharacterDatabase database = CharacterDatabase();
            if (database != null)
            {
                for (int i = 0; i < database.Characters.Count; i++)
                {
                    CharacterData character = database.Characters[i];
                    if (character == null) continue;
                    snapshot.characters.Add(new CharacterEntry
                    {
                        characterId = character.Id,
                        owned = CharacterProgressionService.IsOwned(character),
                        level = CharacterProgressionService.GetLevel(character),
                        skillLevel = CharacterProgressionService.GetSkillLevel(character),
                        awakeningStage = CharacterAwakeningService.Default.GetStage(character),
                        fragments = CharacterAwakeningService.Default.GetFragments(character)
                    });
                }
            }
            return JsonUtility.ToJson(snapshot, true);
        }

        static void ApplyCharacters(CharacterSnapshot snapshot)
        {
            CharacterDatabase database = CharacterDatabase();
            if (snapshot == null || snapshot.characters == null || database == null)
                throw new InvalidOperationException("Characters JSON 또는 CharacterDatabase가 올바르지 않습니다.");
            var resolved = new List<KeyValuePair<CharacterEntry, CharacterData>>(snapshot.characters.Count);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < snapshot.characters.Count; i++)
            {
                CharacterEntry entry = snapshot.characters[i];
                CharacterData character = FindCharacter(database, entry?.characterId);
                if (character == null) throw new InvalidOperationException(
                    "존재하지 않는 캐릭터 ID: " + (entry?.characterId ?? "null"));
                if (!ids.Add(character.Id)) throw new InvalidOperationException(
                    "중복 캐릭터 ID: " + character.Id);
                resolved.Add(new KeyValuePair<CharacterEntry, CharacterData>(entry, character));
            }

            CharacterProgressionService.ResetAll(database);
            for (int i = 0; i < resolved.Count; i++)
            {
                CharacterEntry entry = resolved[i].Key;
                CharacterData character = resolved[i].Value;
                PlayerPrefs.SetInt(OwnedPrefix + character.Id, entry.owned ? 1 : 0);
                PlayerPrefs.SetInt(LevelPrefix + character.Id,
                    Mathf.Clamp(entry.level, character.Level, character.MaxLevel));
                PlayerPrefs.SetInt(SkillPrefix + character.Id,
                    Mathf.Clamp(entry.skillLevel, 1, character.SkillMaxLevel));
                PlayerPrefs.SetInt(AwakeningPrefix + character.Id,
                    Mathf.Clamp(entry.awakeningStage, 0, character.AwakeningStages.Count));
                string fragmentKey = ItemInventoryService.StorageKeyFor(
                    ItemInventoryService.CharacterFragmentId(character.Id));
                ItemInventoryService.TrackPlayerPrefsKey(fragmentKey);
                PlayerPrefs.SetInt(fragmentKey, Mathf.Max(0, entry.fragments));
            }
        }

        static string CaptureEquipment()
        {
            var snapshot = new EquipmentViewerSnapshot
            {
                inventoryJson = EquipmentInventoryService.Default.ExportJson(true)
            };
            CharacterDatabase database = CharacterDatabase();
            if (database != null)
            {
                for (int i = 0; i < database.Characters.Count; i++)
                {
                    CharacterData character = database.Characters[i];
                    if (character == null) continue;
                    foreach (EquipmentSlot slot in EquipmentService.Slots)
                    {
                        int level = EquipmentService.GetLevel(character, slot);
                        if (level <= 0) continue;
                        snapshot.legacySlots.Add(new LegacyEquipmentEntry
                        {
                            characterId = character.Id,
                            slot = slot,
                            level = level
                        });
                    }
                }
            }
            return JsonUtility.ToJson(snapshot, true);
        }

        static bool TryApplyEquipment(string payload, out string error)
        {
            try
            {
                EquipmentViewerSnapshot snapshot = JsonUtility.FromJson<EquipmentViewerSnapshot>(payload);
                CharacterDatabase characters = CharacterDatabase();
                EquipmentDatabase equipment = EquipmentDatabase();
                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.inventoryJson)
                    || snapshot.legacySlots == null || characters == null)
                    throw new InvalidOperationException("Equipment JSON 또는 CharacterDatabase가 올바르지 않습니다.");
                var identities = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < snapshot.legacySlots.Count; i++)
                {
                    LegacyEquipmentEntry entry = snapshot.legacySlots[i];
                    CharacterData character = FindCharacter(characters, entry?.characterId);
                    if (character == null || entry == null
                        || !Enum.IsDefined(typeof(EquipmentSlot), entry.slot)
                        || entry.level < 0 || entry.level > EquipmentService.MaxEquipmentLevel)
                        throw new InvalidOperationException("잘못된 레거시 장비 항목이 있습니다.");
                    if (!identities.Add(character.Id + ":" + entry.slot))
                        throw new InvalidOperationException("중복 레거시 장비 슬롯: "
                            + character.Id + "/" + entry.slot);
                }
                if (!EquipmentInventoryService.Default.TryImportJson(snapshot.inventoryJson,
                    equipment, out error)) return false;
                ResetLegacyEquipment(characters);
                for (int i = 0; i < snapshot.legacySlots.Count; i++)
                {
                    LegacyEquipmentEntry entry = snapshot.legacySlots[i];
                    CharacterData character = FindCharacter(characters, entry.characterId);
                    string key = EquipmentService.GetLegacyStorageKey(character, entry.slot);
                    if (!string.IsNullOrEmpty(key) && entry.level > 0)
                        PlayerPrefs.SetInt(key, entry.level);
                }
                PlayerPrefs.Save();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        static string CaptureGacha()
        {
            var snapshot = new GachaSnapshot
            {
                historyJson = GachaHistoryService.ExportJson(),
                selectedBannerId = PlayerPrefs.GetString(
                    GachaBannerScheduleService.SelectedBannerKey, string.Empty)
            };
            foreach (string group in GachaGroups())
                snapshot.pityGroups.Add(new GachaPityEntry
                {
                    groupId = group,
                    pity = Mathf.Max(0, PlayerPrefs.GetInt("StarfallAcademy.Gacha.Pity." + group, 0)),
                    featuredGuaranteed = PlayerPrefs.GetInt(
                        "StarfallAcademy.Gacha.FeaturedGuarantee." + group, 0) == 1
                });
            return JsonUtility.ToJson(snapshot, true);
        }

        static void ApplyGacha(GachaSnapshot snapshot)
        {
            if (snapshot == null || snapshot.pityGroups == null)
                throw new InvalidOperationException("Gacha JSON이 올바르지 않습니다.");
            if (!string.IsNullOrWhiteSpace(snapshot.selectedBannerId))
            {
                GachaBannerDatabase database = GachaDatabase();
                if (database == null || database.Find(snapshot.selectedBannerId) == null)
                    throw new InvalidOperationException("존재하지 않는 배너 ID: "
                        + snapshot.selectedBannerId);
            }
            var groups = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < snapshot.pityGroups.Count; i++)
            {
                GachaPityEntry entry = snapshot.pityGroups[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.groupId)
                    || !groups.Add(entry.groupId))
                    throw new InvalidOperationException("비어 있거나 중복된 pity group이 있습니다.");
            }
            if (!GachaHistoryService.TryImportJson(snapshot.historyJson, out string error))
                throw new InvalidOperationException(error);
            if (string.IsNullOrWhiteSpace(snapshot.selectedBannerId))
                PlayerPrefs.DeleteKey(GachaBannerScheduleService.SelectedBannerKey);
            else
            {
                PlayerPrefs.SetString(GachaBannerScheduleService.SelectedBannerKey,
                    snapshot.selectedBannerId);
            }
            foreach (string group in GachaGroups())
            {
                PlayerPrefs.DeleteKey("StarfallAcademy.Gacha.Pity." + group);
                PlayerPrefs.DeleteKey("StarfallAcademy.Gacha.FeaturedGuarantee." + group);
            }
            for (int i = 0; i < snapshot.pityGroups.Count; i++)
            {
                GachaPityEntry entry = snapshot.pityGroups[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.groupId)) continue;
                PlayerPrefs.SetInt("StarfallAcademy.Gacha.Pity." + entry.groupId, Mathf.Max(0, entry.pity));
                PlayerPrefs.SetInt("StarfallAcademy.Gacha.FeaturedGuarantee." + entry.groupId,
                    entry.featuredGuaranteed ? 1 : 0);
            }
        }

        static void ResetGacha()
        {
            GachaHistoryService.Clear();
            GachaBannerScheduleService.ClearSelection();
            foreach (string group in GachaGroups())
            {
                PlayerPrefs.DeleteKey("StarfallAcademy.Gacha.Pity." + group);
                PlayerPrefs.DeleteKey("StarfallAcademy.Gacha.FeaturedGuarantee." + group);
            }
        }

        static void ResetLegacyEquipment(CharacterDatabase database)
        {
            if (database == null) return;
            for (int i = 0; i < database.Characters.Count; i++)
            {
                CharacterData character = database.Characters[i];
                if (character == null) continue;
                foreach (EquipmentSlot slot in EquipmentService.Slots)
                {
                    string key = EquipmentService.GetLegacyStorageKey(character, slot);
                    if (!string.IsNullOrEmpty(key)) PlayerPrefs.DeleteKey(key);
                }
            }
        }

        static string CaptureMissions()
        {
            var snapshot = new MissionSnapshot
            {
                utcDay = PlayerPrefs.GetString(MissionDayKey, string.Empty)
            };
            IReadOnlyList<DailyMissionDefinition> definitions = MissionService.Default.Definitions;
            for (int i = 0; i < definitions.Count; i++)
            {
                DailyMissionDefinition definition = definitions[i];
                DailyMissionProgress progress = MissionService.Default.GetProgress(definition.Type);
                snapshot.missions.Add(new MissionEntry
                {
                    missionId = definition.Id,
                    progress = progress.Current,
                    claimed = progress.Claimed
                });
            }
            return JsonUtility.ToJson(snapshot, true);
        }

        static void ApplyMissions(MissionSnapshot snapshot)
        {
            if (snapshot == null || snapshot.missions == null)
                throw new InvalidOperationException("Missions JSON이 올바르지 않습니다.");
            MissionService.Default.Reset();
            PlayerPrefs.SetString(MissionDayKey, snapshot.utcDay ?? string.Empty);
            var definitions = MissionService.Default.Definitions.ToDictionary(value => value.Id,
                StringComparer.Ordinal);
            for (int i = 0; i < snapshot.missions.Count; i++)
            {
                MissionEntry entry = snapshot.missions[i];
                if (entry == null || !definitions.TryGetValue(entry.missionId, out DailyMissionDefinition definition))
                    throw new InvalidOperationException("존재하지 않는 미션 ID: " + (entry?.missionId ?? "null"));
                PlayerPrefs.SetInt(MissionProgressPrefix + definition.Id,
                    Mathf.Clamp(entry.progress, 0, definition.Target));
                PlayerPrefs.SetInt(MissionClaimedPrefix + definition.Id, entry.claimed ? 1 : 0);
            }
        }

        void DrawCharacterOverview()
        {
            CharacterDatabase database = CharacterDatabase();
            if (database == null) { Label("Characters", "CharacterDatabase 없음"); return; }
            int owned = database.Characters.Count(character => character != null
                && CharacterProgressionService.IsOwned(character));
            Label("Owned", owned + " / " + database.Characters.Count);
            for (int i = 0; i < Mathf.Min(database.Characters.Count, 8); i++)
            {
                CharacterData character = database.Characters[i];
                if (character == null) continue;
                Label(character.DisplayName, CharacterProgressionService.IsOwned(character)
                    ? "Lv." + CharacterProgressionService.GetLevel(character) + " · Skill "
                        + CharacterProgressionService.GetSkillLevel(character) + " · Awaken "
                        + CharacterAwakeningService.Default.GetStage(character) + " · Fragments "
                        + CharacterAwakeningService.Default.GetFragments(character)
                    : "Not owned");
            }
        }

        static void DrawEquipmentOverview()
        {
            IReadOnlyList<EquipmentInstance> items = EquipmentInventoryService.Default.GetAll();
            Label("Instances", items.Count.ToString());
            Label("Equipped", items.Count(item => item != null
                && !string.IsNullOrWhiteSpace(item.equippedCharacterId)).ToString());
            for (int i = 0; i < Mathf.Min(items.Count, 8); i++)
            {
                EquipmentInstance item = items[i];
                if (item != null) Label(item.instanceId, item.equipmentId + " · Lv." + item.level
                    + (string.IsNullOrWhiteSpace(item.equippedCharacterId)
                        ? string.Empty : " · " + item.equippedCharacterId));
            }
        }

        static void DrawFormationOverview()
        {
            FormationPresetService service = FormationPresetService.Default;
            Label("Active preset", (service.ActivePresetIndex + 1).ToString());
            IReadOnlyList<FormationPreset> presets = service.GetPresets(CharacterDatabase());
            for (int i = 0; i < presets.Count; i++)
                Label((i + 1) + ". " + presets[i].name,
                    presets[i].characterIds == null ? "Empty" : string.Join(", ", presets[i].characterIds));
        }

        static void DrawGachaOverview()
        {
            IReadOnlyList<GachaHistoryEntry> history = GachaHistoryService.Load();
            Label("History", history.Count + " / " + GachaHistoryService.MaximumEntries);
            foreach (string group in GachaGroups())
                Label("Pity · " + group, PlayerPrefs.GetInt("StarfallAcademy.Gacha.Pity." + group, 0)
                    + (PlayerPrefs.GetInt("StarfallAcademy.Gacha.FeaturedGuarantee." + group, 0) == 1
                        ? " · Pickup guaranteed" : string.Empty));
            for (int i = 0; i < Mathf.Min(history.Count, 10); i++)
            {
                GachaHistoryEntry entry = history[i];
                Label(entry.PulledAtUtc.ToString("u"), entry.BannerId + " · " + entry.CharacterName
                    + " · R" + entry.Rarity + (entry.IsFeatured ? " · PICKUP" : string.Empty)
                    + (entry.IsNew ? " · NEW" : string.Empty) + " · pity " + entry.PityAfter);
            }
        }

        static void DrawAttendanceOverview()
        {
            AttendanceCampaignDatabase database = AttendanceDatabase();
            if (database == null) { Label("Campaigns", "AttendanceCampaignDatabase 없음"); return; }
            for (int i = 0; i < database.Campaigns.Count; i++)
            {
                AttendanceCampaignData campaign = database.Campaigns[i];
                if (campaign == null) continue;
                AttendanceProgress progress = AttendanceService.Default.GetProgress(campaign);
                Label(campaign.DisplayName, progress.CurrentSequenceIndex + " / " + campaign.DayCount
                    + (progress.Completed ? " · Completed" : string.Empty));
            }
        }

        static void DrawMailOverview()
        {
            IReadOnlyList<MailInstance> mails = MailService.Default.GetMails();
            Label("Stored", mails.Count.ToString());
            Label("Unread", MailService.Default.GetUnreadCount().ToString());
            for (int i = 0; i < Mathf.Min(mails.Count, 10); i++)
            {
                MailInstance mail = mails[i];
                Label(mail.SentAtUtcText, mail.Title + (mail.IsRead ? "" : " · NEW")
                    + (mail.IsClaimed ? " · CLAIMED" : ""));
            }
        }

        static void DrawWeeklyBossOverview()
        {
            WeeklyBossPlayerDataSnapshot snapshot = WeeklyBossService.Default.CapturePlayerData(
                WeeklyBossDatabase());
            Label("Week", snapshot.weekId);
            for (int i = 0; i < snapshot.entries.Count; i++)
            {
                WeeklyBossPlayerDataEntry entry = snapshot.entries[i];
                Label(entry.bossId + " / " + entry.difficultyId,
                    "Attempts " + entry.attemptsUsed + "/" + entry.maximumAttempts
                    + " · Best " + entry.bestScore.ToString("N0"));
            }
        }

        static void DrawTowerOverview()
        {
            TowerPlayerDataSnapshot snapshot = TowerProgressService.Default.CapturePlayerData(TowerDatabase());
            Label("Total stars", snapshot.totalStars.ToString());
            for (int i = 0; i < Mathf.Min(snapshot.floors.Count, 12); i++)
            {
                TowerPlayerDataEntry floor = snapshot.floors[i];
                Label("Floor " + floor.floorNumber, floor.cleared ? floor.stars + " stars" : "Not cleared");
            }
        }

        static void DrawMissionOverview()
        {
            IReadOnlyList<DailyMissionDefinition> definitions = MissionService.Default.Definitions;
            for (int i = 0; i < definitions.Count; i++)
            {
                DailyMissionProgress progress = MissionService.Default.GetProgress(definitions[i].Type);
                Label(definitions[i].Id, progress.Current + " / " + definitions[i].Target
                    + (progress.Claimed ? " · Claimed" : progress.CanClaim ? " · Claimable" : string.Empty));
            }
        }

        void DrawValidationWarnings()
        {
            if (tab == DataTab.Equipment)
            {
                EquipmentDatabase database = EquipmentDatabase();
                int invalid = EquipmentInventoryService.Default.GetAll().Count(item => item == null
                    || database == null || database.FindEquipment(item.equipmentId) == null);
                if (invalid > 0) EditorGUILayout.HelpBox("정의가 없는 장비 인스턴스: " + invalid,
                    MessageType.Warning);
            }
            else if (tab == DataTab.Formation)
            {
                CharacterDatabase database = CharacterDatabase();
                FormationRawSnapshot raw = JsonUtility.FromJson<FormationRawSnapshot>(
                    FormationPresetService.Default.ExportJson(false));
                int invalid = raw?.presets == null ? 0 : raw.presets.Sum(preset =>
                    preset?.characterIds == null ? 0 : preset.characterIds.Count(id =>
                        FindCharacter(database, id) == null));
                if (invalid > 0) EditorGUILayout.HelpBox("존재하지 않는 캐릭터 편성 참조: " + invalid,
                    MessageType.Warning);
            }
        }

        string ResolveLastChanged()
        {
            if (tab == DataTab.Mail)
            {
                string value = MailService.Default.CaptureSnapshot().LastModifiedUtc;
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            if (tab == DataTab.Attendance)
            {
                AttendanceSaveSnapshot snapshot = AttendanceService.Default.CaptureSnapshot(AttendanceDatabase());
                string latest = snapshot.Campaigns.Where(value => value != null)
                    .Select(value => value.LastModifiedUtc).Where(value => !string.IsNullOrWhiteSpace(value))
                    .OrderByDescending(value => value, StringComparer.Ordinal).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(latest)) return latest;
            }
            return EditorPrefs.GetString(ChangeKeyPrefix + tab, "기록 없음 (PlayerPrefs는 키별 변경 시간을 제공하지 않음)");
        }

        static string RelatedKeys(DataTab value)
        {
            switch (value)
            {
                case DataTab.Profile: return ProfileLevelKey + "\n" + ProfileExperienceKey;
                case DataTab.Wallet: return PremiumKey + "\n" + CreditsKey + "\n" + MaterialsKey;
                case DataTab.Characters: return OwnedPrefix + "{characterId}\n" + LevelPrefix
                    + "{characterId}\n" + SkillPrefix + "{characterId}\n" + AwakeningPrefix
                    + "{characterId}\nStarfallAcademy.Item.{stableHash(fragment:{characterId})}";
                case DataTab.Equipment: return EquipmentInventoryService.StorageKey;
                case DataTab.Formation: return FormationPresetService.StorageKey;
                case DataTab.Gacha: return GachaHistoryService.PlayerPrefsKey
                    + "\n" + GachaBannerScheduleService.SelectedBannerKey
                    + "\nStarfallAcademy.Gacha.Pity.{pityGroup}\nStarfallAcademy.Gacha.FeaturedGuarantee.{pityGroup}";
                case DataTab.Attendance: return "StarfallAcademy.LiveOps.Attendance.{campaignId}.*";
                case DataTab.Mail: return string.Join("\n", MailService.GetKnownStorageKeys());
                case DataTab.WeeklyBoss: return "StarfallAcademy.WeeklyBoss.v1.{boss}.{difficulty}.{weekId}.*";
                case DataTab.Tower: return "StarfallAcademy.Tower.v1.floor.{number}.*\nStarfallAcademy.Tower.v1.star_reward.{tier}";
                case DataTab.Missions: return MissionDayKey + "\n" + MissionProgressPrefix
                    + "{missionId}\n" + MissionClaimedPrefix + "{missionId}";
                default: return string.Empty;
            }
        }

        static UnityEngine.Object GetContentReference(DataTab value)
        {
            switch (value)
            {
                case DataTab.Characters:
                case DataTab.Formation: return CharacterDatabase();
                case DataTab.Equipment: return EquipmentDatabase();
                case DataTab.Gacha: return GachaDatabase();
                case DataTab.Attendance: return AttendanceDatabase();
                case DataTab.WeeklyBoss: return WeeklyBossDatabase();
                case DataTab.Tower: return TowerDatabase();
                default: return null;
            }
        }

        static CharacterDatabase CharacterDatabase() => Resources.Load<CharacterDatabase>("Data/CharacterDatabase");
        static EquipmentDatabase EquipmentDatabase() => Resources.Load<EquipmentDatabase>("Data/EquipmentDatabase");
        static GachaBannerDatabase GachaDatabase() => Resources.Load<GachaBannerDatabase>("Data/GachaBannerDatabase");
        static AttendanceCampaignDatabase AttendanceDatabase() =>
            Resources.Load<AttendanceCampaignDatabase>("Data/AttendanceCampaignDatabase");
        static WeeklyBossDatabase WeeklyBossDatabase() => Resources.Load<WeeklyBossDatabase>("Data/WeeklyBossDatabase");
        static TowerDatabase TowerDatabase() => Resources.Load<TowerDatabase>("Data/TowerDatabase");

        static CharacterData FindCharacter(CharacterDatabase database, string id)
        {
            if (database == null || string.IsNullOrWhiteSpace(id)) return null;
            for (int i = 0; i < database.Characters.Count; i++)
                if (database.Characters[i] != null && database.Characters[i].Id == id)
                    return database.Characters[i];
            return null;
        }

        static IEnumerable<string> GachaGroups()
        {
            var result = new HashSet<string>(StringComparer.Ordinal)
            {
                "default",
                // Current pre-migration GachaConfig uses this group. Keeping it explicit also
                // makes reset/import safe before a banner database asset has been generated.
                "standard_pickup"
            };
            GachaBannerDatabase database = GachaDatabase();
            if (database != null)
            {
                for (int i = 0; i < database.Banners.Count; i++)
                {
                    GachaBannerData banner = database.Banners[i];
                    if (banner != null && !string.IsNullOrWhiteSpace(banner.PityGroupId))
                        result.Add(banner.PityGroupId);
                }
            }
            GachaConfig legacy = Resources.Load<GachaConfig>(
                GachaBannerScheduleService.LegacyConfigResourcePath);
            if (legacy != null && !string.IsNullOrWhiteSpace(legacy.PityGroupId))
                result.Add(legacy.PityGroupId);
            return result;
        }

        static void Label(string name, string value) =>
            EditorGUILayout.LabelField(name, value ?? string.Empty);

        void MarkChanged(DataTab value) => EditorPrefs.SetString(ChangeKeyPrefix + value,
            DateTime.UtcNow.ToString("O"));

        void SetStatus(string value, MessageType type)
        {
            status = value ?? string.Empty;
            statusType = type;
            Repaint();
        }

        [Serializable] sealed class ProfileSnapshot { public int version = 1; public int level; public int experience; }
        [Serializable] sealed class WalletSnapshot { public int version = 1; public int premiumCurrency; public int credits; public int skillMaterials; }
        [Serializable] sealed class CharacterSnapshot { public int version = 1; public List<CharacterEntry> characters = new List<CharacterEntry>(); }
        [Serializable] sealed class CharacterEntry { public string characterId; public bool owned; public int level; public int skillLevel; public int awakeningStage; public int fragments; }
        [Serializable] sealed class EquipmentViewerSnapshot { public int version = 1; public string inventoryJson; public List<LegacyEquipmentEntry> legacySlots = new List<LegacyEquipmentEntry>(); }
        [Serializable] sealed class LegacyEquipmentEntry { public string characterId; public EquipmentSlot slot; public int level; }
        [Serializable] sealed class GachaSnapshot { public int version = 1; public string historyJson; public string selectedBannerId; public List<GachaPityEntry> pityGroups = new List<GachaPityEntry>(); }
        [Serializable] sealed class GachaPityEntry { public string groupId; public int pity; public bool featuredGuaranteed; }
        [Serializable] sealed class MissionSnapshot { public int version = 1; public string utcDay; public List<MissionEntry> missions = new List<MissionEntry>(); }
        [Serializable] sealed class MissionEntry { public string missionId; public int progress; public bool claimed; }
        [Serializable] sealed class FormationRawSnapshot
        {
            public int version = 1;
            public int activeIndex = 0;
            public List<FormationPreset> presets = new List<FormationPreset>();
        }

        [Serializable]
        sealed class FullBackup
        {
            public int version = 2;
            public string capturedAtUtc;
            public string reason;
            public int saveVersion;
            public string lastMigrationUtc;
            public DynamicKeySnapshot dynamicKeys = new DynamicKeySnapshot();
            public List<BackupSection> sections = new List<BackupSection>();

            public void Set(DataTab value, string payload)
            {
                sections.RemoveAll(section => section != null && section.tab == value.ToString());
                sections.Add(new BackupSection { tab = value.ToString(), json = payload ?? string.Empty });
            }

            public string Get(DataTab value)
            {
                BackupSection section = sections?.Find(item => item != null && item.tab == value.ToString());
                return section?.json ?? string.Empty;
            }
        }

        [Serializable] sealed class BackupSection { public string tab; public string json; }
        [Serializable] sealed class DynamicKeySnapshot
        {
            public List<DynamicIntEntry> itemInventory = new List<DynamicIntEntry>();
            public List<DynamicIntEntry> rewardTransactions = new List<DynamicIntEntry>();
        }
        [Serializable] sealed class DynamicIntEntry
        {
            public string key;
            public bool exists;
            public int value;
        }
    }
}
