using System;
using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.Cutscene;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>The battle dock's skill gears (<see cref="LegacyCombatHud"/>, <see cref="LegacySkillGear"/>): each open lane
    /// a gear showing its current skill at the top, the next at the upper left and the used one at the upper right; clear of
    /// 넘기기, 숨고르기 and the ACT track; queueing turns its own gear, 넘기기 every gear with the idlers the other way and a
    /// ratchet's bounce; what they show always follows the duel's lane orders; the coach fades a held lane's whole gear.</summary>
    public sealed class SkillGearDockPlayModeTests
    {
        [UnityTest]
        public IEnumerator Gears_ShowEachLanesCurrentNextAndUsedSkill_ClearOfTheButtonsAndTheActTrack()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                var duel = new LegacyQueuedDuel();
                fixture.Refresh(duel);
                for (int lane = 0; lane < 3; lane++)
                {
                    string key = "QWE"[lane].ToString();
                    var order = duel.GetLane(lane);
                    Image current = fixture.Get<Image>("Input/Keys/Current " + key + "/Skill Image");
                    Image next = fixture.Get<Image>("Input/Keys/Next " + key + "/Next Skill Image");
                    Image used = fixture.Get<Image>("Input/Keys/Used " + key + "/Used Skill Image");
                    AssertIcon(current, order[0]);
                    AssertIcon(next, order[1]);
                    AssertIcon(used, order[order.Count - 1]);
                    Rect top = ScreenRect(current.rectTransform), left = ScreenRect(next.rectTransform), right = ScreenRect(used.rectTransform);
                    Assert.That(left.center.x, Is.LessThan(top.center.x), key + ": the next skill at the upper left…");
                    Assert.That(right.center.x, Is.GreaterThan(top.center.x), key + ": …the used one at the upper right…");
                    Assert.That(top.center.y, Is.GreaterThan(Mathf.Max(left.center.y, right.center.y)), key + ": …the current one on top.");
                    Assert.That(top.width, Is.GreaterThan(left.width), key + ": the current skill is the largest…");
                    Assert.That(left.width, Is.GreaterThan(right.width), key + ": …the next readable, larger than the used one…");
                    Assert.That(next.color.a, Is.GreaterThan(used.color.a), key + ": …and brighter; the used one is dimmed…");
                    Image usedSocket = fixture.Get<Image>("Input/Keys/Used " + key + "/Used Socket");
                    Assert.That(usedSocket.color.a, Is.EqualTo(1f), key + ": …on a solid socket, darkened, so an idler under it never shows through.");
                    Assert.That(usedSocket.color.r, Is.LessThan(fixture.Get<Image>("Input/Keys/Next " + key + "/Next Socket").color.r));
                    Assert.That(fixture.Get<Image>("Input/Keys/Lane Gears/Gear " + key + "/Wheel").sprite, Is.Not.Null, key + " gear drawn.");
                    foreach (string part in new[] { "Current " + key, "Current " + key + "/Key", "Current " + key + "/Style Name",
                                 "Current " + key + "/Skill Name", "Current " + key + "/SkillCost", "Next " + key + "/Next Skill Image",
                                 "Used " + key + "/Used Skill Image", "Lane Gears/Gear " + key + "/Wheel" })
                        foreach (string other in new[] { "CycleButton", "BreathButton", "Act_BG", "Act_Value" })
                            Assert.That(ScreenRect(fixture.Get<RectTransform>("Input/Keys/" + part)).Overlaps(
                                ScreenRect(fixture.Get<RectTransform>("Input/Keys/" + other))), Is.False, part + " must stay clear of " + other);
                    Assert.That(ScreenRect(fixture.Get<RectTransform>("Input/Keys/Current " + key + "/SkillCost")).Overlaps(top), Is.False,
                        key + " cost must not cover the current skill.");
                    Assert.That(fixture.Get<Text>("Input/Keys/Current " + key + "/SkillCost").text, Is.EqualTo(order[0].Cost + " ACT"));
                    Assert.That(fixture.Get<Text>("Input/Keys/Current " + key + "/Key").text, Is.EqualTo(key), "The input key stays readable.");
                }
                Assert.That(fixture.Get<Image>("Input/Keys/Lane Gears/Idler 1").gameObject.activeSelf, Is.True, "An idler between Q and W…");
                Assert.That(fixture.Get<Image>("Input/Keys/Lane Gears/Idler 2").gameObject.activeSelf, Is.True, "…and between W and E.");
                float idlerX = fixture.Get<RectTransform>("Input/Keys/Lane Gears/Idler 1").position.x;
                Assert.That(idlerX, Is.InRange(fixture.Get<RectTransform>("Input/Keys/Current Q").position.x,
                    fixture.Get<RectTransform>("Input/Keys/Current W").position.x));
                Assert.That(fixture.Get<RectMask2D>("Input/Keys/Lane Gears"), Is.Not.Null, "Only the gears' upper parts show…");
                Assert.That(fixture.Get<RectMask2D>("Input/Keys/Next Q"), Is.Not.Null, "…and the next skill rises out of the dock's edge.");
                for (int lane = 0; lane < 3; lane++)
                    Assert.That(ScreenRect(fixture.Get<RectTransform>("Input/Keys/Current " + "QWE"[lane] + "/Skill Name")).Overlaps(
                        ScreenRect(fixture.Get<RectTransform>("Input/Keys/Current " + "QWE"[(lane + 1) % 3] + "/Skill Name"))), Is.False,
                        "Neighbouring names do not run together.");
            }
        }

        [UnityTest]
        public IEnumerator Queueing_TurnsItsOwnGear_AndShift_TurnsEveryGearWithTheIdlers_EndingOnTheRatchet()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LegacyCombatHud hud = fixture.Hud;
                var duel = new LegacyQueuedDuel(100, 50, 1000, 1000, LegacyInitialSkills.All,
                    new[] { LegacySkillDefinitions.Skill(1) }, new[] { 1 }, 3);
                fixture.Refresh(duel);
                float q = hud.LaneGearRotation(0), w = hud.LaneGearRotation(1), idler = hud.IdlerRotation(0);
                RectTransform icon = fixture.Get<RectTransform>("Input/Keys/Current Q/Skill Image");
                Assert.That(duel.TryQueueLane(0), Is.True);
                hud.PlayLaneTurn(0);
                Assert.That(hud.ActiveSteamPuffs, Is.EqualTo(3), "A normal choice gives its lane one visible valve breath.");
                fixture.Refresh(duel);
                Assert.That(hud.IsLaneTurning(0), Is.True, "Queueing turns its lane's gear…");
                Assert.That(hud.IsLaneTurning(1) || hud.IsLaneTurning(2) || hud.AreIdlersTurning, Is.False, "…and only that one.");
                AssertIcon(icon.GetComponent<Image>(), duel.GetLane(0)[0]);
                AssertIcon(fixture.Get<Image>("Input/Keys/Used Q/Used Skill Image"), duel.PlayerQueue[0]);
                Assert.That(icon.anchoredPosition.x, Is.LessThan(-1f), "The next skill comes up from the upper left.");
                fixture.Refresh(duel, hud.LaneTurnSeconds);
                Assert.That(hud.IsLaneTurning(0), Is.False);
                Assert.That(Turned(hud.LaneGearRotation(0), q), Is.EqualTo(-60f).Within(.01f), "One slot, clockwise.");
                Assert.That(Turned(hud.LaneGearRotation(1), w), Is.EqualTo(0f).Within(.01f));
                Assert.That(Vector2.Distance(icon.anchoredPosition, Vector2.zero), Is.LessThan(.01f), "It rests in the window.");

                fixture.Refresh(duel, 1f);
                Assert.That(hud.ActiveSteamPuffs, Is.Zero);

                Assert.That(duel.TryCycleLanes(), Is.True);
                hud.PlayLaneTurn(new[] { true, true, true });
                Assert.That(hud.ActiveSteamPuffs, Is.EqualTo(18),
                    "Shift dumps a broad pressure burst from every meshed lane, unlike a normal choice.");
                fixture.Refresh(duel);
                Assert.That(hud.IsLaneTurning(0) && hud.IsLaneTurning(1) && hud.IsLaneTurning(2) && hud.AreIdlersTurning, Is.True,
                    "넘기기 turns every gear together, and the idlers.");
                // An idler turns more than half a turn a slot (240° by default), so it is measured from where its slot ends,
                // as the lane gears are: a turn that large has no one reading within ±180°.
                float idlerTurn = LegacySkillGear.IdlerTurnPerSlot(LegacySkillGear.GearTeeth(LegacySkillGear.DefaultTeethPerSlot),
                    LegacySkillGear.IdlerTeeth(LegacySkillGear.GearTeeth(LegacySkillGear.DefaultTeethPerSlot), LegacySkillGear.DefaultRadius,
                        LegacySkillGear.DefaultIdlerRadius, LegacySkillGear.DefaultToothDepth));
                fixture.Refresh(duel, LegacySkillGear.DefaultShiftSeconds * LegacySkillGear.RatchetTurnShare);
                Assert.That(Turned(hud.LaneGearRotation(1), w), Is.LessThan(-60.5f), "The gears run past the slot at the ratchet's catch…");
                Assert.That(Turned(hud.IdlerRotation(0), idler + idlerTurn),
                    Is.EqualTo(idlerTurn / LegacySkillGear.SlotDegrees * LegacySkillGear.DefaultRatchetBounce).Within(.05f),
                    "…the idlers the other way, past theirs by the bounce in proportion…");
                fixture.Refresh(duel, LegacySkillGear.DefaultShiftSeconds);
                Assert.That(Turned(hud.LaneGearRotation(1), w), Is.EqualTo(-60f).Within(.01f), "…and spring back onto it.");
                Assert.That(Turned(hud.LaneGearRotation(0), q), Is.EqualTo(-120f).Within(.01f));
                Assert.That(Turned(hud.IdlerRotation(0), idler + idlerTurn), Is.EqualTo(0f).Within(.01f), "Idlers turn by whole teeth.");
                for (int lane = 0; lane < 3; lane++)
                    AssertIcon(fixture.Get<Image>("Input/Keys/Current " + "QWE"[lane] + "/Skill Image"), duel.GetLane(lane)[0]);
            }
        }

        [UnityTest]
        public IEnumerator TheGears_FollowTheLaneOrders_ThroughUnannouncedTurnsResetsAndNewDuels()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LegacyCombatHud hud = fixture.Hud;
                var duel = new LegacyQueuedDuel(100, 50, 1000, 1000, LegacyInitialSkills.All,
                    new[] { LegacySkillDefinitions.Skill(1) }, new[] { 1 }, 3);
                fixture.Refresh(duel);
                Assert.That(duel.TryQueueLane(1), Is.True, "Queued on the duel itself: no announcement…");
                fixture.Refresh(duel);
                Assert.That(hud.IsLaneTurning(1), Is.True, "…but the gear still turns to the order.");
                AssertLaneShown(fixture, duel, 1);
                hud.PlayLaneTurn(2);
                fixture.Refresh(duel);
                Assert.That(hud.IsLaneTurning(2), Is.False, "Announced, but the order did not turn: it settles on the order.");
                AssertLaneShown(fixture, duel, 2);

                hud.Reset();
                var next = new LegacyQueuedDuel();
                fixture.Refresh(next);
                for (int lane = 0; lane < 3; lane++)
                {
                    Assert.That(hud.IsLaneTurning(lane), Is.False, "A new duel just shows its lanes.");
                    AssertLaneShown(fixture, next, lane);
                }
                LegacyQueuedDuel mission = PrologueMissions.Get(1).CreateDuel(1);
                fixture.Refresh(mission);
                Assert.That(hud.IsLaneTurning(0), Is.False, "Another order without a turn: no turn.");
                AssertLaneShown(fixture, mission, 0);
                foreach (string closed in new[] { "Current W", "Next W", "Used W", "Lane Gears/Gear W", "Lane Gears/Spent W",
                             "Current E", "Next E", "Used E", "Lane Gears/Gear E", "Lane Gears/Spent E", "Lane Gears/Idler 1", "Lane Gears/Idler 2" })
                    Assert.That(fixture.Get<RectTransform>("Input/Keys/" + closed).gameObject.activeSelf, Is.False, closed + " stays alive but hidden.");
                Assert.That(fixture.Get<RectTransform>("Input/Keys/Current Q").anchoredPosition,
                    Is.EqualTo(new Vector2(0f, hud.SkillWindowY)), "The lone gear moves to the middle.");
            }
        }

        [UnityTest]
        public IEnumerator Layout_WaitsForADifferentOpenSet_AndTwoLanesMeshThroughOneIdler()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LegacyCombatHud hud = fixture.Hud;
                fixture.Refresh(new LegacyQueuedDuel());
                RectTransform window = fixture.Get<RectTransform>("Input/Keys/Current Q");
                Vector2 moved = window.anchoredPosition + new Vector2(7f, 5f);
                window.anchoredPosition = moved;
                fixture.Refresh(new LegacyQueuedDuel());
                Assert.That(window.anchoredPosition, Is.EqualTo(moved), "The same lanes are not laid out again.");

                var qAndE = new LegacyQueuedDuel(100, 50, 1000, 1000, LegacyInitialSkills.All,
                    new[] { LegacySkillDefinitions.Skill(1) }, new[] { 1 }, 3, features: CombatFeature.LaneQ | CombatFeature.LaneE);
                fixture.Refresh(qAndE);
                float pitch = hud.LanePitch;
                Assert.That(window.anchoredPosition, Is.EqualTo(new Vector2(-pitch * .5f, hud.SkillWindowY)), "Q packs to the left…");
                Assert.That(fixture.Get<RectTransform>("Input/Keys/Current E").anchoredPosition,
                    Is.EqualTo(new Vector2(pitch * .5f, hud.SkillWindowY)), "…E to the right, with no gap where W will go.");
                Assert.That(fixture.Get<RectTransform>("Input/Keys/Current W").gameObject.activeSelf, Is.False);
                RectTransform band = fixture.Get<RectTransform>("Input/Keys/Lane Gears");
                RectTransform idler = fixture.Get<RectTransform>("Input/Keys/Lane Gears/Idler 1");
                Assert.That(idler.gameObject.activeSelf, Is.True, "One idler between the two open gears…");
                Assert.That(fixture.Get<RectTransform>("Input/Keys/Lane Gears/Idler 2").gameObject.activeSelf, Is.False, "…and no other.");
                Vector2 idlerAt = band.anchoredPosition + idler.anchoredPosition;
                Vector2 gearAt = band.anchoredPosition + fixture.Get<RectTransform>("Input/Keys/Lane Gears/Gear Q").anchoredPosition;
                Assert.That(idlerAt.x, Is.EqualTo(0f).Within(.01f), "Midway between them.");
                Assert.That(idlerAt.y, Is.GreaterThan(LegacyCombatHud.GearCentreY), "Above the dock's edge, where it shows.");
                Assert.That(Vector2.Distance(idlerAt, gearAt), Is.EqualTo(LegacySkillGear.DefaultRadius + LegacySkillGear.DefaultIdlerRadius -
                    LegacySkillGear.DefaultToothDepth).Within(.01f), "Meshed with each gear.");
                Assert.That(fixture.Get<RectTransform>("Input/Keys/CycleButton").anchoredPosition.x, Is.EqualTo(-360f), "넘기기 keeps its place.");
                Assert.That(fixture.Get<RectTransform>("Input/Keys/BreathButton").anchoredPosition.x, Is.EqualTo(360f), "숨고르기 too.");
            }
        }

        [UnityTest]
        public IEnumerator AWideTunedPitch_ClosesUp_SoTheIdlerMeshesWhollyAboveTheDocksEdge()
        {
            yield return null;
            var parent = new GameObject("Skill Gear Pitch Test");
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            LegacyCombatHud hud = null;
            try
            {
                JsonUtility.FromJsonOverwrite("{\"skillGearPitch\":200}", settings);
                Assert.That(settings.SkillGearPitch, Is.EqualTo(200f));
                hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), null, null, null, settings);
                var qAndE = new LegacyQueuedDuel(100, 50, 1000, 1000, LegacyInitialSkills.All,
                    new[] { LegacySkillDefinitions.Skill(1) }, new[] { 1 }, 3, features: CombatFeature.LaneQ | CombatFeature.LaneE);
                hud.Refresh(qAndE, 10f, false, -1, null, null, null);
                float meshed = LegacySkillGear.MeshedPitch(settings.SkillGearRadius, settings.SkillGearIdlerRadius,
                    settings.SkillGearToothDepth);
                Assert.That(meshed, Is.LessThan(200f));
                Assert.That(hud.LanePitch, Is.EqualTo(meshed).Within(.01f),
                    "Two lanes have the room, but the idler between them would fall short: the pitch closes up…");
                Transform keys = hud.Root.transform.Find("Input/Keys");
                var band = (RectTransform)keys.Find("Lane Gears");
                var idler = (RectTransform)keys.Find("Lane Gears/Idler 1");
                Assert.That(idler.gameObject.activeSelf, Is.True);
                Assert.That(band.anchoredPosition.y + idler.anchoredPosition.y - settings.SkillGearIdlerRadius,
                    Is.GreaterThanOrEqualTo(LegacyCombatHud.GearCentreY - .01f), "…until it meshes with both, whole above the dock's edge.");
            }
            finally
            {
                hud?.Dispose();
                Object.Destroy(settings);
                Object.Destroy(parent);
            }
        }

        [UnityTest]
        public IEnumerator ACoachHeldLane_FadesItsWholeGear_AndTheWindowStaysItsButton()
        {
            yield return null;
            int queued = -1;
            var parent = new GameObject("Skill Gear Guide Test");
            LegacyCombatHud hud = null;
            try
            {
                hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), lane => queued = lane, null, null);
                PrologueMission mission = PrologueMissions.Get(3);
                MissionGuide guide = mission.CreateGuide();
                LegacyQueuedDuel duel = mission.CreateDuel(1);
                hud.SetMissionMode(true);
                hud.Refresh(duel, 10f, false, -1, null, null, null);
                hud.SetGuide(guide);
                Transform keys = hud.Root.transform.Find("Input/Keys");
                string[] parts = { "Current Q", "Used Q", "Next Q", "Lane Gears/Gear Q", "Lane Gears/Spent Q" };
                foreach (string part in parts)
                    Assert.That(keys.Find(part).GetComponent<CanvasGroup>().alpha, Is.EqualTo(.4f), part + " fades with its held lane.");
                Assert.That(keys.Find("Current Q").GetComponent<Button>().interactable, Is.False);
                guide.TryAdvance();
                hud.SetGuide(guide);
                hud.SetGuideFocus(0, false, false, false);
                foreach (string part in parts)
                    Assert.That(keys.Find(part).GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f), part + " comes back.");
                Button window = keys.Find("Current Q").GetComponent<Button>();
                Assert.That(window.interactable, Is.True);
                Assert.That(window.GetComponent<Outline>().enabled, Is.True, "The coach's gold outline is on the window.");
                window.onClick.Invoke();
                Assert.That(queued, Is.Zero, "The window is the lane's button.");
                Assert.That(keys.Find("Current Q/Lane Condition Feedback"), Is.Not.Null);
                Assert.That(keys.Find("Current Q/KeyHoldImage").GetComponent<Image>().type, Is.EqualTo(Image.Type.Filled));
                Transform hold = keys.Find("Current Q/KeyHoldImage"), track = keys.Find("Current Q/KeyHoldTrack");
                Assert.That(track, Is.Not.Null, "The hold bar crosses the brass hub, so it fills on a dark track…");
                Assert.That(track.GetSiblingIndex(), Is.LessThan(hold.GetSiblingIndex()), "…drawn under it…");
                Assert.That(((RectTransform)track).anchoredPosition, Is.EqualTo(((RectTransform)hold).anchoredPosition), "…right where it is…");
                Assert.That(((RectTransform)track).sizeDelta.x, Is.GreaterThan(((RectTransform)hold).sizeDelta.x), "…and round it.");
                hud.SetHoldProgress(0, .5f);
                Assert.That(keys.Find("Current Q/KeyHoldImage").GetComponent<Image>().fillAmount, Is.EqualTo(.5f));
            }
            finally { hud?.Dispose(); Object.Destroy(parent); }
        }

        [UnityTest]
        public IEnumerator GearSounds_AreShortMonoPlaceholders_PlayedOnTheHudsOwnVoice()
        {
            yield return null;
            AudioClip tick = Resources.Load<AudioClip>(CutsceneStep.SoundFolder + LegacyCombatHud.GearTickSound);
            AudioClip ratchet = Resources.Load<AudioClip>(CutsceneStep.SoundFolder + LegacyCombatHud.GearRatchetSound);
            Assert.That(tick, Is.Not.Null, "Resources/Sfx/" + LegacyCombatHud.GearTickSound);
            Assert.That(ratchet, Is.Not.Null, "Resources/Sfx/" + LegacyCombatHud.GearRatchetSound);
            Assert.That(tick.channels == 1 && ratchet.channels == 1, Is.True);
            Assert.That(tick.length, Is.InRange(.03f, .3f), "A short click.");
            Assert.That(ratchet.length, Is.InRange(.2f, 1f), "Quick clicks and a soft clunk.");
            using (var fixture = new HudFixture())
            {
                LegacyCombatHud hud = fixture.Hud;
                Assert.That(hud.GearTickClip, Is.SameAs(tick));
                Assert.That(hud.GearRatchetClip, Is.SameAs(ratchet));
                Assert.That(hud.GearAudio, Is.Not.Null);
                Assert.That(hud.GearAudio.gameObject, Is.SameAs(hud.Root), "The HUD's own voice.");
                Assert.That(hud.GearAudio.spatialBlend, Is.Zero);
                Assert.That(hud.GearAudio.playOnAwake, Is.False);
            }
        }

        private static void AssertLaneShown(HudFixture fixture, LegacyQueuedDuel duel, int lane)
        {
            string key = "QWE"[lane].ToString();
            var order = duel.GetLane(lane);
            AssertIcon(fixture.Get<Image>("Input/Keys/Current " + key + "/Skill Image"), order[0]);
            AssertIcon(fixture.Get<Image>("Input/Keys/Next " + key + "/Next Skill Image"), order[LegacySkillGear.SkillIndex(1, order.Count)]);
            AssertIcon(fixture.Get<Image>("Input/Keys/Used " + key + "/Used Skill Image"), order[LegacySkillGear.SkillIndex(-1, order.Count)]);
        }

        private static void AssertIcon(Image image, LegacySkill skill)
        {
            Sprite source = new LegacyDuelArt().GetSkillIcon(skill.IconId);
            Assert.That(image.sprite, Is.Not.Null, image.name);
            Assert.That(image.sprite.texture, Is.EqualTo(source.texture), image.name);
            Assert.That(image.sprite.name, Is.EqualTo(source.name), image.name + " shows " + skill.Name);
        }

        // How far a rotation is from another, -180 to 180 degrees (so only for turns under half a turn either way).
        private static float Turned(float rotation, float from) => LegacySkillGear.Wrap(rotation - from + 180f) - 180f;

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private sealed class HudFixture : IDisposable
        {
            private readonly GameObject parent;
            public LegacyCombatHud Hud { get; }

            public HudFixture()
            {
                parent = new GameObject("Skill Gear Dock Test");
                Hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), null, null, null);
                Canvas.ForceUpdateCanvases();
            }

            /// <summary>A planning frame of the duel, <paramref name="realDelta"/> real seconds on.</summary>
            public void Refresh(LegacyQueuedDuel duel, float realDelta = 0f)
            {
                Hud.Refresh(duel, 10f, false, -1, null, null, null, 0f, realDelta);
                Canvas.ForceUpdateCanvases();
            }

            public T Get<T>(string path) where T : Component
            {
                Transform node = Hud.Root.transform.Find(path);
                Assert.That(node, Is.Not.Null, "Missing dock node: " + path);
                T component = node.GetComponent<T>();
                Assert.That(component, Is.Not.Null, "Missing " + typeof(T).Name + " at " + path);
                return component;
            }

            public void Dispose()
            {
                Hud.Dispose();
                Object.Destroy(parent);
            }
        }
    }
}
