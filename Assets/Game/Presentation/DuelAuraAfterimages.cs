using System;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    /// <summary>The 서막's 수훈 afterimages: while 이아's power aura is up (from her 수훈 in mission 4's event scene, through the
    /// rest of the battle and her final blow, until the outro's <c>@aura knight off</c>) she leaves semi-transparent
    /// yellow silhouettes of her current frame behind her, one every interval. Each snapshot retains its captured world
    /// position, rotation and scale while its opacity fades, so the trail records where she was in either direction.
    /// Its aura drives it (<see cref="DuelPowerAura.Afterimages"/>): it is ticked on the aura's clock with the aura's fade
    /// as its strength (the battle's clock in battle, so hit stop, slow motion and a freeze frame hold it; real time in a
    /// cutscene), so the ghosts fade in and out with the aura, an instant switch-off or a reset takes them at once, and a
    /// cutscene in the middle of the battle starts and ends with none. The ghosts reuse the steps' afterimage pool
    /// (<see cref="DuelStepAfterimages"/>), drawn as flat silhouettes with the break outline's material when there is one
    /// (their colour in its tint), behind her body, her break outline and her shadow but over her aura's glow
    /// (<see cref="SortingOrder"/>). Look: <see cref="DuelPresentationSettings"/> (서막 4 임무 수훈).</summary>
    public sealed class DuelAuraAfterimages : IDisposable
    {
        /// <summary>The most ghosts the settings can have showing at once: their longest fade (1.5 s) at their shortest
        /// interval (0.02 s) is 75, and what a frame leaves over can bring one more. So within the settings' ranges no
        /// ghost is taken back while it still shows; a fade beyond them is cut to what the pool holds
        /// (<see cref="LegacyAfterimageTrail.FadeSeconds"/>).</summary>
        public const int Capacity = 76;
        /// <summary>Behind her break outline and ground shadow (<see cref="DuelBreakAura.SortingOrder"/>, -2), so the
        /// break's red cue is never washed yellow, and her body; tied with her aura's glow and back motes
        /// (<see cref="DuelPowerAura.BackSortingOrder"/>) but nearer the camera (they sit 0.02 and 0.03 behind her, the
        /// ghosts at her depth), so the ghosts draw over them.</summary>
        public const int SortingOrder = DuelPowerAura.BackSortingOrder;
        /// <summary>The pool is "Duel Aura Afterimages", its ghosts "Aura Afterimage N".</summary>
        public const string Label = "Aura";

        private readonly SpriteRenderer source;
        private readonly DuelPresentationSettings settings;
        private readonly DuelStepAfterimages pool;
        private readonly LegacyAfterimageTrail trail = new LegacyAfterimageTrail();
        private bool disposed;

        /// <param name="source">The figure the ghosts copy (이아's body).</param>
        /// <param name="parent">Where the ghosts live: the arena, not the figure, so they stay where they were left.</param>
        /// <param name="material">A flat silhouette material (the break outline's) paints her shape in the colour; a sprite
        /// material (the fallback) tints a copy of her frame instead.</param>
        /// <param name="settings">Read every tick, so live tuning applies to the next ghost.</param>
        /// <param name="facesRightByDefault">Retained for existing callers. Fixed snapshots do not use a facing direction.</param>
        /// <param name="footY">Retained for existing callers. Fixed snapshots keep their captured scale and position.</param>
        public DuelAuraAfterimages(SpriteRenderer source, Transform parent, Material material, int layer,
            DuelPresentationSettings settings, bool facesRightByDefault = false, float footY = DuelPowerAura.FeetY)
        {
            this.source = source != null ? source : throw new ArgumentNullException(nameof(source));
            this.settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            pool = new DuelStepAfterimages(parent, material, layer, false, Capacity, Label);
        }

        /// <summary>How many ghosts show now.</summary>
        public int ActiveCount => disposed ? 0 : pool.ActiveCount;
        /// <summary>The pool's object; every ghost lives under it.</summary>
        public Transform Root => pool.Root;
        public int GroupedSortingCeiling
        {
            get => pool.GroupedSortingCeiling;
            set => pool.GroupedSortingCeiling = value;
        }

        /// <summary>Lets <paramref name="seconds"/> of the aura's clock pass: the ghosts age, and a new one is left when one
        /// is due while the aura is up (<paramref name="strength"/>, its fade, 0 to 1) and the figure shows. No time (hit
        /// stop, a freeze) changes nothing.</summary>
        public void Tick(float seconds, float strength)
        {
            if (disposed) return;
            float delta = seconds > 0f && !float.IsInfinity(seconds) ? seconds : 0f;
            pool.Tick(delta);
            float interval = settings.EmpowermentAfterimageInterval;
            if (!trail.Advance(seconds, interval, strength) || !Shows) return;
            float opacity = LegacyAfterimageTrail.Opacity(settings.EmpowermentAfterimageAlpha, strength);
            if (opacity <= 0f) return;
            Color tint = settings.EmpowermentAfterimageColor;
            tint.a = opacity;
            pool.Emit(source, tint, LegacyAfterimageTrail.FadeSeconds(settings.EmpowermentAfterimageSeconds, interval, Capacity),
                SortingOrder);
        }

        /// <summary>Every ghost goes at once, and the next comes a whole interval after the trail picks up again.</summary>
        public void Clear()
        {
            if (disposed) return;
            pool.Reset();
            trail.Reset();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            pool.Dispose();
        }

        // A hidden figure (off stage in a scene) leaves nothing behind.
        private bool Shows => source != null && source.enabled && source.gameObject.activeInHierarchy && source.sprite != null;
    }
}
