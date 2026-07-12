using System;
using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    public static class LobbyProfileView
    {
        public static void Build(RectTransform root, LobbyUiFactory ui, Action<string, string> openPopup)
        {
            RectTransform panel = ui.CreateImage("Player Profile", root, new Color(.035f, .06f, .13f, .91f),
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(176, -237), new Vector2(300, 226)).rectTransform;
            ui.CreateText("Player Level", "Lv. 42", panel, 15, FontStyle.Bold, ui.Theme.Cyan,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(62, -29), new Vector2(80, 28), TextAnchor.MiddleLeft);
            ui.CreateText("Player Name", LobbyContent.TeacherName, panel, 29, FontStyle.Bold, ui.Theme.White,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -73), new Vector2(200, 42), TextAnchor.MiddleLeft);
            ui.CreateText("EXP Label", "EXP", panel, 13, FontStyle.Normal, ui.Theme.Muted,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(47, -116), new Vector2(50, 20), TextAnchor.MiddleLeft);
            Image track = ui.CreateImage("EXP Track", panel, new Color(1, 1, 1, .12f),
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(149, -141), new Vector2(254, 8));
            ui.CreateImage("EXP Fill", track.rectTransform, ui.Theme.Cyan, Vector2.zero, new Vector2(.68f, 1),
                Vector2.zero, Vector2.zero);
            ui.CreateText("EXP Value", "6,820 / 10,000", panel, 12, FontStyle.Normal, ui.Theme.Muted,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(149, -170), new Vector2(254, 22), TextAnchor.MiddleRight);

            GameObject notice = ui.CreateButton("Open Notice", root, new Vector2(0, 1), new Vector2(176, -406), new Vector2(300, 76),
                "공지사항\n업데이트 노트", 17, new Color(.05f, .09f, .18f, .94f),
                () => openPopup("공지사항", "7월 11일 업데이트\n\n· 메인 스토리 4장 추가\n· 신규 학생 아리아 등장\n· 편의 기능 개선"), TextAnchor.MiddleLeft);
            notice.GetComponentInChildren<Text>().rectTransform.offsetMin = new Vector2(78, 0);
            ui.AddIcon(notice, LobbyIcon.Mail, new Vector2(0, .5f), new Vector2(43, 0), new Vector2(56, 56));
            GameObject mission = ui.CreateButton("Open Daily Mission", root, new Vector2(0, 1), new Vector2(176, -494), new Vector2(300, 76),
                "데일리 미션\n5 / 7 완료", 17, new Color(.05f, .09f, .18f, .94f),
                () => openPopup("데일리 미션", "오늘의 진행도  5 / 7\n\n완료 가능한 보상이 2개 있습니다."), TextAnchor.MiddleLeft);
            mission.GetComponentInChildren<Text>().rectTransform.offsetMin = new Vector2(78, 0);
            ui.AddIcon(mission, LobbyIcon.Mission, new Vector2(0, .5f), new Vector2(43, 0), new Vector2(56, 56));
            ui.CreateBadge(root, new Vector2(0, 1), new Vector2(310, -444), "2");
        }
    }
}
