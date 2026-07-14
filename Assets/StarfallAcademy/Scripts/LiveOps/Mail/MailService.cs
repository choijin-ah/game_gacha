using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public enum MailSendStatus
    {
        Sent,
        MissingTemplate,
        InvalidContent,
        InvalidExpiry,
        InvalidAttachments,
        SaveFailed
    }

    public readonly struct MailSendResult
    {
        public MailSendResult(MailSendStatus status, MailInstance mail, string message)
        {
            Status = status;
            Mail = mail;
            Message = message ?? string.Empty;
        }

        public MailSendStatus Status { get; }
        public MailInstance Mail { get; }
        public string Message { get; }
        public bool Succeeded => Status == MailSendStatus.Sent;
    }

    public enum MailClaimStatus
    {
        Claimed,
        UnknownMail,
        Expired,
        AlreadyClaimed,
        InvalidAttachments,
        RewardRejected,
        SaveFailed
    }

    public readonly struct MailClaimResult
    {
        public MailClaimResult(MailClaimStatus status, MailInstance mail,
            RewardGrantResult rewardResult, string message)
        {
            Status = status;
            Mail = mail;
            RewardResult = rewardResult;
            Message = message ?? string.Empty;
        }

        public MailClaimStatus Status { get; }
        public MailInstance Mail { get; }
        public RewardGrantResult RewardResult { get; }
        public string Message { get; }
        public bool Succeeded => Status == MailClaimStatus.Claimed;
    }

    [Serializable]
    public sealed class MailSaveSnapshot
    {
        [SerializeField] int version = 1;
        [SerializeField] string capturedAtUtc;
        [SerializeField] string lastModifiedUtc;
        [SerializeField] int resetGeneration;
        [SerializeField] List<MailInstance> mails = new List<MailInstance>();

        public int Version => version;
        public string CapturedAtUtc => capturedAtUtc ?? string.Empty;
        public string LastModifiedUtc => lastModifiedUtc ?? string.Empty;
        public int ResetGeneration => Math.Max(0, resetGeneration);
        public IReadOnlyList<MailInstance> Mails => mails;

        internal List<MailInstance> MutableMails
        {
            get
            {
                if (mails == null) mails = new List<MailInstance>();
                return mails;
            }
        }

        internal void SetMetadata(string captured, string modified, int generation)
        {
            version = 1;
            capturedAtUtc = captured ?? string.Empty;
            lastModifiedUtc = modified ?? string.Empty;
            resetGeneration = Math.Max(0, generation);
        }
    }

    /// <summary>
    /// Local bounded inbox. Mail content and attachments are snapshotted when sent so later
    /// template edits cannot change an already delivered message.
    /// </summary>
    public sealed class MailService
    {
        public const int DefaultMaximumStoredMail = 200;
        const string InboxKey = "StarfallAcademy.LiveOps.Mail.Inbox.v1";
        const string ResetGenerationKey = "StarfallAcademy.LiveOps.Mail.ResetGeneration";
        const string LastModifiedKey = "StarfallAcademy.LiveOps.Mail.LastModifiedUtc";

        readonly object syncRoot = new object();
        readonly IMetaStorage storage;
        readonly IUtcClock clock;
        readonly RewardPackageService rewardService;
        readonly int maximumStoredMail;

        public static MailService Default { get; } = new MailService(
            PlayerPrefsMetaStorage.Shared, ContentUtcClock.Shared, RewardPackageService.Default);

        public MailService(IMetaStorage storage, IUtcClock clock,
            RewardPackageService rewardService, int maximumStoredMail = DefaultMaximumStoredMail)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.rewardService = rewardService ?? throw new ArgumentNullException(nameof(rewardService));
            if (maximumStoredMail < 1) throw new ArgumentOutOfRangeException(nameof(maximumStoredMail));
            this.maximumStoredMail = maximumStoredMail;
        }

        public IReadOnlyList<MailInstance> GetMails()
        {
            lock (syncRoot)
            {
                MailSaveSnapshot envelope = LoadEnvelope();
                SortNewestFirst(envelope.MutableMails);
                var result = new List<MailInstance>(envelope.MutableMails.Count);
                for (int i = 0; i < envelope.MutableMails.Count; i++)
                    result.Add(MailInstance.Clone(envelope.MutableMails[i]));
                return result;
            }
        }

        public MailInstance Find(string mailInstanceId)
        {
            if (string.IsNullOrWhiteSpace(mailInstanceId)) return null;
            lock (syncRoot)
            {
                MailInstance mail = FindMutable(LoadEnvelope().MutableMails, mailInstanceId);
                return MailInstance.Clone(mail);
            }
        }

        public int GetUnreadCount()
        {
            lock (syncRoot)
            {
                List<MailInstance> mails = LoadEnvelope().MutableMails;
                int count = 0;
                for (int i = 0; i < mails.Count; i++)
                    if (mails[i] != null && !mails[i].IsRead) count++;
                return count;
            }
        }

        public int GetClaimableCount()
        {
            lock (syncRoot)
            {
                DateTime now = clock.UtcNow;
                List<MailInstance> mails = LoadEnvelope().MutableMails;
                int count = 0;
                for (int i = 0; i < mails.Count; i++)
                {
                    MailInstance mail = mails[i];
                    if (mail != null && !mail.IsClaimed && !mail.IsExpired(now)
                        && mail.Attachments != null && !mail.Attachments.IsEmpty) count++;
                }
                return count;
            }
        }

        public MailSendResult Send(MailTemplateData template, MailSendOptions options = null)
        {
            if (template == null)
                return new MailSendResult(MailSendStatus.MissingTemplate, null,
                    "발송할 우편 템플릿을 선택해 주세요.");

            options = options ?? new MailSendOptions();
            string title = options.OverrideTitle ? options.Title : template.Title;
            string body = options.OverrideBody ? options.Body : template.Body;
            string sender = options.OverrideSender ? options.Sender : template.Sender;
            int expiryHours = options.OverrideExpiryHours
                ? options.ExpiryHours : template.DefaultExpiryHours;
            RewardPackage attachments = options.OverrideAttachments
                ? options.Attachments : template.Attachments;

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body)
                || string.IsNullOrWhiteSpace(sender))
                return new MailSendResult(MailSendStatus.InvalidContent, null,
                    "제목, 본문, 발신자를 모두 입력해 주세요.");
            if (expiryHours < 0)
                return new MailSendResult(MailSendStatus.InvalidExpiry, null,
                    "만료 시간은 0 이상이어야 합니다.");
            if (attachments == null || !attachments.IsValid)
                return new MailSendResult(MailSendStatus.InvalidAttachments, null,
                    "첨부 보상 데이터가 올바르지 않습니다.");

            lock (syncRoot)
            {
                DateTime sentAt = ScheduleRange.NormalizeUtc(clock.UtcNow);
                DateTime? expiresAt = expiryHours > 0
                    ? sentAt.AddHours(expiryHours) : (DateTime?)null;
                MailInstance mail = MailInstance.Create(Guid.NewGuid().ToString("N"),
                    template.TemplateId, title.Trim(), body, sender.Trim(), sentAt,
                    expiresAt, attachments);
                MailSaveSnapshot envelope = LoadEnvelope();
                envelope.MutableMails.Add(mail);
                Prune(envelope.MutableMails, sentAt);
                if (!TrySaveEnvelope(envelope, sentAt, out string error))
                    return new MailSendResult(MailSendStatus.SaveFailed, null,
                        "테스트 우편을 저장하지 못했습니다: " + error);
                return new MailSendResult(MailSendStatus.Sent,
                    MailInstance.Clone(mail), "로컬 계정에 테스트 우편을 발송했습니다.");
            }
        }

        public bool MarkRead(string mailInstanceId)
        {
            if (string.IsNullOrWhiteSpace(mailInstanceId)) return false;
            lock (syncRoot)
            {
                MailSaveSnapshot envelope = LoadEnvelope();
                MailInstance mail = FindMutable(envelope.MutableMails, mailInstanceId);
                if (mail == null) return false;
                if (mail.IsRead) return true;
                mail.MarkRead();
                return TrySaveEnvelope(envelope, clock.UtcNow, out _);
            }
        }

        public MailClaimResult Claim(string mailInstanceId)
        {
            if (string.IsNullOrWhiteSpace(mailInstanceId))
                return ClaimResult(MailClaimStatus.UnknownMail, null, default,
                    "우편을 찾을 수 없습니다.");
            lock (syncRoot)
            {
                MailSaveSnapshot envelope = LoadEnvelope();
                MailInstance mail = FindMutable(envelope.MutableMails, mailInstanceId);
                if (mail == null)
                    return ClaimResult(MailClaimStatus.UnknownMail, null, default,
                        "우편을 찾을 수 없습니다.");
                if (mail.IsClaimed)
                    return ClaimResult(MailClaimStatus.AlreadyClaimed,
                        MailInstance.Clone(mail), default, "이미 첨부 보상을 받았습니다.");
                if (mail.IsExpired(clock.UtcNow))
                    return ClaimResult(MailClaimStatus.Expired,
                        MailInstance.Clone(mail), default, "만료된 우편입니다.");
                if (mail.Attachments == null || !mail.Attachments.IsValid)
                    return ClaimResult(MailClaimStatus.InvalidAttachments,
                        MailInstance.Clone(mail), default, "첨부 보상 데이터가 올바르지 않습니다.");

                int generation = Math.Max(0, storage.GetInt(ResetGenerationKey, 0));
                string transactionId = "mail:" + generation.ToString(CultureInfo.InvariantCulture)
                    + ":" + mail.MailInstanceId;
                DateTime modifiedAt = clock.UtcNow;
                MailSaveSnapshot stagedEnvelope = CloneEnvelope(envelope);
                MailInstance stagedMail = FindMutable(stagedEnvelope.MutableMails, mailInstanceId);
                stagedMail.MarkClaimed();

                bool hadInbox = storage.HasKey(InboxKey);
                bool hadModified = storage.HasKey(LastModifiedKey);
                bool hadGeneration = storage.HasKey(ResetGenerationKey);
                string previousInbox = storage.GetString(InboxKey, string.Empty);
                string previousModified = storage.GetString(LastModifiedKey, string.Empty);
                int previousGeneration = storage.GetInt(ResetGenerationKey, 0);
                Action stageClaim = () => StageEnvelope(stagedEnvelope, modifiedAt, generation);
                Action rollbackClaim = () =>
                {
                    RestoreString(InboxKey, hadInbox, previousInbox);
                    RestoreString(LastModifiedKey, hadModified, previousModified);
                    RestoreInt(ResetGenerationKey, hadGeneration, previousGeneration);
                };
                RewardGrantResult rewardResult = rewardService.Grant(transactionId,
                    mail.Attachments, stageClaim, rollbackClaim);
                if (rewardResult.Succeeded)
                    return ClaimResult(MailClaimStatus.Claimed,
                        MailInstance.Clone(stagedMail), rewardResult,
                        mail.Attachments.IsEmpty ? "우편을 확인했습니다."
                            : mail.Attachments.Summary + " 수령 완료");

                // The stable instance ID makes this an idempotent repair path if an older
                // process committed the reward marker before this caller observed completion.
                if (rewardResult.AlreadyProcessed)
                {
                    try
                    {
                        stageClaim();
                        storage.Save();
                        return ClaimResult(MailClaimStatus.Claimed,
                            MailInstance.Clone(stagedMail), rewardResult,
                            "우편 수령 상태를 복구했습니다.");
                    }
                    catch (Exception exception)
                    {
                        try
                        {
                            rollbackClaim();
                            storage.Save();
                        }
                        catch (Exception)
                        {
                            // Keep the original repair failure as the reported result.
                        }
                        return ClaimResult(MailClaimStatus.SaveFailed,
                            MailInstance.Clone(mail), rewardResult,
                            "우편 수령 상태를 저장하지 못했습니다: " + exception.Message);
                    }
                }
                return ClaimResult(MailClaimStatus.RewardRejected,
                    MailInstance.Clone(mail), rewardResult,
                    "첨부 보상과 우편 수령 상태를 저장하지 못했습니다.");
            }
        }

        public int RemoveExpired(bool includeUnclaimed = false)
        {
            lock (syncRoot)
            {
                MailSaveSnapshot envelope = LoadEnvelope();
                DateTime now = clock.UtcNow;
                int removed = envelope.MutableMails.RemoveAll(mail => mail == null
                    || mail.IsExpired(now) && (includeUnclaimed || mail.IsClaimed));
                if (removed > 0) TrySaveEnvelope(envelope, now, out _);
                return removed;
            }
        }

        public MailSaveSnapshot CaptureSnapshot()
        {
            lock (syncRoot)
            {
                MailSaveSnapshot source = LoadEnvelope();
                var snapshot = new MailSaveSnapshot();
                snapshot.SetMetadata(FormatUtc(clock.UtcNow),
                    storage.GetString(LastModifiedKey, source.LastModifiedUtc),
                    Math.Max(0, storage.GetInt(ResetGenerationKey, source.ResetGeneration)));
                for (int i = 0; i < source.MutableMails.Count; i++)
                    snapshot.MutableMails.Add(MailInstance.Clone(source.MutableMails[i]));
                return snapshot;
            }
        }

        public bool RestoreSnapshot(MailSaveSnapshot snapshot)
        {
            if (snapshot == null) return false;
            lock (syncRoot)
            {
                try
                {
                    var restored = new MailSaveSnapshot();
                    restored.SetMetadata(FormatUtc(clock.UtcNow), snapshot.LastModifiedUtc,
                        snapshot.ResetGeneration);
                    if (snapshot.Mails != null)
                    {
                        for (int i = 0; i < snapshot.Mails.Count; i++)
                        {
                            MailInstance mail = snapshot.Mails[i];
                            if (mail != null) restored.MutableMails.Add(MailInstance.Clone(mail));
                        }
                    }
                    Prune(restored.MutableMails, clock.UtcNow);
                    storage.SetInt(ResetGenerationKey, snapshot.ResetGeneration);
                    return TrySaveEnvelope(restored, clock.UtcNow, out _);
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public void Reset()
        {
            lock (syncRoot)
            {
                int nextGeneration = Math.Max(0, storage.GetInt(ResetGenerationKey, 0)) + 1;
                storage.DeleteKey(InboxKey);
                storage.DeleteKey(LastModifiedKey);
                storage.SetInt(ResetGenerationKey, nextGeneration);
                storage.Save();
            }
        }

        public static IReadOnlyList<string> GetKnownStorageKeys() => new[]
        {
            InboxKey, ResetGenerationKey, LastModifiedKey
        };

        MailSaveSnapshot LoadEnvelope()
        {
            string json = storage.GetString(InboxKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                var empty = new MailSaveSnapshot();
                empty.SetMetadata(string.Empty, storage.GetString(LastModifiedKey, string.Empty),
                    Math.Max(0, storage.GetInt(ResetGenerationKey, 0)));
                return empty;
            }
            try
            {
                MailSaveSnapshot parsed = JsonUtility.FromJson<MailSaveSnapshot>(json);
                if (parsed == null) parsed = new MailSaveSnapshot();
                parsed.SetMetadata(parsed.CapturedAtUtc,
                    storage.GetString(LastModifiedKey, parsed.LastModifiedUtc),
                    Math.Max(0, storage.GetInt(ResetGenerationKey, parsed.ResetGeneration)));
                return parsed;
            }
            catch (Exception)
            {
                var fallback = new MailSaveSnapshot();
                fallback.SetMetadata(string.Empty, storage.GetString(LastModifiedKey, string.Empty),
                    Math.Max(0, storage.GetInt(ResetGenerationKey, 0)));
                return fallback;
            }
        }

        bool TrySaveEnvelope(MailSaveSnapshot envelope, DateTime modifiedAt, out string error)
        {
            try
            {
                string modified = FormatUtc(modifiedAt);
                int generation = Math.Max(0, storage.GetInt(ResetGenerationKey,
                    envelope.ResetGeneration));
                envelope.SetMetadata(string.Empty, modified, generation);
                storage.SetString(InboxKey, JsonUtility.ToJson(envelope));
                storage.SetString(LastModifiedKey, modified);
                storage.SetInt(ResetGenerationKey, generation);
                storage.Save();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        void StageEnvelope(MailSaveSnapshot envelope, DateTime modifiedAt, int generation)
        {
            string modified = FormatUtc(modifiedAt);
            envelope.SetMetadata(string.Empty, modified, Math.Max(0, generation));
            storage.SetString(InboxKey, JsonUtility.ToJson(envelope));
            storage.SetString(LastModifiedKey, modified);
            storage.SetInt(ResetGenerationKey, Math.Max(0, generation));
        }

        static MailSaveSnapshot CloneEnvelope(MailSaveSnapshot source)
        {
            var clone = new MailSaveSnapshot();
            if (source == null) return clone;
            clone.SetMetadata(source.CapturedAtUtc, source.LastModifiedUtc,
                source.ResetGeneration);
            for (int i = 0; i < source.MutableMails.Count; i++)
            {
                MailInstance mail = source.MutableMails[i];
                if (mail != null) clone.MutableMails.Add(MailInstance.Clone(mail));
            }
            return clone;
        }

        void RestoreInt(string key, bool hadValue, int value)
        {
            if (hadValue) storage.SetInt(key, value);
            else storage.DeleteKey(key);
        }

        void RestoreString(string key, bool hadValue, string value)
        {
            if (hadValue) storage.SetString(key, value);
            else storage.DeleteKey(key);
        }

        void Prune(List<MailInstance> mails, DateTime now)
        {
            mails.RemoveAll(mail => mail == null || string.IsNullOrWhiteSpace(mail.MailInstanceId));
            SortNewestFirst(mails);
            while (mails.Count > maximumStoredMail)
            {
                int removable = -1;
                for (int i = mails.Count - 1; i >= 0; i--)
                {
                    if (mails[i].IsClaimed || mails[i].IsExpired(now))
                    {
                        removable = i;
                        break;
                    }
                }
                mails.RemoveAt(removable >= 0 ? removable : mails.Count - 1);
            }
        }

        static void SortNewestFirst(List<MailInstance> mails)
        {
            mails.Sort((left, right) =>
            {
                if (ReferenceEquals(left, right)) return 0;
                if (left == null) return 1;
                if (right == null) return -1;
                return right.SentAtUtc.CompareTo(left.SentAtUtc);
            });
        }

        static MailInstance FindMutable(List<MailInstance> mails, string instanceId)
        {
            for (int i = 0; i < mails.Count; i++)
            {
                MailInstance mail = mails[i];
                if (mail != null && string.Equals(mail.MailInstanceId, instanceId,
                    StringComparison.Ordinal)) return mail;
            }
            return null;
        }

        static MailClaimResult ClaimResult(MailClaimStatus status, MailInstance mail,
            RewardGrantResult rewardResult, string message) =>
            new MailClaimResult(status, mail, rewardResult, message);

        static string FormatUtc(DateTime value) => ScheduleRange.NormalizeUtc(value)
            .ToString("O", CultureInfo.InvariantCulture);
    }
}
