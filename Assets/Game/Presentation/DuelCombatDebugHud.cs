using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Compact controls over the debug arena. The preview owns its duel, timing and restart behavior.</summary>
    public sealed class DuelCombatDebugHud : IDisposable
    {
        public const int SortingOrder = 1500;
        public const int MaximumAttackCount = 10;

        private readonly LegacyDuelArt art;
        private readonly Action<int> setEnemyCount, setPlayerCount;
        private readonly Action<bool> setAttackRhythm, setWeaponTrail, setDeepThrust;
        private readonly Text enemyValue, playerValue, status, attackRhythmLabel, weaponTrailLabel, deepThrustLabel;
        private readonly Button enemyLess, enemyMore, playerLess, playerMore, restartButton, closeButton, attackRhythmButton,
            weaponTrailButton, deepThrustButton;
        private int enemyCount, playerCount;
        private bool attackRhythmEnabled, weaponTrailEnabled, deepThrustEnabled, disposed;

        public GameObject Root { get; }

        public DuelCombatDebugHud(Transform parent, LegacyDuelArt art, Action<int> setEnemyCount,
            Action<int> setPlayerCount, Action restart, Action close, Action<bool> setAttackRhythm = null,
            Action<bool> setWeaponTrail = null, Action<bool> setDeepThrust = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            this.setEnemyCount = setEnemyCount;
            this.setPlayerCount = setPlayerCount;
            this.setAttackRhythm = setAttackRhythm;
            this.setWeaponTrail = setWeaponTrail;
            this.setDeepThrust = setDeepThrust;

            RectTransform root = Rect("Combat Debug HUD", parent, Vector2.zero, Vector2.zero);
            Root = root.gameObject;
            var canvas = Root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = SortingOrder;
            var scaler = Root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            Root.AddComponent<GraphicRaycaster>();

            Image card = Panel("Combat Debug Controls", root, new Vector2(0f, -18f), new Vector2(1280f, 124f),
                DuelVisualTheme.Surface);
            card.rectTransform.anchorMin = card.rectTransform.anchorMax = new Vector2(.5f, 1f);
            card.rectTransform.pivot = new Vector2(.5f, 1f);
            card.raycastTarget = true;
            DuelVisualTheme.DressPanel(card);

            Label("Debug Title", card.transform, new Vector2(-465f, 34f), new Vector2(280f, 32f), 23,
                DuelVisualTheme.Foreground, TextAnchor.MiddleLeft).text = "전투 연출 · 디버그";
            status = Label("Debug Status", card.transform, new Vector2(-185f, 34f), new Vector2(270f, 30f), 17,
                DuelVisualTheme.Muted);
            attackRhythmButton = ActionButton("Debug Attack Rhythm", card.transform, string.Empty, new Vector2(75f, 34f),
                new Vector2(210f, 36f), ToggleAttackRhythm);
            attackRhythmButton.interactable = setAttackRhythm != null;
            attackRhythmLabel = attackRhythmButton.GetComponentInChildren<Text>();
            weaponTrailButton = ActionButton("Debug Weapon Trail", card.transform, string.Empty, new Vector2(300f, 34f),
                new Vector2(210f, 36f), ToggleWeaponTrail);
            weaponTrailButton.interactable = setWeaponTrail != null;
            weaponTrailLabel = weaponTrailButton.GetComponentInChildren<Text>();
            closeButton = ActionButton("Debug Close", card.transform, "F1 닫기", new Vector2(535f, 34f),
                new Vector2(140f, 36f), close);
            Panel("Debug Divider", card.transform, new Vector2(0f, 7f), new Vector2(1216f, 1f), DuelVisualTheme.Border);

            Label("Enemy Attack Label", card.transform, new Vector2(-348f, -28f), new Vector2(100f, 36f), 21,
                DuelVisualTheme.Foreground).text = "적 공격";
            enemyLess = ActionButton("Enemy Attack Less", card.transform, "-", new Vector2(-272f, -28f),
                new Vector2(38f, 38f), () => ChangeEnemy(-1));
            enemyValue = Label("Enemy Attack Count", card.transform, new Vector2(-222f, -28f), new Vector2(50f, 38f),
                26, DuelVisualTheme.Accent);
            enemyMore = ActionButton("Enemy Attack More", card.transform, "+", new Vector2(-172f, -28f),
                new Vector2(38f, 38f), () => ChangeEnemy(1));

            Label("Player Attack Label", card.transform, new Vector2(-48f, -28f), new Vector2(100f, 36f), 21,
                DuelVisualTheme.Foreground).text = "내 공격";
            playerLess = ActionButton("Player Attack Less", card.transform, "-", new Vector2(28f, -28f),
                new Vector2(38f, 38f), () => ChangePlayer(-1));
            playerValue = Label("Player Attack Count", card.transform, new Vector2(78f, -28f), new Vector2(50f, 38f),
                26, DuelVisualTheme.Accent);
            playerMore = ActionButton("Player Attack More", card.transform, "+", new Vector2(128f, -28f),
                new Vector2(38f, 38f), () => ChangePlayer(1));
            restartButton = ActionButton("Debug Restart", card.transform, "다시 재생", new Vector2(316f, -28f),
                new Vector2(164f, 40f), restart, true);
            deepThrustButton = ActionButton("Debug Deep Thrust", card.transform, string.Empty, new Vector2(510f, -28f),
                new Vector2(210f, 40f), ToggleDeepThrust);
            deepThrustButton.interactable = setDeepThrust != null;
            deepThrustLabel = deepThrustButton.GetComponentInChildren<Text>();
            Refresh(0, 0, 0);
        }

        public void Refresh(int enemyCount, int playerCount, int loopNumber, bool attackRhythmEnabled = true,
            bool weaponTrailEnabled = true, bool deepThrustEnabled = false)
        {
            if (disposed) return;
            this.enemyCount = Mathf.Clamp(enemyCount, 0, MaximumAttackCount);
            this.playerCount = Mathf.Clamp(playerCount, 0, MaximumAttackCount);
            RefreshCounts();
            this.attackRhythmEnabled = attackRhythmEnabled;
            RefreshAttackRhythm();
            this.weaponTrailEnabled = weaponTrailEnabled;
            RefreshWeaponTrail();
            this.deepThrustEnabled = deepThrustEnabled;
            RefreshDeepThrust();
            status.text = "자동 반복 · 양측 무적" + (loopNumber > 0 ? " · " + loopNumber + "회" : string.Empty);
        }

        private void ToggleAttackRhythm()
        {
            if (setAttackRhythm == null) return;
            attackRhythmEnabled = !attackRhythmEnabled;
            RefreshAttackRhythm();
            setAttackRhythm(attackRhythmEnabled);
        }

        private void RefreshAttackRhythm()
        {
            attackRhythmLabel.text = "공격 강조 · " + (attackRhythmEnabled ? "켬" : "끔");
            attackRhythmLabel.color = attackRhythmEnabled ? DuelVisualTheme.Accent : DuelVisualTheme.Muted;
            attackRhythmButton.targetGraphic.color = attackRhythmEnabled ? DuelVisualTheme.Selected : DuelVisualTheme.RaisedSurface;
        }

        private void ToggleWeaponTrail()
        {
            if (setWeaponTrail == null) return;
            weaponTrailEnabled = !weaponTrailEnabled;
            RefreshWeaponTrail();
            setWeaponTrail(weaponTrailEnabled);
        }

        private void RefreshWeaponTrail()
        {
            weaponTrailLabel.text = "베기 잔광 · " + (weaponTrailEnabled ? "켬" : "끔");
            weaponTrailLabel.color = weaponTrailEnabled ? DuelVisualTheme.Accent : DuelVisualTheme.Muted;
            weaponTrailButton.targetGraphic.color = weaponTrailEnabled ? DuelVisualTheme.Selected : DuelVisualTheme.RaisedSurface;
        }

        private void ToggleDeepThrust()
        {
            if (setDeepThrust == null) return;
            deepThrustEnabled = !deepThrustEnabled;
            RefreshDeepThrust();
            setDeepThrust(deepThrustEnabled);
        }

        private void RefreshDeepThrust()
        {
            deepThrustLabel.text = "깊은 찌르기 · " + (deepThrustEnabled ? "켬" : "끔");
            deepThrustLabel.color = deepThrustEnabled ? DuelVisualTheme.Accent : DuelVisualTheme.Muted;
            deepThrustButton.targetGraphic.color = deepThrustEnabled ? DuelVisualTheme.Selected : DuelVisualTheme.RaisedSurface;
        }

        private void ChangeEnemy(int delta)
        {
            int next = Mathf.Clamp(enemyCount + delta, 0, MaximumAttackCount);
            if (next == enemyCount) return;
            enemyCount = next;
            RefreshCounts();
            setEnemyCount?.Invoke(next);
        }

        private void ChangePlayer(int delta)
        {
            int next = Mathf.Clamp(playerCount + delta, 0, MaximumAttackCount);
            if (next == playerCount) return;
            playerCount = next;
            RefreshCounts();
            setPlayerCount?.Invoke(next);
        }

        private void RefreshCounts()
        {
            enemyValue.text = enemyCount.ToString();
            playerValue.text = playerCount.ToString();
            enemyLess.interactable = enemyCount > 0;
            enemyMore.interactable = enemyCount < MaximumAttackCount;
            playerLess.interactable = playerCount > 0;
            playerMore.interactable = playerCount < MaximumAttackCount;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (Button button in new[] { enemyLess, enemyMore, playerLess, playerMore, restartButton, closeButton,
                attackRhythmButton, weaponTrailButton, deepThrustButton })
                button.onClick.RemoveAllListeners();
            Root.SetActive(false);
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && selected.transform.IsChildOf(Root.transform)) EventSystem.current.SetSelectedGameObject(null);
            if (Application.isPlaying) UnityEngine.Object.Destroy(Root);
            else UnityEngine.Object.DestroyImmediate(Root);
        }

        private Button ActionButton(string name, Transform parent, string caption, Vector2 position, Vector2 size,
            Action action, bool primary = false)
        {
            Image face = Panel(name, parent, position, size, primary ? DuelVisualTheme.Accent : DuelVisualTheme.RaisedSurface);
            face.raycastTarget = true;
            var button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(button, primary);
            Label(name + " Label", face.transform, Vector2.zero, size - new Vector2(8f, 2f), 21,
                primary ? DuelVisualTheme.Ink : DuelVisualTheme.Foreground).text = caption;
            button.onClick.AddListener(() =>
            {
                if (disposed || !Root.activeInHierarchy) return;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                action?.Invoke();
            });
            return button;
        }

        private Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            Text label = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            label.font = art.UIFont;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.supportRichText = false;
            label.raycastTarget = false;
            return label;
        }

        private static Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            Image image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
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
    }
}
