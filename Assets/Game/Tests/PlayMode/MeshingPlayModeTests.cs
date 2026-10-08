using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.Cutscene;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>맞물림 on screen (<see cref="LegacyCombatHud"/>'s marks and note, <see cref="DuelMeshCues"/>, Docs/Meshing.md):
    /// meshed cards carry their chain's bonus, live as skills are queued; a chain made or lengthened bites on its seam with
    /// sparks and a sound, beside the row and never in it; a meshed slot shows no step rings, takes no steps and flashes its
    /// gears at its first hit; the held explanation tells the bonus; every duel takes the tuned percent; the enemy shows
    /// nothing.</summary>
    public sealed class MeshingPlayModeTests
    {
        [UnityTest]
        public IEnumerator MeshedCards_CarryTheirChainsBonus_LiveAsSkillsAreQueued()
        {
            yield return null;
            using (var fixture = new MeshFixture())
            {
                LegacyCombatHud hud = fixture.Hud;
                LegacyQueuedDuel duel = FreeDuel();
                fixture.Refresh(duel);
                Assert.That(duel.TryQueueLane(0), Is.True);
                Assert.That(fixture.Cues.Queued(duel), Is.False, "One skill alone does not mesh…");
                Assert.That(duel.TryQueueLane(0), Is.True);
                Assert.That(fixture.Cues.Queued(duel), Is.False, "…nor two of one school.");
                fixture.Refresh(duel);
                Assert.That(hud.GetMeshMark(0) ?? hud.GetMeshMark(1), Is.Null);

                Assert.That(duel.TryQueueLane(1), Is.True);
                Assert.That(fixture.Cues.Queued(duel), Is.True, "Q Q W: the W meshes with the Q before it.");
                fixture.Refresh(duel);
                Assert.That(hud.GetMeshMarkLabel(0), Is.Null, "The first Q stands alone…");
                Assert.That(hud.GetMeshMarkLabel(1), Is.EqualTo("+20%"), "…the chain of two shows +20% on both.");
                Assert.That(hud.GetMeshMarkLabel(2), Is.EqualTo("+20%"));
                RectTransform mark = hud.GetMeshMark(2), card = hud.GetQueuedSkillAnchor(true, 2);
                Assert.That(mark.parent, Is.SameAs(card), "The mark rides its card…");
                Assert.That(ScreenRect(mark).center.y, Is.LessThan(ScreenRect(card).center.y), "…under its icon.");
                Assert.That(mark.Find("Mesh Gear").GetComponent<Image>().sprite, Is.SameAs(hud.MeshGearSprite), "A gear and the bonus.");

                Assert.That(duel.TryQueueLane(2), Is.True);
                Assert.That(fixture.Cues.Queued(duel), Is.True);
                fixture.Refresh(duel);
                for (int slot = 1; slot <= 3; slot++)
                    Assert.That(hud.GetMeshMarkLabel(slot), Is.EqualTo("+30%"), "Q Q W E: the chain of three, +30% each, slot " + slot);
                Assert.That(hud.GetMeshMark(1).localScale.x, Is.GreaterThan(1f), "A bonus that rose pops…");
                fixture.Refresh(duel, LegacyMeshCue.MarkPopSeconds);
                Assert.That(hud.GetMeshMark(1).localScale.x, Is.EqualTo(1f).Within(.001f), "…and settles.");

                Assert.That(duel.TryQueueBreath(), Is.True);
                Assert.That(fixture.Cues.Queued(duel), Is.False, "숨고르기 meshes with nothing.");
                fixture.Refresh(duel);
                Assert.That(hud.GetMeshMark(4), Is.Null);
                Assert.That(hud.GetMeshMarkLabel(3), Is.EqualTo("+30%"), "It leaves the chain before it as it was.");

                Transform enemyRow = hud.Root.transform.Find("Enemy Requests");
                foreach (Image image in enemyRow.GetComponentsInChildren<Image>())
                    Assert.That(image.name, Is.Not.EqualTo("Mesh Mark"), "The enemy's row never carries a mark.");
                foreach (Transform child in hud.PlayerQueueRow)
                    if (child.gameObject.activeSelf)
                        Assert.That(child.name, Is.EqualTo("Queued Skill"), "The row's children stay its cards.");

                duel.Commit();
                fixture.Refresh(duel);
                Assert.That(hud.GetMeshMarkLabel(1), Is.EqualTo("+30%"), "The committed queue keeps its marks while it resolves.");
                while (!duel.IsTurnResolved) duel.ResolveNextSlot();
                duel.BeginNextTurn();
                fixture.Refresh(duel);
                Assert.That(hud.GetMeshMark(1), Is.Null, "A new turn's queue starts unmarked.");
            }
        }

        [UnityTest]
        public IEnumerator AChainMadeOrLengthened_BitesOnItsSeam_WithSparksAndASound_BesideTheRow()
        {
            yield return null;
            AudioClip sound = Resources.Load<AudioClip>(CutsceneStep.SoundFolder + DuelMeshCues.MeshSound);
            Assert.That(sound, Is.Not.Null, "Resources/Sfx/" + DuelMeshCues.MeshSound);
            Assert.That(sound.channels, Is.EqualTo(1));
            Assert.That(sound.length, Is.InRange(.2f, .8f), "A short mechanical bite with sparks.");
            using (var fixture = new MeshFixture())
            {
                DuelMeshCues cues = fixture.Cues;
                Assert.That(cues.MeshClip, Is.SameAs(sound));
                Assert.That(cues.Audio.spatialBlend, Is.Zero);
                Assert.That(cues.Audio.playOnAwake, Is.False);
                RectTransform row = fixture.Hud.PlayerQueueRow;
                Assert.That(cues.QueueLayer.parent, Is.SameAs(row.parent), "Beside the row…");
                Assert.That(cues.QueueLayer.GetSiblingIndex(), Is.EqualTo(row.GetSiblingIndex() - 1), "…drawn behind its cards.");

                LegacyQueuedDuel duel = FreeDuel();
                fixture.Refresh(duel);
                Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(2), Is.True);
                Assert.That(cues.Queued(duel), Is.True);
                Assert.That(cues.LastSlot, Is.EqualTo(1));
                Assert.That(cues.LastChainLength, Is.EqualTo(2));
                fixture.Refresh(duel, .1f);
                Assert.That(cues.IsPlaying, Is.True);
                Image upper = cues.BurstGear(0);
                Transform burst = upper.transform.parent;
                Image lower = burst.Find("Lower Gear").GetComponent<Image>();
                RectTransform first = fixture.Hud.GetQueuedSkillAnchor(true, 0), second = fixture.Hud.GetQueuedSkillAnchor(true, 1);
                Assert.That(burst.position.x, Is.EqualTo((first.position.x + second.position.x) * .5f).Within(.5f), "On the seam…");
                Assert.That(burst.position.y, Is.EqualTo(first.position.y).Within(.5f), "…between the two icons.");
                Assert.That(upper.sprite, Is.SameAs(fixture.Hud.MeshGearSprite), "Brass gears…");
                Assert.That(upper.color.a, Is.GreaterThan(.5f));
                Assert.That(upper.rectTransform.anchoredPosition.y, Is.GreaterThan(0f));
                Assert.That(lower.rectTransform.anchoredPosition.y, Is.LessThan(0f), "…one over the other, teeth meeting at the seam…");
                Assert.That(Mathf.DeltaAngle(0f, upper.rectTransform.localEulerAngles.z), Is.LessThan(0f));
                Assert.That(Mathf.DeltaAngle(LegacyMeshCue.LowerPhase, lower.rectTransform.localEulerAngles.z), Is.GreaterThan(0f),
                    "…turning against each other.");
                DuelMeshSparks sparks = cues.BurstSparks(0);
                Assert.That(sparks.Count, Is.EqualTo(LegacyMeshCue.SparkCount(LegacyMeshCue.DefaultSparkCount, 2)), "Sparks fly…");
                Assert.That(sparks.Reach, Is.GreaterThan(0f));
                int pairSparks = cues.LastSparkCount;

                Assert.That(duel.TryQueueLane(1), Is.True);
                Assert.That(cues.Queued(duel), Is.True, "Q E W lengthens the chain…");
                Assert.That(cues.LastChainLength, Is.EqualTo(3));
                Assert.That(cues.LastSparkCount, Is.GreaterThan(pairSparks), "…a bigger burst…");
                Assert.That(cues.LastBurstScale, Is.GreaterThan(1f));
                Assert.That(cues.Audio.pitch, Is.GreaterThan(1f), "…a higher sound.");

                fixture.Refresh(duel, LegacyMeshCue.DefaultBurstSeconds);
                Assert.That(cues.IsPlaying, Is.False, "Gone once played.");
                Assert.That(burst.gameObject.activeSelf, Is.False);
                Assert.That(fixture.Hud.GetMeshMarkLabel(1), Is.EqualTo("+30%"), "The marks stay.");

                fixture.Settings("{\"meshBurstSeconds\":0}");
                duel.Commit();
                while (!duel.IsTurnResolved) duel.ResolveNextSlot();
                duel.BeginNextTurn();
                fixture.Refresh(duel);
                Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(1), Is.True);
                Assert.That(cues.Queued(duel), Is.True, "Still a chain…");
                fixture.Refresh(duel);
                Assert.That(cues.IsPlaying, Is.False, "…but the burst is tuned off.");
                Assert.That(cues.ActiveLinkCount, Is.EqualTo(1), "The lasting seam does not depend on burst duration.");
                Assert.That(cues.LinkGear(1).gameObject.activeInHierarchy, Is.True);
                Assert.That(fixture.Hud.GetMeshMarkLabel(1), Is.EqualTo("+20%"));
            }
        }

        [UnityTest]
        public IEnumerator EveryMeshedSeam_KeepsItsGearsAndSparks_AfterTheQueueBurstEnds()
        {
            yield return null;
            using (var fixture = new MeshFixture())
            {
                LegacyQueuedDuel duel = FreeDuel();
                int[] lanes = { 0, 2, 1, 0, 2, 1 };
                foreach (int lane in lanes)
                {
                    Assert.That(duel.TryQueueLane(lane), Is.True);
                    fixture.Cues.Queued(duel);
                    fixture.Refresh(duel);
                }

                DuelMeshCues cues = fixture.Cues;
                Assert.That(cues.ActiveLinkCount, Is.EqualTo(lanes.Length - 1),
                    "All five seams of one chain must coexist, beyond the four transient burst slots.");
                fixture.Refresh(duel, LegacyMeshCue.DefaultBurstSeconds + .1f);
                Assert.That(cues.IsPlaying, Is.False, "The reservation burst still has a finite lifetime.");
                Assert.That(cues.ActiveLinkCount, Is.EqualTo(lanes.Length - 1), "The actual links remain.");

                for (int seam = 1; seam < lanes.Length; seam++)
                {
                    Image gear = cues.LinkGear(seam);
                    Assert.That(gear, Is.Not.Null, "Seam " + seam + " has its own persistent gear.");
                    Assert.That(gear.gameObject.activeInHierarchy, Is.True, "Seam " + seam + " is visible after the burst.");
                    Assert.That(gear.color.a, Is.GreaterThan(0f), "Seam " + seam + " has visible gear ink.");
                    Assert.That(gear.sprite, Is.SameAs(fixture.Hud.MeshGearSprite));
                    RectTransform before = fixture.Hud.GetQueuedSkillAnchor(true, seam - 1);
                    RectTransform after = fixture.Hud.GetQueuedSkillAnchor(true, seam);
                    Assert.That(Vector3.Distance(gear.transform.parent.position, (before.position + after.position) * .5f),
                        Is.LessThan(1f), "The persistent gear stays on seam " + seam + ".");
                    Assert.That(cues.LinkSparks(seam), Is.Not.Null, "Seam " + seam + " has a spark emitter.");
                }

                // Sample after the original burst lifetime twice: a single residual particle cannot satisfy both windows.
                for (int window = 0; window < 2; window++)
                {
                    var seen = new bool[lanes.Length];
                    for (int sample = 0; sample < 10; sample++)
                    {
                        fixture.Refresh(duel, .1f);
                        for (int seam = 1; seam < lanes.Length; seam++)
                        {
                            DuelMeshSparks sparks = cues.LinkSparks(seam);
                            Mesh rendered = sparks != null ? sparks.canvasRenderer.GetMesh() : null;
                            if (sparks != null && sparks.gameObject.activeInHierarchy && sparks.Count > 0 &&
                                rendered != null && rendered.vertexCount > 0)
                                seen[seam] = true;
                        }
                    }
                    for (int seam = 1; seam < lanes.Length; seam++)
                        Assert.That(seen[seam], Is.True, "Seam " + seam + " keeps drawing sparks in later window " + window + ".");
                }
            }
        }

        [UnityTest]
        public IEnumerator PersistentLinks_LeaveOnlyWhenTheirCardsAreConsumedOrTheTurnEnds()
        {
            yield return null;
            using (var fixture = new MeshFixture())
            {
                LegacyQueuedDuel duel = FreeDuel();
                Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(2) && duel.TryQueueLane(1), Is.True);
                fixture.Refresh(duel, LegacyMeshCue.DefaultBurstSeconds + .1f);
                Assert.That(fixture.Cues.ActiveLinkCount, Is.EqualTo(2));

                duel.Commit();
                fixture.Refresh(duel);
                Assert.That(fixture.Cues.ActiveLinkCount, Is.EqualTo(2), "Commit does not erase unspent links.");

                duel.ResolveNextSlot();
                fixture.Refresh(duel);
                Assert.That(fixture.Cues.ActiveLinkCount, Is.EqualTo(1), "Only the seam touching the spent card disappears.");
                Assert.That(fixture.Cues.LinkGear(1), Is.Null);
                Assert.That(fixture.Cues.LinkSparks(1), Is.Null);
                Assert.That(fixture.Cues.LinkGear(2), Is.Not.Null);

                duel.ResolveNextSlot();
                fixture.Refresh(duel);
                Assert.That(fixture.Cues.ActiveLinkCount, Is.Zero);
                Assert.That(fixture.Cues.LinkGear(2), Is.Null);

                while (!duel.IsTurnResolved) duel.ResolveNextSlot();
                duel.BeginNextTurn();
                fixture.Refresh(duel);
                Assert.That(fixture.Cues.ActiveLinkCount, Is.Zero, "A new planning turn cannot show old links.");
                Assert.That(fixture.Cues.LinkSparks(2), Is.Null);
            }
        }

        [UnityTest]
        public IEnumerator MeshedCardBonus_RemainsReadableAtDefaultAndHighTunedValues()
        {
            yield return null;
            foreach (int percent in new[] { LegacyMeshing.DefaultPercent, 100 })
            {
                using (var fixture = new MeshFixture())
                {
                    LegacyQueuedDuel duel = FreeDuel(meshPercent: percent);
                    int[] lanes = { 0, 2, 1, 0, 2, 1 };
                    foreach (int lane in lanes) Assert.That(duel.TryQueueLane(lane), Is.True);
                    fixture.Refresh(duel, LegacyMeshCue.MarkPopSeconds);
                    string expected = LegacyMeshCue.BonusLabel(lanes.Length * percent);
                    for (int slot = 0; slot < lanes.Length; slot++)
                    {
                        RectTransform mark = fixture.Hud.GetMeshMark(slot);
                        Assert.That(mark, Is.Not.Null, "Every chain card retains its bonus badge.");
                        Text label = mark.Find("Label").GetComponent<Text>();
                        Image inset = mark.Find("Mesh Mark Inset").GetComponent<Image>();
                        RectTransform gear = (RectTransform)mark.Find("Mesh Gear");
                        Assert.That(label.text, Is.EqualTo(expected));
                        Assert.That(label.cachedTextGenerator.lineCount, Is.EqualTo(1), "Bonus stays on one line.");
                        Assert.That(label.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(expected.Length),
                            "The entire number and percent sign are drawn.");
                        Assert.That(label.cachedTextGenerator.fontSizeUsedForBestFit,
                            Is.GreaterThanOrEqualTo(Mathf.FloorToInt(18f * label.pixelsPerUnit)),
                            "The number remains large enough to read.");
                        Assert.That(ContrastRatio(label.color, inset.color), Is.GreaterThanOrEqualTo(4.5f),
                            "The number contrasts with its background.");
                        Assert.That(ScreenRect(label.rectTransform).xMin, Is.GreaterThanOrEqualTo(ScreenRect(gear).xMax),
                            "The number does not cover the gear.");
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator AMeshedSlot_ShowsNoRings_TakesNoSteps_AndFlashesItsGearsAtTheFirstHit()
        {
            yield return null;
            using (var scope = new MeshScope())
            {
                DuelPrototypeController controller = scope.Controller;
                Assert.That(controller.QueueLane(0) && controller.QueueLane(0), Is.True);
                Assert.That(controller.MeshCues.IsPlaying, Is.False);
                Assert.That(controller.QueueLane(2), Is.True);
                Assert.That(controller.MeshCues.IsPlaying, Is.True, "Q Q E: the gears bite as the E is queued.");
                Assert.That(controller.Hud.GetMeshMarkLabel(2), Is.EqualTo("+20%"));
                controller.CommitTurn();

                scope.Until(() => controller.Session.CurrentSlot != null);
                Assert.That(controller.Session.CurrentSlot.PlayerMesh.IsMeshed, Is.False);
                Assert.That(controller.IsSkillWindup, Is.True, "The lone Q keeps its windup…");
                scope.Advance(0f);
                Assert.That(controller.StepHud.DodgeRing.gameObject.activeInHierarchy, Is.True, "…and its rings.");
                Assert.That(controller.MeshCues.IsFlashing, Is.False);

                scope.Until(() => controller.Session.CurrentSlot != null && controller.Session.CurrentSlot.SlotIndex == 1);
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                Assert.That(slot.PlayerMesh.IsMeshed, Is.True);
                Assert.That(slot.PlayerMesh.BonusPercent, Is.EqualTo(20));
                Assert.That(controller.IsSkillWindup, Is.False, "A meshed slot takes no steps, so it strikes without their windup.");
                scope.Advance(0f);
                Assert.That(controller.StepHud.DodgeRing.gameObject.activeInHierarchy, Is.False, "No ring for it…");
                Assert.That(controller.StepHud.PressureRing.gameObject.activeInHierarchy, Is.False);
                Assert.That(controller.StepHud.Root.transform.Find("ACT Recovery Notice").GetComponent<Text>().text,
                    Does.StartWith(DuelStepHud.MeshedHintFor(controller.Session.Features)), "…and the hint says why.");
                int attempts = controller.Session.StepAttemptsThisTurn;
                Assert.That(controller.TryStep(LegacyStepAction.Dodge, out _), Is.False, "A and D are ignored…");
                Assert.That(controller.TryStep(LegacyStepAction.Pressure, out _), Is.False);
                Assert.That(controller.Session.StepAttemptsThisTurn, Is.EqualTo(attempts), "…not even counted.");
                Assert.That(controller.Session.StepMissedThisTurn, Is.False);

                if (slot.HitsResolved == 0)
                {
                    Assert.That(controller.MeshCues.IsFlashing, Is.False, "The flash waits for the first hit.");
                    scope.Until(() => slot.HitsResolved > 0);
                }
                Assert.That(controller.MeshCues.IsFlashing, Is.True, "The first hit flashes the gears…");
                RectTransform flash = (RectTransform)controller.MeshCues.FlashGear.transform.parent;
                Assert.That(Vector2.Distance(flash.anchoredPosition, ExpectedFlash(controller)), Is.LessThan(.5f),
                    "…at the player's body, where the rings would have been.");
                Assert.That(controller.MeshCues.FlashSparks.Count, Is.GreaterThan(0));
                scope.Until(() => !controller.MeshCues.IsFlashing);
                Assert.That(flash.gameObject.activeSelf, Is.False, "Brief.");
            }
        }

        [Test]
        public void TheMeshedHint_NamesOnlyTheStepsTheDuelHasOpened()
        {
            Assert.That(DuelStepHud.MeshedHintFor(CombatFeature.All), Is.EqualTo(DuelStepHud.MeshedHint));
            Assert.That(DuelStepHud.MeshedHintFor(CombatFeature.All & ~CombatFeature.Pressure),
                Is.EqualTo("맞물린 기술 · 회피 없음"), "Missions 7 and 8 have not taught 압박 yet.");
            Assert.That(DuelStepHud.MeshedHintFor(CombatFeature.All & ~CombatFeature.Dodge),
                Is.EqualTo("맞물린 기술 · 압박 없음"));
            Assert.That(DuelStepHud.MeshedHintFor(CombatFeature.All & ~(CombatFeature.Dodge | CombatFeature.Pressure)),
                Is.EqualTo("맞물린 기술"));
        }

        [UnityTest]
        public IEnumerator EveryDuel_TakesTheTunedPercent_AndZeroMeshesNothing()
        {
            yield return null;
            DuelPresentationSettings asset = Resources.Load<DuelPresentationSettings>("DuelPresentationSettings");
            Assert.That(asset, Is.Not.Null, "The game loads its serialized settings from Resources.");
            Assert.That(asset.MeshPercent, Is.EqualTo(10), "The shipped setting uses 10% per chained skill.");
            using (var scope = new MeshScope())
            {
                DuelPrototypeController controller = scope.Controller;
                scope.Settings("{\"meshPercent\":30}");
                controller.RestartMatch();
                Assert.That(controller.MeshPercent, Is.EqualTo(30));
                Assert.That(controller.Session.MeshPercent, Is.EqualTo(30), "The stage's duel takes the tuned percent.");
                Assert.That(controller.QueueLane(0) && controller.QueueLane(2), Is.True);
                Assert.That(controller.Hud.GetMeshMarkLabel(1), Is.EqualTo("+60%"), "A chain of two at 30% each.");

                scope.Settings("{\"meshPercent\":0}");
                controller.RestartMatch();
                Assert.That(controller.Session.MeshPercent, Is.Zero);
                Assert.That(controller.QueueLane(0) && controller.QueueLane(2), Is.True);
                Assert.That(controller.MeshCues.IsPlaying, Is.False, "Off: nothing meshes…");
                Assert.That(controller.MeshCues.ActiveLinkCount, Is.Zero, "Off: no lasting seam either.");
                Assert.That(controller.Hud.GetMeshMark(0) ?? controller.Hud.GetMeshMark(1), Is.Null, "…no card is marked…");
                controller.Hud.ShowExplanation(controller.Session.GetLane(1)[0], false);
                Assert.That(controller.Hud.MeshNote, Is.Null, "…and the explanation says nothing of it.");
                controller.Hud.HideExplanation();
            }
        }

        [UnityTest]
        public IEnumerator TheHeldExplanation_TellsWhatQueueingItNowEarns_WhileTheDuelCanMesh()
        {
            yield return null;
            using (var fixture = new MeshFixture())
            {
                LegacyCombatHud hud = fixture.Hud;
                RectTransform panel = (RectTransform)hud.Root.transform.Find("Skill Explain");
                LegacyQueuedDuel oneLane = FreeDuel(CombatFeature.LaneQ | CombatFeature.Breath);
                fixture.Refresh(oneLane);
                hud.ShowExplanation(oneLane.GetLane(0)[0], false);
                Assert.That(hud.MeshNote, Is.Null, "With one lane nothing can mesh, so nothing is said.");
                float plain = panel.rect.height;

                hud.Reset();
                LegacyQueuedDuel duel = FreeDuel();
                fixture.Refresh(duel);
                hud.ShowExplanation(duel.GetLane(2)[0], false);
                Assert.That(hud.MeshNote, Is.EqualTo(LegacyCombatHud.MeshNoteHint), "How to earn it, before anything is queued…");
                Assert.That(panel.rect.height, Is.GreaterThan(plain), "…on a row of its own…");
                RectTransform note = (RectTransform)panel.Find("Mesh Note"), hint = (RectTransform)panel.Find("Explanation Hint");
                Assert.That(ScreenRect(note).Overlaps(ScreenRect(hint)), Is.False, "…clear of the hint…");
                Assert.That(ScreenRect(note).Overlaps(ScreenRect((RectTransform)panel.Find("Skill Summary"))), Is.False, "…and the text.");

                Assert.That(duel.TryQueueLane(0), Is.True);
                fixture.Refresh(duel);
                hud.ShowExplanation(duel.GetLane(2)[0], false);
                Assert.That(hud.MeshNote, Is.EqualTo(LegacyCombatHud.MeshNoteText(20)), "After a Q, the E would mesh: +20%…");
                hud.ShowExplanation(duel.GetLane(0)[0], false);
                Assert.That(hud.MeshNote, Is.EqualTo(LegacyCombatHud.MeshNoteHint), "…another Q would not.");
                Assert.That(duel.TryQueueLane(2), Is.True);
                fixture.Refresh(duel);
                hud.ShowExplanation(duel.GetLane(1)[0], false);
                Assert.That(hud.MeshNote, Is.EqualTo(LegacyCombatHud.MeshNoteText(30)), "After Q E, a W lengthens it: +30%.");
                float noted = panel.rect.height;
                hud.ShowExplanation(duel.GetLane(0)[0], false);
                Assert.That(panel.rect.height, Is.EqualTo(noted).Within(.01f), "The card keeps its size as the note changes.");

                hud.ShowExplanation(duel.EnemyQueue.Count > 0 ? duel.EnemyQueue[0] : LegacySkillDefinitions.Skill(1), true);
                Assert.That(hud.Root.transform.Find("Enemy Skill Explain/Mesh Note"), Is.Null, "The enemy's card says nothing of it.");
            }
        }

        // The flash's place: the player's body projected into its layer, kept clear of the screen's edges.
        private static Vector2 ExpectedFlash(DuelPrototypeController controller)
        {
            RectTransform layer = controller.MeshCues.FlashLayer;
            Camera camera = controller.ArenaView.ArenaCamera;
            Vector3 screen = camera.WorldToScreenPoint(
                controller.ArenaView.PlayerRenderer.transform.TransformPoint(DuelStepHud.ActorBodyLocalOffset));
            Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, null, out Vector2 position), Is.True);
            Rect bounds = layer.rect;
            float inset = 120f;
            return new Vector2(Mathf.Clamp(position.x, bounds.xMin + inset, bounds.xMax - inset),
                Mathf.Clamp(position.y, bounds.yMin + inset, bounds.yMax - inset));
        }

        // Two free skills a lane, the table's own (their icons show), against an enemy that does one thing a turn.
        private static LegacyQueuedDuel FreeDuel(CombatFeature features = CombatFeature.All, int meshPercent = LegacyMeshing.DefaultPercent)
        {
            var player = new LegacySkill[6];
            for (int index = 0; index < player.Length; index++) player[index] = Free(LegacySkillDefinitions.Skill(index + 1));
            return new LegacyQueuedDuel(1000, 50, 1000, 1000, player, new[] { LegacySkillDefinitions.Skill(1) }, new[] { 1 }, 3,
                features: features, meshPercent: meshPercent);
        }

        private static LegacySkill Free(LegacySkill skill)
            => new LegacySkill(skill.Id, skill.Name, 0, skill.MinPower, skill.MaxPower, skill.Kind, skill.Property,
                skill.AttackCount, skill.LaneIndex, skill.Description, skill.AnimationName, skill.IconId);

        private static float ContrastRatio(Color first, Color second)
        {
            float a = Luminance(first), b = Luminance(second);
            return (Mathf.Max(a, b) + .05f) / (Mathf.Min(a, b) + .05f);
        }

        private static float Luminance(Color color)
            => .2126f * LinearChannel(color.r) + .7152f * LinearChannel(color.g) + .0722f * LinearChannel(color.b);

        private static float LinearChannel(float value)
            => value <= .04045f ? value / 12.92f : Mathf.Pow((value + .055f) / 1.055f, 2.4f);

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        /// <summary>A duel HUD and 맞물림's cues on their own, tuned by a settings instance of their own.</summary>
        private sealed class MeshFixture : IDisposable
        {
            private readonly GameObject parent;
            private readonly DuelPresentationSettings settings;
            public LegacyCombatHud Hud { get; }
            public DuelMeshCues Cues { get; }

            public MeshFixture()
            {
                parent = new GameObject("Meshing Test");
                settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                Hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), null, null, null, settings);
                Cues = new DuelMeshCues(Hud, () => settings);
                Canvas.ForceUpdateCanvases();
            }

            public void Settings(string json) => JsonUtility.FromJsonOverwrite(json, settings);

            /// <summary>A planning (or resolving) frame of the duel, <paramref name="realDelta"/> real seconds on: the HUD lays
            /// out its rows, then the cues follow them.</summary>
            public void Refresh(LegacyQueuedDuel duel, float realDelta = 0f)
            {
                int activeSlot = duel.Phase == LegacyDuelPhase.Planning ? -1 : duel.LastResolvedSlot + 1;
                Hud.Refresh(duel, 10f, duel.Phase != LegacyDuelPhase.Planning, activeSlot, null, null, null, 0f, realDelta);
                Canvas.ForceUpdateCanvases();
                Cues.SyncLinks(duel);
                Cues.Tick(realDelta);
                Canvas.ForceUpdateCanvases();
            }

            public void Dispose()
            {
                Cues.Dispose();
                Hud.Dispose();
                Object.Destroy(parent);
                Object.Destroy(settings);
            }
        }

        /// <summary>The scene's controller on a duel of weak, free skills in all three lanes (Q, W, E) against one enemy
        /// attack a turn, under a settings clone; its frames advanced by hand.</summary>
        private sealed class MeshScope : IDisposable
        {
            private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            private readonly DuelPresentationSettings originalSettings, originalArenaSettings, settingsClone;
            private readonly FieldInfo arenaSettingsField;
            private readonly LegacyQueuedDuel originalSession;
            private readonly bool originalEnabled;
            private readonly Action<float, UnityEngine.InputSystem.Keyboard> advance;
            public DuelPrototypeController Controller { get; }

            public MeshScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                originalSession = Controller.Session;
                arenaSettingsField = typeof(LegacyArenaView).GetField("settings", PrivateInstance);
                Assert.That(arenaSettingsField, Is.Not.Null);
                originalArenaSettings = (DuelPresentationSettings)arenaSettingsField.GetValue(Controller.ArenaView);
                settingsClone = Object.Instantiate(originalSettings);
                JsonUtility.FromJsonOverwrite("{\"animationPlaybackSpeed\":1,\"attackInterval\":0.1,\"skillInterval\":0.28," +
                    "\"hitStopDuration\":0,\"stepAnticipationDuration\":0.24,\"stepTimingWindow\":0.1,\"meshPercent\":10," +
                    "\"meshBurstSeconds\":0.6,\"meshFlashSeconds\":0.45}", settingsClone);
                SetField("presentationSettings", settingsClone);
                arenaSettingsField.SetValue(Controller.ArenaView, settingsClone);
                var playerSkills = new[] { Weak(101, 0), Weak(103, 1), Weak(105, 2) };
                SetField("session", new LegacyQueuedDuel(1000, 1000, 1000, 1000, playerSkills, new[] { Weak(201, 0) }, new[] { 1 }, 1));
                MethodInfo method = typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance);
                Assert.That(method, Is.Not.Null);
                advance = (Action<float, UnityEngine.InputSystem.Keyboard>)Delegate.CreateDelegate(
                    typeof(Action<float, UnityEngine.InputSystem.Keyboard>), Controller, method);
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance).Invoke(Controller,
                    new object[] { null, null });
            }

            public void Advance(float realDelta) => advance(realDelta, null);
            public void Settings(string json) => JsonUtility.FromJsonOverwrite(json, settingsClone);

            public void Until(Func<bool> condition)
            {
                int steps = 0;
                while (!condition() && steps++ < 10000) Advance(.001f);
                Assert.That(condition(), Is.True, "The controller must progress within ten simulated seconds.");
            }

            private static LegacySkill Weak(int id, int lane)
                => new LegacySkill(id, "Mesh Test", 0, 1, 1, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, lane,
                    string.Empty, "Slash", 1);

            private void SetField(string name, object value)
            {
                FieldInfo field = typeof(DuelPrototypeController).GetField(name, PrivateInstance);
                Assert.That(field, Is.Not.Null);
                field.SetValue(Controller, value);
            }

            public void Dispose()
            {
                SetField("presentationSettings", originalSettings);
                arenaSettingsField.SetValue(Controller.ArenaView, originalArenaSettings);
                SetField("session", originalSession);
                Controller.RestartMatch();
                Controller.enabled = originalEnabled;
                Object.Destroy(settingsClone);
            }
        }
    }
}
