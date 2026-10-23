using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Tavern;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class SkillTavernHudPlayModeTests
    {
        private sealed class Fixture : IDisposable
        {
            private readonly LegacySkill[] catalog =
            {
                new LegacySkill(9001, "시험 베기", 2, 4, 6, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "참격 시험"),
                new LegacySkill(9002, "시험 찌르기", 2, 5, 7, LegacySkillKind.Attack, LegacySkillProperty.Penetrate, 1, 1, "관통 시험"),
                new LegacySkill(9003, "시험 막기", 1, 3, 5, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 2, "방어 시험")
            };
            public readonly GameObject Parent = new GameObject("Skill Tavern Test");
            public readonly LegacyDuelArt Art = new LegacyDuelArt();
            public readonly SkillTavernHud Hud;
            public SkillTavernRun Run;
            public int Commands, Returns, Resets, BattleStarts;

            public Fixture(bool authority = true)
            {
                Run = new SkillTavernRun(catalog, 7);
                Hud = new SkillTavernHud(Parent.transform, Art, () => { Returns++; Hud.Hide(); },
                    () => { Resets++; Run = new SkillTavernRun(catalog, 7); Hud.Show(Run); },
                    authority ? (Action<SkillTavernAction, int>)Execute : null);
                Hud.Show(Run);
                Canvas.ForceUpdateCanvases();
            }

            private void Execute(SkillTavernAction action, int value)
            {
                Commands++;
                switch (action)
                {
                    case SkillTavernAction.Buy: Assert.That(Run.TryBuy(value), Is.True); break;
                    case SkillTavernAction.BuyAndDeploy: Assert.That(Run.TryBuyAndDeploy(value), Is.True); break;
                    case SkillTavernAction.Deploy: Assert.That(Run.TryDeploy(value), Is.True); break;
                    case SkillTavernAction.SellBoard: Assert.That(Run.TrySellBoard(value), Is.True); break;
                    case SkillTavernAction.SellInventory: Assert.That(Run.TrySellInventory(value), Is.True); break;
                    case SkillTavernAction.RefreshShop: Assert.That(value, Is.Zero); Assert.That(Run.TryRefreshShop(), Is.True); break;
                    case SkillTavernAction.NextRound: Run.NextRound(); break;
                    case SkillTavernAction.StartBattle: BattleStarts++; Hud.Hide(); return;
                    default: Assert.Fail("Unexpected view command."); break;
                }
                Hud.Show(Run);
            }

            public SkillTavernCard Buy()
            {
                int offer = -1;
                for (int i = 0; i < SkillTavernRun.ShopSize; i++)
                    if (Run.CanBuy(i)) { offer = i; break; }
                if (offer < 0)
                {
                    // Economy setup for a later preparation; the complete fight/result loop has its own flow tests.
                    Run.NextRound();
                    Hud.Show(Run);
                    offer = 0;
                }
                Click(Hud, "Tavern Buy " + offer);
                return Run.Inventory[Run.Inventory.Count - 1];
            }

            public SkillTavernCard BuyAndDeploy()
            {
                SkillTavernCard card = Buy();
                Click(Hud, "Tavern Inventory Card " + card.InstanceId);
                Click(Hud, "Tavern Deploy");
                return card;
            }

            public void Dispose()
            {
                Hud.Dispose();
                Art.Dispose();
                Object.Destroy(Parent);
            }
        }

        [UnityTest]
        public IEnumerator OpeningShowsThreeOffersEightBreathsAndAnEmptyInventory()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                SkillTavernHud hud = fixture.Hud;
                Assert.That(hud.IsVisible, Is.True);
                Assert.That(hud.Run, Is.SameAs(fixture.Run));
                Assert.That(Text(hud, "Tavern Heading"), Is.EqualTo("선술집 실험"));
                Assert.That(Text(hud, "Tavern Rules"), Does.Contain("8칸을 자동 실행한 뒤 상점으로 돌아옵니다").And.Contain("예약 비용으로 쓰지 않습니다"));
                Assert.That(Button(hud, "Tavern Next Round").GetComponentInChildren<Text>().text, Is.EqualTo("8칸 실행"));
                Assert.That(Text(hud, "Tavern Round Hint"), Does.Contain("상대 기술 1/8").And.Contain("자동으로 다음 상점")
                    .And.Contain("이번 라운드 자원 3").And.Contain("이월 없음"));
                Assert.That(Text(hud, "Tavern Player State"), Does.Contain("HP 500 / 500").And.Contain("저항 30 / 30"));
                Assert.That(Text(hud, "Tavern Enemy State"), Does.Contain("HP 500 / 500").And.Contain("저항 30 / 30"));
                Assert.That(Text(hud, "Tavern Wallet"), Does.Contain("라운드 1").And.Contain("자원 3"));
                Assert.That(Button(hud, "Tavern Refresh Shop").GetComponentInChildren<Text>().text, Is.EqualTo("새로고침 1"));
                Assert.That(Button(hud, "Tavern Refresh Shop").interactable, Is.True);
                var scaler = hud.Root.GetComponent<CanvasScaler>();
                Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
                Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
                for (int i = 0; i < SkillTavernRun.ShopSize; i++)
                {
                    Assert.That(Button(hud, "Tavern Buy " + i).interactable, Is.True);
                    Assert.That(Button(hud, "Tavern Buy " + i).GetComponentInChildren<Text>().text, Is.EqualTo("보관 3"));
                    Assert.That(Button(hud, "Tavern Buy And Deploy " + i).interactable, Is.True);
                    Assert.That(Button(hud, "Tavern Buy And Deploy " + i).GetComponentInChildren<Text>().text, Is.EqualTo("편성 3"));
                }
                RectTransform surface = Find(hud.Root, "Tavern Surface").GetComponent<RectTransform>();
                Image backdrop = Find(hud.Root, "Tavern Backdrop").GetComponent<Image>();
                Assert.That(surface.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(surface.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(surface.sizeDelta, Is.EqualTo(Vector2.zero));
                Assert.That(surface.GetComponent<Image>(), Is.Null, "The workspace has no inset modal card or border.");
                Assert.That(surface.Find("Brass Trim"), Is.Null);
                Assert.That(backdrop.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(backdrop.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(backdrop.color.a, Is.EqualTo(1f));
                Assert.That(backdrop.raycastTarget, Is.True);
                for (int i = 0; i < SkillTavernRun.SlotCount; i++)
                {
                    Transform slot = Find(hud.Root, "Tavern Board Slot " + i);
                    Assert.That(Find(slot.gameObject, "Board Card Name").GetComponent<Text>().text, Is.EqualTo("숨고르기"));
                    Assert.That(Button(hud, "Tavern Sell Board " + i).interactable, Is.False);
                }
                Assert.That(Find(hud.Root, "Tavern Inventory Empty"), Is.Not.Null);
                Assert.That(Button(hud, "Tavern Deploy").interactable, Is.False);
                Assert.That(Button(hud, "Tavern Sell Inventory").interactable, Is.False);
                Click(hud, "Tavern Board Slot 3");
                Assert.That(Text(hud, "Tavern Detail Name"), Is.EqualTo("숨고르기"));
                Assert.That(fixture.Commands, Is.Zero, "Inspecting a breathing position only changes the view.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RefreshingRefillsSoldOutOffers_AndKeepsTheSameRoundAndPurchasedCopies()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                for (int i = 0; i < 7; i++) fixture.Run.NextRound();
                fixture.Hud.Show(fixture.Run);
                Click(fixture.Hud, "Tavern Buy And Deploy 0");
                Click(fixture.Hud, "Tavern Buy 1");
                SkillTavernCard deployed = fixture.Run.Board[0], stored = fixture.Run.Inventory.Single();
                Click(fixture.Hud, "Tavern Inventory Card " + stored.InstanceId);
                int round = fixture.Run.RoundNumber, income = fixture.Run.RoundIncome, commands = fixture.Commands;
                LegacySkill[] oldOffers = fixture.Run.ShopOffers.ToArray();
                Assert.That(oldOffers[0], Is.Null);
                Assert.That(oldOffers[1], Is.Null);
                Assert.That(fixture.Run.Gold, Is.EqualTo(4));

                Click(fixture.Hud, "Tavern Refresh Shop");
                Assert.That(fixture.Commands, Is.EqualTo(commands + 1), "The refresh is one request to the state owner.");
                Assert.That(fixture.Run.Gold, Is.EqualTo(3));
                Assert.That(fixture.Run.ShopOffers, Has.Count.EqualTo(SkillTavernRun.ShopSize).And.All.Not.Null);
                Assert.That(fixture.Run.ShopOffers[2], Is.Not.SameAs(oldOffers[2]), "Unsold offers are redrawn too; skill ids may repeat.");
                for (int i = 0; i < SkillTavernRun.ShopSize; i++)
                {
                    Assert.That(Button(fixture.Hud, "Tavern Buy " + i).interactable, Is.True);
                    Assert.That(Button(fixture.Hud, "Tavern Buy And Deploy " + i).interactable, Is.True);
                }
                Assert.That(Text(fixture.Hud, "Tavern Selected Inventory"), Does.Contain("#" + stored.InstanceId));

                for (int i = 0; i < 3; i++) Click(fixture.Hud, "Tavern Refresh Shop");
                Assert.That(fixture.Commands, Is.EqualTo(commands + 4));
                Assert.That(fixture.Run.Gold, Is.Zero);
                Assert.That(Button(fixture.Hud, "Tavern Refresh Shop").interactable, Is.False);
                Assert.That(Button(fixture.Hud, "Tavern Refresh Shop").GetComponentInChildren<Text>().text, Is.EqualTo("새로고침 1"));
                Assert.That(fixture.Run.RoundNumber, Is.EqualTo(round));
                Assert.That(fixture.Run.RoundIncome, Is.EqualTo(income));
                Assert.That(fixture.Run.Board[0], Is.SameAs(deployed));
                Assert.That(fixture.Run.Board.Skip(1), Is.All.Null);
                Assert.That(fixture.Run.Inventory.Single(), Is.SameAs(stored));
                Assert.That(fixture.BattleStarts, Is.Zero);
                Assert.That(Text(fixture.Hud, "Tavern Wallet"), Does.Contain("라운드 " + round).And.Contain("자원 0"));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RefreshOnlyRequestsAuthority_AndHiddenOrDisposedControlsCannotRequestIt()
        {
            yield return null;
            var parent = new GameObject("Skill Tavern Refresh Authority Test");
            try
            {
                using (var art = new LegacyDuelArt())
                {
                    var skill = new LegacySkill(9001, "시험 베기", 2, 4, 6, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "참격 시험");
                    var run = new SkillTavernRun(new[] { skill }, 7);
                    LegacySkill[] offers = run.ShopOffers.ToArray();
                    int requests = 0;
                    using (var hud = new SkillTavernHud(parent.transform, art, null, null, (action, value) =>
                    {
                        Assert.That(action, Is.EqualTo(SkillTavernAction.RefreshShop));
                        Assert.That(value, Is.Zero);
                        requests++;
                    }))
                    {
                        hud.Show(run);
                        Button refresh = Button(hud, "Tavern Refresh Shop");
                        Click(hud, "Tavern Refresh Shop");
                        Assert.That(requests, Is.EqualTo(1));
                        Assert.That(run.Gold, Is.EqualTo(3), "The view waits for the owner to accept the request.");
                        Assert.That(run.ShopOffers, Is.EqualTo(offers));
                        hud.Hide();
                        refresh.onClick.Invoke();
                        Assert.That(requests, Is.EqualTo(1));
                        hud.Show(run);
                        hud.Dispose();
                        refresh.onClick.Invoke();
                        Assert.That(requests, Is.EqualTo(1));
                        Assert.That(run.Gold, Is.EqualTo(3));
                    }
                }
            }
            finally { Object.Destroy(parent); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DirectAndStoredPurchasesCostTheSame_AndDirectPurchasesFillOnlyTheFirstHole()
        {
            yield return null;
            using (var stored = new Fixture())
            {
                Click(stored.Hud, "Tavern Buy 0");
                Assert.That(stored.Run.Gold, Is.Zero);
                Assert.That(stored.Run.Inventory.Count, Is.EqualTo(1));
                Assert.That(stored.Run.Board, Is.All.Null);
            }
            using (var direct = new Fixture())
            {
                Click(direct.Hud, "Tavern Buy And Deploy 0");
                Assert.That(direct.Run.Gold, Is.Zero);
                Assert.That(direct.Run.Inventory, Is.Empty);
                Assert.That(direct.Run.Board[0], Is.Not.Null);
                Assert.That(direct.Run.ShopOffers[0], Is.Null);
                direct.Run.NextRound();
                direct.Hud.Show(direct.Run);
                Click(direct.Hud, "Tavern Buy And Deploy 0");
                SkillTavernCard later = direct.Run.Board[1];
                Assert.That(later, Is.Not.Null);
                Click(direct.Hud, "Tavern Sell Board 0");
                direct.Run.NextRound();
                direct.Hud.Show(direct.Run);
                int before = direct.Run.Gold;
                Click(direct.Hud, "Tavern Buy And Deploy 0");
                Assert.That(direct.Run.Gold, Is.EqualTo(before - SkillTavernRun.PurchasePrice));
                Assert.That(direct.Run.Board[0], Is.Not.Null);
                Assert.That(direct.Run.Board[1], Is.SameAs(later));
                Assert.That(direct.Run.Inventory, Is.Empty, "Direct formation never takes a detour through storage.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FullScreenShopReadsCarriedHealthAndResistance_FromThePersistentBattle()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                var battle = new SkillTavernBattle(fixture.Run, 11);
                LegacySkill[] player = Enumerable.Repeat(LegacyCommonActions.Breathe, SkillTavernRun.SlotCount).ToArray();
                LegacySkill[] enemy = Enumerable.Repeat(LegacyCommonActions.Breathe, SkillTavernRun.SlotCount).ToArray();
                player[0] = new LegacySkill(-9401, "저항 시험", 0, 10, 10, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "");
                enemy[0] = new LegacySkill(-9402, "저항 시험", 0, 6, 6, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "");
                player[1] = new LegacySkill(-9403, "피해 시험", 0, 15, 15, LegacySkillKind.Attack, LegacySkillProperty.Penetrate, 1, 0, "");
                enemy[2] = new LegacySkill(-9404, "피해 시험", 0, 20, 20, LegacySkillKind.Attack, LegacySkillProperty.Penetrate, 1, 0, "");
                Assert.That(battle.Duel.TrySetFixedQueues(player, enemy), Is.True);
                battle.Duel.Commit();
                while (!battle.Duel.IsTurnResolved && !battle.Duel.IsFinished) battle.Duel.ResolveNextSlot();
                Assert.That(battle.Duel.Player.Health, Is.LessThan(battle.Duel.Player.MaxHealth));
                Assert.That(battle.Duel.Enemy.Health, Is.LessThan(battle.Duel.Enemy.MaxHealth));
                Assert.That(battle.Duel.Player.Resistance, Is.LessThan(battle.Duel.Player.MaxResistance));
                Assert.That(battle.Duel.Enemy.Resistance, Is.LessThan(battle.Duel.Enemy.MaxResistance));
                fixture.Run.NextRound();
                fixture.Hud.Show(fixture.Run, battle);
                string playerStatus = "나 · HP " + battle.Duel.Player.Health + " / " + battle.Duel.Player.MaxHealth
                    + " · 저항 " + battle.Duel.Player.Resistance + " / " + battle.Duel.Player.MaxResistance;
                string enemyStatus = battle.EnemyName + " · HP " + battle.Duel.Enemy.Health + " / " + battle.Duel.Enemy.MaxHealth
                    + " · 저항 " + battle.Duel.Enemy.Resistance + " / " + battle.Duel.Enemy.MaxResistance;
                Assert.That(Text(fixture.Hud, "Tavern Player State"), Is.EqualTo(playerStatus));
                Assert.That(Text(fixture.Hud, "Tavern Enemy State"), Is.EqualTo(enemyStatus));
                Assert.That(Text(fixture.Hud, "Tavern Wallet"), Does.Contain("라운드 2").And.Contain("자원 4"));
                Assert.That(Text(fixture.Hud, "Tavern Round Hint"), Does.Contain("이번 라운드 자원 4").And.Contain("이월 없음"));
                Click(fixture.Hud, "Tavern Refresh Shop");
                Assert.That(fixture.Run.RoundNumber, Is.EqualTo(2));
                Assert.That(fixture.Run.Gold, Is.EqualTo(3));
                Assert.That(Text(fixture.Hud, "Tavern Player State"), Is.EqualTo(playerStatus));
                Assert.That(Text(fixture.Hud, "Tavern Enemy State"), Is.EqualTo(enemyStatus));
                fixture.Hud.Hide();
                fixture.Hud.Show(fixture.Run);
                Assert.That(Text(fixture.Hud, "Tavern Player State"), Is.EqualTo(playerStatus), "Reopening the same shop retains its battle binding.");
                Assert.That(Text(fixture.Hud, "Tavern Enemy State"), Is.EqualTo(enemyStatus));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator BuyingWaitsInInventory_DeploymentUsesFirstHole_AndSellingDoesNotShiftLaterCards()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                SkillTavernCard first = fixture.Buy();
                Assert.That(fixture.Run.Board.All(card => card == null), Is.True);
                Assert.That(fixture.Run.Inventory.Count, Is.EqualTo(1));
                Assert.That(Button(fixture.Hud, "Tavern Buy 0").interactable, Is.False);
                Click(fixture.Hud, "Tavern Inventory Card " + first.InstanceId);
                Assert.That(Text(fixture.Hud, "Tavern Detail Name"), Is.EqualTo(first.Skill.Name));
                Click(fixture.Hud, "Tavern Deploy");
                Assert.That(fixture.Run.Board[0], Is.SameAs(first));
                Assert.That(fixture.Run.Inventory, Is.Empty);
                SkillTavernCard second = fixture.BuyAndDeploy();
                Assert.That(fixture.Run.Board[1], Is.SameAs(second));

                Click(fixture.Hud, "Tavern Sell Board 0");
                Assert.That(fixture.Run.Board[0], Is.Null);
                Assert.That(fixture.Run.Board[1], Is.SameAs(second));
                Assert.That(fixture.Run.BuildQueue()[0].IsWait, Is.True);
                Assert.That(fixture.Run.Inventory, Is.Empty, "Selling does not return the deployed card to storage.");
                SkillTavernCard replacement = fixture.BuyAndDeploy();
                Assert.That(fixture.Run.Board[0], Is.SameAs(replacement));
                Assert.That(fixture.Run.Board[1], Is.SameAs(second));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator InsufficientMoneyDisablesPurchases_AndInventorySaleAddsOneResource()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                SkillTavernCard bought = fixture.Buy();
                Assert.That(fixture.Run.Gold, Is.Zero);
                Assert.That(Button(fixture.Hud, "Tavern Buy 1").interactable, Is.False);
                Assert.That(Button(fixture.Hud, "Tavern Buy 1").GetComponentInChildren<Text>().text, Does.Contain("자원 부족"));
                Assert.That(Button(fixture.Hud, "Tavern Buy And Deploy 1").interactable, Is.False);
                Assert.That(Button(fixture.Hud, "Tavern Buy And Deploy 1").GetComponentInChildren<Text>().text, Does.Contain("자원 부족"));
                Click(fixture.Hud, "Tavern Inventory Card " + bought.InstanceId);
                Click(fixture.Hud, "Tavern Sell Inventory");
                Assert.That(fixture.Run.Gold, Is.EqualTo(SkillTavernRun.SellPrice));
                Assert.That(fixture.Run.Inventory, Is.Empty);
                Assert.That(Find(fixture.Hud.Root, "Tavern Inventory Card " + bought.InstanceId), Is.Null);
                Assert.That(Button(fixture.Hud, "Tavern Deploy").interactable, Is.False);
                Assert.That(Button(fixture.Hud, "Tavern Sell Inventory").interactable, Is.False);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FullBoardKeepsTheSelectedCardInInventory_UntilASaleMakesASeat()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                for (int i = 0; i < SkillTavernRun.SlotCount; i++) fixture.BuyAndDeploy();
                fixture.Run.NextRound();
                fixture.Hud.Show(fixture.Run);
                Assert.That(Button(fixture.Hud, "Tavern Buy And Deploy 0").interactable, Is.False);
                Assert.That(Button(fixture.Hud, "Tavern Buy And Deploy 0").GetComponentInChildren<Text>().text, Is.EqualTo("편성 가득 참"));
                Assert.That(Button(fixture.Hud, "Tavern Buy 0").interactable, Is.True, "A full formation can still buy a stored copy.");
                SkillTavernCard spare = fixture.Buy();
                int later = fixture.Run.Board[3].InstanceId;
                Click(fixture.Hud, "Tavern Inventory Card " + spare.InstanceId);
                Assert.That(Button(fixture.Hud, "Tavern Deploy").interactable, Is.False);
                Assert.That(Text(fixture.Hud, "Tavern Inventory Hint"), Does.Contain("8칸이 모두 찼습니다"));
                Click(fixture.Hud, "Tavern Sell Board 2");
                Assert.That(Button(fixture.Hud, "Tavern Deploy").interactable, Is.True,
                    "A board sale retains the selected inventory copy.");
                Click(fixture.Hud, "Tavern Deploy");
                Assert.That(fixture.Run.Board[2], Is.SameAs(spare));
                Assert.That(fixture.Run.Board[3].InstanceId, Is.EqualTo(later));
                Assert.That(fixture.Run.Inventory, Is.Empty);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartingBattleRequestsAnAutomaticFight_WithoutAdvancingTheShopOrChangingItsFormation()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                SkillTavernCard deployed = fixture.BuyAndDeploy();
                SkillTavernCard stored = fixture.Buy();
                Click(fixture.Hud, "Tavern Inventory Card " + stored.InstanceId);
                int round = fixture.Run.RoundNumber, gold = fixture.Run.Gold;
                LegacySkill[] offers = fixture.Run.ShopOffers.ToArray();
                Click(fixture.Hud, "Tavern Next Round");
                Assert.That(fixture.BattleStarts, Is.EqualTo(1));
                Assert.That(fixture.Hud.IsVisible, Is.False);
                Assert.That(fixture.Run.RoundNumber, Is.EqualTo(round));
                Assert.That(fixture.Run.Gold, Is.EqualTo(gold));
                Assert.That(fixture.Run.ShopOffers, Is.EqualTo(offers));
                Assert.That(fixture.Run.Board[0], Is.SameAs(deployed));
                Assert.That(fixture.Run.Inventory.Single(), Is.SameAs(stored));
                fixture.Hud.Show(fixture.Run);
                Assert.That(Text(fixture.Hud, "Tavern Selected Inventory"), Does.Contain("#" + stored.InstanceId));
                Assert.That(Button(fixture.Hud, "Tavern Deploy").interactable, Is.True);
                Assert.That(Text(fixture.Hud, "Tavern Wallet"), Does.Contain("라운드 " + round));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator GrowingInventoryScrolls_AndDuplicateSkillsKeepSeparateSelectionsAndSales()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                for (int i = 0; i < 12; i++) fixture.Buy();
                var scroll = Find(fixture.Hud.Root, "Tavern Inventory Scroll").GetComponent<ScrollRect>();
                Assert.That(scroll.horizontal, Is.True);
                Assert.That(scroll.content.sizeDelta.x, Is.GreaterThan(scroll.viewport.rect.width));
                Assert.That(scroll.horizontalScrollbar, Is.Not.Null);
                var copies = fixture.Run.Inventory.GroupBy(card => card.Skill.Id).First(group => group.Count() > 1).ToArray();
                SkillTavernCard removed = copies[0], retained = copies[1];
                Click(fixture.Hud, "Tavern Inventory Card " + removed.InstanceId);
                Assert.That(Text(fixture.Hud, "Tavern Selected Inventory"), Does.Contain("#" + removed.InstanceId));
                Click(fixture.Hud, "Tavern Sell Inventory");
                Assert.That(fixture.Run.Inventory.Any(card => card.InstanceId == removed.InstanceId), Is.False);
                Assert.That(fixture.Run.Inventory.Any(card => card.InstanceId == retained.InstanceId), Is.True);
                Click(fixture.Hud, "Tavern Inventory Card " + retained.InstanceId);
                Assert.That(Text(fixture.Hud, "Tavern Selected Inventory"), Does.Contain("#" + retained.InstanceId));
                Assert.That(Find(fixture.Hud.Root, "Tavern Inventory Empty"), Is.Null);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReopeningRetainsTheExperiment_AndNeverDuplicatesCommandListeners()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                SkillTavernCard bought = fixture.Buy();
                Click(fixture.Hud, "Tavern Inventory Card " + bought.InstanceId);
                for (int i = 0; i < 4; i++) { fixture.Hud.Hide(); fixture.Hud.Show(fixture.Run); }
                Assert.That(fixture.Hud.Run, Is.SameAs(fixture.Run));
                Assert.That(fixture.Run.Inventory.Single(), Is.SameAs(bought));
                Assert.That(Text(fixture.Hud, "Tavern Selected Inventory"), Does.Contain("#" + bought.InstanceId));
                int before = fixture.Commands;
                Click(fixture.Hud, "Tavern Deploy");
                Assert.That(fixture.Commands, Is.EqualTo(before + 1));
                Click(fixture.Hud, "Tavern Reset");
                Assert.That(fixture.Resets, Is.EqualTo(1));
                Assert.That(fixture.Run.Board.All(card => card == null), Is.True);
                Assert.That(fixture.Run.Inventory, Is.Empty);
                Assert.That(fixture.Run.RoundNumber, Is.EqualTo(1));
                Assert.That(Button(fixture.Hud, "Tavern Deploy").interactable, Is.False);
                Button next = Button(fixture.Hud, "Tavern Next Round");
                Click(fixture.Hud, "Tavern Return");
                Assert.That(fixture.Returns, Is.EqualTo(1));
                Assert.That(fixture.Hud.IsVisible, Is.False);
                before = fixture.Commands;
                next.onClick.Invoke();
                Assert.That(fixture.Commands, Is.EqualTo(before), "Hidden controls cannot issue commands.");
                fixture.Hud.Show(fixture.Run);
                fixture.Hud.Dispose();
                next.onClick.Invoke();
                Assert.That(fixture.Commands, Is.EqualTo(before), "Disposal detaches the commands.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator WithoutAnAuthorityTheViewAllowsInspectionAndBlocksEconomyActions()
        {
            yield return null;
            using (var fixture = new Fixture(false))
            {
                for (int i = 0; i < SkillTavernRun.ShopSize; i++)
                {
                    Assert.That(Button(fixture.Hud, "Tavern Buy " + i).interactable, Is.False);
                    Assert.That(Button(fixture.Hud, "Tavern Buy And Deploy " + i).interactable, Is.False);
                }
                Assert.That(Button(fixture.Hud, "Tavern Next Round").interactable, Is.False);
                Button refresh = Button(fixture.Hud, "Tavern Refresh Shop");
                Assert.That(refresh.interactable, Is.False);
                LegacySkill[] offers = fixture.Run.ShopOffers.ToArray();
                refresh.onClick.Invoke();
                Assert.That(fixture.Run.ShopOffers, Is.EqualTo(offers));
                Click(fixture.Hud, "Tavern Shop Card 0");
                Assert.That(Text(fixture.Hud, "Tavern Detail Name"), Is.EqualTo(fixture.Run.ShopOffers[0].Name));
                Assert.That(fixture.Commands, Is.Zero);
                Assert.That(fixture.Run.Gold, Is.EqualTo(3));
                Assert.That(fixture.Run.Inventory, Is.Empty);
            }
            yield return null;
        }

        private static Transform Find(GameObject root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name && child.gameObject.activeInHierarchy) return child;
            return null;
        }

        private static Button Button(SkillTavernHud hud, string name)
        {
            Transform found = Find(hud.Root, name);
            Assert.That(found, Is.Not.Null, name);
            var button = found.GetComponent<Button>();
            Assert.That(button, Is.Not.Null, name);
            return button;
        }

        private static void Click(SkillTavernHud hud, string name)
        {
            Button button = Button(hud, name);
            Assert.That(button.interactable, Is.True, name);
            button.onClick.Invoke();
        }

        private static string Text(SkillTavernHud hud, string name)
        {
            Transform found = Find(hud.Root, name);
            Assert.That(found, Is.Not.Null, name);
            return found.GetComponent<Text>().text;
        }
    }
}
