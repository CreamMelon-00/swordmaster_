using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    /// <summary>How the tension lighting looks (2026-10-15, <see cref="DuelPresentationSettings.TensionLighting"/>): its
    /// rules' numbers (<see cref="LegacyTensionTuning"/>) and what the arena's existing light does with them. At full
    /// tension the scenery and the sky darken (the fighters keep their light, as under a step's focus), the whole picture
    /// dims a touch, and the vignette deepens toward a warm ember; a flow's swell brightens the picture a moment, opens the
    /// vignette and warms the light toward gold. No new art: the arena's own post-processing (colour adjustments, vignette)
    /// and backdrop tint.</summary>
    public readonly struct DuelTensionLook
    {
        public const float DefaultBackdropDarkening = .3f, DefaultDimming = .12f, DefaultVignette = .12f,
            DefaultSwellExposure = .3f, DefaultSwellVignetteRelief = .12f, DefaultSwellWarmTint = .25f;
        public const float MaximumBackdropDarkening = .85f, MaximumVignette = .4f;
        /// <summary>The tense vignette's ember when nothing is tuned.</summary>
        public static readonly Color DefaultVignetteInk = new Color(.32f, .09f, .03f, 1f);
        /// <summary>The colour a swell warms the light toward.</summary>
        public static readonly Color SwellWarmColor = new Color(1f, .9f, .68f, 1f);

        /// <param name="backdropDarkening">How much the scenery and the sky darken at full tension (0 to 0.85).</param>
        /// <param name="dimming">How far the whole picture's exposure drops at full tension (EV, 0 to 1).</param>
        /// <param name="vignette">How much the vignette deepens at full tension (added to its intensity, 0 to 0.4).</param>
        /// <param name="vignetteInk">The vignette's colour at full tension (opaque).</param>
        /// <param name="swellExposure">How far a swell lifts the exposure at its height (EV, 0 to 1).</param>
        /// <param name="swellVignetteRelief">How much a swell opens the vignette at its height (0 to 0.4).</param>
        /// <param name="swellWarmTint">How far a swell warms the light toward gold at its height (0 to 1).</param>
        public DuelTensionLook(LegacyTensionTuning tuning, float backdropDarkening, float dimming, float vignette,
            Color vignetteInk, float swellExposure, float swellVignetteRelief, float swellWarmTint)
        {
            Tuning = tuning;
            BackdropDarkening = Safe(backdropDarkening, MaximumBackdropDarkening);
            Dimming = Safe(dimming, 1f);
            Vignette = Safe(vignette, MaximumVignette);
            VignetteInk = float.IsNaN(vignetteInk.r) || float.IsNaN(vignetteInk.g) || float.IsNaN(vignetteInk.b) ||
                float.IsInfinity(vignetteInk.r) || float.IsInfinity(vignetteInk.g) || float.IsInfinity(vignetteInk.b)
                    ? DefaultVignetteInk
                    : new Color(Mathf.Clamp01(vignetteInk.r), Mathf.Clamp01(vignetteInk.g), Mathf.Clamp01(vignetteInk.b), 1f);
            SwellExposure = Safe(swellExposure, 1f);
            SwellVignetteRelief = Safe(swellVignetteRelief, MaximumVignette);
            SwellWarmTint = Safe(swellWarmTint, 1f);
        }

        /// <summary>The code's defaults (<see cref="LegacyTensionTuning.Default"/> and the numbers above).</summary>
        public static DuelTensionLook Default => new DuelTensionLook(LegacyTensionTuning.Default, DefaultBackdropDarkening,
            DefaultDimming, DefaultVignette, DefaultVignetteInk, DefaultSwellExposure, DefaultSwellVignetteRelief,
            DefaultSwellWarmTint);

        /// <summary>Nothing lights.</summary>
        public static DuelTensionLook Off => new DuelTensionLook(LegacyTensionTuning.Off, 0f, 0f, 0f, DefaultVignetteInk,
            0f, 0f, 0f);

        public LegacyTensionTuning Tuning { get; }
        public float BackdropDarkening { get; }
        public float Dimming { get; }
        public float Vignette { get; }
        public Color VignetteInk { get; }
        public float SwellExposure { get; }
        public float SwellVignetteRelief { get; }
        public float SwellWarmTint { get; }

        private static float Safe(float value, float max)
            => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp(value, 0f, max);
    }

    /// <summary>The arena's tension lighting (2026-10-15, <see cref="LegacyTensionLight"/>): low health anywhere shifts the
    /// light, a turn of the flow for the player swells it. Presentation only; it takes no time and never touches the
    /// battle's clock. <see cref="Reset"/> clears it at once; a scene in the middle of the battle sets it aside and gives it
    /// back.</summary>
    public sealed partial class LegacyArenaView
    {
        private readonly LegacyTensionLight tensionLight = new LegacyTensionLight();
        private DuelTensionLook tensionLook = DuelTensionLook.Off;
        private LegacyHealthReading tensionReading;
        private bool tensionRequested;
        private const float HeartbeatHealthShare = .3f, HeartbeatRiseSeconds = .32f, HeartbeatFadeSeconds = .45f;
        private static readonly Color HeartbeatInk = new Color(.48f, .012f, .025f, 1f);
        private float heartbeatAmount, heartbeatPhase, heartbeatDanger;
        private bool heartbeatSuspended;

        /// <summary>How far the tension shows now, 0 to 1 (eased in and out on real time).</summary>
        public float TensionAmount => tensionLight.TensionAmount;
        /// <summary>Whether the last frame's reading was tense (the light heads for full tension).</summary>
        public bool IsTense => tensionLight.IsTense;
        /// <summary>A flow's swell now, 0 to 1 (0 while none plays).</summary>
        public float FlowSwellAmount => tensionLight.Swell;
        public bool IsFlowSwelling => tensionLight.IsSwelling;
        /// <summary>The swells since the last <see cref="Reset"/>.</summary>
        public int FlowSwellCount => tensionLight.SwellCount;
        /// <summary>The last turn of the flow read since the last <see cref="Reset"/>.</summary>
        public LegacyFlowTurn LastFlowTurn => tensionLight.LastTurn;
        /// <summary>The look the light last asked for (<see cref="SetTensionLighting"/>).</summary>
        public DuelTensionLook TensionLook => tensionLook;
        /// <summary>The local player's low-health warning, eased on real time; an opponent's low health never starts it.</summary>
        public float LowHealthHeartbeatAmount => heartbeatSuspended ? 0f : heartbeatAmount;
        /// <summary>The warning's two heart beats per cycle (0 to 1), independent of the slowed battle clock.</summary>
        public float LowHealthHeartbeatPulse => LowHealthHeartbeatAmount * HeartbeatEnvelope(heartbeatPhase);

        /// <summary>This frame's health, as the rules have it now, from the player's side (a versus screen's own player), and
        /// the look to light it with. Asked for frame by frame like the time pressure: the next Tick consumes it, so an arena
        /// ticked without it (a start card, a cutscene, training, a test) eases the tension back out with the last look.</summary>
        public void SetTensionLighting(LegacyHealthReading reading, DuelTensionLook look)
        {
            if (disposed) return;
            tensionReading = reading;
            tensionLook = look;
            tensionRequested = true;
        }

        /// <summary>The duel ended or its connection closed: leave no red warning over the next screen, even when the
        /// last combat picture is kept and there will be no further arena Tick. Other tension lighting is preserved.</summary>
        public void ClearLowHealthHeartbeat()
        {
            if (disposed) return;
            ClearHeartbeat();
            RefreshExposure();
            ApplyGrade();
        }

        // Each Tick, before the grade: read this frame's request (or none) and let the real time pass.
        private void TickLighting(float realDelta)
        {
            TickHeartbeat(realDelta);
            if (tensionRequested) tensionLight.Observe(tensionReading, tensionLook.Tuning);
            else tensionLight.Release();
            tensionRequested = false;
            tensionLight.Advance(realDelta);
        }

        // A fresh battle: no tension, no swell, and the next reading only sets where the flow stands.
        private void ClearLighting()
        {
            tensionLight.Clear();
            tensionRequested = false;
            tensionReading = LegacyHealthReading.None;
            ClearHeartbeat();
            heartbeatSuspended = false;
        }

        // A scene in the middle of the battle plays under the neutral light: the tension is kept for its end, a swell ends.
        private float SetLightingAside()
        {
            // Keep the warning's phase with the battle, without painting red over the scene or ticking its heart.
            heartbeatSuspended = true;
            return tensionLight.SetAside();
        }

        private void PutLightingBack(float tension)
        {
            heartbeatSuspended = false;
            tensionLight.PutBack(tension);
        }

        private void TickHeartbeat(float realDelta)
        {
            if (heartbeatSuspended) return;
            // No request means no battle (including training); a fall leaves the finale its own light immediately.
            if (!tensionRequested || !tensionReading.IsKnown || tensionReading.AnyoneFell ||
                !tensionLook.Tuning.TensionEnabled)
            {
                ClearHeartbeat();
                return;
            }
            float share = tensionReading.PlayerShare;
            float target = 0f;
            if (share <= HeartbeatHealthShare)
            {
                heartbeatDanger = 1f - share / HeartbeatHealthShare;
                target = Mathf.Lerp(.45f, 1f, heartbeatDanger);
            }
            if (realDelta <= 0f) return;
            heartbeatAmount = Mathf.MoveTowards(heartbeatAmount, target,
                realDelta / (target >= heartbeatAmount ? HeartbeatRiseSeconds : HeartbeatFadeSeconds));
            if (heartbeatAmount <= 0f)
            {
                ClearHeartbeat();
                return;
            }
            float period = Mathf.Lerp(1.04f, .58f, heartbeatDanger);
            heartbeatPhase = Mathf.Repeat(heartbeatPhase + realDelta / period, 1f);
        }

        private void ClearHeartbeat() => heartbeatAmount = heartbeatPhase = heartbeatDanger = 0f;

        private static float HeartbeatEnvelope(float phase)
        {
            // A strong beat, a softer echo, then a long rest: distinguish a heartbeat from a continuous red flash.
            float first = HeartbeatBeat(phase, .04f, .11f, .21f);
            float second = HeartbeatBeat(phase, .25f, .31f, .43f) * .68f;
            return Mathf.Max(first, second);
        }

        private static float HeartbeatBeat(float phase, float start, float peak, float end)
        {
            if (phase <= start || phase >= end) return 0f;
            float amount = phase <= peak ? (phase - start) / (peak - start) : (end - phase) / (end - peak);
            return amount * amount * (3f - 2f * amount);
        }

        private float HeartbeatExposure => -(LowHealthHeartbeatAmount * .025f + LowHealthHeartbeatPulse * .075f);

        private float HeartbeatVignette(float under)
        {
            float amount = LowHealthHeartbeatAmount * .045f + LowHealthHeartbeatPulse * .19f;
            return amount > 0f ? Mathf.Max(under, Mathf.Min(.82f, under + amount * (1f - flashbackAmount))) : under;
        }

        private Color HeartbeatVignetteInk(Color under)
            => Color.Lerp(under, HeartbeatInk,
                (LowHealthHeartbeatAmount * .22f + LowHealthHeartbeatPulse * .68f) * (1f - flashbackAmount));

        // The exposure the light adds (EV): the tension's dimming and the swell's lift; a flashback drains it with the colour.
        private float LightingExposure
        {
            get
            {
                float tension = TensionAmount, swell = FlowSwellAmount;
                return (1f - flashbackAmount) *
                    (swell * tensionLook.SwellExposure - tension * tensionLook.Dimming + HeartbeatExposure);
            }
        }

        // The vignette's intensity the light adds: deeper with the tension, opened by a swell.
        private float LightingVignette
            => tensionLook.Vignette * TensionAmount - tensionLook.SwellVignetteRelief * FlowSwellAmount;

        // The vignette's colour under the light: toward the ember with the tension.
        private Color LightingVignetteInk(Color under)
        {
            float tension = TensionAmount;
            return tension > 0f ? Color.Lerp(under, tensionLook.VignetteInk, tension) : under;
        }

        // The swell's warm filter (white while none plays).
        private Color LightingFilter(Color filter)
        {
            float warmth = tensionLook.SwellWarmTint * FlowSwellAmount;
            return warmth > 0f ? filter * Color.Lerp(Color.white, DuelTensionLook.SwellWarmColor, warmth) : filter;
        }

        // How much the scenery and the sky darken with the tension (the fighters keep their light).
        private float LightingBackdropDarkening => tensionLook.BackdropDarkening * TensionAmount;
    }
}
