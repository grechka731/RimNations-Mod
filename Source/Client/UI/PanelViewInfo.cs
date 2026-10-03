using Model;
using OCUnion;
using OCUnion.Transfer;
using OCUnion.Transfer.Model;
using RimWorld;
using RimWorld.Planet;
using RimWorldOnlineCity.GameClasses;
using RimWorldOnlineCity.Services;
using RimWorldOnlineCity.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Transfer.ModelMails;
using UnityEngine;
using Verse;

namespace RimWorldOnlineCity
{
    public class PanelViewInfo : DialogControlBase
    {
        private bool Inited;
        private Vector2 scrollPosLeft = Vector2.zero;
        private Vector2 scrollPosRight = Vector2.zero;
        private string selectedLogin = null;

        public void Init()
        {
            Inited = true;
        }

        public void Drow(Rect inRect)
        {
            if (!Inited) Init();

            var myLogin = SessionClientController.My?.Login;
            var myStateName = SessionClientController.My?.StateName ?? (myLogin + " Держава");

            // 1. ВЕРХНИЙ БАННЕР МОЕГО ГОСУДАРСТВА
            var topRect = new Rect(inRect.x, inRect.y, inRect.width, 58f);
            Widgets.DrawMenuSection(topRect);

            var allBases = Find.WorldObjects != null 
                ? Find.WorldObjects.AllWorldObjects.OfType<BaseOnline>().ToList() 
                : new List<BaseOnline>();
            
            var myBases = allBases.Where(b => b.OnlineWObject != null && b.OnlineWObject.LoginOwner == myLogin).ToList();
            var myCapital = myBases.FirstOrDefault(b => b.OnlineWObject.IsCapital) ?? myBases.FirstOrDefault();
            string myCapitalName = myCapital != null ? myCapital.OnlineWObject.Name : "Не основана";

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(topRect.x + 10f, topRect.y + 4f, topRect.width - 20f, 26f),
                $"👑 Ваше государство: {myStateName} | Правитель: {myLogin}");

            Text.Font = GameFont.Small;
            GUI.color = new Color(0.8f, 0.9f, 0.8f);
            Widgets.Label(new Rect(topRect.x + 10f, topRect.y + 30f, topRect.width - 20f, 22f),
                $"Столица: {myCapitalName} | Городов: {myBases.Count} | Экспансия: строго в соседних чанках (≤ 5 кл. от границы)");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            // 2. ДВУХКОЛОНОЧНЫЙ ИНТЕРФЕЙС
            float leftWidth = 280f;
            var leftRect = new Rect(inRect.x, inRect.y + 65f, leftWidth, inRect.height - 65f);
            var rightRect = new Rect(inRect.x + leftWidth + 10f, inRect.y + 65f, inRect.width - leftWidth - 10f, inRect.height - 65f);

            DrawLeftList(leftRect, myLogin, allBases);
            DrawRightDetails(rightRect, myLogin, myStateName, allBases);
        }

        private void DrawLeftList(Rect rect, string myLogin, List<BaseOnline> allBases)
        {
            Widgets.DrawMenuSection(rect);
            var titleRect = new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 24f);
            Text.Font = GameFont.Small;
            Widgets.Label(titleRect, "ГОСУДАРСТВА МИРА:");

            var contentRect = new Rect(rect.x + 4f, rect.y + 32f, rect.width - 8f, rect.height - 36f);

            var players = SessionClientController.Data.Players.Values
                .Where(p => p.Public != null && p.Public.Login != myLogin)
                .OrderByDescending(p => p.Online)
                .ThenBy(p => p.Public.Login)
                .ToList();

            float rowHeight = 44f;
            var viewRect = new Rect(0, 0, contentRect.width - 16f, Math.Max(contentRect.height, players.Count * rowHeight));

            Widgets.BeginScrollView(contentRect, ref scrollPosLeft, viewRect);
            float curY = 0;

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                var rowRect = new Rect(0, curY, viewRect.width, rowHeight - 2f);

                if (selectedLogin == p.Public.Login)
                {
                    Widgets.DrawHighlightSelected(rowRect);
                }
                else
                {
                    Widgets.DrawHighlightIfMouseover(rowRect);
                }

