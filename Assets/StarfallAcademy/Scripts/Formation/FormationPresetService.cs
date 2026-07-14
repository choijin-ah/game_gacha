using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [Serializable]
    sealed class FormationPresetPayload
    {
        public int version = 1;
        public int activeIndex;
        public List<FormationPreset> presets = new List<FormationPreset>();
    }

    public sealed class FormationPresetService
    {
        public const string StorageKey = "StarfallAcademy.Formation.Presets.v1";
        const string LegacyFormationKey = "StarfallAcademy.Formation";

        readonly IMetaStorage storage;
        readonly FormationSettings settings;

        public FormationPresetService(IMetaStorage storage, FormationSettings settings = null)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.settings = settings ?? Resources.Load<FormationSettings>("Data/FormationSettings");
        }

        public static FormationPresetService Default =>
            new FormationPresetService(PlayerPrefsMetaStorage.Shared);

        public int PresetCount => settings != null ? settings.MaximumPresetCount : 3;
        public int ActivePresetIndex => Mathf.Clamp(Load().activeIndex, 0, PresetCount - 1);

        public IReadOnlyList<FormationPreset> GetPresets(CharacterDatabase database = null)
        {
            FormationPresetPayload payload = Load();
            EnsureCount(payload);
            var results = new List<FormationPreset>(payload.presets.Count);
            for (int i = 0; i < payload.presets.Count; i++)
            {
                FormationPreset preset = payload.presets[i]?.Clone() ?? CreateDefault(i);
                Sanitize(preset, database);
                results.Add(preset);
            }
            return results;
        }

        public FormationPreset GetActive(CharacterDatabase database = null)
        {
            IReadOnlyList<FormationPreset> presets = GetPresets(database);
            return presets[Mathf.Clamp(ActivePresetIndex, 0, presets.Count - 1)];
        }

        public bool Select(int index)
        {
            if (index < 0 || index >= PresetCount) return false;
            FormationPresetPayload payload = Load();
            EnsureCount(payload);
            payload.activeIndex = index;
            try
            {
                CommitPayload(payload);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool Rename(int index, string name)
        {
            if (index < 0 || index >= PresetCount || string.IsNullOrWhiteSpace(name)) return false;
            FormationPresetPayload payload = Load();
            EnsureCount(payload);
            payload.presets[index].name = name.Trim();
            try
            {
                CommitPayload(payload);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool Save(int index, IEnumerable<CharacterData> members)
        {
            if (index < 0 || index >= PresetCount) return false;
            FormationPresetPayload payload = Load();
            EnsureCount(payload);
            var ids = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            if (members != null)
            {
                foreach (CharacterData character in members)
                    if (character != null && unique.Add(character.Id) && ids.Count < FormationState.MaxMembers)
                        ids.Add(character.Id);
            }
            payload.presets[index].characterIds = ids;
            payload.activeIndex = index;
            try
            {
                CommitPayload(payload, index == 0, string.Join("|", ids));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public string ExportJson(bool pretty = true) => JsonUtility.ToJson(Load(), pretty);

        public bool TryImportJson(string json, CharacterDatabase database, out string error)
        {
            try
            {
                FormationPresetPayload payload = JsonUtility.FromJson<FormationPresetPayload>(json);
                if (payload == null || payload.presets == null) throw new InvalidOperationException("Invalid formation JSON.");
                EnsureCount(payload);
                for (int i = 0; i < payload.presets.Count; i++) Sanitize(payload.presets[i], database);
                payload.activeIndex = Mathf.Clamp(payload.activeIndex, 0, PresetCount - 1);
                CommitPayload(payload);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public void Reset()
        {
            CommitStorage(new[] { StorageKey, LegacyFormationKey }, () =>
            {
                storage.DeleteKey(StorageKey);
                storage.DeleteKey(LegacyFormationKey);
            });
        }

        public bool MigrateLegacyIfNeeded()
        {
            if (storage.HasKey(StorageKey) || !storage.HasKey(LegacyFormationKey)) return false;
            FormationPresetPayload payload = Load();
            CommitPayload(payload);
            return true;
        }

        FormationPresetPayload Load()
        {
            FormationPresetPayload payload = null;
            string json = storage.GetString(StorageKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(json))
            {
                try { payload = JsonUtility.FromJson<FormationPresetPayload>(json); }
                catch (Exception) { }
            }
            payload ??= new FormationPresetPayload();
            payload.presets ??= new List<FormationPreset>();
            EnsureCount(payload);
            if (!storage.HasKey(StorageKey) && storage.HasKey(LegacyFormationKey))
            {
                string legacy = storage.GetString(LegacyFormationKey, string.Empty);
                payload.presets[0].characterIds = string.IsNullOrWhiteSpace(legacy)
                    ? new List<string>() : new List<string>(legacy.Split('|'));
            }
            return payload;
        }

        void CommitPayload(FormationPresetPayload payload, bool writeLegacy = false,
            string legacyValue = null)
        {
            EnsureCount(payload);
            string json = JsonUtility.ToJson(payload);
            IReadOnlyList<string> keys = writeLegacy
                ? new[] { StorageKey, LegacyFormationKey }
                : new[] { StorageKey };
            CommitStorage(keys, () =>
            {
                storage.SetString(StorageKey, json);
                if (writeLegacy)
                    storage.SetString(LegacyFormationKey, legacyValue ?? string.Empty);
            });
        }

        void CommitStorage(IReadOnlyList<string> keys, Action stageChanges)
        {
            var snapshots = new StorageSnapshot[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                snapshots[i] = new StorageSnapshot(key, storage.HasKey(key),
                    storage.GetString(key, string.Empty));
            }

            try
            {
                stageChanges();
                storage.Save();
            }
            catch
            {
                for (int i = 0; i < snapshots.Length; i++)
                {
                    try { Restore(snapshots[i]); }
                    catch (Exception) { }
                }
                try { storage.Save(); }
                catch (Exception) { }
                throw;
            }
        }

        void Restore(StorageSnapshot snapshot)
        {
            if (snapshot.HadValue) storage.SetString(snapshot.Key, snapshot.Value);
            else storage.DeleteKey(snapshot.Key);
        }

        readonly struct StorageSnapshot
        {
            public StorageSnapshot(string key, bool hadValue, string value)
            {
                Key = key;
                HadValue = hadValue;
                Value = value ?? string.Empty;
            }

            public string Key { get; }
            public bool HadValue { get; }
            public string Value { get; }
        }

        void EnsureCount(FormationPresetPayload payload)
        {
            while (payload.presets.Count < PresetCount) payload.presets.Add(CreateDefault(payload.presets.Count));
            if (payload.presets.Count > PresetCount)
                payload.presets.RemoveRange(PresetCount, payload.presets.Count - PresetCount);
            for (int i = 0; i < payload.presets.Count; i++) payload.presets[i] ??= CreateDefault(i);
        }

        FormationPreset CreateDefault(int index) => new FormationPreset
        {
            name = settings != null ? settings.GetDefaultName(index) : "파티 " + (index + 1),
            characterIds = new List<string>()
        };

        static void Sanitize(FormationPreset preset, CharacterDatabase database)
        {
            if (preset == null) return;
            preset.characterIds ??= new List<string>();
            var valid = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < preset.characterIds.Count && valid.Count < FormationState.MaxMembers; i++)
            {
                string id = preset.characterIds[i];
                CharacterData character = database?.Find(id);
                if (string.IsNullOrWhiteSpace(id) || !unique.Add(id)) continue;
                if (database != null && (character == null || !CharacterProgressionService.IsOwned(character))) continue;
                valid.Add(id);
            }
            preset.characterIds = valid;
        }
    }
}
