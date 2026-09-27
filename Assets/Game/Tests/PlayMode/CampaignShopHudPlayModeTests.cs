using System;
using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class CampaignShopHudPlayModeTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly GameObject CanvasRoot;
            public readonly RectTransform Panel;
            public readonly CampaignRun Run = new CampaignRun();
            public readonly CampaignShopHud.ViewState State = new CampaignShopHud.ViewState();
            public CampaignShopHud Hud;
            public int AcquireCalls, UpgradeCalls;

            public Fixture()
            {
                CanvasRoot = new GameObject("Shop View Test Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasScaler));
                var canvas = CanvasRoot.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 600;
                var scaler = CanvasRoot.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = .5f;
                Panel = new GameObject("Shop Test Panel", typeof(RectTransform)).GetComponent<RectTransform>();
                Panel.SetParent(CanvasRoot.transform, false);
                Panel.anchorMin = Panel.anchorMax = Panel.pivot = Vector2.one * .5f;
                Panel.sizeDelta = new Vector2(1300f, 850f);
                Build();
            }

            public void Build()
            {
                Hud?.Dispose();
                Hud = new CampaignShopHud(Panel, new LegacyDuelArt(), Run,
                    id => { AcquireCalls++; Assert.That(Run.TryAcquireSkill(id), Is.True); },
                    id => { UpgradeCalls++; Assert.That(Run.TryUpgradeSkill(id), Is.True); }, State);
                Canvas.ForceUpdateCanvases();
            }

            public void GrantWins(int count)
            {
                for (int stage = 1; stage <= count; stage++)
                {
                    Assert.That(Run.TryStartStage(stage), Is.True);
                    Assert.That(Run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                    Assert.That(Run.ReturnToLobby(), Is.True);
                }
                Hud.Refresh();
            }

            public void Dispose() { Hud?.Dispose(); Object.Destroy(CanvasRoot); }
        }

        [UnityTest]
        public IEnumerator ShopSelection_UsesCompactListOneTransactionAndExplicitInsufficientReason()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                int cardCount = 0;
                foreach (Button button in fixture.Hud.Root.GetComponentsInChildren<Button>(true))
                    if (button.name.StartsWith("Shop Skill ", StringComparison.Ordinal))
                    {
                        Assert.That(button.GetComponentsInChildren<Text>(true).Length, Is.EqualTo(3),
                            "A list card contains only its name, ACT and ownership/level state.");
                        cardCount++;
                    }
                Assert.That(cardCount, Is.EqualTo(CampaignSkillCatalog.AcquisitionSkills.Count));
                Assert.That(Button(fixture.Hud.Root, "Shop Primary Action").interactable, Is.False);
                Assert.That(Label(fixture.Hud.Root, "Shop Availability").text, Is.EqualTo("재화 45 부족"));
                Assert.That(Label(fixture.Hud.Root, "Shop Wallet").text, Is.EqualTo("보유 재화 0"));
                Button(fixture.Hud.Root, "Shop Skill 16").onClick.Invoke();
                Assert.That(fixture.State.SelectedSkillId, Is.EqualTo(16));
                Assert.That(Label(fixture.Hud.Root, "Shop Detail Name").text, Is.EqualTo("일도양단"));
                Assert.That(Label(fixture.Hud.Root, "Shop Detail Effect").text, Does.Contain("최대 위력이 보장되지는"));
                Assert.That(Label(fixture.Hud.Root, "Shop Price").text, Is.EqualTo("구매 비용 85"));
                Assert.That(Label(fixture.Hud.Root, "Shop Availability").text, Is.EqualTo("재화 85 부족"));
                Button(fixture.Hud.Root, "Shop Primary Action").onClick.Invoke();
                Assert.That(fixture.AcquireCalls, Is.Zero, "Selection and disabled action cannot transact.");
                Assert.That(fixture.Run.Currency, Is.Zero);
                foreach (Button button in fixture.Hud.Root.GetComponentsInChildren<Button>(true))
                    Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.None));
            }
        }

        [UnityTest]
        public IEnumerator ShopPurchase_PaysOnceKeepsPurchasedSelectionAndDoesNotEquip()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                fixture.GrantWins(1);
                Button(fixture.Hud.Root, "Shop Skill 16").onClick.Invoke();
                Assert.That(Label(fixture.Hud.Root, "Shop Availability").text, Is.EqualTo("재화 25 부족"));
                Button(fixture.Hud.Root, "Shop Skill 14").onClick.Invoke();
                Assert.That(fixture.Run.Currency, Is.EqualTo(60));
                Assert.That(fixture.AcquireCalls, Is.Zero);
                var primary = Button(fixture.Hud.Root, "Shop Primary Action");
                Assert.That(primary.interactable, Is.True);
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(primary.gameObject);
                primary.onClick.Invoke();
                if (EventSystem.current != null) Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
                Assert.That(fixture.AcquireCalls, Is.EqualTo(1));
                Assert.That(fixture.Run.Currency, Is.EqualTo(15));
                Assert.That(fixture.Run.OwnedSkills.Count, Is.EqualTo(10));
                Assert.That(fixture.Run.IsSkillInLoadout(14), Is.False);
                Assert.That(fixture.Run.IsSkillEquipped(14), Is.False);
                Assert.That(fixture.State.SelectedSkillId, Is.EqualTo(14));
                Assert.That(primary.interactable, Is.False);
                Assert.That(primary.GetComponentInChildren<Text>().text, Is.EqualTo("획득 완료"));
                Assert.That(Label(fixture.Hud.Root, "Shop Availability").text, Does.Contain("편성 화면에서 장착"));
                primary.onClick.Invoke();
                Assert.That(fixture.AcquireCalls, Is.EqualTo(1));
                fixture.Build();
                Assert.That(fixture.State.SelectedSkillId, Is.EqualTo(14));
                Assert.That(Label(fixture.Hud.Root, "Shop Detail Name").text, Is.EqualTo("가로베기"));
                Assert.That(Label(Button(fixture.Hud.Root, "Shop Skill 14").gameObject, "Shop Card State").text,
                    Is.EqualTo("획득 완료"));
                Assert.That(Button(fixture.Hud.Root, "Shop Primary Action").interactable, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator ShopUpgrade_PreviewsActualNextPowerUpdatesCostsAndStopsAtMaximum()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                fixture.GrantWins(3);
                Button(fixture.Hud.Root, "Shop Category Upgrade").onClick.Invoke();
                Button(fixture.Hud.Root, "Shop Skill 1").onClick.Invoke();
                Assert.That(fixture.UpgradeCalls, Is.Zero);
                Assert.That(Label(fixture.Hud.Root, "Shop Upgrade Preview").text, Does.Contain("0/3 → 1/3").And.Contain("4–5 → 6–7"));
                Assert.That(Label(fixture.Hud.Root, "Shop Price").text, Is.EqualTo("강화 비용 30"));
                Assert.That(fixture.Run.Currency, Is.EqualTo(210));
                Button(fixture.Hud.Root, "Shop Primary Action").onClick.Invoke();
                Assert.That(Label(fixture.Hud.Root, "Shop Detail Name").text, Is.EqualTo("베기 +1"));
                Assert.That(Label(fixture.Hud.Root, "Shop Upgrade Preview").text, Does.Contain("6–7 → 8–9"));
                Assert.That(Label(fixture.Hud.Root, "Shop Price").text, Is.EqualTo("강화 비용 55"));
                Assert.That(Label(fixture.Hud.Root, "Shop Wallet").text, Is.EqualTo("보유 재화 180"));
                Button(fixture.Hud.Root, "Shop Primary Action").onClick.Invoke();
                Assert.That(Label(fixture.Hud.Root, "Shop Price").text, Is.EqualTo("강화 비용 80"));
                Button(fixture.Hud.Root, "Shop Primary Action").onClick.Invoke();
                Assert.That(fixture.UpgradeCalls, Is.EqualTo(3));
                Assert.That(fixture.Run.Currency, Is.EqualTo(45));
                Assert.That(fixture.Run.OwnedSkills[0].Level, Is.EqualTo(3));
                Assert.That(Label(fixture.Hud.Root, "Shop Detail Values").text, Does.Contain("10–11"));
                Assert.That(Label(fixture.Hud.Root, "Shop Upgrade Preview").text, Is.EqualTo("강화 3/3 · 최대 단계"));
                Assert.That(Button(fixture.Hud.Root, "Shop Primary Action").interactable, Is.False);
                Assert.That(Button(fixture.Hud.Root, "Shop Primary Action").GetComponentInChildren<Text>().text, Is.EqualTo("강화 완료"));
                Button(fixture.Hud.Root, "Shop Primary Action").onClick.Invoke();
                Assert.That(fixture.UpgradeCalls, Is.EqualTo(3));
            }
        }

        [UnityTest]
        public IEnumerator ShopCategories_KeepSeparateSelectionAndScrollAcrossRebuildAndDispose()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                fixture.GrantWins(8);
                foreach (var skill in CampaignSkillCatalog.AcquisitionSkills)
                    Assert.That(fixture.Run.TryAcquireSkill(skill.Id), Is.True);
                fixture.Hud.Refresh();
                fixture.Hud.SelectSkill(16);
                fixture.Hud.SelectCategory(CampaignShopHud.ShopCategory.Upgrade);
                fixture.Hud.SelectSkill(8);
                Assert.That(Label(fixture.Hud.Root, "Shop Damage Hint").text, Is.Empty);
                Assert.That(Label(fixture.Hud.Root, "Shop Detail Effect").text, Does.Contain("30%").And.Contain("이번 턴"));
                Canvas.ForceUpdateCanvases();
                var scroll = Named(fixture.Hud.Root, "Shop Skill List").GetComponent<ScrollRect>();
                Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
                scroll.verticalNormalizedPosition = .2f;
                Canvas.ForceUpdateCanvases();
                fixture.Build();
                Assert.That(fixture.State.Category, Is.EqualTo(CampaignShopHud.ShopCategory.Upgrade));
                Assert.That(fixture.State.SelectedSkillId, Is.EqualTo(8));
                Assert.That(Named(fixture.Hud.Root, "Shop Skill List").GetComponent<ScrollRect>().verticalNormalizedPosition,
                    Is.EqualTo(.2f).Within(.01f));
                fixture.Hud.SelectCategory(CampaignShopHud.ShopCategory.Purchase);
                Assert.That(fixture.State.SelectedSkillId, Is.EqualTo(16));
                fixture.Hud.SelectCategory(CampaignShopHud.ShopCategory.Upgrade);
                Assert.That(fixture.State.SelectedSkillId, Is.EqualTo(8));
                int nodes = fixture.Hud.Root.GetComponentsInChildren<Transform>(true).Length;
                for (int i = 0; i < 10; i++) fixture.Hud.Refresh();
                Assert.That(fixture.Hud.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(nodes));
                var primary = Button(fixture.Hud.Root, "Shop Primary Action");
                int currency = fixture.Run.Currency;
                fixture.Hud.Dispose();
                fixture.Hud.Dispose();
                primary.onClick.Invoke();
                Assert.That(fixture.Run.Currency, Is.EqualTo(currency));
                Assert.That(fixture.UpgradeCalls, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator ShopUpgrade_InsufficientFundsCannotModifyPowerAndListDecorationsDoNotBlockSelection()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                fixture.Hud.SelectCategory(CampaignShopHud.ShopCategory.Upgrade);
                fixture.Hud.SelectSkill(1);
                Assert.That(Label(fixture.Hud.Root, "Shop Availability").text, Is.EqualTo("재화 30 부족"));
                Assert.That(Button(fixture.Hud.Root, "Shop Primary Action").interactable, Is.False);
                Button(fixture.Hud.Root, "Shop Primary Action").onClick.Invoke();
                Assert.That(fixture.UpgradeCalls, Is.Zero);
                Assert.That(fixture.Run.OwnedSkills[0].Level, Is.Zero);
                Assert.That(fixture.Run.OwnedSkills[0].Skill.MinPower, Is.EqualTo(4));
                foreach (Graphic graphic in Button(fixture.Hud.Root, "Shop Skill 1").GetComponentsInChildren<Graphic>(true))
                    if (graphic.GetComponent<Button>() == null) Assert.That(graphic.raycastTarget, Is.False);
                Assert.That(fixture.Run.TryStartStage(1), Is.True);
                fixture.Hud.Refresh();
                Assert.That(Label(fixture.Hud.Root, "Shop Availability").text, Does.Contain("로비에서"));
                Assert.That(Button(fixture.Hud.Root, "Shop Primary Action").interactable, Is.False);
            }
        }

        private static Transform Named(GameObject root, string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true)) if (candidate.name == name) return candidate;
            Assert.Fail("Missing shop node: " + name);
            return null;
        }

        private static Text Label(GameObject root, string name) => Named(root, name).GetComponent<Text>();
        private static Button Button(GameObject root, string name) => Named(root, name).GetComponent<Button>();
    }
}
