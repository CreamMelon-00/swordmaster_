using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Tavern;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class SkillTavernBattleHudPlayModeTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Parent = new GameObject("Skill Tavern Battle HUD Test");
            public readonly LegacyDuelArt Art = new LegacyDuelArt();
            public readonly SkillTavernRun Run = new SkillTavernRun(new[]
            {
                new LegacySkill(9101, "시험 베기", 2, 4, 6, LegacySkillKind.Attack,
                    LegacySkillProperty.Slash, 1, 0, "시험 기술")
            }, 9);
            public readonly SkillTavernBattle Battle;
            public readonly SkillTavernBattleHud Hud;
            public int FormationCalls, NextCalls, TitleCalls;

            public Fixture(bool callbacks = true)
            {
                Battle = new SkillTavernBattle(Run, 9);
                Hud = new SkillTavernBattleHud(Parent.transform, Art,
                    callbacks ? (Action)(() => FormationCalls++) : null,
                    callbacks ? (Action)(() => NextCalls++) : null,
                    callbacks ? (Action)(() => TitleCalls++) : null);
            }

            public void Dispose()
            {
                Hud.Dispose();
                Art.Dispose();
                Object.Destroy(Parent);
            }
        }

        [UnityTest]
        public IEnumerator AutomaticBattleOnlyAddsASmallTopStrip_WithoutBlockingTheArena()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                fixture.Hud.ShowBattle(fixture.Run, fixture.Battle);
                Canvas.ForceUpdateCanvases();
                Assert.That(fixture.Hud.IsVisible, Is.True);
                Assert.That(fixture.Hud.IsShowingResult, Is.False);
                Assert.That(Text(fixture.Hud, "Tavern Battle Caption"), Does.Contain("1라운드").And.Contain("자동 전투").And.Contain(fixture.Battle.EnemyName));
                Assert.That(Find(fixture.Hud, "Tavern Result Backdrop").gameObject.activeSelf, Is.False);
                var strip = Find(fixture.Hud, "Tavern Battle Strip").GetComponent<RectTransform>();
                Assert.That(strip.anchorMin, Is.EqualTo(new Vector2(.5f, 1f)));
                Assert.That(strip.rect.height, Is.LessThanOrEqualTo(60f));
                Image[] hitTargets = fixture.Hud.Root.GetComponentsInChildren<Image>()
                    .Where(image => image.raycastTarget).ToArray();
                Assert.That(hitTargets.Length, Is.EqualTo(1), "Only the return button intercepts clicks during the fight.");
                Assert.That(hitTargets.Single().name, Is.EqualTo("Tavern Battle Return"));
                Assert.That(fixture.Hud.Root.GetComponent<CanvasScaler>().screenMatchMode,
                    Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
                Assert.That(fixture.Run.RoundNumber, Is.EqualTo(1));
                Assert.That(fixture.Run.BattlesPlayed, Is.Zero);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReturningToFormationRequestsOneExit_AndRepeatedRefreshCannotRepeatIt()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                fixture.Hud.ShowBattle(fixture.Run, fixture.Battle);
                Button back = Button(fixture.Hud, "Tavern Battle Return");
                back.onClick.Invoke();
                back.onClick.Invoke();
                Assert.That(fixture.FormationCalls, Is.EqualTo(1));
                Assert.That(back.interactable, Is.False);
                fixture.Hud.ShowBattle(fixture.Run, fixture.Battle);
                back.onClick.Invoke();
                Assert.That(fixture.FormationCalls, Is.EqualTo(1));
                fixture.Hud.Hide();
                back.onClick.Invoke();
                Assert.That(fixture.FormationCalls, Is.EqualTo(1));
                fixture.Hud.ShowBattle(fixture.Run, fixture.Battle);
                Assert.That(back.interactable, Is.True);
                back.onClick.Invoke();
                Assert.That(fixture.FormationCalls, Is.EqualTo(2));
                Assert.That(fixture.NextCalls + fixture.TitleCalls, Is.Zero);
                Assert.That(fixture.Run.Gold, Is.EqualTo(3));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SettledResultsShowVictoryDefeatAndDraw_WithActualHealthTurnsAndAccumulatedRecord()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                var outcomes = new[] { DuelMatchOutcome.PlayerVictory, DuelMatchOutcome.EnemyVictory, DuelMatchOutcome.Draw };
                string[] captions = { "승리", "패배", "무승부" };
                for (int i = 0; i < outcomes.Length; i++)
                {
                    SkillTavernBattle battle = Settle(fixture.Run, outcomes[i]);
                    fixture.Hud.ShowResult(fixture.Run, battle);
                    Assert.That(fixture.Hud.IsShowingResult, Is.True);
                    Assert.That(Text(fixture.Hud, "Tavern Result Heading"), Is.EqualTo(captions[i]));
                    Assert.That(Text(fixture.Hud, "Tavern Result Turns"), Is.EqualTo(battle.Duel.RoundNumber.ToString()));
                    Assert.That(Text(fixture.Hud, "Tavern Result Player HP"),
                        Is.EqualTo(battle.Duel.Player.Health + " / " + SkillTavernBattle.FighterHealth));
                    Assert.That(Text(fixture.Hud, "Tavern Result Enemy HP"),
                        Is.EqualTo(battle.Duel.Enemy.Health + " / " + SkillTavernBattle.FighterHealth));
                    Assert.That(Find(fixture.Hud, "Tavern Battle Strip").gameObject.activeSelf, Is.False);
                    Assert.That(Find(fixture.Hud, "Tavern Result Backdrop").GetComponent<Image>().raycastTarget, Is.True);
                    fixture.Hud.ShowResult(fixture.Run, battle);
                    Assert.That(fixture.Run.BattlesPlayed, Is.EqualTo(i + 1), "Refreshing a result cannot record it again.");
                }
                Assert.That(Text(fixture.Hud, "Tavern Result Record"), Is.EqualTo("누적 3전 · 1승 · 1패 · 1무"));
                Assert.That(Text(fixture.Hud, "Tavern Result Round Hint"), Does.Contain("대전이 끝났습니다").And.Contain("처음 상태로 되돌립니다"));
                Assert.That(Button(fixture.Hud, "Tavern Result Next Round").GetComponentInChildren<Text>().text, Is.EqualTo("새 실험"));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator NewExperimentAndTitleActionsWaitForTheFinalResult_AndShareOneExitRequestWithoutChangingTheRun()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                fixture.Hud.ShowBattle(fixture.Run, fixture.Battle);
                Button next = Button(fixture.Hud, "Tavern Result Next Round");
                Button title = Button(fixture.Hud, "Tavern Result Title");
                next.onClick.Invoke();
                title.onClick.Invoke();
                Assert.That(fixture.NextCalls + fixture.TitleCalls, Is.Zero);
                SkillTavernBattle ended = Settle(fixture.Run, DuelMatchOutcome.PlayerVictory);
                fixture.Hud.ShowResult(fixture.Run, ended);
                int round = fixture.Run.RoundNumber, gold = fixture.Run.Gold;
                next.onClick.Invoke();
                next.onClick.Invoke();
                title.onClick.Invoke();
                Assert.That(fixture.NextCalls, Is.EqualTo(1));
                Assert.That(fixture.TitleCalls, Is.Zero);
                Assert.That(next.interactable || title.interactable, Is.False);
                fixture.Hud.ShowResult(fixture.Run, ended);
                next.onClick.Invoke();
                Assert.That(fixture.NextCalls, Is.EqualTo(1));
                Assert.That(fixture.Run.RoundNumber, Is.EqualTo(round));
                Assert.That(fixture.Run.Gold, Is.EqualTo(gold), "The authority, rather than the result view, pays income.");
                fixture.Hud.Hide();
                fixture.Hud.ShowResult(fixture.Run, ended);
                title.onClick.Invoke();
                Assert.That(fixture.TitleCalls, Is.EqualTo(1));
                Assert.That(fixture.FormationCalls, Is.Zero);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator LiveBattlesCannotOpenAResult_AndAbsentCallbacksDisableTheirControls()
        {
            yield return null;
            using (var fixture = new Fixture(false))
            {
                Assert.Throws<InvalidOperationException>(() => fixture.Hud.ShowResult(fixture.Run, fixture.Battle));
                Assert.That(fixture.Hud.IsVisible, Is.False);
                fixture.Hud.ShowBattle(fixture.Run, fixture.Battle);
                Assert.That(Button(fixture.Hud, "Tavern Battle Return").interactable, Is.False);
                fixture.Hud.ShowResult(fixture.Run, Settle(fixture.Run, DuelMatchOutcome.Draw));
                Assert.That(Button(fixture.Hud, "Tavern Result Next Round").interactable, Is.False);
                Assert.That(Button(fixture.Hud, "Tavern Result Title").interactable, Is.False);
                Assert.That(fixture.Run.BattlesPlayed, Is.EqualTo(1));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisposalDetachesBattleAndResultCallbacks_AndIsSafeToRepeat()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                fixture.Hud.ShowBattle(fixture.Run, fixture.Battle);
                Button back = Button(fixture.Hud, "Tavern Battle Return");
                Button next = Button(fixture.Hud, "Tavern Result Next Round");
                Button title = Button(fixture.Hud, "Tavern Result Title");
                fixture.Hud.Dispose();
                fixture.Hud.Dispose();
                back.onClick.Invoke();
                next.onClick.Invoke();
                title.onClick.Invoke();
                Assert.That(fixture.FormationCalls + fixture.NextCalls + fixture.TitleCalls, Is.Zero);
                Assert.That(fixture.Hud.IsVisible, Is.False);
                Assert.That(fixture.Run.BattlesPlayed, Is.Zero);
            }
            yield return null;
        }

        private static SkillTavernBattle Settle(SkillTavernRun run, DuelMatchOutcome wanted)
        {
            var battle = new SkillTavernBattle(run, 9);
            LegacySkill[] player = Enumerable.Repeat(LegacyCommonActions.Breathe, SkillTavernRun.SlotCount).ToArray();
            LegacySkill[] enemy = Enumerable.Repeat(LegacyCommonActions.Breathe, SkillTavernRun.SlotCount).ToArray();
            var strike = new LegacySkill(-9100, "결정타", 0, 200, 200, LegacySkillKind.Attack,
                LegacySkillProperty.Penetrate, 1, 0, string.Empty, iconId: 3);
            if (wanted == DuelMatchOutcome.PlayerVictory) player[0] = strike;
            else if (wanted == DuelMatchOutcome.EnemyVictory) enemy[0] = strike;
            Assert.That(battle.Duel.TrySetFixedQueues(player, enemy), Is.True);
            battle.Duel.Commit();
            int safety = 0;
            int maximumSteps = SkillTavernBattle.MaximumTurns * (SkillTavernRun.SlotCount + 1);
            while (!battle.Duel.IsFinished && safety++ < maximumSteps)
            {
                if (!battle.Duel.IsTurnResolved) battle.Duel.ResolveNextSlot();
                else
                {
                    battle.Duel.BeginNextTurn();
                    Assert.That(battle.Duel.TrySetFixedQueues(player, enemy), Is.True);
                    battle.Duel.Commit();
                }
            }
            Assert.That(battle.Duel.IsFinished, Is.True);
            Assert.That(battle.Duel.Outcome, Is.EqualTo(wanted));
            Assert.That(battle.TryRecordResult(), Is.True);
            return battle;
        }

        private static Transform Find(SkillTavernBattleHud hud, string name)
        {
            Transform found = hud.Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == name);
            Assert.That(found, Is.Not.Null, name);
            return found;
        }

        private static Button Button(SkillTavernBattleHud hud, string name) => Find(hud, name).GetComponent<Button>();
        private static string Text(SkillTavernBattleHud hud, string name) => Find(hud, name).GetComponent<Text>().text;
    }
}
