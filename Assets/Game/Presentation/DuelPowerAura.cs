using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>Golden power around a figure, drawn in code with pooled sprites so it needs no art. An aura stays until
    /// it is switched off: a soft glow behind the body and motes drifting up around it. A charge runs for a set time:
    /// motes are drawn in to the chest while a glow swells there, then it flares and fades; or it is cut off at once. A
    /// held charge swells for its time and then keeps gathering at full glow until it is cut off (or a new one starts).
    /// It owns its renderers under the figure, so it moves with the figure and is hidden with it, and leaves the
    /// figure's own colour, hit flash and sorting alone. Cutscenes drive it (@charge, @aura) and the battle can keep an
    /// aura on a fighter (수훈). An aura can also leave afterimages of its figure while it is up (<see cref="Afterimages"/>:
    /// 이아's yellow 수훈 ghosts). Placeholder look; replace it here.</summary>
    public sealed class DuelPowerAura : IDisposable
    {
        /// <summary>The figure's feet in its own space (the arena's ground offset) and its rough size.</summary>
        public const float FeetY = -2.23f;
        public const float BodyHeight = 3.3f;
        public const float BodyHalfWidth = .9f;
        public const int Capacity = 72;
        /// <summary>The glow and the far motes draw behind the body and its ground shadow (-2); the near motes and the
        /// charge's core draw over the body (0) and under the impact glow (20).</summary>
        public const int BackSortingOrder = -3;
        public const int FrontSortingOrder = 5;
        public const float AuraMotesPerSecond = 28f;
        public const float ChargeMotesPerSecond = 70f;
        /// <summary>How long a finished charge flares and fades.</summary>
        public const float ReleaseSeconds = .35f;
        public static readonly Color Gold = new Color(1f, .78f, .3f, 1f);
        public static readonly Color PaleGold = new Color(1f, .94f, .7f, 1f);
        private const float ChestY = FeetY + BodyHeight * .55f;
        private const float GlowPulsePeriod = 1.4f;
        // Motes are spawned over at most this much of one frame; a longer frame would only age them out at once.
        private const float MaximumSpawnStep = .1f;

        private sealed class Mote
        {
            public SpriteRenderer Renderer;
            public bool Active, Charge;
            public Vector2 Position, Velocity;
            public float Age, Life, Size, Phase;
            public Color Color;
        }

        private readonly Transform root;
        private readonly SpriteRenderer glow, core;
        private readonly Mote[] motes = new Mote[Capacity];
        private readonly Texture2D glowTexture, moteTexture;
        private readonly Sprite glowSprite, moteSprite;
        private readonly System.Random random;
        private float auraTarget, auraFadeSeconds;
        private float chargeSeconds, chargeElapsed, releaseElapsed;
        private bool charging, releasing, chargeHolds;
        private float auraSpawn, chargeSpawn, clock;
        private int activeMotes;
        // Everything was last drawn switched off; with nothing to show, Apply can skip the renderers.
        private bool drawnIdle;
        private bool disposed;

        /// <summary>How far the aura has faded in, 0 to 1.</summary>
        public float AuraAmount { get; private set; }
        /// <summary>Whether the aura is on or fading in (it may still be fading out when false).</summary>
        public bool IsAuraOn => auraTarget > 0f;
        /// <summary>Whether power is still gathering (not yet released or cut off).</summary>
        public bool IsCharging => charging;
        /// <summary>Whether the charge in progress is held: it never releases on its own, only <see cref="StopCharge"/>
        /// (or a new charge) ends it.</summary>
        public bool IsChargeHeld => charging && chargeHolds;
        /// <summary>The charge's glow: rising to 1 while it gathers (and staying there while it is held), flaring and fading
        /// after it, 0 once cut off.</summary>
        public float ChargeGlow
        {
            get
            {
                if (charging) return Mathf.SmoothStep(0f, 1f, chargeSeconds > 0f ? chargeElapsed / chargeSeconds : 1f);
                if (!releasing) return 0f;
                float left = 1f - Mathf.Clamp01(releaseElapsed / ReleaseSeconds);
                return 1.3f * left * left;
            }
        }
        public int ActiveMoteCount => activeMotes;
        public Transform Root => root;
        /// <summary>Ghosts of the figure this aura leaves behind while it is up (이아's 수훈 afterimages), or null for none.
        /// The aura drives them but does not own them: each <see cref="Tick"/> ticks them on the same clock with the aura's
        /// fade as their strength (so they fade in and out with it), and switching the aura off at once
        /// (<see cref="SetAura"/> with no fade) or <see cref="Reset"/> takes every ghost away.</summary>
        public DuelAuraAfterimages Afterimages { get; set; }

        /// <param name="figure">The figure's transform (its feet at <see cref="FeetY"/>); the effect lives under it.</param>
        /// <param name="material">The arena's sprite material, or null for the default.</param>
        /// <param name="seed">Cosmetic rolls only; never the combat or global random stream.</param>
        public DuelPowerAura(Transform figure, Material material, int layer, int seed = 0)
        {
            if (figure == null) throw new ArgumentNullException(nameof(figure));
            random = new System.Random(seed);
            var rootObject = new GameObject("Power Aura") { layer = layer };
            root = rootObject.transform;
            root.SetParent(figure, false);
            int sortingLayer = figure.TryGetComponent(out SpriteRenderer body) ? body.sortingLayerID : 0;

            glowTexture = CreateTexture("Power Aura Glow", 64, 2f);
            moteTexture = CreateTexture("Power Aura Mote", 16, 1.6f);
            glowSprite = CreateSprite(glowTexture);
            moteSprite = CreateSprite(moteTexture);
            glow = CreateRenderer("Power Aura Glow", glowSprite, material, layer, sortingLayer, BackSortingOrder);
            glow.transform.localPosition = new Vector3(0f, FeetY + BodyHeight * .5f, .02f);
            core = CreateRenderer("Power Charge Core", glowSprite, material, layer, sortingLayer, FrontSortingOrder);
            core.transform.localPosition = new Vector3(0f, ChestY, -.02f);
            for (int index = 0; index < motes.Length; index++)
                motes[index] = new Mote
                {
                    Renderer = CreateRenderer("Power Mote " + index, moteSprite, material, layer, sortingLayer, FrontSortingOrder),
                };
            Apply();
        }

        /// <summary>Turns the aura on or off, fading over <paramref name="fadeSeconds"/> (0 = at once). Switched off at once,
        /// its <see cref="Afterimages"/> go with it; a fading aura lets them fade out with it.</summary>
        public void SetAura(bool on, float fadeSeconds = 0f)
        {
            if (disposed) return;
            auraTarget = on ? 1f : 0f;
            auraFadeSeconds = Mathf.Max(0f, fadeSeconds);
            if (auraFadeSeconds <= 0f) AuraAmount = auraTarget;
            if (!on && auraFadeSeconds <= 0f) Afterimages?.Clear();
            Apply();
        }

        /// <summary>Gathers power for <paramref name="seconds"/>, then flares and fades. A new charge starts over.</summary>
        /// <param name="hold">Reach full glow in <paramref name="seconds"/>, then keep gathering (motes still drawn in, no
        /// flare) until <see cref="StopCharge"/> cuts it off: a charge that is interrupted however long it lasts.</param>
        public void Charge(float seconds, bool hold = false)
        {
            if (disposed) return;
            charging = true;
            releasing = false;
            chargeHolds = hold;
            chargeSeconds = Mathf.Max(0f, seconds);
            chargeElapsed = releaseElapsed = chargeSpawn = 0f;
            Apply();
        }

        /// <summary>Cuts a charge off at once: its glow and every mote being drawn in vanish. The aura is untouched.</summary>
        public void StopCharge()
        {
            if (disposed) return;
            charging = releasing = chargeHolds = false;
            chargeElapsed = releaseElapsed = chargeSpawn = 0f;
            foreach (Mote mote in motes)
                if (mote.Charge) Deactivate(mote);
            Apply();
        }

        /// <summary>Everything off at once (a new duel, a retry), its <see cref="Afterimages"/> included.</summary>
        public void Reset()
        {
            if (disposed) return;
            auraTarget = AuraAmount = auraFadeSeconds = auraSpawn = 0f;
            charging = releasing = chargeHolds = false;
            chargeElapsed = releaseElapsed = chargeSpawn = 0f;
            foreach (Mote mote in motes) Deactivate(mote);
            Afterimages?.Clear();
            Apply();
        }

        /// <summary>Advances the fades and motes, and the <see cref="Afterimages"/>, by <paramref name="delta"/> seconds on
        /// the caller's clock (the battle's combat clock, so hit stop holds it; a cutscene's real time).</summary>
        public void Tick(float delta)
        {
            if (disposed) return;
            delta = delta > 0f && !float.IsInfinity(delta) ? delta : 0f;
            clock = (clock + delta) % 1000f;
            if (AuraAmount != auraTarget)
                AuraAmount = auraFadeSeconds > 0f ? Mathf.MoveTowards(AuraAmount, auraTarget, delta / auraFadeSeconds) : auraTarget;
            if (charging)
            {
                chargeElapsed = Mathf.Min(chargeSeconds, chargeElapsed + delta);
                // A held charge stays at full glow until it is cut off.
                if (chargeElapsed >= chargeSeconds && !chargeHolds)
                {
                    charging = false;
                    releasing = true;
                    releaseElapsed = 0f;
                }
            }
            else if (releasing)
            {
                releaseElapsed += delta;
                if (releaseElapsed >= ReleaseSeconds) releasing = false;
            }
            foreach (Mote mote in motes)
            {
                if (!mote.Active) continue;
                mote.Age += delta;
                if (mote.Age >= mote.Life)
                {
                    Deactivate(mote);
                    continue;
                }
                mote.Position += mote.Velocity * delta;
                if (mote.Charge) continue;
                // Aura motes rise a little faster as they go and sway as they rise.
                mote.Velocity.y += .5f * delta;
                mote.Position.x += Mathf.Sin(clock * 5f + mote.Phase) * .3f * delta;
            }
            float spawnStep = Mathf.Min(delta, MaximumSpawnStep);
            auraSpawn += AuraAmount * AuraMotesPerSecond * spawnStep;
            while (auraSpawn >= 1f)
            {
                auraSpawn -= 1f;
                SpawnAuraMote();
            }
            if (charging)
            {
                chargeSpawn += Mathf.Lerp(.35f, 1f, ChargeGlow) * ChargeMotesPerSecond * spawnStep;
                while (chargeSpawn >= 1f)
                {
                    chargeSpawn -= 1f;
                    SpawnChargeMote();
                }
            }
            Apply();
            // On the same clock and with the aura's fade: a ghost copies the figure's frame as it stands.
            Afterimages?.Tick(delta, AuraAmount);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            // The root may already be gone with the figure; Unity's null check covers that.
            if (root != null) Release(root.gameObject);
            Release(glowSprite);
            Release(moteSprite);
            Release(glowTexture);
            Release(moteTexture);
        }

        private void SpawnAuraMote()
        {
            Mote mote = FreeMote();
            if (mote == null) return;
            // More of them near the middle of the body than at its edges.
            float x = ((float)random.NextDouble() + (float)random.NextDouble() - 1f) * BodyHalfWidth;
            float y = FeetY + (float)random.NextDouble() * BodyHeight * .85f;
            float roll = (float)random.NextDouble();
            mote.Charge = false;
            mote.Position = new Vector2(x, y);
            mote.Velocity = new Vector2(0f, Mathf.Lerp(.7f, 1.5f, (float)random.NextDouble()));
            mote.Life = Mathf.Lerp(.8f, 1.4f, (float)random.NextDouble());
            mote.Size = Mathf.Lerp(.1f, .24f, roll);
            mote.Phase = (float)random.NextDouble() * 6.2832f;
            mote.Color = Color.Lerp(Gold, roll > .7f ? Color.white : PaleGold, (float)random.NextDouble());
            Activate(mote, random.NextDouble() < .45);
        }

        private void SpawnChargeMote()
        {
            Mote mote = FreeMote();
            if (mote == null) return;
            float angle = (float)random.NextDouble() * 6.2832f;
            float radius = Mathf.Lerp(1.8f, 2.8f, (float)random.NextDouble());
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            mote.Charge = true;
            mote.Life = Mathf.Lerp(.35f, .55f, (float)random.NextDouble());
            mote.Position = new Vector2(0f, ChestY) + direction * radius;
            // Straight in, arriving at the chest as it fades.
            mote.Velocity = -direction * (radius / mote.Life);
            mote.Size = Mathf.Lerp(.08f, .16f, (float)random.NextDouble());
            mote.Phase = 0f;
            mote.Color = Color.Lerp(PaleGold, Color.white, (float)random.NextDouble());
            Activate(mote, random.NextDouble() < .6);
        }

        private Mote FreeMote()
        {
            foreach (Mote mote in motes)
                if (!mote.Active) return mote;
            return null;
        }

        private void Activate(Mote mote, bool front)
        {
            mote.Active = true;
            mote.Age = 0f;
            mote.Renderer.sortingOrder = front ? FrontSortingOrder : BackSortingOrder;
            activeMotes++;
        }

        private void Deactivate(Mote mote)
        {
            if (mote.Active) activeMotes--;
            mote.Active = false;
            mote.Age = 0f;
            if (mote.Renderer != null) mote.Renderer.enabled = false;
        }

        private void Apply()
        {
            if (disposed || root == null) return;
            bool idle = AuraAmount <= 0f && !charging && !releasing && activeMotes == 0;
            if (idle && drawnIdle) return;
            drawnIdle = idle;
            float pulse = .85f + .15f * Mathf.Sin(clock / GlowPulsePeriod * 6.2832f);
            float charge = ChargeGlow;
            float glowAlpha = Mathf.Clamp01(AuraAmount * .32f * pulse + charge * .28f);
            glow.enabled = glowAlpha > 0f;
            glow.color = new Color(Gold.r, Gold.g, Gold.b, glowAlpha);
            glow.transform.localScale = new Vector3(3.4f, 4.4f, 1f) * (1f + .08f * charge);
            core.enabled = charge > 0f;
            core.color = new Color(1f, .96f, .82f, Mathf.Clamp01(.75f * charge));
            core.transform.localScale = Vector3.one * (.5f + 1.3f * charge);
            foreach (Mote mote in motes)
            {
                SpriteRenderer view = mote.Renderer;
                view.enabled = mote.Active;
                if (!mote.Active) continue;
                float t = Mathf.Clamp01(mote.Age / mote.Life);
                float alpha;
                Transform node = view.transform;
                bool front = view.sortingOrder == FrontSortingOrder;
                node.localPosition = new Vector3(mote.Position.x, mote.Position.y, front ? -.03f : .03f);
                if (mote.Charge)
                {
                    // A short streak along its path, brightening as it comes in and gone as it arrives.
                    alpha = Mathf.Clamp01(t / .25f) * Mathf.Clamp01((1f - t) / .12f);
                    float angle = Mathf.Atan2(mote.Velocity.y, mote.Velocity.x) * Mathf.Rad2Deg;
                    node.localRotation = Quaternion.Euler(0f, 0f, angle);
                    node.localScale = new Vector3(mote.Size * (3.2f - 1.6f * t), mote.Size * .7f, 1f);
                }
                else
                {
                    alpha = Mathf.Clamp01(t / .15f) * Mathf.Clamp01((1f - t) / .45f);
                    node.localRotation = Quaternion.identity;
                    node.localScale = Vector3.one * (mote.Size * (1f - .35f * t));
                }
                view.color = new Color(mote.Color.r, mote.Color.g, mote.Color.b, alpha);
            }
        }

        private SpriteRenderer CreateRenderer(string name, Sprite sprite, Material material, int layer, int sortingLayer,
            int sortingOrder)
        {
            var node = new GameObject(name) { layer = layer };
            node.transform.SetParent(root, false);
            var view = node.AddComponent<SpriteRenderer>();
            view.sprite = sprite;
            if (material != null) view.sharedMaterial = material;
            view.sortingLayerID = sortingLayer;
            view.sortingOrder = sortingOrder;
            view.enabled = false;
            return view;
        }

        /// <summary>A white disc whose alpha falls off from the centre; <paramref name="falloff"/> sharpens the core.</summary>
        private static Texture2D CreateTexture(string name, int size, float falloff)
        {
            var pixels = new Color32[size * size];
            float centre = (size - 1) * .5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre)) / (size * .5f);
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), falloff);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // One world unit across, so a renderer's scale is its size.
        private static Sprite CreateSprite(Texture2D texture)
        {
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), Vector2.one * .5f,
                texture.width, 0, SpriteMeshType.FullRect);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }
    }
}
