using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyCombatHud
    {
        private const float PowerClashMeetSeconds = .075f, PowerFragmentSeconds = .55f;
        private const int PowerFragmentColumns = 3, PowerFragmentRows = 2, MaximumPowerFragments = 24;
        private static readonly Vector2 PowerNumberSize = new Vector2(112f, 62f);
        private RectTransform powerClashLayer, powerClashNumbers;
        private Text playerClashPower, enemyClashPower, powerClashCaption;
        private ILegacySlotView powerClashSlot;
        private bool powerClashImpacted, powerNumberShattered, powerPlayerBroken, powerEnemyBroken;
        private float powerClashElapsed;
        private readonly List<PowerNumberFragment> powerNumberFragments = new List<PowerNumberFragment>();

        public bool PowerClashVisible => !disposed && powerClashNumbers != null && powerClashNumbers.gameObject.activeSelf;
        public string PlayerClashPowerText => PowerClashVisible && playerClashPower.gameObject.activeSelf ? playerClashPower.text : null;
        public string EnemyClashPowerText => PowerClashVisible && enemyClashPower.gameObject.activeSelf ? enemyClashPower.text : null;
        public int ActivePowerNumberFragmentCount
        {
            get
            {
                int count = 0;
                if (!disposed)
                    foreach (PowerNumberFragment fragment in powerNumberFragments)
                        if (fragment.Root.gameObject.activeSelf) count++;
                return count;
            }
        }

        private void BuildPowerClash()
        {
            powerClashLayer = Rect("Power Clash", root, Vector2.zero, new Vector2(420f, 100f), Vector2.one * .5f);
            powerClashNumbers = Rect("Current Power Numbers", powerClashLayer, Vector2.zero,
                new Vector2(420f, 100f), Vector2.one * .5f);
            playerClashPower = BuildPowerNumber("Player Final Power", powerClashNumbers, -1f);
            enemyClashPower = BuildPowerNumber("Enemy Final Power", powerClashNumbers, 1f);
            powerClashCaption = Text("Power Comparison", powerClashNumbers, new Vector2(0f, 34f),
                new Vector2(380f, 20f), 13, TextAnchor.MiddleCenter);
            powerClashCaption.color = MutedText;
            powerClashCaption.supportRichText = false;
            powerClashNumbers.gameObject.SetActive(false);
        }

        private Text BuildPowerNumber(string name, Transform parent, float side)
        {
            Text number = Text(name, parent, new Vector2(side * 80f, 0f), PowerNumberSize, 42, TextAnchor.MiddleCenter);
            number.supportRichText = false;
            number.fontStyle = FontStyle.Bold;
            number.horizontalOverflow = HorizontalWrapMode.Overflow;
            number.verticalOverflow = VerticalWrapMode.Truncate;
            var outline = number.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.effectColor = new Color(.035f, .045f, .055f, .95f);
            return number;
        }

        private void SetPowerClash(ILegacySlotView slot)
        {
            if (displayedSession?.UsePowerBreak != true || slot == null || !IsPowerClash(slot)) return;
            powerClashSlot = slot;
            powerClashElapsed = 0f;
            powerClashImpacted = powerNumberShattered = powerPlayerBroken = powerEnemyBroken = false;
            bool playerActs = slot.PlayerSkill != null && !slot.PlayerSkill.IsWait;
            bool enemyActs = slot.EnemySkill != null && !slot.EnemySkill.IsWait;
            playerClashPower.gameObject.SetActive(playerActs);
            enemyClashPower.gameObject.SetActive(enemyActs);
            playerClashPower.text = slot.PlayerTotalPower.ToString();
            enemyClashPower.text = slot.EnemyTotalPower.ToString();
            playerClashPower.color = Accent;
            enemyClashPower.color = Foreground;
            bool clash = IsPowerClash(slot);
            powerClashCaption.text = clash ? "위력 차이 " + LegacyPowerBreakRules.MinimumPowerDifference + " 이상이면 붕괴"
                : slot.PlayerBrokenGap || slot.EnemyBrokenGap ? "파괴된 빈틈 · 피해 2배" : "위력";
            powerClashNumbers.gameObject.SetActive(playerActs || enemyActs);
            PositionPowerNumbers(0f);
        }

        /// <summary>Call after the actual hit has resolved. The immutable rolled totals stay readable throughout the
        /// action; only an actual power-break result can shatter a number. Repeated multi-hits never replay the break.</summary>
        public void PlayPowerClashImpact(ILegacySlotView slot)
        {
            if (disposed || displayedSession?.UsePowerBreak != true || slot == null) return;
            if (!IsPowerClash(slot))
            {
                // A manual dodge may succeed after the two powers were already shown. The missed attack no longer
                // forms a clash, so close the previous comparison instead of suggesting a break can still occur.
                if (ReferenceEquals(powerClashSlot, slot)) ClearPowerClash(false);
                return;
            }
            if (!ReferenceEquals(powerClashSlot, slot)) SetPowerClash(slot);
            if (powerClashImpacted) return;
            powerClashImpacted = true;
            powerClashElapsed = 0f;
            // A counter can settle as the hit begins; read the final snapshot again without rolling anything here.
            playerClashPower.text = slot.PlayerTotalPower.ToString();
            enemyClashPower.text = slot.EnemyTotalPower.ToString();
            powerPlayerBroken = slot.PlayerBroken;
            powerEnemyBroken = slot.EnemyBroken;
            if (powerPlayerBroken || powerEnemyBroken)
                powerClashCaption.text = HasDestroyedFollowup(slot) ? "붕괴 · 다음 기술 파괴" : "붕괴";
        }

        private static bool HasDestroyedFollowup(ILegacySlotView slot)
            => slot is LegacyCurrentSlot current ? current.PlayerDestroyedQueueIndex >= 0 || current.EnemyDestroyedQueueIndex >= 0
                : slot is LocalVersusPerspectiveSlot versus &&
                    (versus.Source.LeftDestroyedQueueIndex >= 0 || versus.Source.RightDestroyedQueueIndex >= 0);

        private static bool IsPowerClash(ILegacySlotView slot)
            => slot.PlayerSkill?.Kind == LegacySkillKind.Attack && slot.EnemySkill?.Kind == LegacySkillKind.Attack &&
                !(slot is LegacyCurrentSlot current && current.DodgeSucceeded);

        private void AdvancePowerClash(float realDelta)
        {
            if (powerClashLayer == null) return;
            if (powerClashSlot != null && !IsPowerClash(powerClashSlot)) ClearPowerClash(false);
            float delta = realDelta > 0f && !float.IsInfinity(realDelta) ? realDelta : 0f;
            // Share the existing readout's safe area rather than putting numbers over the animated fighters.
            powerClashLayer.anchoredPosition = combatReadout.anchoredPosition + Vector2.up * 25f;
            if (powerClashImpacted && PowerClashVisible)
            {
                powerClashElapsed += delta;
                float meet = Mathf.Clamp01(powerClashElapsed / PowerClashMeetSeconds);
                PositionPowerNumbers(OutQuad(meet));
                if (meet >= 1f && !powerNumberShattered && (powerPlayerBroken || powerEnemyBroken))
                {
                    powerNumberShattered = true;
                    Text loser = powerPlayerBroken ? playerClashPower : enemyClashPower;
                    EmitPowerNumberFragments(loser, powerPlayerBroken ? -1f : 1f);
                }
                if (powerNumberShattered)
                {
                    (powerPlayerBroken ? playerClashPower : enemyClashPower).color = Color.clear;
                    Text winner = powerPlayerBroken ? enemyClashPower : playerClashPower;
                    float punch = 1f + .23f * Mathf.Exp(-Mathf.Max(0f, powerClashElapsed - PowerClashMeetSeconds) * 10f);
                    winner.rectTransform.localScale = Vector3.one * punch;
                }
            }
            foreach (PowerNumberFragment fragment in powerNumberFragments)
            {
                if (!fragment.Root.gameObject.activeSelf) continue;
                fragment.Elapsed += delta;
                float progress = Mathf.Clamp01(fragment.Elapsed / PowerFragmentSeconds);
                fragment.Root.anchoredPosition = fragment.Start + fragment.Velocity * fragment.Elapsed +
                    Vector2.down * (160f * fragment.Elapsed * fragment.Elapsed);
                fragment.Root.localRotation = Quaternion.Euler(0f, 0f, fragment.Spin * fragment.Elapsed);
                Color ink = DuelVisualTheme.Danger;
                ink.a = 1f - progress * progress;
                fragment.Number.color = ink;
                if (progress >= 1f) fragment.Root.gameObject.SetActive(false);
            }
        }

        private void PositionPowerNumbers(float meet)
        {
            float offset = Mathf.Lerp(80f, 37f, meet);
            playerClashPower.rectTransform.anchoredPosition = Vector2.left * offset;
            enemyClashPower.rectTransform.anchoredPosition = Vector2.right * offset;
            playerClashPower.rectTransform.localScale = enemyClashPower.rectTransform.localScale = Vector3.one;
        }

        private void EmitPowerNumberFragments(Text source, float side)
        {
            Vector2 cell = new Vector2(PowerNumberSize.x / PowerFragmentColumns, PowerNumberSize.y / PowerFragmentRows);
            for (int row = 0; row < PowerFragmentRows; row++)
                for (int column = 0; column < PowerFragmentColumns; column++)
                {
                    PowerNumberFragment fragment = AcquirePowerNumberFragment(cell);
                    if (fragment == null) return;
                    Vector2 offset = new Vector2((column + .5f) * cell.x - PowerNumberSize.x * .5f,
                        (row + .5f) * cell.y - PowerNumberSize.y * .5f);
                    fragment.Start = source.rectTransform.anchoredPosition + offset;
                    fragment.Root.anchoredPosition = fragment.Start;
                    fragment.Root.localRotation = Quaternion.identity;
                    fragment.Root.localScale = Vector3.one;
                    fragment.Number.rectTransform.anchoredPosition = -offset;
                    fragment.Number.text = source.text;
                    fragment.Number.color = DuelVisualTheme.Danger;
                    fragment.Velocity = new Vector2(side * (95f + column * 25f), 65f + row * 85f);
                    fragment.Spin = side * (column % 2 == 0 ? 150f : -110f);
                    fragment.Elapsed = 0f;
                    fragment.Root.gameObject.SetActive(true);
                }
        }

        private PowerNumberFragment AcquirePowerNumberFragment(Vector2 cell)
        {
            foreach (PowerNumberFragment fragment in powerNumberFragments)
                if (!fragment.Root.gameObject.activeSelf) return fragment;
            if (powerNumberFragments.Count >= MaximumPowerFragments) return null;
            Image clip = Image("Power Number Fragment", powerClashLayer, null, Vector2.zero, cell, Color.white);
            clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            Text number = BuildPowerNumber("Clipped Number", clip.transform, 0f);
            var created = new PowerNumberFragment(clip.rectTransform, number);
            clip.gameObject.SetActive(false);
            powerNumberFragments.Add(created);
            return created;
        }

        private void ClearPowerClash(bool fragments)
        {
            powerClashSlot = null;
            powerClashImpacted = powerNumberShattered = powerPlayerBroken = powerEnemyBroken = false;
            powerClashElapsed = 0f;
            if (powerClashNumbers != null) powerClashNumbers.gameObject.SetActive(false);
            if (fragments)
                foreach (PowerNumberFragment fragment in powerNumberFragments) fragment.Root.gameObject.SetActive(false);
        }

        private sealed class PowerNumberFragment
        {
            public readonly RectTransform Root;
            public readonly Text Number;
            public Vector2 Start, Velocity;
            public float Spin, Elapsed;
            public PowerNumberFragment(RectTransform root, Text number) { Root = root; Number = number; }
        }
    }
}
