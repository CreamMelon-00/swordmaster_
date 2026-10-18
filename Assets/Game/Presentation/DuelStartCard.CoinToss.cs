using System;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    public sealed partial class DuelStartCard
    {
        public const float MinimumCoinTossSeconds = 2.8f;
        private const float TossBegins = .12f, TossLands = .6f;
        private RectTransform coinRoot, coinFrame;
        private CanvasGroup coinGroup, coinResultGroup;
        private DuelTossCoin coin;
        private Image coinShadow;
        private Text coinFace, coinResult, coinCaption;
        private bool isCoinToss, coinResultRevealed, localOpens;

        public bool IsCoinToss => isCoinToss && IsShowing;
        public bool CoinResultRevealed => IsCoinToss && coinResultRevealed;
        public string CoinResultText => coinResult == null ? string.Empty : coinResult.text;

        /// <summary>Shows the already chosen opening player. The host supplies the outcome; this view never rolls it.
        /// Positive durations allow the complete toss and reveal. Zero still disables the card for tests or tooling.</summary>
        public void ShowVersusCoinToss(float seconds, int openingPlayer, int localPlayer)
        {
            if (disposed) return;
            if (openingPlayer < 0 || openingPlayer > 1) throw new ArgumentOutOfRangeException(nameof(openingPlayer));
            if (localPlayer < 0 || localPlayer > 1) throw new ArgumentOutOfRangeException(nameof(localPlayer));
            float duration = seconds > 0f && !float.IsInfinity(seconds)
                ? Mathf.Max(MinimumCoinTossSeconds, seconds) : 0f;
            Show(duration, "상대", PlayerSilhouette, "나", mirrorOpponent: true);
            if (!IsShowing) return;
            localOpens = openingPlayer == localPlayer;
            isCoinToss = true;
            coinRoot.gameObject.SetActive(true);
            swordsGroup.gameObject.SetActive(false);
            Apply();
        }

        private void CreateCoinToss()
        {
            coinRoot = Rect("Versus Coin Toss", root);
            Stretch(coinRoot);
            coinGroup = coinRoot.gameObject.AddComponent<CanvasGroup>();
            coinGroup.interactable = coinGroup.blocksRaycasts = false;

            coinShadow = Graphic<Image>("Coin Ground Shadow", coinRoot);
            coinShadow.sprite = backlightSprite;
            coinShadow.rectTransform.anchoredPosition = new Vector2(0f, -106f);
            coinShadow.rectTransform.sizeDelta = new Vector2(235f, 42f);
            coinShadow.color = new Color(0f, 0f, 0f, .5f);
            coinShadow.raycastTarget = false;

            coinFrame = Rect("Tossed Coin", coinRoot);
            coinFrame.sizeDelta = Vector2.one * 230f;
            coin = Graphic<DuelTossCoin>("Brass Coin", coinFrame);
            Stretch(coin.rectTransform);
            coin.raycastTarget = false;
            coinFace = CoinText("Coin Face", coinFrame, 0f, 47, 210f, 80f, DuelVisualTheme.Ink);

            RectTransform result = Rect("Coin Result", coinRoot);
            result.anchoredPosition = new Vector2(0f, -196f);
            result.sizeDelta = new Vector2(500f, 110f);
            coinResultGroup = result.gameObject.AddComponent<CanvasGroup>();
            coinResultGroup.interactable = coinResultGroup.blocksRaycasts = false;
            coinResult = CoinText("Opening Player", result, 17f, 45, 500f, 65f, DuelVisualTheme.Foreground);
            coinCaption = CoinText("First Turn Caption", result, -40f, 24, 360f, 40f, DuelVisualTheme.Accent);
        }

        private Text CoinText(string name, Transform parent, float y, int size, float width, float height, Color color)
        {
            Text text = Graphic<Text>(name, parent);
            text.rectTransform.anchoredPosition = new Vector2(0f, y);
            text.rectTransform.sizeDelta = new Vector2(width, height);
            text.font = art.UIFont;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.color = color;
            if (parent != coinFrame)
            {
                Shadow shadow = text.gameObject.AddComponent<Shadow>();
                shadow.effectColor = NameShadow;
                shadow.effectDistance = new Vector2(2f, -2f);
            }
            return text;
        }

        private void ApplyCoinToss()
        {
            if (!isCoinToss) return;
            float progress = timeline.Progress;
            float toss = Mathf.Clamp01((progress - TossBegins) / (TossLands - TossBegins));
            bool landed = progress >= TossLands;
            float settle = Mathf.Clamp01((progress - TossLands) / .075f);
            float rise = Mathf.Sin(toss * Mathf.PI) * 160f;
            float bounce = landed ? Mathf.Sin(settle * Mathf.PI) * 13f * (1f - settle) : 0f;
            coinFrame.anchoredPosition = new Vector2(0f, FigureY + rise + bounce);
            // Full turns finish face-on. The visible face contains no player identity until the coin has landed.
            coin.SetPose(landed ? 0f : toss * 1080f, landed, localOpens,
                landed ? 1f - settle : 0f);
            coinShadow.rectTransform.localScale = new Vector3(1f - rise / 480f, 1f - rise / 700f, 1f);
            coinGroup.alpha = Mathf.SmoothStep(0f, 1f, progress / .13f) * timeline.Presence;
            coinResultGroup.alpha = landed ? Mathf.SmoothStep(0f, 1f, settle * 2f) : 0f;
            if (landed && !coinResultRevealed)
            {
                coinResultRevealed = true;
                coinFace.text = localOpens ? "나" : "상대";
                coinResult.text = localOpens ? "선공 · 나" : "선공 · 상대";
                coinCaption.text = "첫 턴 예약";
            }
        }

        private void ResetCoinToss()
        {
            isCoinToss = coinResultRevealed = false;
            if (coinRoot == null) return;
            coinRoot.gameObject.SetActive(false);
            coinFace.text = coinResult.text = coinCaption.text = string.Empty;
            coinResultGroup.alpha = 0f;
            swordsGroup.gameObject.SetActive(true);
        }
    }
}
