using System;
using TurnLimbo.Runtime.Cutscene;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>맞물림's cues (<see cref="LegacyMeshCue"/> has their curves). When queueing a skill makes or lengthens a chain
    /// (<see cref="Queued"/>), a pair of small brass gears bites and turns on the seam between its icon and the one before
    /// it in the player's queue row, sparks fly from where their teeth meet and the mesh sounds
    /// (<c>Resources/Sfx/mesh-spark</c>); a longer chain gives a bigger burst, more sparks and a higher sound. When a
    /// meshed slot's first hit lands (<see cref="ShowSlotFlash"/>), the pair flashes larger at the player's body, where the
    /// step rings would have been, so the player sees why that slot takes no steps. The bursts are drawn in the duel HUD
    /// beside the player's queue row, never in it (the row's children stay its cards), follow the row as it is laid out,
    /// and play on real time whatever the battle's clock does; the flash sits over the HUD like the step cues. The marks
    /// under meshed icons are the HUD's own (<see cref="LegacyCombatHud.GetMeshMark"/>). The enemy shows nothing.</summary>
    public sealed class DuelMeshCues : IDisposable
    {
        /// <summary>The mesh sound in <c>Resources/Sfx</c>.</summary>
        public const string MeshSound = "mesh-spark";
        /// <summary>How many queue bursts can play at once: a quick hand queues faster than one fades.</summary>
        public const int BurstCapacity = 4;
        private const float SparkWidthShare = .07f, MinimumSparkWidth = 1.5f;
        // How far the flash keeps from the screen's edges (HUD units), as the break impact does.
        private const float FlashInset = 120f;
        private static readonly Color Brass = DuelVisualTheme.Accent;
        private static readonly Color GlowInk = new Color(1f, .97f, .86f, 1f);

        private readonly LegacyCombatHud hud;
        private readonly Func<DuelPresentationSettings> settings;
        private readonly RectTransform queueLayer, flashLayer;
        private readonly Canvas canvas;
        private readonly Burst[] bursts = new Burst[BurstCapacity];
        private readonly Burst flash;
        private readonly AudioSource audio;
        private readonly AudioClip clip;
        private int nextBurst;
        private bool disposed;

        /// <param name="settings">Read when used, so live tuning (and a test's clone) applies at once.</param>
        public DuelMeshCues(LegacyCombatHud hud, Func<DuelPresentationSettings> settings)
        {
            this.hud = hud ?? throw new ArgumentNullException(nameof(hud));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            RectTransform row = hud.PlayerQueueRow;
            if (row == null) throw new ArgumentException("The duel HUD has no player queue row.", nameof(hud));
            Transform parent = row.parent;
            canvas = parent.GetComponentInParent<Canvas>();
            queueLayer = Layer("Player Queue Mesh", parent, false);
            KeepAfter(row);
            // Over the HUD, as the step cues are: the flash takes their place in a meshed slot.
            flashLayer = Layer("Player Mesh Flash", parent, true);
            flashLayer.SetAsLastSibling();
            audio = queueLayer.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.loop = false;
            audio.spatialBlend = 0f;
            clip = Resources.Load<AudioClip>(CutsceneStep.SoundFolder + MeshSound);
            for (int index = 0; index < bursts.Length; index++) bursts[index] = CreateBurst("Mesh Burst " + (index + 1), queueLayer);
            flash = CreateBurst("Mesh Flash", flashLayer);
            Clear();
        }

        /// <summary>Whether any queue burst is playing.</summary>
        public bool IsPlaying
        {
            get
            {
                if (disposed) return false;
                foreach (Burst burst in bursts)
                    if (burst.IsPlaying) return true;
                return false;
            }
        }
        /// <summary>Whether the first hit's flash is playing.</summary>
        public bool IsFlashing => !disposed && flash.IsPlaying;
        /// <summary>The slot whose seam with the one before it the latest burst plays on (-1 before any), the chain it made,
        /// its sparks and its size against a two-skill chain's.</summary>
        public int LastSlot { get; private set; } = -1;
        public int LastChainLength { get; private set; }
        public int LastSparkCount { get; private set; }
        public float LastBurstScale { get; private set; }
        /// <summary>The cues' own voice and the mesh sound (null when Resources has none).</summary>
        public AudioSource Audio => audio;
        public AudioClip MeshClip => clip;
        /// <summary>The layers, for tests and tools: the queue bursts' beside the player's row, the flash's over the HUD.</summary>
        public RectTransform QueueLayer => queueLayer;
        public RectTransform FlashLayer => flashLayer;
        /// <summary>The flash's upper gear and its sparks, and a queue burst's (index up to <see cref="BurstCapacity"/>).</summary>
        public Image FlashGear => flash.Upper;
        public DuelMeshSparks FlashSparks => flash.Sparks;
        public Image BurstGear(int index) => index >= 0 && index < bursts.Length ? bursts[index].Upper : null;
        public DuelMeshSparks BurstSparks(int index) => index >= 0 && index < bursts.Length ? bursts[index].Sparks : null;

        /// <summary>A skill was just queued (call after the duel queued it): when it made or lengthened a chain, the gears bite
        /// on the seam between it and the skill before it and the mesh sounds. False when it did not mesh.</summary>
        public bool Queued(LegacyQueuedDuel duel)
        {
            if (disposed || duel == null || duel.Phase != LegacyDuelPhase.Planning || duel.PlayerQueue.Count < 2) return false;
            LegacyMeshSlot mesh = duel.PlayerMesh(duel.PlayerQueue.Count - 1);
            if (!mesh.MeshesWithPrevious) return false;
            DuelPresentationSettings tuning = settings();
            PlaySound(mesh.ChainLength, tuning.MeshSoundVolume);
            Burst burst = bursts[nextBurst];
            nextBurst = (nextBurst + 1) % bursts.Length;
            Begin(burst, mesh.SlotIndex, mesh.ChainLength, tuning.MeshBurstSeconds, tuning.MeshGearSize, tuning.MeshSparkCount);
            LastSlot = mesh.SlotIndex;
            LastChainLength = mesh.ChainLength;
            LastSparkCount = LegacyMeshCue.SparkCount(tuning.MeshSparkCount, mesh.ChainLength);
            LastBurstScale = LegacyMeshCue.BurstScale(mesh.ChainLength);
            return true;
        }

        /// <summary>A meshed slot's first hit has landed: the gears flash at the player's body, where the step rings would have
        /// been, and the mesh sounds again, softer. Nothing for a slot that is not meshed.</summary>
        public bool ShowSlotFlash(LegacyMeshSlot mesh)
        {
            if (disposed || !mesh.IsMeshed) return false;
            DuelPresentationSettings tuning = settings();
            PlaySound(mesh.ChainLength, tuning.MeshSoundVolume * LegacyMeshCue.FlashVolumeShare);
            Begin(flash, mesh.SlotIndex, mesh.ChainLength, tuning.MeshFlashSeconds, tuning.MeshFlashSize, tuning.MeshSparkCount);
            return true;
        }

        /// <summary>A frame, after the duel HUD's layout: the cues move on by <paramref name="realDelta"/>, the bursts follow
        /// the player's row as it now stands and the flash the player (<paramref name="arenaCamera"/>,
        /// <paramref name="player"/>; without them it waits on the player's side of the screen).</summary>
        public void Tick(float realDelta, Camera arenaCamera = null, Transform player = null)
        {
            if (disposed) return;
            realDelta = realDelta > 0f && !float.IsNaN(realDelta) && !float.IsInfinity(realDelta) ? realDelta : 0f;
            bool any = false;
            foreach (Burst burst in bursts)
            {
                if (!burst.IsPlaying) continue;
                burst.Elapsed += realDelta;
                if (!burst.IsPlaying || !PlaceOnSeam(burst))
                {
                    Stop(burst);
                    continue;
                }
                Apply(burst);
                any = true;
            }
            if (any) KeepAfter(hud.PlayerQueueRow);
            if (flash.IsPlaying)
            {
                flash.Elapsed += realDelta;
                if (!flash.IsPlaying) Stop(flash);
                else
                {
                    PlaceAtPlayer(arenaCamera, player);
                    Apply(flash);
                }
            }
        }

        /// <summary>Gone at once, the sound too (the battle is over, restarted or left).</summary>
        public void Clear()
        {
            if (disposed) return;
            foreach (Burst burst in bursts) Stop(burst);
            Stop(flash);
            if (audio != null) audio.Stop();
        }

        public void Dispose()
        {
            if (disposed) return;
            Clear();
            disposed = true;
            if (queueLayer != null) DuelGearShimmer.Release(queueLayer.gameObject);
            if (flashLayer != null) DuelGearShimmer.Release(flashLayer.gameObject);
        }

        private void PlaySound(int chainLength, float volume)
        {
            if (clip == null || !(volume > 0f) || audio == null || !audio.isActiveAndEnabled) return;
            audio.pitch = LegacyMeshCue.Pitch(chainLength);
            audio.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private static void Begin(Burst burst, int slot, int chainLength, float seconds, float size, int sparks)
        {
            if (!(seconds > 0f))
            {
                Stop(burst);
                return;
            }
            burst.Slot = slot;
            burst.Seconds = seconds;
            burst.Elapsed = 0f;
            burst.Size = size;
            burst.Scale = LegacyMeshCue.BurstScale(chainLength);
            burst.SparkCount = LegacyMeshCue.SparkCount(sparks, chainLength);
            burst.Node.gameObject.SetActive(true);
            // Placed and drawn on the next tick, after the HUD's layout; until then it shows nothing.
            burst.Upper.color = burst.Lower.color = Color.clear;
            burst.Sparks.Configure(0, 0f, 0f, 0f);
        }

        private static void Stop(Burst burst)
        {
            burst.Seconds = burst.Elapsed = 0f;
            burst.Slot = -1;
            burst.Node.gameObject.SetActive(false);
        }

        // On the seam between the burst's card and the one before it, where the row now has them; false once either card
        // has gone (the queue was cleared, or resolution consumed it).
        private bool PlaceOnSeam(Burst burst)
        {
            RectTransform before = hud.GetQueuedSkillAnchor(true, burst.Slot - 1), after = hud.GetQueuedSkillAnchor(true, burst.Slot);
            if (before == null || after == null) return false;
            Vector3 seam = (before.position + after.position) * .5f;
            burst.Node.anchoredPosition = queueLayer.InverseTransformPoint(seam);
            return true;
        }

        private void PlaceAtPlayer(Camera arenaCamera, Transform player)
        {
            Rect bounds = flashLayer.rect;
            Vector2 position = new Vector2(bounds.xMin + bounds.width * .3f, bounds.yMin + bounds.height * .48f);
            if (arenaCamera != null && player != null && player.gameObject.activeInHierarchy)
            {
                Vector3 screen = arenaCamera.WorldToScreenPoint(player.TransformPoint(DuelStepHud.ActorBodyLocalOffset));
                Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                if (screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(flashLayer, screen, uiCamera,
                        out Vector2 projected))
                    position = projected;
            }
            float insetX = Mathf.Min(FlashInset, bounds.width * .5f), insetY = Mathf.Min(FlashInset, bounds.height * .5f);
            flash.Node.anchoredPosition = new Vector2(Mathf.Clamp(position.x, bounds.xMin + insetX, bounds.xMax - insetX),
                Mathf.Clamp(position.y, bounds.yMin + insetY, bounds.yMax - insetY));
        }

        // The pair where the effect has them: popping up, turning against each other with their teeth meshed, glowing white
        // as they bite and fading out, the sparks flying from where they meet.
        private static void Apply(Burst burst)
        {
            float t = LegacyMeshCue.Progress(burst.Elapsed, burst.Seconds);
            float size = burst.Size * burst.Scale, pop = LegacyMeshCue.GearScale(t);
            float offset = LegacyMeshCue.MeshOffset * size * pop, turn = LegacyMeshCue.GearTurn(t);
            RectTransform upper = burst.Upper.rectTransform, lower = burst.Lower.rectTransform;
            upper.sizeDelta = lower.sizeDelta = Vector2.one * size;
            upper.localScale = lower.localScale = Vector3.one * pop;
            upper.anchoredPosition = new Vector2(0f, offset);
            lower.anchoredPosition = new Vector2(0f, -offset);
            upper.localRotation = Quaternion.Euler(0f, 0f, -turn);
            lower.localRotation = Quaternion.Euler(0f, 0f, LegacyMeshCue.LowerPhase + turn);
            Color ink = Color.Lerp(Brass, GlowInk, LegacyMeshCue.GearGlow(t));
            ink.a = LegacyMeshCue.GearAlpha(t);
            burst.Upper.color = burst.Lower.color = ink;
            burst.Sparks.Configure(burst.SparkCount, t, size * LegacyMeshCue.SparkReach,
                Mathf.Max(MinimumSparkWidth, size * SparkWidthShare));
        }

        // The gear draws only after the row (over its cards) whatever the HUD has added or moved since.
        private void KeepAfter(RectTransform row)
        {
            if (row == null) return;
            int at = row.GetSiblingIndex();
            int mine = queueLayer.GetSiblingIndex();
            if (mine != at + 1) queueLayer.SetSiblingIndex(mine > at ? at + 1 : at);
        }

        private Burst CreateBurst(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            node.gameObject.layer = parent.gameObject.layer;
            node.SetParent(parent, false);
            node.anchorMin = node.anchorMax = node.pivot = Vector2.one * .5f;
            node.sizeDelta = Vector2.zero;
            var burst = new Burst
            {
                Node = node,
                Upper = Gear("Upper Gear", node),
                Lower = Gear("Lower Gear", node),
            };
            var sparks = new GameObject("Sparks", typeof(RectTransform), typeof(CanvasRenderer), typeof(DuelMeshSparks))
                .GetComponent<RectTransform>();
            sparks.gameObject.layer = node.gameObject.layer;
            sparks.SetParent(node, false);
            sparks.anchorMin = sparks.anchorMax = sparks.pivot = Vector2.one * .5f;
            sparks.sizeDelta = Vector2.one * 200f;
            burst.Sparks = sparks.GetComponent<DuelMeshSparks>();
            burst.Sparks.raycastTarget = false;
            return burst;
        }

        private Image Gear(string name, Transform parent)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.gameObject.layer = parent.gameObject.layer;
            RectTransform rect = image.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            image.sprite = hud.MeshGearSprite;
            image.raycastTarget = false;
            image.color = Color.clear;
            return image;
        }

        private static RectTransform Layer(string name, Transform parent, bool stretch)
        {
            var layer = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            layer.gameObject.layer = parent.gameObject.layer;
            layer.SetParent(parent, false);
            if (stretch)
            {
                layer.anchorMin = Vector2.zero;
                layer.anchorMax = Vector2.one;
            }
            else layer.anchorMin = layer.anchorMax = Vector2.one * .5f;
            layer.pivot = Vector2.one * .5f;
            layer.sizeDelta = layer.anchoredPosition = Vector2.zero;
            return layer;
        }

        /// <summary>One burst: its node (on the seam, or at the player), its pair of gears and its sparks.</summary>
        private sealed class Burst
        {
            public RectTransform Node;
            public Image Upper, Lower;
            public DuelMeshSparks Sparks;
            public float Elapsed, Seconds, Size, Scale = 1f;
            public int Slot = -1, SparkCount;

            public bool IsPlaying => Seconds > 0f && Elapsed < Seconds;
        }
    }

    /// <summary>맞물림's sparks: short streaks flying from where two gears' teeth meet, hot at the head and cooling to nothing
    /// along their arc (<see cref="LegacyMeshCue.SparkX"/>). Drawn in code; no texture.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DuelMeshSparks : MaskableGraphic
    {
        private static readonly Color Head = new Color(1f, .96f, .74f, 1f);
        private static readonly Color Tail = new Color(1f, .56f, .16f, 0f);
        private int count;
        private float progress = 1f, reach, width;

        /// <summary>The sparks drawn now (0 once they have flown, or for none).</summary>
        public int Count => progress >= 1f || reach <= 0f ? 0 : count;
        public float Progress => progress;
        /// <summary>How far they reach from the seam (HUD units).</summary>
        public float Reach => reach;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        /// <param name="sparkCount">How many fly.</param>
        /// <param name="t">Where the burst is, 0 to 1.</param>
        /// <param name="sparkReach">How far the farthest flies from the seam (HUD units).</param>
        /// <param name="sparkWidth">A streak's width at its head (HUD units).</param>
        public void Configure(int sparkCount, float t, float sparkReach, float sparkWidth)
        {
            sparkCount = Mathf.Max(0, sparkCount);
            t = float.IsNaN(t) || float.IsInfinity(t) ? 1f : Mathf.Clamp01(t);
            sparkReach = sparkReach > 0f && !float.IsInfinity(sparkReach) ? sparkReach : 0f;
            sparkWidth = sparkWidth > 0f && !float.IsInfinity(sparkWidth) ? sparkWidth : 0f;
            if (count == sparkCount && Mathf.Approximately(progress, t) && Mathf.Approximately(reach, sparkReach) &&
                Mathf.Approximately(width, sparkWidth)) return;
            count = sparkCount;
            progress = t;
            reach = sparkReach;
            width = sparkWidth;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            float alpha = LegacyMeshCue.SparkAlpha(progress) * color.a;
            if (count <= 0 || reach <= 0f || width <= 0f || alpha <= 0f) return;
            float trail = LegacyMeshCue.SparkTrail(progress);
            Color head = Head, tail = Tail;
            head.a *= alpha;
            for (int index = 0; index < count; index++)
            {
                var front = new Vector2(LegacyMeshCue.SparkX(index, progress), LegacyMeshCue.SparkY(index, progress)) * reach;
                var back = new Vector2(LegacyMeshCue.SparkX(index, trail), LegacyMeshCue.SparkY(index, trail)) * reach;
                Vector2 along = front - back;
                if (along.sqrMagnitude < 1e-4f)
                {
                    float angle = LegacyMeshCue.SparkAngle(index) * Mathf.Deg2Rad;
                    along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * .5f;
                    back = front - along;
                }
                Vector2 side = new Vector2(-along.y, along.x).normalized * (width * .5f);
                int first = vertices.currentVertCount;
                vertices.AddVert(front + side, head, Vector2.zero);
                vertices.AddVert(front - side, head, Vector2.up);
                vertices.AddVert(back - side * .3f, tail, Vector2.one);
                vertices.AddVert(back + side * .3f, tail, Vector2.right);
                vertices.AddTriangle(first, first + 1, first + 2);
                vertices.AddTriangle(first + 2, first + 3, first);
            }
        }
    }
}
