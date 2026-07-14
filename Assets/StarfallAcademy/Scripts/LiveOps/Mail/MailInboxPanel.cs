using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    /// <summary>Runtime-created inbox overlay intended to be hosted by LobbyScreen.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class MailInboxPanel : MonoBehaviour
    {
        const int PageSize = 7;

        sealed class MailRow
        {
            public GameObject Root;
            public Text Title;
            public Text Meta;
            public Text Badge;
        }

        readonly List<MailRow> rows = new List<MailRow>();
        readonly List<MailInstance> visibleMails = new List<MailInstance>();
        GameObject layer;
        LobbyUiFactory ui;
        LobbyToastOverlay toast;
        MailService service;
        string selectedMailId;
        Text inboxCount;
        Text detailTitle;
        Text detailSender;
        Text detailBody;
        Text detailExpiry;
        Text detailAttachments;
        Text emptyDetail;
        Button claimButton;
        Text claimButtonLabel;
        Text pageLabel;
        Button previousPageButton;
        Button nextPageButton;
        int pageIndex;

        public bool IsOpen => layer != null && layer.activeSelf;
        public int UnreadCount => service == null ? 0 : service.GetUnreadCount();

        public void Initialize(RectTransform parent, LobbyUiFactory factory,
            LobbyToastOverlay toastOverlay, MailService mailService = null)
        {
            ui = factory ?? new LobbyUiFactory(new LobbyTheme());
            toast = toastOverlay;
            service = mailService ?? MailService.Default;
            transform.SetParent(parent, false);
            RectTransform controller = (RectTransform)transform;
            controller.anchorMin = Vector2.zero;
            controller.anchorMax = Vector2.one;
            controller.offsetMin = controller.offsetMax = Vector2.zero;
            Build();
        }

        public void Open()
        {
            if (layer == null) return;
            layer.transform.SetAsLastSibling();
            layer.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (layer != null) layer.SetActive(false);
        }

        public void Refresh()
        {
            if (layer == null || service == null) return;
            IReadOnlyList<MailInstance> mails = service.GetMails();
            visibleMails.Clear();
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(mails.Count / (float)PageSize));
            pageIndex = Mathf.Clamp(pageIndex, 0, pageCount - 1);
            int first = pageIndex * PageSize;
            int last = Mathf.Min(mails.Count, first + PageSize);
            for (int i = first; i < last; i++)
                if (mails[i] != null) visibleMails.Add(mails[i]);

            inboxCount.text = "전체 " + mails.Count + "  ·  읽지 않음 "
                + service.GetUnreadCount() + "  ·  수령 가능 " + service.GetClaimableCount();
            for (int i = 0; i < rows.Count; i++)
            {
                MailRow row = rows[i];
                bool visible = i < visibleMails.Count;
                row.Root.SetActive(visible);
                if (!visible) continue;
                MailInstance mail = visibleMails[i];
                row.Title.text = mail.Title;
                row.Meta.text = mail.Sender + "  ·  " + FormatSent(mail);
                row.Badge.text = mail.IsClaimed ? "CLAIMED"
                    : mail.IsExpired(ContentTime.UtcNow) ? "EXPIRED"
                    : !mail.IsRead ? "NEW" : "OPEN";
            }
            pageLabel.text = (pageIndex + 1) + " / " + pageCount;
            previousPageButton.interactable = pageIndex > 0;
            nextPageButton.interactable = pageIndex + 1 < pageCount;

            MailInstance selected = FindVisible(selectedMailId);
            if (selected == null && visibleMails.Count > 0)
            {
                selected = visibleMails[0];
                selectedMailId = selected.MailInstanceId;
            }
            DrawDetail(selected);
        }

        void Build()
        {
            layer = new GameObject("Mail Inbox Layer", typeof(RectTransform));
            layer.transform.SetParent(transform, false);
            RectTransform layerRect = layer.GetComponent<RectTransform>();
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.offsetMin = layerRect.offsetMax = Vector2.zero;

            Image dim = ui.CreateImage("Mail Backdrop", layer.transform,
                UrbanFantasyStyle.Backdrop, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, true);
            Button backdrop = dim.gameObject.AddComponent<Button>();
            backdrop.transition = Selectable.Transition.None;
            backdrop.onClick.AddListener(Close);

            RectTransform card = ui.CreateImage("Mail Window", layer.transform,
                UrbanFantasyStyle.PanelStrong, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(1260, 760), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, card, UrbanFantasyStyle.StrongLine);
            ui.CreateImage("Mail Accent", card, UrbanFantasyStyle.Silver,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -3), new Vector2(-52, 2));
            ui.CreateText("Mail Eyebrow", "M A I L   I N B O X", card, 11,
                FontStyle.Normal, UrbanFantasyStyle.Muted, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -35), new Vector2(-78, 22), TextAnchor.MiddleLeft);
            ui.CreateText("Mail Title", "우편함", card, 30, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -72), new Vector2(-78, 42), TextAnchor.MiddleLeft);
            inboxCount = ui.CreateText("Mail Count", string.Empty, card, 12,
                FontStyle.Normal, UrbanFantasyStyle.Muted, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -107), new Vector2(-78, 24), TextAnchor.MiddleLeft);
            ui.CreateButton("Close Mail", card, new Vector2(1, 1), new Vector2(-34, -34),
                new Vector2(46, 46), "×", 27, UrbanFantasyStyle.PanelSoft, Close);

            RectTransform listPanel = ui.CreateImage("Mail List", card, new Color(.015f, .015f, .02f, .82f),
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(238, -16),
                new Vector2(410, -178), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, listPanel);
            for (int i = 0; i < PageSize; i++) CreateRow(listPanel, i);
            GameObject previous = ui.CreateButton("Previous Mail Page", listPanel,
                new Vector2(0, 0), new Vector2(50, 27), new Vector2(82, 34), "‹ 이전", 12,
                UrbanFantasyStyle.PanelSoft, () => ChangePage(-1));
            previousPageButton = previous.GetComponent<Button>();
            pageLabel = ui.CreateText("Mail Page", string.Empty, listPanel, 12,
                FontStyle.Bold, UrbanFantasyStyle.Muted, new Vector2(.5f, 0),
                new Vector2(.5f, 0), new Vector2(0, 27), new Vector2(110, 32),
                TextAnchor.MiddleCenter);
            GameObject next = ui.CreateButton("Next Mail Page", listPanel,
                new Vector2(1, 0), new Vector2(-50, 27), new Vector2(82, 34), "다음 ›", 12,
                UrbanFantasyStyle.PanelSoft, () => ChangePage(1));
            nextPageButton = next.GetComponent<Button>();

            RectTransform detail = ui.CreateImage("Mail Detail", card, UrbanFantasyStyle.PanelSoft,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(222, -16),
                new Vector2(-520, -178), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, detail);
            detailTitle = ui.CreateText("Detail Title", string.Empty, detail, 25,
                FontStyle.Normal, UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -42), new Vector2(-54, 38), TextAnchor.MiddleLeft);
            detailSender = ui.CreateText("Detail Sender", string.Empty, detail, 12,
                FontStyle.Normal, UrbanFantasyStyle.Muted, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -75), new Vector2(-54, 24), TextAnchor.MiddleLeft);
            ui.CreateImage("Detail Divider", detail, UrbanFantasyStyle.Line,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -99), new Vector2(-54, 2));
            detailBody = ui.CreateText("Detail Body", string.Empty, detail, 16,
                FontStyle.Normal, new Color(.88f, .88f, .91f, .78f),
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 28),
                new Vector2(-54, -268), TextAnchor.UpperLeft);
            detailBody.lineSpacing = 1.2f;
            detailExpiry = ui.CreateText("Detail Expiry", string.Empty, detail, 11,
                FontStyle.Normal, UrbanFantasyStyle.Muted, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 150), new Vector2(-54, 22), TextAnchor.MiddleLeft);
            detailAttachments = ui.CreateText("Detail Attachments", string.Empty, detail, 13,
                FontStyle.Normal, UrbanFantasyStyle.Gold, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 112), new Vector2(-54, 44), TextAnchor.MiddleLeft);
            emptyDetail = ui.CreateText("Empty Mail", "받은 우편이 없습니다.", detail, 18,
                FontStyle.Normal, UrbanFantasyStyle.Muted, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            GameObject claim = ui.CreateButton("Claim Mail Attachments", detail,
                new Vector2(.5f, 0), new Vector2(0, 48), new Vector2(270, 58),
                "첨부 보상 수령", 17, new Color(.18f, .17f, .14f, .98f), ClaimSelected);
            UrbanFantasyStyle.AddBorder(ui, claim.GetComponent<RectTransform>(),
                UrbanFantasyStyle.StrongLine);
            claimButton = claim.GetComponent<Button>();
            claimButtonLabel = claim.GetComponentInChildren<Text>();
            layer.SetActive(false);
        }

        void CreateRow(RectTransform parent, int index)
        {
            int capturedIndex = index;
            GameObject root = ui.CreateButton("Mail Row " + index, parent,
                new Vector2(.5f, 1), new Vector2(0, -48 - index * 76),
                new Vector2(378, 66), string.Empty, 12, UrbanFantasyStyle.PanelSoft,
                () => SelectRow(capturedIndex));
            UrbanFantasyStyle.AddBorder(ui, root.GetComponent<RectTransform>());
            Text title = ui.CreateText("Title", string.Empty, root.transform, 14,
                FontStyle.Normal, UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(-34, -20), new Vector2(-106, 24), TextAnchor.MiddleLeft);
            Text meta = ui.CreateText("Meta", string.Empty, root.transform, 10,
                FontStyle.Normal, UrbanFantasyStyle.Muted, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(-34, 15), new Vector2(-106, 18), TextAnchor.MiddleLeft);
            Text badge = ui.CreateText("Badge", string.Empty, root.transform, 9,
                FontStyle.Bold, UrbanFantasyStyle.Gold, new Vector2(1, .5f), new Vector2(1, .5f),
                new Vector2(-42, 0), new Vector2(70, 20), TextAnchor.MiddleRight);
            rows.Add(new MailRow { Root = root, Title = title, Meta = meta, Badge = badge });
        }

        void SelectRow(int index)
        {
            if (index < 0 || index >= visibleMails.Count) return;
            selectedMailId = visibleMails[index].MailInstanceId;
            service.MarkRead(selectedMailId);
            Refresh();
        }

        void ChangePage(int offset)
        {
            if (offset == 0) return;
            pageIndex = Mathf.Max(0, pageIndex + offset);
            selectedMailId = string.Empty;
            Refresh();
        }

        void DrawDetail(MailInstance mail)
        {
            bool hasMail = mail != null;
            emptyDetail.gameObject.SetActive(!hasMail);
            detailTitle.gameObject.SetActive(hasMail);
            detailSender.gameObject.SetActive(hasMail);
            detailBody.gameObject.SetActive(hasMail);
            detailExpiry.gameObject.SetActive(hasMail);
            detailAttachments.gameObject.SetActive(hasMail);
            claimButton.gameObject.SetActive(hasMail);
            if (!hasMail) return;

            bool expired = mail.IsExpired(ContentTime.UtcNow);
            detailTitle.text = mail.Title;
            detailSender.text = mail.Sender + "  ·  " + FormatSent(mail);
            detailBody.text = mail.Body;
            detailExpiry.text = mail.ExpiresAtUtc.HasValue
                ? "만료: " + mail.ExpiresAtUtc.Value.ToString("yyyy-MM-dd HH:mm 'UTC'")
                    + (expired ? "  ·  EXPIRED" : string.Empty)
                : "만료 기한 없음";
            detailAttachments.text = mail.Attachments == null || mail.Attachments.IsEmpty
                ? "첨부 보상 없음" : "첨부  ·  " + mail.Attachments.Summary;
            claimButton.interactable = !mail.IsClaimed && !expired;
            claimButtonLabel.text = mail.IsClaimed ? "수령 완료"
                : expired ? "만료됨" : mail.Attachments.IsEmpty ? "우편 확인" : "첨부 보상 수령";
        }

        void ClaimSelected()
        {
            MailClaimResult result = service.Claim(selectedMailId);
            if (toast != null) toast.Show(result.Message);
            Refresh();
        }

        MailInstance FindVisible(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return null;
            for (int i = 0; i < visibleMails.Count; i++)
                if (visibleMails[i].MailInstanceId == instanceId) return visibleMails[i];
            return null;
        }

        static string FormatSent(MailInstance mail)
        {
            DateTime value = mail.SentAtUtc;
            return value == DateTime.MinValue ? "발송 시각 없음"
                : value.ToString("yyyy-MM-dd HH:mm 'UTC'");
        }
    }
}
