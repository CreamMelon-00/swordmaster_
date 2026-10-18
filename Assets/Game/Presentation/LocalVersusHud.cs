using System;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Versus's thin PvP layer over the duel HUD (<see cref="LegacyCombatHud"/>, the one the campaign's battles use,
    /// which <see cref="LocalVersusController"/> drives from the viewer's side). It shows only what single play has no place
    /// for: whose reservation turn it is (내 차례 / 상대 차례 / 판정 중 / 승부 종료) on a plate under the turn header, a hint under
    /// that (the current planning step or a request waiting for the host), both fighters' remaining ACT beside their queues,
    /// each fighter's identity at its feet with a ring
    /// under the one whose turn it is, and the way out: a 타이틀 [Esc] button and a confirmation that never pauses the duel
    /// (the clocks are the host's). Match rules, input and the result card stay with the controller.</summary>
    public sealed partial class LocalVersusHud : IDisposable
    {
        /// <summary>Over the duel HUD (<see cref="LegacyCombatHud.SortingOrder"/>, 100), under the bars
        /// (<see cref="DuelLetterbox"/>, 350) and the start card (355). The foot markers alone sit under the duel HUD
        /// (<see cref="FootMarkerSortingOrder"/>).</summary>
        public const int SortingOrder = 110;
        /// <summary>The exit confirmation's own canvas: over the result card (<see cref="BattleResultHud"/>, 400) too.</summary>
        public const int ExitSortingOrder = 410;
        public const string MyTurnText = "내 차례", OpponentTurnText = "상대 차례", ResolvingText = "판정 중",
            FinishedText = "승부 종료";
        public const string RequestPendingHint = "전송 중", FirstPlannerHint = "기술을 모두 예약한 뒤 확정하세요.",
            ResponsePlannerHint = "상대 예약을 확인하고 대응하세요.", WaitingForResponseHint = "예약 완료 · 상대를 기다립니다.",
            OpponentChoosingHint = "상대가 기술을 예약하는 중입니다.";
        public const string ExitQuestion = "대전을 끝내고 타이틀로 돌아갈까요?";
        private const float TurnPlateY = -98f, HintPlateY = -136f;
        private readonly LegacyDuelArt art;
        private readonly RectTransform root, battleLayer;
        private readonly GameObject ownedEventSystem;
        private readonly Text activeLabel, hintLabel;
        private readonly GameObject hintPlate;
        private readonly GameObject exitOverlay;
        private readonly Button titleButton, confirmExitButton, cancelExitButton;
        public const string OwnActBadgeName = "Versus Own ACT", OpponentActBadgeName = "Versus Opponent ACT";
        private const float ActBadgeWidth = 190f, ActBadgeHeight = 44f, ActBadgeGap = 10f;
        private readonly ActBadge[] actBadges = new ActBadge[2];
        private readonly RectTransform[] actQueueAnchors = new RectTransform[2];
        private readonly int actStealThreshold;
        private bool disposed;

        public GameObject Root => root.gameObject;
        public bool IsExitConfirming => !disposed && exitOverlay.activeSelf;
        public bool IsVisible => !disposed && root.gameObject.activeSelf;
        /// <summary>Whether the battle's layer (turn plate, hint, foot markers, exit button) shows; the result card and the
        /// start card take its place (<see cref="SetBattleVisible"/>).</summary>
        public bool IsBattleVisible => IsVisible && battleLayer.gameObject.activeSelf;
        /// <summary>The turn plate's words and the hint under it ("" while there is none).</summary>
        public string TurnText => activeLabel.text;
        public string HintText => hintPlate.activeSelf ? hintLabel.text : string.Empty;
        public string OwnActText => actBadges[0].Label.text;
        public string OpponentActText => actBadges[1].Label.text;
        public bool OwnActAtRisk => actBadges[0].AtRisk;
        public bool OpponentActAtRisk => actBadges[1].AtRisk;

        /// <param name="exit">The confirmation's 타이틀로: leaves the duel.</param>
        public LocalVersusHud(Transform parent, LegacyDuelArt art, Action exit)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            actStealThreshold = LegacySkillDefinitions.Find(62)?.Effect?.ActStealThreshold ?? 8;
            if (actStealThreshold <= 0) actStealThreshold = 8;
            root = Rect("Local Versus HUD", parent, Vector2.zero, Vector2.zero);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = SortingOrder;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            if (EventSystem.current == null)
            {
                ownedEventSystem = new GameObject("Local Versus EventSystem", typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                ownedEventSystem.transform.SetParent(parent, false);
                ownedEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            battleLayer = Rect("Versus Battle Layer", root, Vector2.zero, Vector2.zero);
            Stretch(battleLayer);
            // Under the duel HUD's turn header (its crossed flags hang 36 below the top), on the same paper and brass.
            var plate = Panel("Versus Turn State", battleLayer, Vector2.zero, new Vector2(220f, 40f),
                new Color(DuelVisualTheme.Surface.r, DuelVisualTheme.Surface.g, DuelVisualTheme.Surface.b, .94f));
            DuelVisualTheme.Frame(plate);
            PinTop(plate.rectTransform, TurnPlateY);
            activeLabel = Label("Versus Active Player", plate.transform, Vector2.zero, new Vector2(210f, 34f), 23,
                DuelVisualTheme.Foreground);
            var hint = Panel("Versus Turn Hint", battleLayer, Vector2.zero, new Vector2(380f, 30f),
                new Color(DuelVisualTheme.Surface.r, DuelVisualTheme.Surface.g, DuelVisualTheme.Surface.b, .88f));
            DuelVisualTheme.Frame(hint);
            PinTop(hint.rectTransform, HintPlateY);
            hintPlate = hint.gameObject;
            hintLabel = Label("Versus Turn Hint Label", hint.transform, Vector2.zero, new Vector2(368f, 26f), 17,
                DuelVisualTheme.Muted);
            hintPlate.SetActive(false);
            titleButton = ActionButton("Versus Exit", battleLayer, Vector2.zero, new Vector2(172f, 38f),
                "타이틀  [Esc]", ToggleExitConfirmation);
            var exitRect = titleButton.GetComponent<RectTransform>();
            exitRect.anchorMin = exitRect.anchorMax = exitRect.pivot = Vector2.one;
            exitRect.anchoredPosition = new Vector2(-26f, -18f);
            BuildTurnMarkers();
            BuildActBadges();

            // Its own canvas keeps the question over the result card as well; nothing pauses while it is up.
            var exitVeil = Panel("Versus Exit Confirmation", root, Vector2.zero, Vector2.zero,
                new Color(DuelVisualTheme.Track.r, DuelVisualTheme.Track.g, DuelVisualTheme.Track.b, .83f));
            Stretch(exitVeil.rectTransform);
            exitVeil.raycastTarget = true;
            var exitCanvas = exitVeil.gameObject.AddComponent<Canvas>();
            exitCanvas.overrideSorting = true;
            exitCanvas.sortingOrder = ExitSortingOrder;
            exitVeil.gameObject.AddComponent<GraphicRaycaster>();
            exitOverlay = exitVeil.gameObject;
            var exitCard = Panel("Versus Exit Card", exitVeil.transform, Vector2.zero,
                new Vector2(660f, 260f), DuelVisualTheme.Surface);
            DuelVisualTheme.DressPanel(exitCard);
            Label("Versus Exit Question", exitCard.transform, new Vector2(0f, 49f),
                new Vector2(590f, 86f), 27, DuelVisualTheme.Foreground).text = ExitQuestion;
            confirmExitButton = ActionButton("Versus Confirm Exit", exitCard.transform,
                new Vector2(-133f, -74f), new Vector2(230f, 56f), "타이틀로", exit, true);
            cancelExitButton = ActionButton("Versus Cancel Exit", exitCard.transform,
                new Vector2(133f, -74f), new Vector2(230f, 56f), "계속하기  [Esc]",
                ToggleExitConfirmation);
            exitOverlay.SetActive(false);
        }

        /// <summary>Shows the match from <paramref name="localPlayer"/>'s side (0 = Left, the host; 1 = Right, the guest): whose
        /// turn it is, the hint, and the foot markers under the actors (<see cref="SetActorAnchors"/>: own on the left).</summary>
        /// <param name="requestPending">The guest's command waits for the host's approval.</param>
        public void Refresh(LocalVersusMatch current, int localPlayer, bool requestPending = false)
        {
            if (disposed || current == null) return;
            if (localPlayer < 0 || localPlayer > 1) throw new ArgumentOutOfRangeException(nameof(localPlayer));
            bool planning = current.Phase == LegacyDuelPhase.Planning && current.CurrentPlanner >= 0;
            bool mine = planning && current.CurrentPlanner == localPlayer;
            activeLabel.text = TurnLabel(current, localPlayer);
            activeLabel.color = !planning ? DuelVisualTheme.Foreground : mine ? DuelVisualTheme.Accent : DuelVisualTheme.Danger;
            string hint = Hint(current, localPlayer, requestPending);
            hintPlate.SetActive(!string.IsNullOrEmpty(hint));
            hintLabel.text = hint;
            hintLabel.color = mine && requestPending ? DuelVisualTheme.Accent : DuelVisualTheme.Muted;
            RefreshTurnMarkers(current, localPlayer, planning);
            RefreshActBadges(current, localPlayer);
        }

        /// <summary>The duel HUD's queue trays, already in viewer order: own on the left, opponent on the right. The ACT
        /// badges follow their outer edges as the command dock folds, so both values remain visible during resolution.</summary>
        public void SetQueueAnchors(RectTransform ownQueue, RectTransform opponentQueue)
        {
            actQueueAnchors[0] = ownQueue;
            actQueueAnchors[1] = opponentQueue;
        }

        private void BuildActBadges()
        {
            for (int side = 0; side < 2; side++)
            {
                Image plate = Panel(side == 0 ? OwnActBadgeName : OpponentActBadgeName, battleLayer,
                    Vector2.zero, new Vector2(ActBadgeWidth, ActBadgeHeight), DuelVisualTheme.Surface);
                DuelVisualTheme.Frame(plate);
                Text label = Label("Versus ACT Amount", plate.transform, new Vector2(0f, 2f),
                    new Vector2(ActBadgeWidth - 12f, 34f), 22, DuelVisualTheme.Foreground);
                Image track = Panel("ACT Track", plate.transform, new Vector2(0f, -15f),
                    new Vector2(ActBadgeWidth - 20f, 3f), DuelVisualTheme.Track);
                Image fill = Panel("ACT Fill", track.transform, Vector2.zero, Vector2.zero, DuelVisualTheme.Accent);
                RectTransform fillRect = fill.rectTransform;
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = new Vector2(0f, 1f);
                fillRect.pivot = new Vector2(0f, .5f);
                actBadges[side] = new ActBadge(plate, label, fill);
            }
        }

        private void RefreshActBadges(LocalVersusMatch current, int localPlayer)
        {
            Vector2 size = root.rect.size;
            if (size.x < 1f || size.y < 1f) size = new Vector2(1920f, 1080f);
            for (int side = 0; side < 2; side++)
            {
                ActBadge badge = actBadges[side];
                LocalVersusFighter fighter = current.GetFighter(side == 0 ? localPlayer : 1 - localPlayer);
                if (badge.Act != fighter.Act || badge.Maximum != fighter.MaximumAct)
                {
                    badge.Act = fighter.Act;
                    badge.Maximum = fighter.MaximumAct;
                    badge.AtRisk = fighter.Act >= actStealThreshold;
                    badge.Label.text = (side == 0 ? "나" : "상대") + "  ACT " + fighter.Act + " / " + fighter.MaximumAct;
                    badge.Plate.color = badge.AtRisk ? DuelVisualTheme.Danger
                        : side == 0 ? DuelVisualTheme.Paper : DuelVisualTheme.Surface;
                    badge.Label.color = badge.AtRisk || side == 0 ? DuelVisualTheme.Ink : DuelVisualTheme.Foreground;
                    badge.Fill.color = badge.AtRisk ? DuelVisualTheme.Ink
                        : side == 0 ? DuelVisualTheme.Border : DuelVisualTheme.Accent;
                    badge.Fill.rectTransform.anchorMax = new Vector2(
                        fighter.MaximumAct > 0 ? Mathf.Clamp01((float)fighter.Act / fighter.MaximumAct) : 0f, 1f);
                }

                float direction = side == 0 ? -1f : 1f;
                RectTransform queue = actQueueAnchors[side];
                Vector2 point;
                if (queue != null)
                {
                    Vector3 edge = queue.TransformPoint(new Vector3(direction * queue.rect.width * .5f, 0f, 0f));
                    Vector3 local = root.InverseTransformPoint(edge);
                    point = new Vector2(local.x, local.y);
                    point.x += direction * (ActBadgeGap + ActBadgeWidth * .5f);
                }
                else
                {
                    // Direct HUD users without queue anchors still see the values by the normal planning trays.
                    point = new Vector2(direction * (LegacyQueuePanelLayout.DefaultPanelWidth +
                        LegacyQueuePanelLayout.DefaultSeamGap * .5f + ActBadgeGap + ActBadgeWidth * .5f),
                        -size.y * .5f + 300f);
                }
                point.x = Mathf.Clamp(point.x, -size.x * .5f + ActBadgeWidth * .5f + 12f,
                    size.x * .5f - ActBadgeWidth * .5f - 12f);
                badge.Plate.rectTransform.anchoredPosition = point;
            }
        }

        private sealed class ActBadge
        {
            public readonly Image Plate, Fill;
            public readonly Text Label;
            public int Act = -1, Maximum = -1;
            public bool AtRisk;

            public ActBadge(Image plate, Text label, Image fill)
            {
                Plate = plate;
                Label = label;
                Fill = fill;
            }
        }

        /// <summary>The turn plate's words for <paramref name="localPlayer"/>.</summary>
        public static string TurnLabel(LocalVersusMatch match, int localPlayer)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            if (match.Phase == LegacyDuelPhase.Planning && match.CurrentPlanner >= 0)
                return match.CurrentPlanner == localPlayer ? MyTurnText : OpponentTurnText;
            return match.Phase == LegacyDuelPhase.Resolving ? ResolvingText : FinishedText;
        }

        /// <summary>The hint under the turn plate for <paramref name="localPlayer"/>, or "" for none: the request waiting for
        /// the host, the whole queue to prepare, the public queue to answer, or the opponent still planning.</summary>
        public static string Hint(LocalVersusMatch match, int localPlayer, bool requestPending)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            if (match.Phase != LegacyDuelPhase.Planning || match.CurrentPlanner < 0) return string.Empty;
            bool mine = match.CurrentPlanner == localPlayer;
            if (mine && requestPending) return RequestPendingHint;
            if (!mine) return match.CommittedPlannerCount > 0 ? WaitingForResponseHint : OpponentChoosingHint;
            return match.CommittedPlannerCount > 0 ? ResponsePlannerHint : FirstPlannerHint;
        }

        /// <summary>Shows or hides the battle's layer: the start card and the result card take its place. The exit
        /// confirmation stays available either way.</summary>
        public void SetBattleVisible(bool visible)
        {
            if (disposed) return;
            battleLayer.gameObject.SetActive(visible);
        }

        public void ToggleExitConfirmation()
        {
            if (disposed || !root.gameObject.activeSelf) return;
            exitOverlay.SetActive(!exitOverlay.activeSelf);
            ClearSelection();
        }

        public void Hide()
        {
            if (disposed) return;
            exitOverlay.SetActive(false);
            root.gameObject.SetActive(false);
        }

        public void Show()
        {
            if (disposed) return;
            root.gameObject.SetActive(true);
            battleLayer.gameObject.SetActive(true);
            exitOverlay.SetActive(false);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            titleButton.onClick.RemoveAllListeners();
            confirmExitButton.onClick.RemoveAllListeners();
            cancelExitButton.onClick.RemoveAllListeners();
            DestroyObject(root.gameObject);
            if (ownedEventSystem != null) DestroyObject(ownedEventSystem);
        }

        private Button ActionButton(string name, Transform parent, Vector2 position, Vector2 size,
            string caption, Action action, bool primary = false)
        {
            var image = Panel(name, parent, position, size,
                primary ? DuelVisualTheme.Accent : DuelVisualTheme.RaisedSurface);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(button, primary);
            Label("Button Label", image.transform, Vector2.zero, size - new Vector2(12f, 8f),
                20, primary ? DuelVisualTheme.Ink : DuelVisualTheme.Foreground).text = caption;
            button.onClick.AddListener(() => { ClearSelection(); action?.Invoke(); });
            return button;
        }

        private Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize,
            Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = art.UIFont;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            Image image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // Centred across the top edge, its centre this far below it.
        private static void PinTop(RectTransform rect, float y)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = new Vector2(0f, y);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = rect.sizeDelta = Vector2.zero;
        }

        private static void ClearSelection()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
