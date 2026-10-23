using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using TurnLimbo.Runtime.Save;
using TurnLimbo.Runtime.Tavern;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class SkillTavernEntryPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator MainMenuEntry_OwnsItsState_AndNeverWritesTheCampaignSave()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                string saved = scope.CreateSave();
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                int ownedBefore = controller.Campaign.OwnedSkills.Count;
                int currencyBefore = controller.Campaign.Currency;
                Assert.That(controller.TitleHud.SkillTavernButton.gameObject.activeSelf, Is.True);
                controller.TitleHud.SkillTavernButton.onClick.Invoke();
                Assert.That(controller.RestartSkillTavern(), Is.True);

                Assert.That(controller.IsInSkillTavern, Is.True);
                Assert.That(controller.SkillTavernHud.IsVisible, Is.True);
                Assert.That(controller.IsInTitle, Is.False);
                Assert.That(controller.IsInLobby, Is.False);
                Assert.That(controller.IsInAcademyField, Is.False);
                Assert.That(controller.IsInOnlineVersus, Is.False);
                Assert.That(controller.AutoSaveEnabled, Is.False);
                Assert.That(controller.StartSkillTavern(), Is.False, "An already open mode cannot be entered twice.");
                Assert.That(controller.StartOnlineVersus(), Is.False);
                Assert.That(controller.StartCampaignStage(1), Is.False);

                SkillTavernRun run = controller.SkillTavern;
                Assert.That(run.Board.Count, Is.EqualTo(8));
                Assert.That(run.ShopOffers.Count, Is.EqualTo(3));
                Assert.That(run.Gold, Is.EqualTo(3));
                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.Buy, 0), Is.True);
                int instance = run.Inventory[0].InstanceId;
                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.Deploy, instance), Is.True);
                Assert.That(run.Board[0].InstanceId, Is.EqualTo(instance));
                Assert.That(controller.Campaign.OwnedSkills.Count, Is.EqualTo(ownedBefore));
                Assert.That(controller.Campaign.Currency, Is.EqualTo(currencyBefore));
                Assert.That(File.ReadAllText(scope.Store.Path), Is.EqualTo(saved));

                controller.ShowTitle();
                Assert.That(controller.IsInTitle, Is.True);
                Assert.That(controller.SkillTavernHud.IsVisible, Is.False);
                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.NextRound), Is.False,
                    "Hidden mode UI cannot mutate its run.");
                Assert.That(controller.StartSkillTavern(), Is.True);
                Assert.That(controller.SkillTavern, Is.SameAs(run));
                Assert.That(controller.SkillTavern.Board[0].InstanceId, Is.EqualTo(instance));
                Assert.That(File.ReadAllText(scope.Store.Path), Is.EqualTo(saved));
            }
        }

        [UnityTest]
        public IEnumerator ShopRefresh_ChargesOneOnlyInTheVisibleShop_AndPreservesTheRoundAndFighters()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                string saved = scope.CreateSave();
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                Assert.That(controller.StartSkillTavern(), Is.True);
                Assert.That(controller.RestartSkillTavern(), Is.True);
                SkillTavernRun run = controller.SkillTavern;
                LegacySkill initialOffer = run.ShopOffers[0];
                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.RefreshShop, 1), Is.False);
                Assert.That(run.Gold, Is.EqualTo(3));
                Assert.That(run.ShopOffers[0], Is.SameAs(initialOffer));
                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.RefreshShop), Is.True);
                Assert.That(run.Gold, Is.EqualTo(2));
                Assert.That(run.RoundNumber, Is.EqualTo(1));
                Assert.That(run.ShopOffers[0], Is.Not.SameAs(initialOffer));

                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.StartBattle), Is.True);
                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.RefreshShop), Is.False);
                Assert.That(run.Gold, Is.EqualTo(2));
                Assert.That(controller.ReturnToSkillTavernFormation(), Is.True);
                LegacyQueuedDuel duel = controller.SkillTavernBattle.Duel;
                var fighters = (duel.Player.Health, duel.Player.Resistance, duel.Enemy.Health, duel.Enemy.Resistance);
                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.RefreshShop), Is.True);
                Assert.That(run.Gold, Is.EqualTo(1));
                Assert.That(run.RoundNumber, Is.EqualTo(1));
                Assert.That(run.RoundIncome, Is.EqualTo(3));
                Assert.That(controller.SkillTavernBattle.Duel, Is.SameAs(duel));
                Assert.That((duel.Player.Health, duel.Player.Resistance, duel.Enemy.Health, duel.Enemy.Resistance), Is.EqualTo(fighters));

                controller.ShowTitle();
                LegacySkill hiddenOffer = run.ShopOffers[0];
                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.RefreshShop), Is.False);
                Assert.That(run.Gold, Is.EqualTo(1));
                Assert.That(run.ShopOffers[0], Is.SameAs(hiddenOffer));
                Assert.That(controller.StartSkillTavern(), Is.True);
                Assert.That(controller.SkillTavern, Is.SameAs(run));
                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.RefreshShop), Is.True);
                LegacySkill exhaustedOffer = run.ShopOffers[0];
                Assert.That(run.Gold, Is.Zero);
                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.RefreshShop), Is.False);
                Assert.That(run.ShopOffers[0], Is.SameAs(exhaustedOffer));
                Assert.That(run.RoundNumber, Is.EqualTo(1));
                Assert.That(File.ReadAllText(scope.Store.Path), Is.EqualTo(saved));
            }
        }

        [UnityTest]
        public IEnumerator ExperimentPausesCampaignUpdates_AndResetOnlyReplacesItsOwnRun()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                Assert.That(controller.StartSkillTavern(), Is.True);
                Assert.That(controller.RestartSkillTavern(), Is.True);
                SkillTavernRun first = controller.SkillTavern;
                int health = controller.PlayerHealth;
                int round = controller.Session.RoundNumber;
                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.NextRound), Is.False,
                    "Income cannot be skipped to without finishing a battle.");
                Assert.That(first.RoundNumber, Is.EqualTo(1));
                Assert.That(first.Gold, Is.EqualTo(3));
                int gold = first.Gold;
                Assert.That(controller.TrySkillTavernAction((SkillTavernAction)999, 0), Is.False);
                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.NextRound, 1), Is.False);
                Assert.That(first.Gold, Is.EqualTo(gold));

                scope.Advance(30f);
                Assert.That(controller.IsInSkillTavern, Is.True);
                Assert.That(controller.PlayerHealth, Is.EqualTo(health));
                Assert.That(controller.Session.RoundNumber, Is.EqualTo(round));
                Assert.That(controller.CanChoose, Is.False);
                Assert.That(scope.Store.Exists, Is.False);

                Assert.That(controller.RestartSkillTavern(), Is.True);
                Assert.That(controller.SkillTavern, Is.Not.SameAs(first));
                Assert.That(controller.SkillTavern.RoundNumber, Is.EqualTo(1));
                Assert.That(controller.SkillTavern.Gold, Is.EqualTo(3));
                Assert.That(controller.SkillTavern.Inventory, Is.Empty);
                Assert.That(controller.SkillTavern.Board, Is.All.Null);
                Assert.That(scope.Store.Exists, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator OneTurnReturnsToTheShop_WithPersistentFightersAndNewPurchasesForTheNextTurn()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                string saved = scope.CreateSave();
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                string campaignBefore = GameSaveCodec.Serialize(GameSave.Capture(controller.Prologue, controller.Campaign));
                LegacyQueuedDuel campaignSession = controller.Session;
                Assert.That(controller.StartSkillTavern(), Is.True);
                Assert.That(controller.RestartSkillTavern(), Is.True);
                var strike = new LegacySkill(1, "연속 라운드 검증", 0, 40, 40,
                    LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, string.Empty);
                var run = new SkillTavernRun(new[] { strike }, 28);
                typeof(DuelPrototypeController).GetField("skillTavernRun", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, run);
                controller.SkillTavernHud.Show(run, null);
                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.BuyAndDeploy, 0), Is.True);
                Assert.That(run.Inventory, Is.Empty);
                int firstCard = run.Board[0].InstanceId;
                LegacyQueuedDuel continuousDuel = null;

                for (int turn = 1; turn <= 2; turn++)
                {
                    int playerHealth = controller.SkillTavernBattle?.Duel.Player.Health ?? SkillTavernBattle.FighterHealth;
                    int enemyHealth = controller.SkillTavernBattle?.Duel.Enemy.Health ?? SkillTavernBattle.FighterHealth;
                    int goldBefore = run.Gold;
                    Assert.That(controller.TrySkillTavernAction(SkillTavernAction.StartBattle), Is.True);
                    LegacyQueuedDuel duel = controller.SkillTavernBattle.Duel;
                    if (continuousDuel == null) continuousDuel = duel;
                    Assert.That(duel, Is.SameAs(continuousDuel), "Shopping must resume the original duel.");
                    Assert.That(duel.RoundNumber, Is.EqualTo(turn));
                    Assert.That(duel.Player.Health, Is.EqualTo(playerHealth));
                    Assert.That(duel.Enemy.Health, Is.EqualTo(enemyHealth));
                    Assert.That(controller.Session, Is.SameAs(duel));
                    Assert.That(controller.ArenaView.ArenaCamera.enabled, Is.True);
                    Assert.That(controller.SkillTavernHud.IsVisible, Is.False);
                    Assert.That(controller.CanChoose || controller.CanStep, Is.False);
                    Assert.That(controller.TrySkillTavernAction(SkillTavernAction.BuyAndDeploy, 0), Is.False);
                    Assert.That(controller.TrySkillTavernAction(SkillTavernAction.Buy, 0), Is.False);
                    Assert.That(controller.TrySkillTavernAction(SkillTavernAction.SellBoard, 0), Is.False);
                    Assert.That(controller.TrySkillTavernAction(SkillTavernAction.NextRound), Is.False);
                    for (int frame = 0; frame < 20000 && !duel.IsTurnResolved; frame++)
                    {
                        scope.Advance(.025f);
                        if (frame % 128 == 0) yield return null;
                    }
                    Assert.That(duel.IsTurnResolved, Is.True);
                    Assert.That(duel.IsFinished, Is.False);
                    var endOfTurn = (duel.Player.Health, duel.Player.Resistance, duel.Enemy.Health, duel.Enemy.Resistance);
                    Assert.That(run.RoundNumber, Is.EqualTo(turn), "The return animation must settle the turn only once.");
                    Assert.That(run.Gold, Is.EqualTo(goldBefore));
                    for (int frame = 0; frame < 20000 && controller.IsInSkillTavernBattle; frame++) scope.Advance(.025f);
                    Assert.That(controller.IsInSkillTavernBattle, Is.False);
                    Assert.That(controller.SkillTavernHud.IsVisible, Is.True, "A live completed turn opens the shop automatically.");
                    Assert.That(controller.IsShowingSkillTavernResult || controller.SkillTavernBattleHud.IsVisible, Is.False);
                    Assert.That(controller.SkillTavernBattle.Duel, Is.SameAs(continuousDuel));
                    Assert.That((duel.Player.Health, duel.Player.Resistance, duel.Enemy.Health, duel.Enemy.Resistance), Is.EqualTo(endOfTurn));
                    Assert.That(duel.RoundNumber, Is.EqualTo(turn), "Recovery waits until the next 8-slot execution starts.");
                    Assert.That(duel.IsTurnResolved, Is.True);
                    Assert.That(controller.Session, Is.SameAs(campaignSession));
                    Assert.That(controller.ArenaView.ArenaCamera.enabled, Is.False, "The shop owns a separate full-screen view.");
                    Assert.That(run.RoundNumber, Is.EqualTo(turn + 1));
                    Assert.That(run.Gold, Is.EqualTo(turn + 3), "Only the new allowance survives; the old remainder expires.");
                    Assert.That(run.BattlesPlayed, Is.Zero, "One turn is not an entire match result.");
                    Assert.That(run.Board[0].InstanceId, Is.EqualTo(firstCard));
                    int paid = run.Gold;
                    scope.Advance(30f);
                    controller.ShowTitle();
                    Assert.That(controller.StartSkillTavern(), Is.True);
                    Assert.That(run.Gold, Is.EqualTo(paid), "Opening the same shop again cannot pay another allowance.");
                    Assert.That((duel.Player.Health, duel.Player.Resistance, duel.Enemy.Health, duel.Enemy.Resistance), Is.EqualTo(endOfTurn));
                    Assert.That(controller.TrySkillTavernAction(SkillTavernAction.NextRound), Is.False);
                    Assert.That(GameSaveCodec.Serialize(GameSave.Capture(controller.Prologue, controller.Campaign)), Is.EqualTo(campaignBefore));
                    Assert.That(File.ReadAllText(scope.Store.Path), Is.EqualTo(saved));
                    if (turn == 1)
                    {
                        Assert.That(controller.TrySkillTavernAction(SkillTavernAction.BuyAndDeploy, 0), Is.True);
                        Assert.That(run.Board[1], Is.Not.Null);
                        Assert.That(run.Inventory, Is.Empty);
                        Assert.That(run.Gold, Is.EqualTo(1));
                    }
                }
                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.Buy, 0), Is.True);
                Assert.That(run.Inventory.Count, Is.EqualTo(1));
                Assert.That(run.Board[2], Is.Null, "Buying for storage does not deploy the card.");
                Assert.That(run.Gold, Is.EqualTo(2));
                Assert.That(controller.Hud.InputsHeld, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator CancelledTurnsRestoreTheShop_WhileCompletedTurnsPayOnceEvenBeforeTheReturnFinishes()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                LegacyQueuedDuel originalSession = controller.Session;
                Assert.That(controller.StartSkillTavern(), Is.True);
                Assert.That(controller.RestartSkillTavern(), Is.True);
                SkillTavernRun run = controller.SkillTavern;
                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.StartBattle), Is.True);
                for (int frame = 0; frame < 20000 && controller.Session.CurrentSlot?.HitsResolved != 1; frame++) scope.Advance(.025f);
                LegacyQueuedDuel interrupted = controller.SkillTavernBattle.Duel;
                Assert.That(controller.ReturnToSkillTavernFormation(), Is.True);
                Assert.That(controller.Session, Is.SameAs(originalSession));
                Assert.That(controller.SkillTavernBattle.Duel, Is.Not.SameAs(interrupted));
                Assert.That(controller.SkillTavernBattle.Duel.Player.Health, Is.EqualTo(500));
                Assert.That(controller.SkillTavernBattle.Duel.Player.Resistance, Is.EqualTo(30));
                Assert.That(run.RoundNumber, Is.EqualTo(1));
                Assert.That(run.Gold, Is.EqualTo(3));

                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.StartBattle), Is.True);
                scope.Advance(.025f);
                controller.ShowTitle();
                Assert.That(controller.Session, Is.SameAs(originalSession));
                Assert.That(controller.StartSkillTavern(), Is.True);
                Assert.That(run.RoundNumber, Is.EqualTo(1));
                Assert.That(run.Gold, Is.EqualTo(3));
                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.StartBattle), Is.True);
                LegacyQueuedDuel duel = controller.Session;
                for (int frame = 0; frame < 20000 && !duel.IsTurnResolved; frame++) scope.Advance(.025f);
                Assert.That(duel.IsTurnResolved, Is.True);
                var after = (duel.Player.Health, duel.Player.Resistance, duel.Enemy.Health, duel.Enemy.Resistance);
                Assert.That(controller.ReturnToSkillTavernFormation(), Is.True);
                Assert.That(controller.ReturnToSkillTavernFormation(), Is.False);
                scope.Advance(30f);
                Assert.That(controller.SkillTavernHud.IsVisible, Is.True);
                Assert.That(controller.SkillTavernBattle.Duel, Is.SameAs(duel));
                Assert.That((duel.Player.Health, duel.Player.Resistance, duel.Enemy.Health, duel.Enemy.Resistance), Is.EqualTo(after));
                Assert.That(run.RoundNumber, Is.EqualTo(2));
                Assert.That(run.Gold, Is.EqualTo(4));
                Assert.That(run.BattlesPlayed, Is.Zero);
                Assert.That(controller.Session, Is.SameAs(originalSession));
                Assert.That(scope.Store.Exists, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator KnockoutEndsTheExperiment_AndOnlyANewExperimentResetsTheFighters()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                Assert.That(controller.StartSkillTavern(), Is.True);
                Assert.That(controller.RestartSkillTavern(), Is.True);
                var finishing = new LegacySkill(-9100, "마무리 검증", 0, 5000, 5000,
                    LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, string.Empty);
                var run = new SkillTavernRun(new[] { finishing }, 28);
                typeof(DuelPrototypeController).GetField("skillTavernRun", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, run);
                controller.SkillTavernHud.Show(run, null);
                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.BuyAndDeploy, 0), Is.True);
                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.StartBattle), Is.True);
                for (int frame = 0; frame < 20000 && !controller.Session.IsFinished; frame++) scope.Advance(.025f);
                Assert.That(controller.Session.IsFinished, Is.True);
                Assert.That(controller.ReturnToSkillTavernFormation(), Is.True, "The finishing shot can hand over to the decided result.");
                Assert.That(controller.IsShowingSkillTavernResult, Is.True);
                Assert.That(controller.SkillTavernHud.IsVisible, Is.False);
                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.RefreshShop), Is.False);
                Assert.That(run.Wins, Is.EqualTo(1));
                Assert.That(run.RoundNumber, Is.EqualTo(1));
                Assert.That(run.Gold, Is.Zero);
                controller.ShowTitle();
                Assert.That(controller.StartSkillTavern(), Is.True);
                Assert.That(controller.IsShowingSkillTavernResult, Is.True);
                Assert.That(run.Wins, Is.EqualTo(1));
                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.NextRound), Is.False);
                Assert.That(controller.RestartSkillTavern(), Is.True);
                Assert.That(controller.SkillTavernHud.IsVisible, Is.True);
                Assert.That(controller.SkillTavernBattle, Is.Null);
                Assert.That(controller.SkillTavern.RoundNumber, Is.EqualTo(1));
                Assert.That(controller.SkillTavern.Gold, Is.EqualTo(3));
                Assert.That(controller.SkillTavern.Board, Is.All.Null);
                Assert.That(scope.Store.Exists, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator CombatPreviewSuspendsTheTavern_AndRestoresItsActiveScreen()
        {
            yield return null;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                Assert.That(controller.StartSkillTavern(), Is.True);
                Assert.That(controller.RestartSkillTavern(), Is.True);
                Assert.That(controller.SetCombatDebugMode(true), Is.True);
                Press(keyboard.escapeKey);
                scope.Advance(.05f, keyboard);
                Release(keyboard.escapeKey);
                Assert.That(controller.IsCombatDebugMode, Is.True);
                Assert.That(controller.IsInSkillTavern, Is.True);
                Assert.That(controller.SetCombatDebugMode(false), Is.True);
                Assert.That(controller.SkillTavernHud.IsVisible, Is.True);

                Assert.That(controller.TrySkillTavernAction(SkillTavernAction.StartBattle), Is.True);
                int round = controller.Session.RoundNumber;
                int health = controller.PlayerHealth;
                Assert.That(controller.SetCombatDebugMode(true), Is.True);
                scope.Advance(30f);
                Assert.That(controller.Session.RoundNumber, Is.EqualTo(round));
                Assert.That(controller.PlayerHealth, Is.EqualTo(health));
                Assert.That(controller.SetCombatDebugMode(false), Is.True);
                Assert.That(controller.SkillTavernBattleHud.IsVisible, Is.True);
                Assert.That(controller.ReturnToSkillTavernFormation(), Is.True);
                Assert.That(scope.Store.Exists, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator EscapeReturnsToMainMenu_WithoutCreatingASave()
        {
            yield return null;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                Assert.That(controller.StartSkillTavern(), Is.True);
                controller.enabled = true;
                Press(keyboard.escapeKey);
                yield return null;
                Release(keyboard.escapeKey);
                yield return null;
                controller.enabled = false;
                Assert.That(controller.IsInTitle, Is.True);
                Assert.That(controller.IsInSkillTavern, Is.False);
                Assert.That(scope.Store.Exists, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator NewGameConfirmationBlocksExperimentEntry()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                string saved = scope.CreateSave();
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                controller.TitleHud.NewGameButton.onClick.Invoke();
                Assert.That(controller.TitleHud.IsConfirming, Is.True);
                controller.TitleHud.SkillTavernButton.onClick.Invoke();
                Assert.That(controller.StartSkillTavern(), Is.False);
                Assert.That(controller.IsInTitle, Is.True);
                Assert.That(controller.IsInSkillTavern, Is.False);
                Assert.That(File.ReadAllText(scope.Store.Path), Is.EqualTo(saved));
            }
        }

        private sealed class SaveScope : IDisposable
        {
            private readonly GameSaveStore originalStore;
            private readonly bool originalEnabled;
            private readonly string directory;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }
            public GameSaveStore Store { get; }

            public SaveScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                originalStore = Controller.SaveStore;
                directory = Path.Combine(Application.temporaryCachePath, "SkillTavernEntry-" + Guid.NewGuid().ToString("N"));
                Store = new GameSaveStore(Path.Combine(directory, GameSaveStore.FileName));
                Controller.SaveStore = Store;
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                    typeof(DuelPrototypeController).GetMethod("AdvancePresentation", BindingFlags.Instance | BindingFlags.NonPublic));
            }

            public string CreateSave()
            {
                Assert.That(Store.TrySave(GameSave.Capture(new PrologueRun(), new CampaignRun()), out string error), Is.True, error);
                return File.ReadAllText(Store.Path);
            }

            public void Advance(float delta, Keyboard keyboard = null) => advance(delta, keyboard);

            public void Dispose()
            {
                Controller.SetCombatDebugMode(false);
                Controller.ShowTitle();
                Controller.SaveStore = originalStore;
                Controller.enabled = originalEnabled;
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
