using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class BattleEndDrawFlowPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator SinglePlayer_TurnLimitAndEmptyCommitDrawsShowTheEndImpact_WithoutStartingAnotherFinale()
        {
            yield return null;
            RequireShader();
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            bool enabled = controller.enabled;
            controller.enabled = false;
            try
            {
                controller.RestartJourney();
                controller.RestartMatch();
                foreach (bool empty in new[] { false, true })
                {
                    LegacyQueuedDuel duel = SingleTurn(empty, 1);
                    Assert.That(duel.Outcome, Is.EqualTo(DuelMatchOutcome.Draw));
                    Assert.That(duel.IsFinished, Is.True);
                    controller.Finale.Clear();
                    controller.ArenaView.Reset();
                    Set(controller, "session", duel);

                    Invoke(controller, "PrepareNextSlot", false);

                    Assert.That(controller.ArenaView.ContrastImpactActive, Is.True,
                        empty ? "An empty final commit has no hit event." : "The draw is decided after the last slot, not by its hit.");
                    Assert.That(controller.Finale.Timeline.Current, Is.EqualTo(LegacyFinishingBlow.Stage.None));
                    Assert.That(Get(controller, "viewPhase").ToString(), Is.EqualTo("AfterTurn"));
                    Assert.That(duel.RoundNumber, Is.EqualTo(1));
                    Assert.That(duel.Player.Health, Is.EqualTo(100));
                    Assert.That(duel.Enemy.Health, Is.EqualTo(100));
                }

                LegacyQueuedDuel live = SingleTurn(false, 2);
                Assert.That(live.IsTurnResolved && !live.IsFinished, Is.True);
                controller.ArenaView.Reset();
                Set(controller, "session", live);
                Invoke(controller, "PrepareNextSlot", false);
                Assert.That(controller.ArenaView.ContrastImpactActive, Is.False,
                    "An ordinary turn ending is not a battle ending.");
            }
            finally
            {
                controller.RestartJourney();
                controller.enabled = enabled;
            }
        }

        [UnityTest]
        public IEnumerator BothVersusPerspectives_ShowTurnLimitAndEmptyCommitDraws_WithoutChangingTheReturnPhase()
        {
            yield return null;
            RequireShader();
            using (var scope = new VersusScope())
            {
                foreach (int localPlayer in new[] { 0, 1 })
                {
                    foreach (bool empty in new[] { false, true })
                    {
                        LocalVersusMatch match = VersusTurn(empty, 1);
                        Assert.That(match.Outcome, Is.EqualTo(LocalVersusOutcome.Draw));
                        Assert.That(match.IsFinished, Is.True);
                        scope.Arena.Reset();
                        Set(scope.Controller, "match", match);
                        Set(scope.Controller, "view", new LocalVersusPerspective(match, localPlayer));

                        Invoke(scope.Controller, "PrepareSlot", false);

                        Assert.That(scope.Arena.ContrastImpactActive, Is.True,
                            "The draw must reach both host and mirrored guest even when no hit decided it.");
                        Assert.That(scope.Controller.Finale.Timeline.Current, Is.EqualTo(LegacyFinishingBlow.Stage.None));
                        Assert.That(Get(scope.Controller, "viewPhase").ToString(), Is.EqualTo("AfterTurn"));
                        Assert.That(match.RoundNumber, Is.EqualTo(1));
                        Assert.That(match.Left.State.Health, Is.EqualTo(100));
                        Assert.That(match.Right.State.Health, Is.EqualTo(100));
                    }
                }

                LocalVersusMatch live = VersusTurn(false, 2);
                Assert.That(live.IsTurnResolved && !live.IsFinished, Is.True);
                scope.Arena.Reset();
                Set(scope.Controller, "match", live);
                Set(scope.Controller, "view", new LocalVersusPerspective(live, 0));
                Invoke(scope.Controller, "PrepareSlot", false);
                Assert.That(scope.Arena.ContrastImpactActive, Is.False);
            }
        }

        private static LegacyQueuedDuel SingleTurn(bool empty, int limit)
        {
            var duel = new LegacyQueuedDuel(100, 30, 100, 30, LegacyInitialSkills.All,
                empty ? Array.Empty<LegacySkill>() : new[] { LegacyCommonActions.Breathe },
                new[] { empty ? 0 : 1 }, roundLimit: limit, meshPercent: 0);
            duel.Commit();
            if (!empty) duel.ResolveNextSlot();
            return duel;
        }

        private static LocalVersusMatch VersusTurn(bool empty, int limit)
        {
            var match = new LocalVersusMatch(LegacyInitialSkills.All, LegacyInitialSkills.All, roundLimit: limit);
            if (!empty) Assert.That(match.TryQueueBreath(0), Is.True);
            Assert.That(match.TryCommit(0), Is.True);
            Assert.That(match.TryCommit(1), Is.True);
            if (!empty) match.ResolveNextSlot();
            return match;
        }

        private static void RequireShader()
        {
            Shader shader = Resources.Load<Shader>("DuelVFX/BreakSilhouette");
            if (shader == null || !shader.isSupported)
                Assert.Ignore("The native break silhouette shader is unavailable on this graphics device.");
        }

        private static void Set(object target, string name, object value)
            => target.GetType().GetField(name, Private).SetValue(target, value);

        private static object Get(object target, string name)
            => target.GetType().GetField(name, Private).GetValue(target);

        private static void Invoke(object target, string name, bool wait)
            => target.GetType().GetMethod(name, Private).Invoke(target, new object[] { wait });

        private sealed class VersusScope : IDisposable
        {
            private readonly DuelPrototypeController scene = Object.FindAnyObjectByType<DuelPrototypeController>();
            private readonly bool sceneEnabled;
            private readonly GameObject root = new GameObject("Battle End Draw Flow Test");
            private readonly LegacyDuelArt art = new LegacyDuelArt();
            private readonly DuelPresentationSettings settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            public LegacyArenaView Arena { get; }
            public LocalVersusController Controller { get; }

            public VersusScope()
            {
                if (scene != null)
                {
                    sceneEnabled = scene.enabled;
                    scene.enabled = false;
                }
                Arena = LegacyArenaView.Create(root.transform, art, settings);
                Controller = new LocalVersusController(root.transform, art, Arena, () => { }, settings);
            }

            public void Dispose()
            {
                Controller.Dispose();
                Arena.Dispose();
                art.Dispose();
                Object.Destroy(root);
                Object.Destroy(settings);
                if (scene != null) scene.enabled = sceneEnabled;
            }
        }
    }
}
