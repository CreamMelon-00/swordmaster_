using System;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>The planning bar's travelling gear and the restrained, non-pixel steam shared by the clock and lane gears.
    /// All pictures are bilinear runtime UI sprites, so the effect follows the non-pixel HUD rather than the pixel fighters.</summary>
    public sealed partial class LegacyCombatHud
    {
        private const float TimerTrackWidth = 520f;
        private const float TimerGearSize = 32f;
        private const float TimerTrackY = 12f;
        private const int TimerGearTexels = 96, SteamTexels = 64, SteamPoolSize = 24;
        private static readonly Color SteamInk = new Color(.94f, .92f, .84f, .52f);
        private static readonly Color PressureSteamInk = new Color(1f, .80f, .50f, .68f);

        private Image timerGear;
        private RectTransform steamLayer;
        private Texture2D timerGearTexture, steamTexture;
        private Sprite timerGearSprite, steamSprite;
        private readonly SteamPuff[] steamPuffs = new SteamPuff[SteamPoolSize];
        private float timerGearRotation, previousTimerRemaining = float.NaN, timerVentRemaining;
        private int steamCursor, steamSequence;
        private bool timerFinishedVented;

        public float TimerGearRotation => timerGearRotation;
        public int ActiveSteamPuffs
        {
            get
            {
                int count = 0;
                foreach (SteamPuff puff in steamPuffs)
                    if (puff != null && puff.Age < puff.Duration) count++;
                return count;
            }
        }

        private void BuildTimerGear(RectTransform controls)
        {
            timerGearTexture = GearTexture("Planning Timer Gear", TimerGearTexels, 12, .78f, .58f, .28f, .11f, 5);
            timerGearSprite = DuelGearShimmer.CreateSprite(timerGearTexture);
            timerGear = Image("Timer Gear", timerPanel, timerGearSprite, TimerGearPosition(1f),
                Vector2.one * TimerGearSize, GearBrass);

            steamTexture = CreateSteamTexture();
            steamSprite = DuelGearShimmer.CreateSprite(steamTexture);
            steamLayer = Rect("Steam Effects", controls, Vector2.zero, Vector2.zero, Vector2.one * .5f);
            steamLayer.anchorMin = Vector2.zero;
            steamLayer.anchorMax = Vector2.one;
            // Above the lane machinery but behind the timer's labels and gear.
            steamLayer.SetSiblingIndex(timerPanel.GetSiblingIndex());
            for (int index = 0; index < steamPuffs.Length; index++)
            {
                Image image = Image("Steam Puff " + (index + 1), steamLayer, steamSprite, Vector2.zero,
                    Vector2.one * 32f, Color.clear);
                image.gameObject.SetActive(false);
                steamPuffs[index] = new SteamPuff(image);
            }
            timerPanel.SetAsLastSibling();
        }

        private static Vector2 TimerGearPosition(float ratio)
            => new Vector2(-TimerTrackWidth * .5f + TimerTrackWidth * Mathf.Clamp01(ratio), TimerTrackY);

        private void SetTimerGearVisible(bool visible)
        {
            if (timerGear != null) timerGear.gameObject.SetActive(visible);
            if (!visible) ClearSteamPuffs();
        }

        private void AdvanceTimerGear(float remaining, float duration, float ratio, bool planning, float realDelta)
        {
            TickSteam(realDelta);
            if (timerGear == null) return;
            timerGear.rectTransform.anchoredPosition = TimerGearPosition(ratio);
            timerGear.color = ratio <= LegacyTimerGear.DangerShare ? DuelVisualTheme.Danger : GearBrass;
            if (!planning || !timerGear.gameObject.activeSelf)
            {
                previousTimerRemaining = remaining;
                return;
            }

            timerGearRotation = LegacySkillGear.Wrap(timerGearRotation -
                LegacyTimerGear.DegreesPerSecond(remaining, duration) * realDelta);
            timerGear.rectTransform.localRotation = Quaternion.Euler(0f, 0f, timerGearRotation);

            // A larger jump upward is a new planning turn, not time running backwards.
            if (float.IsNaN(previousTimerRemaining) || remaining > previousTimerRemaining + Mathf.Max(.25f, duration * .2f))
            {
                previousTimerRemaining = remaining;
                timerVentRemaining = LegacyTimerGear.DangerVentInterval;
                timerFinishedVented = false;
                return;
            }

            bool crossedDanger = LegacyTimerGear.Crossed(previousTimerRemaining, remaining, duration,
                LegacyTimerGear.DangerShare);
            bool crossedCritical = LegacyTimerGear.Crossed(previousTimerRemaining, remaining, duration,
                LegacyTimerGear.CriticalShare);
            if (crossedDanger || crossedCritical)
            {
                EmitTimerSteam(crossedCritical ? 1f : .7f);
                timerVentRemaining = LegacyTimerGear.VentInterval(remaining, duration);
            }

            float interval = LegacyTimerGear.VentInterval(remaining, duration);
            if (!float.IsInfinity(interval))
            {
                timerVentRemaining -= realDelta;
                if (timerVentRemaining <= 0f && remaining > 0f)
                {
                    EmitTimerSteam(ratio <= LegacyTimerGear.CriticalShare ? .8f : .55f);
                    timerVentRemaining += interval;
                }
            }
            if (remaining <= 0f && !timerFinishedVented)
            {
                timerFinishedVented = true;
                EmitTimerSteam(1.25f);
            }
            previousTimerRemaining = remaining;
        }

        private void EmitTimerSteam(float strength)
        {
            if (timerGear == null || !timerGear.gameObject.activeInHierarchy) return;
            Vector2 at = timerPanel.anchoredPosition + timerGear.rectTransform.anchoredPosition + new Vector2(0f, 8f);
            EmitSteam(at, 4 + (strength >= 1f ? 2 : 0), 30f * strength, 56f * strength, false);
        }

        private void EmitLaneSteam(int lane, bool ratchet)
        {
            if (lane < 0 || lane >= gears.Length || gears[lane] == null || !gears[lane].Present || steamLayer == null) return;
            Vector2 at = gearBand.anchoredPosition + gears[lane].Node.anchoredPosition + new Vector2(38f, 48f);
            // A normal choice gives a small valve breath. Shift is the dock dumping pressure through every meshed lane:
            // more, larger, warmer puffs spreading sideways distinguish it before the ratchet sound even lands.
            EmitSteam(at, ratchet ? 6 : 3, ratchet ? 48f : 21f, ratchet ? 70f : 42f, ratchet);
        }

        private void EmitSteam(Vector2 at, int count, float spread, float rise, bool pressure)
        {
            for (int index = 0; index < count; index++)
            {
                SteamPuff puff = steamPuffs[steamCursor++ % steamPuffs.Length];
                float phase = (steamSequence++ * .6180339f) % 1f;
                float side = phase * 2f - 1f;
                puff.Age = 0f;
                puff.Duration = .52f + phase * .28f;
                puff.Start = at + new Vector2(side * 4f, phase * 3f);
                puff.Velocity = new Vector2(side * spread, rise * (.85f + phase * .35f));
                puff.StartSize = (pressure ? 36f : 28f) + phase * (pressure ? 18f : 12f);
                puff.EndSize = puff.StartSize * (pressure ? 2.2f : 1.9f + phase * .25f);
                puff.Spin = side * 24f;
                puff.Tint = pressure ? PressureSteamInk : SteamInk;
                puff.Image.rectTransform.anchoredPosition = puff.Start;
                puff.Image.rectTransform.localRotation = Quaternion.identity;
                puff.Image.gameObject.SetActive(true);
            }
        }

        private void TickSteam(float realDelta)
        {
            realDelta = realDelta > 0f && !float.IsInfinity(realDelta) ? realDelta : 0f;
            foreach (SteamPuff puff in steamPuffs)
            {
                if (puff == null || !(puff.Age < puff.Duration)) continue;
                puff.Age = Mathf.Min(puff.Duration, puff.Age + realDelta);
                float t = puff.Duration > 0f ? puff.Age / puff.Duration : 1f;
                float eased = 1f - (1f - t) * (1f - t);
                puff.Image.rectTransform.anchoredPosition = puff.Start + puff.Velocity * (puff.Duration * eased);
                float size = Mathf.Lerp(puff.StartSize, puff.EndSize, eased);
                puff.Image.rectTransform.sizeDelta = Vector2.one * size;
                puff.Image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, puff.Spin * eased);
                Color color = puff.Tint;
                color.a *= Mathf.Sin(Mathf.PI * Mathf.Clamp01(t));
                puff.Image.color = color;
                if (puff.Age >= puff.Duration) puff.Image.gameObject.SetActive(false);
            }
        }

        private void ResetTimerGear()
        {
            timerGearRotation = 0f;
            previousTimerRemaining = float.NaN;
            timerVentRemaining = 0f;
            timerFinishedVented = false;
            if (timerGear != null) timerGear.rectTransform.localRotation = Quaternion.identity;
            ClearSteamPuffs();
        }

        private void ClearSteamPuffs()
        {
            foreach (SteamPuff puff in steamPuffs)
            {
                if (puff == null) continue;
                puff.Age = puff.Duration;
                puff.Image.gameObject.SetActive(false);
            }
        }

        private void ReleaseTimerGearPictures()
        {
            DuelGearShimmer.Release(timerGearSprite);
            DuelGearShimmer.Release(timerGearTexture);
            DuelGearShimmer.Release(steamSprite);
            DuelGearShimmer.Release(steamTexture);
            timerGearSprite = steamSprite = null;
            timerGearTexture = steamTexture = null;
        }

        private static Texture2D CreateSteamTexture()
        {
            var pixels = new Color32[SteamTexels * SteamTexels];
            for (int y = 0; y < SteamTexels; y++)
                for (int x = 0; x < SteamTexels; x++)
                {
                    float u = ((x + .5f) / SteamTexels - .5f) * 2f;
                    float v = ((y + .5f) / SteamTexels - .5f) * 2f;
                    // Three overlapping soft lobes keep a small puff organic without a pixel outline or saved bitmap asset.
                    float a = Blob(u + .28f, v + .05f, .58f);
                    a = Mathf.Max(a, Blob(u - .18f, v + .18f, .67f));
                    a = Mathf.Max(a, Blob(u + .02f, v - .28f, .62f));
                    a *= Mathf.Clamp01((1f - Mathf.Sqrt(u * u + v * v)) * 2.5f);
                    pixels[y * SteamTexels + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            return DuelGearShimmer.CreateTexture("HUD Steam Puff", SteamTexels, pixels);
        }

        private static float Blob(float x, float y, float radius)
        {
            float distance = Mathf.Sqrt(x * x + y * y) / radius;
            float value = Mathf.Clamp01(1f - distance);
            return value * value * (3f - 2f * value);
        }

        private sealed class SteamPuff
        {
            public readonly Image Image;
            public Vector2 Start, Velocity;
            public float Age = 1f, Duration = 1f, StartSize, EndSize, Spin;
            public Color Tint;

            public SteamPuff(Image image) => Image = image;
        }
    }
}
