using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using TurnLimbo.Runtime.Save;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class LocalVersusPlayModeTests
    {
        [UnityTest]
        public IEnumerator TitleEntry_PublicAlternatingReservations_ResolveAndReturnWithoutChangingSave()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                string saveBefore = scope.CreateSaveAndReadText();
                controller.ShowTitle();
                Assert.That(controller.TitleHud.LocalVersusButton.interactable, Is.True);

                controller.TitleHud.LocalVersusButton.onClick.Invoke();
                Assert.That(controller.IsInLocalVersus, Is.True);
                Assert.That(controller.IsInTitle, Is.False);
                Assert.That(controller.LocalVersus.IsPreparing, Is.True);
                Assert.That(controller.LocalVersus.PreparationHud.IsVisible, Is.True);
                Assert.That(controller.AutoSaveEnabled, Is.False);
                Assert.That(controller.LocalVersus.ConfirmPreparation(), Is.True);
                Assert.That(controller.LocalVersus.PreparingPlayer, Is.EqualTo(1));
                Assert.That(controller.LocalVersus.ConfirmPreparation(), Is.True);
                Assert.That(controller.LocalVersus.Hud.IsVisible, Is.True);

                LocalVersusMatch match = controller.LocalVersus.Match;
                Assert.That(match.RoundNumber, Is.EqualTo(1));
                Assert.That(match.OpeningPlayer, Is.Zero);
                Assert.That(match.CurrentPlanner, Is.Zero);
                Assert.That(match.Left.Queue, Is.Empty);
                Assert.That(match.Right.Queue, Is.Empty);

                // Both players use the same visible command desk. Every reservation
                // transfers control, and both queues stay public on screen.
                Button lane = ButtonNamed(controller.LocalVersus.Hud.Root, "Versus Lane Q");
                Assert.That(lane.interactable, Is.True);
                lane.onClick.Invoke();
                Assert.That(match.Left.Queue.Count, Is.EqualTo(1));
                Assert.That(match.Right.Queue, Is.Empty);
                Assert.That(match.CurrentPlanner, Is.EqualTo(1));
                Assert.That(LabelNamed(controller.LocalVersus.Hud.Root, "Versus Active Player").text,
                    Does.Contain("2P"));

                lane.onClick.Invoke();
                Assert.That(match.Left.Queue.Count, Is.EqualTo(1));
                Assert.That(match.Right.Queue.Count, Is.EqualTo(1));
                Assert.That(match.CurrentPlanner, Is.Zero);
                Assert.That(LabelNamed(controller.LocalVersus.Hud.Root, "Versus Active Player").text,
                    Does.Contain("1P"));

                Button pass = ButtonNamed(controller.LocalVersus.Hud.Root, "Versus Pass");
                pass.onClick.Invoke();
                Assert.That(match.ConsecutivePasses, Is.EqualTo(1));
                Assert.That(match.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
                pass.onClick.Invoke();
                Assert.That(match.Phase, Is.EqualTo(LegacyDuelPhase.Resolving));

                int frames = 0;
                while (!match.IsFinished &&
                       !(match.RoundNumber == 2 && match.Phase == LegacyDuelPhase.Planning) &&
                       frames++ < 2000)
                    controller.LocalVersus.Tick(.05f, null);
                Assert.That(frames, Is.LessThan(2000), "A committed turn must finish in finite presentation time.");
                Assert.That(match.IsFinished || match.RoundNumber == 2, Is.True);
                if (!match.IsFinished)
                {
                    Assert.That(match.OpeningPlayer, Is.EqualTo(1), "The first reservation alternates next turn.");
                    Assert.That(match.CurrentPlanner, Is.EqualTo(1));
                }
                Assert.That(File.ReadAllText(scope.Store.Path), Is.EqualTo(saveBefore),
                    "A local match must not award, advance, or overwrite campaign progress.");

                ButtonNamed(controller.LocalVersus.Hud.Root, "Versus Exit").onClick.Invoke();
                Assert.That(controller.LocalVersus.Hud.IsExitConfirming, Is.True);
                ButtonNamed(controller.LocalVersus.Hud.Root, "Versus Confirm Exit").onClick.Invoke();
                Assert.That(controller.IsInTitle, Is.True);
                Assert.That(controller.IsInLocalVersus, Is.False);
                Assert.That(File.ReadAllText(scope.Store.Path), Is.EqualTo(saveBefore));
            }
        }

        [UnityTest]
        public IEnumerator VersusUsesMirroredElisaForBothFightersAcrossAttackAndReactions()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                Assert.That(controller.StartLocalVersus(), Is.True);
                Assert.That(controller.LocalVersus.ConfirmPreparation(), Is.True);
                Assert.That(controller.LocalVersus.ConfirmPreparation(), Is.True);
                LegacyArenaView arena = controller.ArenaView;
                Assert.That(arena.EnemyAppearance, Is.EqualTo(EnemyAppearance.Elisa));
                Assert.That(arena.PlayerRenderer.sprite.name, Does.StartWith("idle-frame-"));
                Assert.That(arena.EnemyRenderer.sprite, Is.SameAs(arena.PlayerRenderer.sprite));
                Assert.That(arena.PlayerRenderer.flipX, Is.False);
                Assert.That(arena.EnemyRenderer.flipX, Is.True);

                var attack = new LegacySkill(991, "검증", 1, 1, 1,
                    LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, string.Empty);
                arena.BeginSlot(attack, attack);
                Assert.That(arena.PlayerRenderer.sprite.name, Does.StartWith("slash"));
                Assert.That(arena.EnemyRenderer.sprite.name, Does.StartWith("slash"),
                    "The opposing fighter must keep Elisa's attack frames beyond the first idle frame.");

                arena.PresentHit(true, 0, 1, true, false, 1);
                Assert.That(arena.EnemyRenderer.sprite.name, Does.StartWith("poses-block"));
                arena.PresentHit(true, 2, 0, false, false, 1);
                Assert.That(arena.EnemyRenderer.sprite.name, Does.StartWith("poses-hurt"));
                Assert.That(arena.EnemyRenderer.flipX, Is.True);

                arena.SetEnemyAppearance(EnemyAppearance.CadetA);
                arena.Reset();
                Assert.That(arena.EnemyRenderer.sprite.name, Does.StartWith("cadet-a-idle-frame-"));
                Assert.That(arena.EnemyRenderer.flipX, Is.False,
                    "Switching away from versus must restore the normal opposing facing.");
            }
        }

        [UnityTest]
        public IEnumerator FirstPlayersRewardSkillSelectionReachesMatch_SecondPlayerKeepsDefaultLoadout()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                Assert.That(controller.StartLocalVersus(), Is.True);
                LocalVersusController versus = controller.LocalVersus;
                Assert.That(versus.PreparingPlayer, Is.Zero);
                Assert.That(versus.PreparationHud.IsVisible, Is.True);

                LegacySkill reward = LegacySkillDefinitions.AcquisitionSkills[0];
                int lane = reward.LaneIndex;
                string laneLetter = new[] { "Q", "W", "E" }[lane];
                int replacedId = versus.GetLocalLoadout(0).GetSkillId(lane, 0);
                int[] secondPlayerDefault = versus.GetLocalLoadout(1).ExportIds();

                ButtonNamed(versus.PreparationHud.Root, "Versus Available Skill " + reward.Id).onClick.Invoke();
                ButtonNamed(versus.PreparationHud.Root, "Versus Slot " + laneLetter + " 1").onClick.Invoke();
                Assert.That(versus.GetLocalLoadout(0).GetSkillId(lane, 0), Is.EqualTo(reward.Id));
                Assert.That(versus.GetLocalLoadout(1).GetSkillId(lane, 0), Is.EqualTo(replacedId));

                versus.PreparationHud.ConfirmButton.onClick.Invoke();
                Assert.That(versus.PreparingPlayer, Is.EqualTo(1));
                versus.PreparationHud.ConfirmButton.onClick.Invoke();
                Assert.That(versus.IsPreparing, Is.False);
                Assert.That(versus.Match.Left.GetLane(lane)[0].Id, Is.EqualTo(reward.Id));
                for (int sideLane = 0; sideLane < VersusLoadout.LaneCount; sideLane++)
                    for (int slot = 0; slot < VersusLoadout.SlotsPerLane; slot++)
                        Assert.That(versus.Match.Right.GetLane(sideLane)[slot].Id,
                            Is.EqualTo(secondPlayerDefault[sideLane * VersusLoadout.SlotsPerLane + slot]),
                            "2P should retain their own default lineup.");
            }
        }

        [UnityTest]
        public IEnumerator TwentyEmptyRounds_DrawAndRematchSwitchesOpeningPlayer()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                string saveBefore = scope.CreateSaveAndReadText();
                controller.ShowTitle();
                Assert.That(controller.StartLocalVersus(), Is.True);
                Assert.That(controller.LocalVersus.ConfirmPreparation(), Is.True);
                Assert.That(controller.LocalVersus.ConfirmPreparation(), Is.True);

                LocalVersusController versus = controller.LocalVersus;
                LocalVersusMatch first = versus.Match;
                Assert.That(first.RoundLimit, Is.EqualTo(20));
                for (int round = 1; round <= first.RoundLimit; round++)
                {
                    Assert.That(first.RoundNumber, Is.EqualTo(round));
                    Assert.That(first.Phase, Is.EqualTo(LegacyDuelPhase.Planning));
                    Assert.That(first.OpeningPlayer, Is.EqualTo((round - 1) % 2));
                    Assert.That(first.CurrentPlanner, Is.EqualTo(first.OpeningPlayer));
                    Assert.That(versus.TryPass(), Is.True);
                    Assert.That(versus.TryPass(), Is.True);
                    if (round == first.RoundLimit) break;

                    int frames = 0;
                    while (first.RoundNumber == round && frames++ < 400)
                        versus.Tick(.05f, null);
                    Assert.That(frames, Is.LessThan(400), "An empty turn should hand control to the next player.");
                    Assert.That(first.RoundNumber, Is.EqualTo(round + 1));
                }

                Assert.That(first.IsFinished, Is.True);
                Assert.That(first.Outcome, Is.EqualTo(LocalVersusOutcome.Draw));
                Assert.That(versus.Hud.Root.transform.Find("Versus Result Overlay").gameObject.activeSelf, Is.True);
                Assert.That(LabelNamed(versus.Hud.Root, "Versus Result Heading").text, Is.EqualTo("무승부"));

                ButtonNamed(versus.Hud.Root, "Versus Rematch").onClick.Invoke();
                Assert.That(versus.IsActive, Is.True);
                Assert.That(versus.Match, Is.Not.SameAs(first));
                Assert.That(versus.Match.RoundNumber, Is.EqualTo(1));
                Assert.That(versus.Match.Outcome, Is.EqualTo(LocalVersusOutcome.InProgress));
                Assert.That(versus.Match.OpeningPlayer, Is.EqualTo(1));
                Assert.That(versus.Match.CurrentPlanner, Is.EqualTo(1));
                Assert.That(versus.Match.Left.Queue, Is.Empty);
                Assert.That(versus.Match.Right.Queue, Is.Empty);
                Assert.That(versus.Hud.Root.transform.Find("Versus Result Overlay").gameObject.activeSelf, Is.False);
                Assert.That(File.ReadAllText(scope.Store.Path), Is.EqualTo(saveBefore));
            }
        }
        private static Button ButtonNamed(GameObject root, string name)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.name == name) return button;
            Assert.Fail("Missing local versus button: " + name);
            return null;
        }

        private static Text LabelNamed(GameObject root, string name)
        {
            foreach (Text label in root.GetComponentsInChildren<Text>(true))
                if (label.name == name) return label;
            Assert.Fail("Missing local versus label: " + name);
            return null;
        }

        private sealed class SaveScope : IDisposable
        {
            private readonly GameSaveStore originalStore;
            private readonly bool originalEnabled;
            private readonly string directory;

            public DuelPrototypeController Controller { get; }
            public GameSaveStore Store { get; }

            public SaveScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                originalStore = Controller.SaveStore;
                directory = Path.Combine(Application.temporaryCachePath,
                    "LocalVersusTests-" + Guid.NewGuid().ToString("N"));
                Store = new GameSaveStore(Path.Combine(directory, GameSaveStore.FileName));
                Controller.SaveStore = Store;
            }

            public string CreateSaveAndReadText()
            {
                Assert.That(Store.TrySave(GameSave.Capture(new PrologueRun(), new CampaignRun()),
                    out string error), Is.True, error);
                return File.ReadAllText(Store.Path);
            }

            public void Dispose()
            {
                Controller.ShowTitle();
                Controller.SaveStore = originalStore;
                Controller.enabled = originalEnabled;
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}