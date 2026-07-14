using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [CreateAssetMenu(fileName = "MailTemplate",
        menuName = "Starfall/LiveOps/Mail Template")]
    public sealed class MailTemplateData : ScriptableObject
    {
        [SerializeField] string templateId = "mail_template";
        [SerializeField] string title = "운영팀 선물";
        [SerializeField, TextArea(5, 12)] string body = "첨부된 보상을 받아 주세요.";
        [SerializeField] string sender = "Starfall Academy";
        [SerializeField, Min(0)] int defaultExpiryHours = 168;
        [SerializeField] RewardPackage attachments = new RewardPackage();

        public string TemplateId => string.IsNullOrWhiteSpace(templateId)
            ? name : templateId.Trim();
        public string Title => title ?? string.Empty;
        public string Body => body ?? string.Empty;
        public string Sender => string.IsNullOrWhiteSpace(sender) ? "Starfall Academy" : sender.Trim();
        public int DefaultExpiryHours => Mathf.Max(0, defaultExpiryHours);
        public RewardPackage Attachments => attachments ?? (attachments = new RewardPackage());

        void OnValidate()
        {
            templateId = templateId == null ? string.Empty : templateId.Trim();
            title = title ?? string.Empty;
            body = body ?? string.Empty;
            sender = sender ?? string.Empty;
            defaultExpiryHours = Mathf.Max(0, defaultExpiryHours);
            if (attachments == null) attachments = new RewardPackage();
        }
    }

    [CreateAssetMenu(fileName = "MailTemplateDatabase",
        menuName = "Starfall/LiveOps/Mail Template Database")]
    public sealed class MailTemplateDatabase : ScriptableObject
    {
        [SerializeField] List<MailTemplateData> templates = new List<MailTemplateData>();

        public IReadOnlyList<MailTemplateData> Templates => templates;

        public MailTemplateData Find(string templateId)
        {
            if (templates == null || string.IsNullOrWhiteSpace(templateId)) return null;
            string normalized = templateId.Trim();
            for (int i = 0; i < templates.Count; i++)
            {
                MailTemplateData template = templates[i];
                if (template != null && string.Equals(template.TemplateId, normalized,
                    StringComparison.Ordinal)) return template;
            }
            return null;
        }

        public void Add(MailTemplateData template)
        {
            if (template == null) return;
            if (templates == null) templates = new List<MailTemplateData>();
            if (!templates.Contains(template)) templates.Add(template);
        }

        public void Remove(MailTemplateData template)
        {
            if (templates != null) templates.Remove(template);
        }

        public bool Move(MailTemplateData template, int offset)
        {
            if (templates == null || template == null || offset == 0) return false;
            int index = templates.IndexOf(template);
            int target = index + offset;
            if (index < 0 || target < 0 || target >= templates.Count) return false;
            templates.RemoveAt(index);
            templates.Insert(target, template);
            return true;
        }

        void OnValidate()
        {
            if (templates == null) templates = new List<MailTemplateData>();
        }
    }

    [Serializable]
    public sealed class MailInstance
    {
        [SerializeField] string mailInstanceId;
        [SerializeField] string templateId;
        [SerializeField] string title;
        [SerializeField, TextArea(4, 10)] string body;
        [SerializeField] string sender;
        [SerializeField] string sentAtUtc;
        [SerializeField] string expiresAtUtc;
        [SerializeField] bool isRead;
        [SerializeField] bool isClaimed;
        [SerializeField] RewardPackage attachments = new RewardPackage();

        public string MailInstanceId => mailInstanceId ?? string.Empty;
        public string TemplateId => templateId ?? string.Empty;
        public string Title => title ?? string.Empty;
        public string Body => body ?? string.Empty;
        public string Sender => sender ?? string.Empty;
        public string SentAtUtcText => sentAtUtc ?? string.Empty;
        public string ExpiresAtUtcText => expiresAtUtc ?? string.Empty;
        public DateTime SentAtUtc => ParseUtc(sentAtUtc) ?? DateTime.MinValue;
        public DateTime? ExpiresAtUtc => ParseUtc(expiresAtUtc);
        public bool IsRead => isRead;
        public bool IsClaimed => isClaimed;
        public RewardPackage Attachments => attachments ?? (attachments = new RewardPackage());

        public bool IsExpired(DateTime utcNow)
        {
            DateTime? expiry = ExpiresAtUtc;
            return expiry.HasValue && ScheduleRange.NormalizeUtc(utcNow) >= expiry.Value;
        }

        internal static MailInstance Create(string instanceId, string sourceTemplateId,
            string resolvedTitle, string resolvedBody, string resolvedSender,
            DateTime sentUtc, DateTime? expiresUtc, RewardPackage resolvedAttachments)
        {
            return new MailInstance
            {
                mailInstanceId = instanceId ?? string.Empty,
                templateId = sourceTemplateId ?? string.Empty,
                title = resolvedTitle ?? string.Empty,
                body = resolvedBody ?? string.Empty,
                sender = resolvedSender ?? string.Empty,
                sentAtUtc = FormatUtc(sentUtc),
                expiresAtUtc = expiresUtc.HasValue ? FormatUtc(expiresUtc.Value) : string.Empty,
                attachments = CloneReward(resolvedAttachments),
                isRead = false,
                isClaimed = false
            };
        }

        internal void MarkRead() => isRead = true;
        internal void MarkClaimed()
        {
            isRead = true;
            isClaimed = true;
        }

        internal static MailInstance Clone(MailInstance source)
        {
            if (source == null) return null;
            return new MailInstance
            {
                mailInstanceId = source.MailInstanceId,
                templateId = source.TemplateId,
                title = source.Title,
                body = source.Body,
                sender = source.Sender,
                sentAtUtc = source.SentAtUtcText,
                expiresAtUtc = source.ExpiresAtUtcText,
                isRead = source.IsRead,
                isClaimed = source.IsClaimed,
                attachments = CloneReward(source.Attachments)
            };
        }

        internal static RewardPackage CloneReward(RewardPackage source)
        {
            if (source == null) return new RewardPackage();
            var items = new List<ItemReward>();
            if (source.ItemRewards != null)
            {
                for (int i = 0; i < source.ItemRewards.Count; i++)
                {
                    ItemReward item = source.ItemRewards[i];
                    if (item != null) items.Add(new ItemReward(item.ItemId, item.Amount));
                }
            }
            return new RewardPackage(source.Currency, items);
        }

        static string FormatUtc(DateTime value) => ScheduleRange.NormalizeUtc(value)
            .ToString("O", CultureInfo.InvariantCulture);

        static DateTime? ParseUtc(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTime parsed)) return null;
            return ScheduleRange.NormalizeUtc(parsed);
        }
    }

    public sealed class MailSendOptions
    {
        public bool OverrideTitle { get; set; }
        public string Title { get; set; }
        public bool OverrideBody { get; set; }
        public string Body { get; set; }
        public bool OverrideSender { get; set; }
        public string Sender { get; set; }
        public bool OverrideExpiryHours { get; set; }
        public int ExpiryHours { get; set; }
        public bool OverrideAttachments { get; set; }
        public RewardPackage Attachments { get; set; }
    }
}