                string statusText;
                Color statusColor;
                GetDiplomacyStatus(myLogin, p.Public.Login, out statusText, out statusColor);

                string stateName = p.Public.StateName ?? (p.Public.Login + " Держава");

                // Индикатор онлайн
                Rect circleRect = new Rect(rowRect.x + 4f, rowRect.y + 8f, 10f, 10f);
                Widgets.DrawBoxSolid(circleRect, p.Online ? Color.green : Color.gray);

                // Название государства
                Rect textRect = new Rect(rowRect.x + 18f, rowRect.y + 2f, rowRect.width - 22f, 20f);
                Text.Font = GameFont.Small;
                Widgets.Label(textRect, stateName);

                // Правитель и статус
                Rect subTextRect = new Rect(rowRect.x + 18f, rowRect.y + 22f, rowRect.width - 22f, 18f);
                Text.Font = GameFont.Tiny;
                GUI.color = statusColor;
                Widgets.Label(subTextRect, $"{p.Public.Login} [{statusText}]");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;

                if (Widgets.ButtonInvisible(rowRect))
                {
                    selectedLogin = p.Public.Login;
                }

                curY += rowHeight;
            }

            Widgets.EndScrollView();
        }

        private void DrawRightDetails(Rect rect, string myLogin, string myStateName, List<BaseOnline> allBases)
        {
            Widgets.DrawMenuSection(rect);

            if (string.IsNullOrEmpty(selectedLogin) || !SessionClientController.Data.Players.TryGetValue(selectedLogin, out var selectedPlayer))
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.Font = GameFont.Medium;
                GUI.color = Color.gray;
                Widgets.Label(rect, "Выберите государство из списка слева\nдля дипломатических действий");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                return;
            }

            var contentRect = new Rect(rect.x + 14f, rect.y + 12f, rect.width - 28f, rect.height - 24f);

            // Заголовок выбранного государства
            string stateName = selectedPlayer.Public.StateName ?? (selectedPlayer.Public.Login + " Держава");
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(contentRect.x, contentRect.y, contentRect.width, 30f), $"🚩 {stateName}");

            Text.Font = GameFont.Small;
            string onlineStr = selectedPlayer.Online ? "● Онлайн" : "○ Оффлайн (в защитном сне)";
            GUI.color = selectedPlayer.Online ? Color.green : Color.gray;
            Widgets.Label(new Rect(contentRect.x, contentRect.y + 32f, contentRect.width, 22f), $"Правитель: {selectedPlayer.Public.Login} ({onlineStr})");
            GUI.color = Color.white;

            // Города государства
            var targetBases = allBases.Where(b => b.OnlineWObject != null && b.OnlineWObject.LoginOwner == selectedPlayer.Public.Login).ToList();
            var targetCapital = targetBases.FirstOrDefault(b => b.OnlineWObject.IsCapital) ?? targetBases.FirstOrDefault();
            string capitalName = targetCapital != null ? targetCapital.OnlineWObject.Name : "Неизвестна";

            Widgets.Label(new Rect(contentRect.x, contentRect.y + 56f, contentRect.width, 22f), 
                $"👑 Столица: {capitalName} {(targetCapital != null ? $"[Клетка: {targetCapital.Tile}]" : "")}");
            Widgets.Label(new Rect(contentRect.x, contentRect.y + 78f, contentRect.width, 22f), 
                $"Всего городов под контролем: {targetBases.Count}");

            // Статус отношений
            string statusText;
            Color statusColor;
            GetDiplomacyStatus(myLogin, selectedPlayer.Public.Login, out statusText, out statusColor);

            var wars = SessionClientController.Data.Wars;
            var war = wars?.FirstOrDefault(w =>
                (w.AttackerLogin == myLogin && w.DefenderLogin == selectedPlayer.Public.Login) ||
                (w.DefenderLogin == myLogin && w.AttackerLogin == selectedPlayer.Public.Login));

            var alliances = SessionClientController.Data.Alliances;
            var alliance = alliances?.FirstOrDefault(a => a.ContainsPlayer(myLogin) && a.ContainsPlayer(selectedPlayer.Public.Login));

            var treaties = SessionClientController.Data.Treaties;
            var treaty = treaties?.FirstOrDefault(t => t.IsActive && 
                ((t.VictorLogin == myLogin && t.DefeatedLogin == selectedPlayer.Public.Login) || 
                 (t.DefeatedLogin == myLogin && t.VictorLogin == selectedPlayer.Public.Login)));

            Rect statusBox = new Rect(contentRect.x, contentRect.y + 105f, contentRect.width, 60f);
            Widgets.DrawBoxSolid(statusBox, new Color(0.15f, 0.15f, 0.15f));
            GUI.color = statusColor;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(statusBox.x + 8f, statusBox.y + 6f, statusBox.width - 16f, 26f), $"Статус: {statusText}");
            Text.Font = GameFont.Small;

            if (war != null && war.IsInPreparation)
            {
                var prepMin = Math.Max(1, (int)Math.Ceiling((war.ActiveTimeUtc - DateTime.UtcNow).TotalMinutes));
                GUI.color = Color.yellow;
                Widgets.Label(new Rect(statusBox.x + 8f, statusBox.y + 32f, statusBox.width - 16f, 22f), 
                    $"Идет мобилизация и подготовка! Штурм начнется через {prepMin} мин.");
            }
            else if (war != null && war.IsActiveNow)
            {
                GUI.color = Color.red;
                Widgets.Label(new Rect(statusBox.x + 8f, statusBox.y + 32f, statusBox.width - 16f, 22f), 
                    "Боевые действия активны! Разрешен штурм баз и столицы.");
            }
            else if (treaty != null)
            {
                GUI.color = new Color(1f, 0.7f, 0.2f);
                var daysLeft = Math.Max(1, (int)Math.Ceiling((treaty.ExpiresUtc - DateTime.UtcNow).TotalDays));
                Widgets.Label(new Rect(statusBox.x + 8f, statusBox.y + 32f, statusBox.width - 16f, 22f), 
                    $"Договор о мире и дани: осталось {daysLeft} дн. (ставка: {treaty.TributeWealthPercent:F0}%).");
            }
            else
            {
                GUI.color = Color.white;
                Widgets.Label(new Rect(statusBox.x + 8f, statusBox.y + 32f, statusBox.width - 16f, 22f), 
                    "Состояние мира. Нападение без объявления войны запрещено.");
            }
            GUI.color = Color.white;

            // Кнопки дипломатического взаимодействия (UI вместо чат-команд!)
            float btnY = contentRect.y + 180f;
            float btnWidth = 240f;
            float btnHeight = 36f;
            float btnSpacing = 8f;

            // Кнопка: ОБЪЯВИТЬ ВОЙНУ
            if (war == null || war.Status == WarStatus.Peace)
            {
                if (alliance == null)
                {
                    if (Widgets.ButtonText(new Rect(contentRect.x, btnY, btnWidth, btnHeight), "⚔ Объявить войну"))
                    {
                        Find.WindowStack.Add(new Dialog_MessageBox(
                            $"Вы действительно хотите объявить официальную войну государству {stateName} (Правитель: {selectedPlayer.Public.Login})?\n\nВнимание: у защитника будет 15 МИНУТ защитного экрана на подготовку обороны.",
                            "Объявить войну!",
                            () =>
                            {
                                SessionClientController.Command((connect) =>
                                {
                                    connect.PostingChat(1, $"/declarewar {selectedPlayer.Public.Login} Территориальный конфликт");
                                });
                            },
                            "Отмена",
                            null,
                            "Подтверждение войны"
                        ));
                    }
                    btnY += btnHeight + btnSpacing;
                }
            }

            // Кнопка: ПРЕДЛОЖИТЬ МИР (если идет война)
            if (war != null && (war.IsActiveNow || war.IsInPreparation))
            {
                if (Widgets.ButtonText(new Rect(contentRect.x, btnY, btnWidth, btnHeight), "🕊 Предложить мир"))
                {
                    SessionClientController.Command((connect) =>
                    {
                        connect.PostingChat(1, $"/peace offer {selectedPlayer.Public.Login}");
                    });
                    Messages.Message($"Предложение мира отправлено государству {stateName}!", MessageTypeDefOf.PositiveEvent);
                }
                btnY += btnHeight + btnSpacing;
            }

            // Кнопка: ПРЕДЛОЖИТЬ СОЮЗ / АЛЬЯНС
            if ((war == null || war.Status == WarStatus.Peace) && alliance == null)
            {
                if (Widgets.ButtonText(new Rect(contentRect.x, btnY, btnWidth, btnHeight), "🤝 Предложить союз (Альянс)"))
                {
                    SessionClientController.Command((connect) =>
                    {
                        connect.PostingChat(1, $"/alliance invite {selectedPlayer.Public.Login}");
                    });
                    Messages.Message($"Предложение военного союза отправлено правителю {selectedPlayer.Public.Login}!", MessageTypeDefOf.PositiveEvent);
                }
                btnY += btnHeight + btnSpacing;
            }

            // Кнопка: РАЗОРВАТЬ СОЮЗ
            if (alliance != null)
            {
                if (Widgets.ButtonText(new Rect(contentRect.x, btnY, btnWidth, btnHeight), "❌ Разорвать союз"))
                {
                    Find.WindowStack.Add(new Dialog_MessageBox(
                        $"Разорвать военный союз с государством {stateName}?",
                        "Разорвать",
                        () =>
                        {
                            SessionClientController.Command((connect) =>
                            {
                                connect.PostingChat(1, $"/alliance break {selectedPlayer.Public.Login}");
                            });
                            Messages.Message($"Вы расторгли союз с {stateName}!", MessageTypeDefOf.NegativeEvent);
                        },
                        "Отмена",
                        null
                    ));
                }
                btnY += btnHeight + btnSpacing;
            }

            // Кнопка: ПОКАЗАТЬ СТОЛИЦУ НА КАРТЕ МИРА
            if (targetCapital != null)
            {
                if (Widgets.ButtonText(new Rect(contentRect.x, btnY, btnWidth, btnHeight), "🗺 Показать столицу на карте"))
                {
                    CameraJumper.TryJump(new GlobalTargetInfo(targetCapital.Tile));
                    Find.World.renderer.wantedMode = WorldRenderMode.Planet;
                    Dialog_MainOnlineCity.ShowHide();
                }
                btnY += btnHeight + btnSpacing;
            }
        }

        private void GetDiplomacyStatus(string myLogin, string otherLogin, out string statusText, out Color statusColor)
        {
            var wars = SessionClientController.Data.Wars;
            var war = wars?.FirstOrDefault(w =>
                (w.AttackerLogin == myLogin && w.DefenderLogin == otherLogin) ||
                (w.DefenderLogin == myLogin && w.AttackerLogin == otherLogin));

            if (war != null)
            {
                if (war.IsInTruce)
                {
                    statusText = "Перемирие";
                    statusColor = Color.yellow;
                    return;
                }
                if (war.IsInPreparation)
                {
                    statusText = "Война (Подготовка)";
                    statusColor = new Color(1f, 0.4f, 0.4f);
                    return;
                }
                if (war.IsActiveNow)
                {
                    statusText = "ВОЙНА";
                    statusColor = Color.red;
                    return;
                }
            }

            var alliances = SessionClientController.Data.Alliances;
            var alliance = alliances?.FirstOrDefault(a => a.ContainsPlayer(myLogin) && a.ContainsPlayer(otherLogin));
            if (alliance != null)
            {
                statusText = "СОЮЗНИК";
                statusColor = Color.cyan;
                return;
            }

            var treaties = SessionClientController.Data.Treaties;
            var treaty = treaties?.FirstOrDefault(t => t.IsActive && 
                ((t.VictorLogin == myLogin && t.DefeatedLogin == otherLogin) || 
                 (t.DefeatedLogin == myLogin && t.VictorLogin == otherLogin)));
            if (treaty != null)
            {
                statusText = treaty.VictorLogin == myLogin ? "Дань нам" : "Мы платим дань";
                statusColor = new Color(1f, 0.7f, 0.2f);
                return;
            }

            statusText = "Нейтралитет";
            statusColor = new Color(0.7f, 0.7f, 0.7f);
        }
    }
}
