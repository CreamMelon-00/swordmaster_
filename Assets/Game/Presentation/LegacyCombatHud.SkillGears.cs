using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Cutscene;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>The battle dock's skill gears (<see cref="LegacySkillGear"/> has their geometry and curves). Each open lane
    /// is a brass gear turning out of the dock's bottom edge, only its upper part showing: its skills sit in sockets on the
    /// rim, the lane's order repeated round it, the current one at the top inside a fixed window (the lane's button,
    /// <c>Input/Keys/Current X</c>, with the key, school, name, the ACT cost on the hub and the hold bar; held, it is the
    /// lane's key held, <see cref="LanePointerHold"/>), the next at the
    /// upper left (<c>Next X</c>) and the one just used or sent back, dimmed, at the upper right (<c>Used X</c>). Small idlers
    /// sit between neighbouring open gears so every gear turns the same way. What the gears show is always read from the
    /// duel's lane orders; a turn only animates the change, on real time: queueing turns that lane's gear one slot
    /// (<see cref="PlayLaneTurn(int)"/>, a tick), 넘기기 turns every open gear together with the idlers the other way and a
    /// ratchet's bounce (<see cref="PlayLaneTurn(bool[])"/>, a ratchet), and a turn nobody announced (a duel queued on
    /// directly) still turns its gear, silently.</summary>
    public sealed partial class LegacyCombatHud
    {
        /// <summary>The gears' centres in the dock ('Input/Keys' space: its centre at 0, y up), just inside its bottom edge:
        /// the line they turn out of, below which nothing of them shows.</summary>
        public const float GearCentreY = -96f;
        /// <summary>How far either side of the middle the gears may reach: 넘기기 and 숨고르기 (x ±360, 136 wide) start at ±292.</summary>
        public const float GearRoomHalfWidth = 284f;
        /// <summary>The current skill's size at the top of its gear; the other slots scale from it
        /// (<see cref="LegacySkillGear.SlotScale"/>).</summary>
        public const float CurrentSkillSize = 52f;
        /// <summary>The fixed window round the current skill, the lane's button.</summary>
        public const float SkillWindowSize = 66f;
        /// <summary>The gears' sounds (<c>Resources/Sfx</c>): a lane's turn and 넘기기's.</summary>
        public const string GearTickSound = "gear-tick", GearRatchetSound = "gear-ratchet";
        private const float SocketPad = 6f;
        // The gears' clip runs up to the dock's top, so nothing of a gear or an idler is cut but what is below the edge.
        private const float GearMaskTop = DockHeight * .5f;
        // Over the window: the skill's name, and above it the lane's key and school.
        private const float SkillNameRow = SkillWindowSize * .5f + 12f, KeyRow = SkillWindowSize * .5f + 32f;
        // On the hub, below the window: the cost's plate and, under it, the hold bar on its dark track (from the gear's centre).
        private const float CostAboveHub = 18f, HoldAboveHub = 4f;
        private const int WheelTexels = 256, IdlerTexels = 96, SocketTexels = 64;
        // The idler's spokes: two, a bar across it, so its turn reads forward even in a 30 fps frame's step (under half a
        // turn of their symmetry), where its six like teeth alone could seem to step back.
        private const int IdlerSpokes = 2;
        private static readonly float Cos30 = Mathf.Cos(30f * Mathf.Deg2Rad), Sin30 = .5f;
        private static readonly Color GearBrass = Color.Lerp(DuelVisualTheme.Border, DuelVisualTheme.Accent, .4f);
        private static readonly Color IdlerBrass = DuelVisualTheme.Border;
        // A socket dims with its skill by darkening, never by fading: whatever it sits over (an idler's teeth) stays hidden.
        private static readonly Color DimSocket = new Color(.45f, .45f, .45f, 1f);

        private readonly SkillGear[] gears = new SkillGear[3];
        private readonly Image[] idlers = new Image[2];
        // The open lanes either side of each idler as last laid out (-1: none).
        private readonly int[] idlerLeft = { -1, -1 }, idlerRight = { -1, -1 };
        private readonly float[] idlerRest = new float[2], idlerPhase = new float[2];
        private readonly List<int> laneOrder = new List<int>();
        private float idlerFrom, idlerElapsed, idlerSeconds, idlerBounce, idlerRatio = 1f, idlerHeight;
        private RectTransform gearBand;
        private GearGeometry laidOutGeometry;
        private float slotRadius = LegacySkillGear.SlotRadius(LegacySkillGear.DefaultRadius);
        // The pointer held on each lane's window (the lane's hold, read by the controller like its key).
        private readonly LanePointerHold[] lanePointers = new LanePointerHold[3];
        private Texture2D wheelTexture, idlerTexture, socketTexture;
        private Sprite wheelSprite, idlerSprite, socketSprite;
        private (float, int, float) wheelPicture, idlerPicture;
        private AudioSource gearAudio;
        private AudioClip gearTick, gearRatchet;

        /// <summary>The pitch the open lanes' gears are packed at now (HUD units; less than tuned when three would not fit
        /// between 넘기기 and 숨고르기, or when an idler between two would not mesh with both above the dock's edge,
        /// <see cref="LegacySkillGear.MeshedPitch"/>).</summary>
        public float LanePitch { get; private set; }

        /// <summary>Where the current skills' windows sit in the dock: the top of the gears ('Input/Keys' space).</summary>
        public float SkillWindowY => GearCentreY + slotRadius;

        /// <summary>The HUD's own voice for the gears' sounds, and the sounds (null when Resources has none).</summary>
        public AudioSource GearAudio => gearAudio;
        public AudioClip GearTickClip => gearTick;
        public AudioClip GearRatchetClip => gearRatchet;

        /// <summary>Whether a lane's gear is still turning.</summary>
        public bool IsLaneTurning(int lane) => lane >= 0 && lane < gears.Length && gears[lane] != null && gears[lane].IsTurning;

        /// <summary>Whether the idlers are still turning after 넘기기.</summary>
        public bool AreIdlersTurning => idlerElapsed < idlerSeconds;

        /// <summary>A lane gear's rotation now (degrees, counter-clockwise; it turns clockwise).</summary>
        public float LaneGearRotation(int lane) => lane >= 0 && lane < gears.Length ? gears[lane].Rotation : 0f;

        /// <summary>An idler's rotation now (degrees, counter-clockwise: the other way from the lane gears). Idler 1 sits
        /// between the first two open lanes, idler 2 between the second and the third.</summary>
        public float IdlerRotation(int index)
            => index >= 0 && index < idlers.Length ? idlerRest[index] + idlerPhase[index] - idlerRatio * IdlerRemaining : 0f;

        /// <summary>넘기기 just turned the lanes marked in <paramref name="turned"/> (every open lane with a skill: the gears
        /// mesh): their gears turn one slot together, the idlers the other way, ending on a ratchet's bounce, and the
        /// ratchet sounds. Call after the duel turned them; the next <see cref="Refresh"/> reads the new orders.</summary>
        public void PlayLaneTurn(bool[] turned)
        {
            if (disposed || turned == null) return;
            bool any = false;
            for (int lane = 0; lane < gears.Length && lane < turned.Length; lane++)
            {
                if (!turned[lane] || !gears[lane].Present) continue;
                gears[lane].PendingTurns++;
                StartTurn(gears[lane], 1, true);
                any = true;
            }
            if (!any) return;
            StartIdlers();
            PlayGearSound(gearRatchet, presentationSettings != null ? presentationSettings.GearRatchetVolume
                : LegacySkillGear.DefaultRatchetVolume);
            AdvanceLaneTurns(0f);
        }

        /// <summary>A skill was just queued from <paramref name="lane"/>: its gear turns one slot (the skill goes to the upper
        /// right, the next one comes to the top) and ticks. Call after the duel turned it.</summary>
        public void PlayLaneTurn(int lane)
        {
            if (disposed || lane < 0 || lane >= gears.Length || !gears[lane].Present) return;
            gears[lane].PendingTurns++;
            StartTurn(gears[lane], 1, false);
            PlayGearSound(gearTick, presentationSettings != null ? presentationSettings.GearTickVolume
                : LegacySkillGear.DefaultTickVolume);
            AdvanceLaneTurns(0f);
        }

        /// <summary>Reads the pointer held on a lane's window since the last read (the controller, every planning frame, beside
        /// the lane's key): whether it is down now, whether it went down (<paramref name="pressed"/>) and whether it came up
        /// (<paramref name="released"/>; both within one frame for a quick click). <paramref name="away"/>: it came up off the
        /// window, which lets go without queuing, as a button's click is cancelled. Reading clears the edges.</summary>
        public bool ReadLanePointer(int lane, out bool pressed, out bool released, out bool away)
        {
            pressed = released = away = false;
            if (disposed || lane < 0 || lane >= lanePointers.Length || lanePointers[lane] == null) return false;
            return lanePointers[lane].Read(out pressed, out released, out away);
        }

        /// <summary>Forgets the lanes' pointer presses and releases not yet read (a pointer still down stays down), so a
        /// press from before planning, or under Tab, is not taken for a tap later.</summary>
        public void ClearLanePointers()
        {
            foreach (LanePointerHold press in lanePointers)
                if (press != null) press.Read(out _, out _, out _);
        }

        private float TurnSeconds => presentationSettings != null ? presentationSettings.SkillGearTurnSeconds
            : LegacySkillGear.DefaultTurnSeconds;
        private float ShiftSeconds => presentationSettings != null ? presentationSettings.SkillGearShiftSeconds
            : LegacySkillGear.DefaultShiftSeconds;
        private float RatchetBounce => presentationSettings != null ? presentationSettings.SkillGearRatchetBounce
            : LegacySkillGear.DefaultRatchetBounce;

        /// <summary>The longest a gear's turn can take now (a queue's or 넘기기's, real seconds).</summary>
        public float LaneTurnSeconds => Mathf.Max(TurnSeconds, ShiftSeconds);

        private float IdlerRemaining => idlerElapsed < idlerSeconds
            ? idlerFrom * (1f - LegacySkillGear.ShiftShare(idlerElapsed, idlerSeconds, idlerBounce)) : 0f;

        // A turn begins (or overtakes the one still running: never more than one slot is left to show, so the gear only
        // ever shows the slots it has).
        private void StartTurn(SkillGear gear, int turns, bool shift)
        {
            float from = Mathf.Min(gear.Remaining + turns * LegacySkillGear.SlotDegrees, LegacySkillGear.SlotDegrees);
            gear.WheelRest = LegacySkillGear.Wrap(gear.WheelRest - turns * LegacySkillGear.SlotDegrees);
            gear.Shift = shift;
            gear.TurnBounce = shift ? RatchetBounce : 0f;
            gear.TurnSeconds = shift ? ShiftSeconds : TurnSeconds;
            gear.TurnFrom = from;
            gear.TurnElapsed = 0f;
            gear.Dirty = true;
        }

        private void StartIdlers()
        {
            idlerFrom = Mathf.Min(IdlerRemaining + LegacySkillGear.SlotDegrees, LegacySkillGear.SlotDegrees);
            for (int index = 0; index < idlerRest.Length; index++)
                idlerRest[index] = LegacySkillGear.Wrap(idlerRest[index] + idlerRatio * LegacySkillGear.SlotDegrees);
            idlerSeconds = ShiftSeconds;
            idlerBounce = RatchetBounce;
            idlerElapsed = 0f;
        }

        private void PlayGearSound(AudioClip clip, float volume)
        {
            if (clip == null || !(volume > 0f) || gearAudio == null || !gearAudio.isActiveAndEnabled) return;
            gearAudio.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        /// <summary>Builds the gears, their windows and the idlers into the dock, under everything else of the lanes.</summary>
        private void BuildSkillGears(RectTransform controls, Sprite white, Action<int> queue)
        {
            gearAudio = root.gameObject.AddComponent<AudioSource>();
            gearAudio.playOnAwake = false;
            gearAudio.loop = false;
            gearAudio.spatialBlend = 0f;
            gearTick = Resources.Load<AudioClip>(CutsceneStep.SoundFolder + GearTickSound);
            gearRatchet = Resources.Load<AudioClip>(CutsceneStep.SoundFolder + GearRatchetSound);
            socketTexture = SocketTexture();
            socketSprite = DuelGearShimmer.CreateSprite(socketTexture);
            // The gears and idlers turn behind a clip at the line they come out of; only their upper parts show.
            gearBand = Rect("Lane Gears", controls, Vector2.zero, Vector2.zero, Vector2.one * .5f);
            gearBand.gameObject.AddComponent<RectMask2D>();
            for (int lane = 0; lane < gears.Length; lane++)
            {
                var gear = new SkillGear();
                gears[lane] = gear;
                gear.Node = Rect("Gear " + "QWE"[lane], gearBand, Vector2.zero, Vector2.zero, Vector2.one * .5f);
                gear.Wheel = Image("Wheel", gear.Node, null, Vector2.zero, Vector2.one * 100f, GearBrass);
                gear.Groups[3] = gear.Node.gameObject.AddComponent<CanvasGroup>();
            }
            for (int index = 0; index < idlers.Length; index++)
            {
                idlers[index] = Image("Idler " + (index + 1), gearBand, null, Vector2.zero, Vector2.one * 40f, IdlerBrass);
                idlers[index].gameObject.SetActive(false);
            }
            // The slot leaving below the right of the gear, on its way out of sight: over the idlers, still clipped.
            for (int lane = 0; lane < gears.Length; lane++)
            {
                SkillGear gear = gears[lane];
                gear.Spent = Rect("Spent " + "QWE"[lane], gearBand, Vector2.zero, Vector2.zero, Vector2.one * .5f);
                gear.Groups[4] = gear.Spent.gameObject.AddComponent<CanvasGroup>();
                AddSlot(gear, 0, gear.Spent, "Spent Socket", "Spent Skill Image");
            }
            // The edge the gears turn out of.
            Image("Gear Rail", controls, white, new Vector2(0f, GearCentreY), new Vector2(GearRoomHalfWidth * 2f, 4f), Border);
            for (int lane = 0; lane < gears.Length; lane++)
            {
                SkillGear gear = gears[lane];
                char key = "QWE"[lane];
                var window = Panel("Current " + key, controls, Vector2.zero, Vector2.one * SkillWindowSize, Card, Border);
                gear.Window = window.rectTransform;
                AddSlot(gear, 2, window.transform, "Socket", "Skill Image");
                var keyLabel = Text("Key", window.transform, new Vector2(-18f, KeyRow), new Vector2(24f, 22f), 20, TextAnchor.MiddleCenter);
                keyLabel.text = key.ToString(); keyLabel.color = Accent;
                var styleName = Text("Style Name", window.transform, new Vector2(16f, KeyRow), new Vector2(48f, 20f), 14, TextAnchor.MiddleLeft);
                styleName.text = SkillLaneStyle.Name(lane); styleName.color = Foreground;
                skillNames[lane] = Text("Skill Name", window.transform, new Vector2(0f, SkillNameRow), new Vector2(150f, 20f), 15, TextAnchor.MiddleCenter);
                skillNames[lane].supportRichText = false;
                skillNames[lane].verticalOverflow = VerticalWrapMode.Truncate;
                skillNames[lane].resizeTextForBestFit = true;
                skillNames[lane].resizeTextMinSize = 11;
                skillNames[lane].resizeTextMaxSize = 15;
                gear.CostPlate = Panel("Cost Plate", window.transform, Vector2.zero, new Vector2(60f, 20f), Card, Border).rectTransform;
                costs[lane] = Text("SkillCost", window.transform, Vector2.zero, new Vector2(64f, 18f), 16, TextAnchor.MiddleCenter);
                costs[lane].color = Foreground;
                // The hold bar crosses the gear's brass hub, so it fills on a dark track of its own.
                gear.HoldTrack = Image("KeyHoldTrack", window.transform, white, Vector2.zero, new Vector2(60f, 7f), Track).rectTransform;
                holdImages[lane] = Image("KeyHoldImage", window.transform, white, Vector2.zero, new Vector2(56f, 3f), Accent);
                Filled(holdImages[lane], UnityEngine.UI.Image.FillMethod.Horizontal, 0);
                holdImages[lane].fillAmount = 0;
                // A brief replacement for the cost plate tells the player that releasing during the hold bar's
                // in-between interval deliberately chose neither the skill nor its explanation.
                var cancel = Panel("Hold Cancel", window.transform, Vector2.zero, new Vector2(68f, 20f), RaisedSurface, Border);
                holdCancelPlates[lane] = cancel.rectTransform;
                var cancelLabel = Text("Hold Cancel Label", cancel.transform, Vector2.zero, new Vector2(62f, 18f), 12,
                    TextAnchor.MiddleCenter);
                cancelLabel.text = "입력 취소";
                cancelLabel.color = MutedText;
                holdCancelGroups[lane] = cancel.gameObject.AddComponent<CanvasGroup>();
                cancel.gameObject.SetActive(false);
                var button = AddButton(window);
                button.transition = Selectable.Transition.None;
                int selectedLane = lane;
                LanePointerHold press = window.gameObject.AddComponent<LanePointerHold>();
                lanePointers[lane] = press;
                button.onClick.AddListener(() =>
                {
                    // A press the pointer made is judged on its release, as a key's is (ReadLanePointer: a tap queues, a
                    // hold explains); a click that comes without one queues at once.
                    if (!press.EndsPress) queue?.Invoke(selectedLane);
                    ClearSelection(button.gameObject);
                });
                laneButtons[lane] = button;
                guideLaneGroups[lane] = window.gameObject.AddComponent<CanvasGroup>();
                gear.Groups[0] = guideLaneGroups[lane];
                guideLaneFocus[lane] = AddGuideOutline(window);
                laneFeedback[lane] = SkillCardFeedbackGraphic.Create(window.transform, "Lane Condition Feedback", 3f);

                // The one just used or sent back, dimmed at the upper right.
                gear.Used = Rect("Used " + key, controls, Vector2.zero, Vector2.zero, Vector2.one * .5f);
                gear.Groups[1] = gear.Used.gameObject.AddComponent<CanvasGroup>();
                AddSlot(gear, 1, gear.Used, "Used Socket", "Used Skill Image");
                // The next, at the upper left: it rises out of the dock's edge as the gear turns, so it is clipped there.
                gear.Next = Rect("Next " + key, controls, Vector2.zero, Vector2.zero, Vector2.one * .5f);
                gear.Next.gameObject.AddComponent<RectMask2D>();
                gear.Groups[2] = gear.Next.gameObject.AddComponent<CanvasGroup>();
                AddSlot(gear, 3, gear.Next, "Next Socket", "Next Skill Image");
            }
        }

        // A slot's socket and its skill's icon, the icon over the socket.
        private void AddSlot(SkillGear gear, int index, Transform parent, string socketName, string iconName)
        {
            gear.Sockets[index] = Image(socketName, parent, socketSprite, Vector2.zero, Vector2.one * CurrentSkillSize);
            gear.Icons[index] = Image(iconName, parent, null, Vector2.zero, Vector2.one * CurrentSkillSize);
            gear.Icons[index].preserveAspect = true;
        }

        /// <summary>Packs the open lanes' gears side by side in Q, W, E order and words 넘기기 for them. Runs only when the
        /// open set (or the gears' tuning) changes, so everything rests where it was put between duels with the same lanes.</summary>
        private void LayoutGears(CombatFeature features, bool force = false)
        {
            CombatFeature open = features & AllLanes;
            if (open == CombatFeature.None) return;
            GearGeometry geometry = GearGeometry.From(presentationSettings);
            if (!force && open == laidOutLanes && geometry.Equals(laidOutGeometry)) return;
            laidOutLanes = open;
            laidOutGeometry = geometry;
            int gearTeeth = LegacySkillGear.GearTeeth(geometry.TeethPerSlot);
            int idlerTeeth = LegacySkillGear.IdlerTeeth(gearTeeth, geometry.Radius, geometry.IdlerRadius, geometry.ToothDepth);
            DrawGearPictures(geometry, gearTeeth, idlerTeeth);
            idlerRatio = gearTeeth / (float)idlerTeeth;
            slotRadius = LegacySkillGear.SlotRadius(geometry.Radius);
            int count = open.LaneCount();
            float laneHalfWidth = Mathf.Max(geometry.Radius,
                slotRadius * Cos30 + (CurrentSkillSize * LegacySkillGear.NextScale + SocketPad) * .5f);
            LanePitch = LegacySkillGear.FitPitch(count, geometry.Pitch, laneHalfWidth, GearRoomHalfWidth);
            // With idlers, never so wide that one sinks to the dock's edge: each meshes with both gears, wholly above it.
            float meshed = geometry.IdlerRadius > 0f
                ? LegacySkillGear.MeshedPitch(geometry.Radius, geometry.IdlerRadius, geometry.ToothDepth) : 0f;
            if (meshed > 0f) LanePitch = Mathf.Min(LanePitch, meshed);
            idlerHeight = LegacySkillGear.IdlerHeight(LanePitch, geometry.Radius, geometry.IdlerRadius, geometry.ToothDepth);
            float contact = Mathf.Atan2(idlerHeight, LanePitch * .5f) * Mathf.Rad2Deg;
            float bandCentre = (GearCentreY + GearMaskTop) * .5f;
            gearBand.anchoredPosition = new Vector2(0f, bandCentre);
            gearBand.sizeDelta = new Vector2(GearRoomHalfWidth * 2f + 8f, GearMaskTop - GearCentreY);
            Vector2 wheelSize = Vector2.one * (geometry.Radius * 2f / LegacySkillGear.TipShare);
            Vector2 idlerSize = Vector2.one * (geometry.IdlerRadius * 2f / LegacySkillGear.TipShare);
            for (int index = 0; index < idlers.Length; index++)
            {
                idlerLeft[index] = idlerRight[index] = -1;
                idlers[index].rectTransform.sizeDelta = idlerSize;
            }
            // Where each slot's holder rests, from the gear's centre: the spent slot's node is the centre itself.
            float nextLeft = -(slotRadius + 30f), nextRight = -6f, nextTop = slotRadius + 30f;
            var homes = new[]
            {
                Vector2.zero,
                new Vector2(slotRadius * Cos30, slotRadius * Sin30),
                new Vector2(0f, slotRadius),
                new Vector2((nextLeft + nextRight) * .5f, nextTop * .5f),
            };
            int packed = 0, previous = -1;
            float phase = 0f;
            for (int lane = 0; lane < gears.Length; lane++)
            {
                if (!open.HasLane(lane)) continue;
                SkillGear gear = gears[lane];
                float x = LegacySkillGear.LaneX(packed, count, LanePitch);
                if (previous >= 0)
                {
                    // Mesh along the train: this idler to the gear before it, this gear to the idler.
                    int gap = packed - 1;
                    idlerLeft[gap] = previous;
                    idlerRight[gap] = lane;
                    idlerPhase[gap] = LegacySkillGear.MeshPhase(contact, phase, gearTeeth, idlerTeeth);
                    phase = LegacySkillGear.MeshPhase(-contact, idlerPhase[gap], idlerTeeth, gearTeeth);
                    idlers[gap].rectTransform.anchoredPosition = new Vector2(x - LanePitch * .5f, GearCentreY + idlerHeight - bandCentre);
                }
                gear.MeshPhase = phase;
                Array.Copy(homes, gear.Homes, homes.Length);
                gear.Node.anchoredPosition = gear.Spent.anchoredPosition = new Vector2(x, GearCentreY - bandCentre);
                gear.Wheel.rectTransform.sizeDelta = wheelSize;
                gear.Window.anchoredPosition = new Vector2(x, GearCentreY) + homes[2];
                gear.Used.anchoredPosition = new Vector2(x, GearCentreY) + homes[1];
                gear.Next.anchoredPosition = new Vector2(x, GearCentreY) + homes[3];
                gear.Next.sizeDelta = new Vector2(nextRight - nextLeft, nextTop);
                // The cost and the hold bar (on its track) sit on the hub, under the window.
                gear.CostPlate.anchoredPosition = new Vector2(0f, CostAboveHub - slotRadius);
                costs[lane].rectTransform.anchoredPosition = new Vector2(0f, CostAboveHub - slotRadius);
                gear.HoldTrack.anchoredPosition = holdImages[lane].rectTransform.anchoredPosition =
                    new Vector2(0f, HoldAboveHub - slotRadius);
                holdCancelPlates[lane].anchoredPosition = gear.CostPlate.anchoredPosition;
                gear.Dirty = true;
                previous = lane;
                packed++;
            }
            UpdateIdlerPresence();
            ApplyGears();
            cycleEffect.text = CycleEffectText(open);
        }

        /// <summary>The lane's gear shows or hides with the lane (a closed lane or an empty one is not drawn).</summary>
        private void SetGearPresent(int lane, bool present)
        {
            SkillGear gear = gears[lane];
            if (gear.Present == present && gear.Window.gameObject.activeSelf == present) return;
            gear.Present = present;
            gear.Window.gameObject.SetActive(present);
            gear.Used.gameObject.SetActive(present);
            gear.Next.gameObject.SetActive(present);
            gear.Node.gameObject.SetActive(present);
            gear.Spent.gameObject.SetActive(present);
            if (!present) ForgetLaneOrder(gear);
        }

        // An idler shows only between two open lanes that are both drawn, and only when idlers are tuned in and reach both
        // gears above the dock's edge (never parked on it, unmeshed).
        private void UpdateIdlerPresence()
        {
            bool tuned = laidOutGeometry.IdlerRadius > 0f && idlerHeight > 0f;
            for (int index = 0; index < idlers.Length; index++)
            {
                bool shown = tuned && idlerLeft[index] >= 0 && idlerRight[index] >= 0 &&
                    gears[idlerLeft[index]].Present && gears[idlerRight[index]].Present;
                if (idlers[index].gameObject.activeSelf != shown) idlers[index].gameObject.SetActive(shown);
            }
        }

        /// <summary>Reads a lane's order from the duel: a turn since the last look that nobody announced starts its gear
        /// turning, an announced one that did not happen settles, and every slot shows its skill.</summary>
        private void FollowLaneOrder(int lane, IReadOnlyList<LegacySkill> sequence, bool affordable)
        {
            SkillGear gear = gears[lane];
            laneOrder.Clear();
            for (int index = 0; index < sequence.Count; index++) laneOrder.Add(sequence[index].Id);
            int pending = gear.PendingTurns;
            gear.PendingTurns = 0;
            int turns = LegacySkillGear.TurnsBetween(gear.ShownOrder, laneOrder, pending);
            if (turns != pending)
            {
                if (pending > 0) gear.Settle();
                if (turns > 0) StartTurn(gear, turns, false);
            }
            gear.ShownOrder.Clear();
            gear.ShownOrder.AddRange(laneOrder);
            for (int index = 0; index < gear.Icons.Length; index++)
            {
                LegacySkill skill = sequence[LegacySkillGear.SkillIndex(index - 2, sequence.Count)];
                if (gear.ShownIcons[index] == skill.IconId) continue;
                gear.ShownIcons[index] = skill.IconId;
                gear.Icons[index].sprite = HudIcon(skill.IconId);
            }
            if (gear.Affordable != affordable)
            {
                gear.Affordable = affordable;
                gear.Dirty = true;
            }
        }

        private static void ForgetLaneOrder(SkillGear gear)
        {
            gear.ShownOrder.Clear();
            gear.PendingTurns = 0;
            gear.Settle();
        }

        /// <summary>Moves the turns on by <paramref name="realDelta"/> and draws the gears where they now are.</summary>
        private void AdvanceLaneTurns(float realDelta)
        {
            realDelta = realDelta > 0f && !float.IsInfinity(realDelta) ? realDelta : 0f;
            foreach (SkillGear gear in gears)
            {
                if (!gear.IsTurning) continue;
                gear.TurnElapsed = Mathf.Min(gear.TurnSeconds, gear.TurnElapsed + realDelta);
                gear.Dirty = true;
            }
            if (idlerElapsed < idlerSeconds) idlerElapsed = Mathf.Min(idlerSeconds, idlerElapsed + realDelta);
            ApplyGears();
        }

        private void ApplyGears()
        {
            for (int lane = 0; lane < gears.Length; lane++) ApplyGear(gears[lane]);
            for (int index = 0; index < idlers.Length; index++)
                idlers[index].rectTransform.localRotation = Quaternion.Euler(0f, 0f, IdlerRotation(index));
        }

        // The gear's wheel, and each slot's socket and skill where the turn has them: upright, largest at the top, the next
        // larger and brighter than the used one.
        private void ApplyGear(SkillGear gear)
        {
            if (!gear.Dirty) return;
            gear.Dirty = false;
            float remaining = gear.Remaining;
            gear.Wheel.rectTransform.localRotation = Quaternion.Euler(0f, 0f, gear.Rotation);
            for (int index = 0; index < gear.Icons.Length; index++)
            {
                float angle = LegacySkillGear.SlotAngle(index - 2, remaining);
                Vector2 at = new Vector2(LegacySkillGear.SlotX(angle, slotRadius), LegacySkillGear.SlotY(angle, slotRadius))
                    - gear.Homes[index];
                float size = CurrentSkillSize * LegacySkillGear.SlotScale(angle), alpha = LegacySkillGear.SlotAlpha(angle);
                RectTransform icon = gear.Icons[index].rectTransform, socket = gear.Sockets[index].rectTransform;
                icon.anchoredPosition = socket.anchoredPosition = at;
                icon.sizeDelta = Vector2.one * size;
                socket.sizeDelta = Vector2.one * (size + SocketPad);
                // The current skill greys while the ACT for it is short.
                Color ink = index == 2 && !gear.Affordable ? new Color(MutedText.r, MutedText.g, MutedText.b, .62f) : Color.white;
                ink.a *= alpha;
                gear.Icons[index].color = ink;
                // The socket stays solid and darkens instead: an idler tucked under the next or used slot never shows
                // through a dimmed skill.
                gear.Sockets[index].color = Color.Lerp(DimSocket, Color.white, alpha);
            }
        }

        /// <summary>A lane held back by the coach shows faded, its gear and slots with its window.</summary>
        private void SetLaneFade(int lane, float alpha)
        {
            foreach (CanvasGroup group in gears[lane].Groups)
                if (group != null) group.alpha = alpha;
        }

        /// <summary>Every gear settles where its lane stands and forgets the order it showed (a new duel).</summary>
        private void ResetGears()
        {
            foreach (SkillGear gear in gears)
            {
                ForgetLaneOrder(gear);
                gear.WheelRest = 0f;
                for (int index = 0; index < gear.ShownIcons.Length; index++) gear.ShownIcons[index] = -1;
                gear.Dirty = true;
            }
            for (int index = 0; index < idlerRest.Length; index++) idlerRest[index] = 0f;
            idlerElapsed = idlerSeconds = 0f;
            ApplyGears();
        }

        // Draws the lane gear and the idler for these proportions, again only when they change.
        private void DrawGearPictures(GearGeometry geometry, int gearTeeth, int idlerTeeth)
        {
            var wheel = (geometry.Radius, gearTeeth, geometry.ToothDepth);
            if (wheelSprite == null || wheel != wheelPicture)
            {
                DuelGearShimmer.Release(wheelSprite);
                DuelGearShimmer.Release(wheelTexture);
                // The shimmer's gear: a rim on spokes to a hub, its teeth as deep as tuned.
                float rootRadius = LegacySkillGear.TipShare * Mathf.Clamp((geometry.Radius - geometry.ToothDepth) / geometry.Radius, .4f, .95f);
                wheelTexture = GearTexture("Skill Gear", WheelTexels, gearTeeth, rootRadius, rootRadius * .75f, .26f, .11f,
                    LegacyGearShimmer.Spokes);
                wheelSprite = DuelGearShimmer.CreateSprite(wheelTexture);
                wheelPicture = wheel;
                foreach (SkillGear gear in gears) gear.Wheel.sprite = wheelSprite;
            }
            var idler = (geometry.IdlerRadius, idlerTeeth, geometry.ToothDepth);
            if (geometry.IdlerRadius > 0f && (idlerSprite == null || idler != idlerPicture))
            {
                DuelGearShimmer.Release(idlerSprite);
                DuelGearShimmer.Release(idlerTexture);
                // A small pinion with teeth the lane gears' size: an open rim crossed by a bar (IdlerSpokes) through its
                // hub, the one mark that shows which way it turns.
                float rootRadius = LegacySkillGear.TipShare * Mathf.Clamp((geometry.IdlerRadius - geometry.ToothDepth) / geometry.IdlerRadius, .4f, .95f);
                idlerTexture = GearTexture("Skill Gear Idler", IdlerTexels, idlerTeeth, rootRadius, rootRadius - .14f, .22f, .1f,
                    IdlerSpokes);
                idlerSprite = DuelGearShimmer.CreateSprite(idlerTexture);
                idlerPicture = idler;
                foreach (Image image in idlers) image.sprite = idlerSprite;
            }
        }

        private void ReleaseGearPictures()
        {
            DuelGearShimmer.Release(wheelSprite);
            DuelGearShimmer.Release(wheelTexture);
            DuelGearShimmer.Release(idlerSprite);
            DuelGearShimmer.Release(idlerTexture);
            DuelGearShimmer.Release(socketSprite);
            DuelGearShimmer.Release(socketTexture);
            wheelSprite = idlerSprite = socketSprite = null;
            wheelTexture = idlerTexture = socketTexture = null;
        }

        // A gear in white, its edges softened over a texel (LegacyGearShimmer.GearCoverage); the Image tints it brass.
        private static Texture2D GearTexture(string name, int texels, int teeth, float rootRadius, float rimInnerRadius,
            float hubRadius, float axleRadius, int spokes)
        {
            var pixels = new Color32[texels * texels];
            float texel = 2f / texels;
            for (int y = 0; y < texels; y++)
                for (int x = 0; x < texels; x++)
                {
                    float coverage = LegacyGearShimmer.GearCoverage((x + .5f) * texel - 1f, (y + .5f) * texel - 1f, texel,
                        teeth, LegacySkillGear.TipShare, rootRadius, rimInnerRadius, hubRadius, axleRadius, spokes);
                    pixels[y * texels + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
                }
            return DuelGearShimmer.CreateTexture(name, texels, pixels);
        }

        // A socket on the rim: a dark well a skill's icon reads on, ringed in brass.
        private static Texture2D SocketTexture()
        {
            var pixels = new Color32[SocketTexels * SocketTexels];
            float texel = 2f / SocketTexels;
            // Solid: nothing behind a socket shows through it (ApplyGear darkens it rather than fading it).
            Color well = DuelVisualTheme.Track, ring = DuelVisualTheme.Accent;
            for (int y = 0; y < SocketTexels; y++)
                for (int x = 0; x < SocketTexels; x++)
                {
                    float u = (x + .5f) * texel - 1f, v = (y + .5f) * texel - 1f;
                    float radius = Mathf.Sqrt(u * u + v * v);
                    float inside = Mathf.Clamp01((1f - radius) / texel + .5f);
                    Color colour = Color.Lerp(well, ring, Mathf.Clamp01((radius - .86f) / texel + .5f));
                    colour.a *= inside;
                    pixels[y * SocketTexels + x] = colour;
                }
            return DuelGearShimmer.CreateTexture("Skill Gear Socket", SocketTexels, pixels);
        }

        /// <summary>The gears' tuning that changes their layout or pictures.</summary>
        private struct GearGeometry : IEquatable<GearGeometry>
        {
            public float Radius, Pitch, ToothDepth, IdlerRadius;
            public int TeethPerSlot;

            public static GearGeometry From(DuelPresentationSettings settings) => settings == null
                ? new GearGeometry
                {
                    Radius = LegacySkillGear.DefaultRadius, Pitch = LegacySkillGear.DefaultPitch,
                    ToothDepth = LegacySkillGear.DefaultToothDepth, IdlerRadius = LegacySkillGear.DefaultIdlerRadius,
                    TeethPerSlot = LegacySkillGear.DefaultTeethPerSlot,
                }
                : new GearGeometry
                {
                    Radius = settings.SkillGearRadius, Pitch = settings.SkillGearPitch, ToothDepth = settings.SkillGearToothDepth,
                    IdlerRadius = settings.SkillGearIdlerRadius, TeethPerSlot = settings.SkillGearTeethPerSlot,
                };

            public bool Equals(GearGeometry other) => Radius == other.Radius && Pitch == other.Pitch &&
                ToothDepth == other.ToothDepth && IdlerRadius == other.IdlerRadius && TeethPerSlot == other.TeethPerSlot;
            public override bool Equals(object other) => other is GearGeometry geometry && Equals(geometry);
            public override int GetHashCode() => Radius.GetHashCode() ^ Pitch.GetHashCode() ^ TeethPerSlot;
        }

        /// <summary>One lane's gear: its pieces and how far its turn has gone. Slots are held by index: 0 the spent slot
        /// leaving below the right, 1 the used slot, 2 the current (in the window), 3 the next.</summary>
        private sealed class SkillGear
        {
            public RectTransform Node, Spent, Window, Used, Next, CostPlate, HoldTrack;
            public Image Wheel;
            public readonly Image[] Icons = new Image[4], Sockets = new Image[4];
            // Where each slot's holder rests, from the gear's centre.
            public readonly Vector2[] Homes = new Vector2[4];
            public readonly int[] ShownIcons = { -1, -1, -1, -1 };
            // The window's, the used slot's, the next slot's, the wheel's and the spent slot's.
            public readonly CanvasGroup[] Groups = new CanvasGroup[5];
            public readonly List<int> ShownOrder = new List<int>();
            public int PendingTurns;
            public float TurnFrom, TurnElapsed, TurnSeconds, TurnBounce, WheelRest, MeshPhase;
            public bool Shift, Present, Affordable = true, Dirty = true;

            public bool IsTurning => TurnElapsed < TurnSeconds;

            /// <summary>Degrees of the turn still to go (0 at rest; below 0 while 넘기기's bounce runs past the slot).</summary>
            public float Remaining
            {
                get
                {
                    if (!IsTurning) return 0f;
                    float share = Shift ? LegacySkillGear.ShiftShare(TurnElapsed, TurnSeconds, TurnBounce)
                        : LegacySkillGear.TurnShare(TurnElapsed, TurnSeconds);
                    return TurnFrom * (1f - share);
                }
            }

            /// <summary>The wheel's rotation: where its turns have left it, meshed with its idlers, plus what is left to go.</summary>
            public float Rotation => WheelRest + MeshPhase + Remaining;

            public void Settle()
            {
                TurnElapsed = TurnSeconds;
                Dirty = true;
            }
        }
    }

    /// <summary>The primary pointer held on a lane's window (<see cref="LegacyCombatHud.ReadLanePointer"/>): its press and
    /// release kept until read, so a click quicker than a frame still counts. No timing or queuing of its own.</summary>
    public sealed class LanePointerHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private bool held, pressed, released, away;
        private int releasedFrame = -1;

        /// <summary>Whether the button's click this frame closes a press this window saw (it then queues on the release,
        /// through the controller, not on the click).</summary>
        public bool EndsPress => releasedFrame == Time.frameCount;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            held = pressed = true;
            away = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !held) return;
            GameObject over = eventData.pointerCurrentRaycast.gameObject;
            held = false;
            released = true;
            away = over == null || !over.transform.IsChildOf(transform);
            releasedFrame = Time.frameCount;
        }

        internal bool Read(out bool wentDown, out bool cameUp, out bool cameUpAway)
        {
            wentDown = pressed;
            cameUp = released;
            cameUpAway = released && away;
            pressed = released = away = false;
            return held;
        }

        // A window that goes away mid-press gets no release; the controller lets such a hold go without queuing.
        private void OnDisable() => held = pressed = released = away = false;
    }
}
