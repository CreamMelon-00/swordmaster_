using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Dialogue;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class DialogueHudPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator DialogueHud_ReusesOneHierarchyForNarratorLeftAndRightLines()
        {
            yield return null;
            var parent = new GameObject("Dialogue HUD Test");
            DialogueHud hud = null;
            try
            {
                hud = new DialogueHud(parent.transform, new LegacyDuelArt(), null, null);
                Assert.That(hud.IsVisible, Is.False);
                int nodeCount = hud.Root.GetComponentsInChildren<Transform>(true).Length;
                DialogueScript script = Script();

                hud.Show(script.Lines[0], 0, script.Lines.Count);
                Assert.That(hud.IsVisible, Is.True);
                Assert.That(hud.Root.GetComponent<Canvas>().sortingOrder, Is.EqualTo(500));
                Assert.That(hud.Root.GetComponent<CanvasScaler>().referenceResolution,
                    Is.EqualTo(new Vector2(1920f, 1080f)));
                Assert.That(Named(hud.Root, "Dialogue Nameplate").activeSelf, Is.False);
                Assert.That(Label(hud.Root, "Dialogue Body").text, Is.EqualTo("나레이션 문장"));
                Assert.That(Label(hud.Root, "Dialogue Progress").text, Is.EqualTo("01 / 03"));

                hud.Show(script.Lines[1], 1, script.Lines.Count);
                Assert.That(Named(hud.Root, "Dialogue Nameplate").activeSelf, Is.True);
                Assert.That(Label(hud.Root, "Dialogue Speaker").text, Is.EqualTo("왼쪽 화자"));
                Assert.That(Label(hud.Root, "Dialogue Role").text, Is.EqualTo("왼쪽 역할"));
                Assert.That(Named(hud.Root, "Dialogue Nameplate").GetComponent<RectTransform>().anchoredPosition.x,
                    Is.LessThan(0f));

                hud.Show(script.Lines[2], 2, script.Lines.Count);
                Assert.That(Label(hud.Root, "Dialogue Speaker").text, Is.EqualTo("오른쪽 화자"));
                Assert.That(Named(hud.Root, "Dialogue Nameplate").GetComponent<RectTransform>().anchoredPosition.x,
                    Is.GreaterThan(0f));
                Assert.That(hud.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(nodeCount));
                Canvas.ForceUpdateCanvases();
                Text body = Label(hud.Root, "Dialogue Body");
                Assert.That(body.preferredHeight, Is.LessThanOrEqualTo(body.rectTransform.rect.height + 1f));
            }
            finally
            {
                hud?.Dispose();
                Object.Destroy(parent);
            }
        }

        [UnityTest]
        public IEnumerator CinematicDialogue_RevealsTextMasksTheNameAndShowsBothChoices()
        {
            yield return null;
            var parent = new GameObject("Dialogue Choice Test");
            DialogueHud hud = null;
            try
            {
                hud = new DialogueHud(parent.transform, new LegacyDuelArt(), null, null);
                hud.SetCinematic(true);
                hud.MaskElisaName = true;
                var line = new DialogueLine(1, DialogueSide.Left, "엘리사", "???",
                    "엘리사라는 이름을 들었다.");
                hud.Show(line, 0, 1);
                Assert.That(Label(hud.Root, "Dialogue Speaker").text, Is.EqualTo("???"));
                Assert.That(hud.IsRevealing, Is.True);
                Assert.That(Label(hud.Root, "Dialogue Body").text, Is.Empty);
                hud.Tick(.1f);
                Assert.That(Label(hud.Root, "Dialogue Body").text.Length, Is.InRange(1, 5));
                hud.CompleteReveal();
                Assert.That(Label(hud.Root, "Dialogue Body").text, Is.EqualTo("???라는 이름을 들었다."));

                int selected = -1;
                hud.ShowChoices("날 보내줘", "…", index => selected = index);
                Button top = Button(hud.Root, "Dialogue Choice A");
                Button bottom = Button(hud.Root, "Dialogue Choice B");
                Assert.That(top.GetComponentInChildren<Text>().text, Is.EqualTo("날 보내줘"));
                Assert.That(bottom.GetComponentInChildren<Text>().text, Is.EqualTo("…"));
                Assert.That(top.GetComponent<RectTransform>().anchoredPosition.x,
                    Is.EqualTo(bottom.GetComponent<RectTransform>().anchoredPosition.x));
                Assert.That(top.GetComponent<RectTransform>().anchoredPosition.y,
                    Is.GreaterThan(bottom.GetComponent<RectTransform>().anchoredPosition.y));
                Assert.That(Label(hud.Root, "Dialogue Input Hint").text, Does.Contain("↑ / ↓"));
                hud.MoveChoiceSelection(1);
                Assert.That(bottom.image.color, Is.EqualTo(DuelVisualTheme.Accent));
                hud.ConfirmChoiceSelection();
                Assert.That(selected, Is.EqualTo(1));
                Assert.That(hud.IsChoosing, Is.False);
            }
            finally
            {
                hud?.Dispose();
                Object.Destroy(parent);
            }
        }

        [UnityTest]
        public IEnumerator DialogueHud_BlocksUnderlyingClicksAndInvokesOnlyRequestedActions()
        {
            yield return null;
            var parent = new GameObject("Dialogue Action Test");
            DialogueHud hud = null;
            try
            {
                int advances = 0, closes = 0;
                hud = new DialogueHud(parent.transform, new LegacyDuelArt(), () => advances++, () => closes++);
                hud.Show(Script().Lines[1], 1, 3);
                Image backdrop = Named(hud.Root, "Dialogue Backdrop").GetComponent<Image>();
                Assert.That(backdrop.raycastTarget, Is.True);
                Assert.That(backdrop.GetComponent<Button>().navigation.mode, Is.EqualTo(Navigation.Mode.None));
                Assert.That(Button(hud.Root, "Dialogue Next").navigation.mode, Is.EqualTo(Navigation.Mode.None));
                Assert.That(Button(hud.Root, "Dialogue Close").navigation.mode, Is.EqualTo(Navigation.Mode.None));

                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(Button(hud.Root, "Dialogue Next").gameObject);
                hud.RequestAdvance();
                Assert.That(advances, Is.EqualTo(1));
                Assert.That(closes, Is.Zero);
                if (EventSystem.current != null)
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);

                Button(hud.Root, "Dialogue Close").onClick.Invoke();
                Assert.That(closes, Is.EqualTo(1));
                Assert.That(advances, Is.EqualTo(1));
                hud.Hide();
                Assert.That(hud.CurrentLine, Is.Null);
                Assert.That(hud.IsVisible, Is.False);
            }
            finally
            {
                hud?.Dispose();
                Object.Destroy(parent);
            }
        }

        [UnityTest]
        public IEnumerator DialogueHud_KeepsBothPortraitsAndDimsTheInactiveSpeaker()
        {
            yield return null;
            var parent = new GameObject("Dialogue Portrait Test");
            var texture = new Texture2D(2, 2);
            Sprite leftSprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            Sprite rightSprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            DialogueHud hud = null;
            try
            {
                hud = new DialogueHud(parent.transform, new LegacyDuelArt(), null, null);
                DialogueScript script = DialogueScriptParser.Parse("portrait-stage",
                    "@show left 왼쪽 화자 | 왼쪽 역할\n" +
                    "@show right 오른쪽 화자 | 오른쪽 역할\n" +
                    "@narrator\n나레이션 문장\n" +
                    "@left\n왼쪽 대사\n" +
                    "@right\n오른쪽 대사\n" +
                    "@hide left\n@narrator\n왼쪽 퇴장\n" +
                    "@move right left\n이동 완료");
                Image leftPortrait = Named(hud.Root, "Dialogue Portrait Left").GetComponent<Image>();
                Image rightPortrait = Named(hud.Root, "Dialogue Portrait Right").GetComponent<Image>();
                int nodeCount = hud.Root.GetComponentsInChildren<Transform>(true).Length;

                hud.Show(script.Lines[0], 0, script.Lines.Count, leftSprite, rightSprite);
                Assert.That(leftPortrait.gameObject.activeSelf, Is.True);
                Assert.That(rightPortrait.gameObject.activeSelf, Is.True);
                Assert.That(leftPortrait.sprite, Is.SameAs(leftSprite));
                Assert.That(rightPortrait.sprite, Is.SameAs(rightSprite));
                AssertInactiveTint(leftPortrait);
                AssertInactiveTint(rightPortrait);

                hud.Show(script.Lines[1], 1, script.Lines.Count, leftSprite, rightSprite);
                AssertActiveTint(leftPortrait);
                AssertInactiveTint(rightPortrait);

                hud.Show(script.Lines[2], 2, script.Lines.Count, leftSprite, rightSprite);
                AssertInactiveTint(leftPortrait);
                AssertActiveTint(rightPortrait);

                hud.Show(script.Lines[3], 3, script.Lines.Count, null, rightSprite);
                Assert.That(leftPortrait.gameObject.activeSelf, Is.False);
                Assert.That(rightPortrait.gameObject.activeSelf, Is.True);
                AssertInactiveTint(rightPortrait);

                hud.Show(script.Lines[4], 4, script.Lines.Count, rightSprite, null);
                Assert.That(leftPortrait.gameObject.activeSelf, Is.True);
                Assert.That(leftPortrait.sprite, Is.SameAs(rightSprite));
                Assert.That(rightPortrait.gameObject.activeSelf, Is.False);
                AssertInactiveTint(leftPortrait);
                Assert.That(hud.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(nodeCount));

                Canvas.ForceUpdateCanvases();
                RectTransform rootRect = hud.Root.GetComponent<RectTransform>();
                Bounds leftBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    rootRect, leftPortrait.rectTransform);
                Assert.That(leftBounds.max.y, Is.LessThanOrEqualTo(rootRect.rect.yMax + 1f),
                    "Portrait art must remain below the top edge instead of clipping the character's head.");
                Bounds cardBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    rootRect, Named(hud.Root, "Dialogue Card Border").GetComponent<RectTransform>());
                Assert.That(leftBounds.min.y, Is.LessThan(cardBounds.max.y),
                    "The dialogue card should overlap the portrait's lower edge in visual-novel style.");
                Assert.That(leftPortrait.transform.GetSiblingIndex(),
                    Is.LessThan(Named(hud.Root, "Dialogue Card Border").transform.GetSiblingIndex()),
                    "Standing portraits must render behind the dialogue card.");

                hud.Hide();
                Assert.That(leftPortrait.gameObject.activeSelf, Is.False);
                Assert.That(rightPortrait.gameObject.activeSelf, Is.False);
                Assert.That(leftPortrait.sprite, Is.Null);
                Assert.That(rightPortrait.sprite, Is.Null);
            }
            finally
            {
                hud?.Dispose();
                Object.Destroy(parent);
                Object.Destroy(leftSprite);
                Object.Destroy(rightSprite);
                Object.Destroy(texture);
            }
        }

        [UnityTest]
        public IEnumerator Controller_ResolvesAndKeepsBothStagePortraitsFromTheCatalog()
        {
            yield return null;
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            controller.ReturnToLobby();
            var catalog = ScriptableObject.CreateInstance<DialoguePortraitCatalog>();
            var texture = new Texture2D(2, 2);
            Sprite leftSprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            Sprite rightSprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            try
            {
                catalog.SetPortraitForEditor("왼쪽 화자", leftSprite);
                catalog.SetPortraitForEditor("오른쪽 화자", rightSprite);
                DialogueScript script = DialogueScriptParser.Parse("controller-portraits",
                    "@right 오른쪽 화자\n오른쪽 대사\n@left 왼쪽 화자\n왼쪽 대사");

                Assert.That(controller.StartDialogue(script, catalog), Is.True);
                Image leftPortrait = Named(controller.DialogueHud.Root,
                    "Dialogue Portrait Left").GetComponent<Image>();
                Image rightPortrait = Named(controller.DialogueHud.Root,
                    "Dialogue Portrait Right").GetComponent<Image>();
                Assert.That(leftPortrait.gameObject.activeSelf, Is.False);
                Assert.That(rightPortrait.gameObject.activeSelf, Is.True);
                Assert.That(rightPortrait.sprite, Is.SameAs(rightSprite));
                AssertActiveTint(rightPortrait);

                Assert.That(controller.ContinueDialogue(), Is.True);
                Assert.That(leftPortrait.gameObject.activeSelf, Is.True);
                Assert.That(rightPortrait.gameObject.activeSelf, Is.True);
                Assert.That(leftPortrait.sprite, Is.SameAs(leftSprite));
                Assert.That(rightPortrait.sprite, Is.SameAs(rightSprite));
                AssertActiveTint(leftPortrait);
                AssertInactiveTint(rightPortrait);
            }
            finally
            {
                controller.CloseDialogue();
                Object.Destroy(leftSprite);
                Object.Destroy(rightSprite);
                Object.Destroy(texture);
                Object.Destroy(catalog);
            }
        }

        [UnityTest]
        public IEnumerator Controller_DialogueTemporarilyOwnsInputAndRestoresLobbyAfterCompletion()
        {
            yield return null;
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            controller.ReturnToLobby();
            int currency = controller.Campaign.Currency;
            DialogueScript script = DialogueScriptParser.Parse("controller-test", "첫 줄\n둘째 줄");

            Assert.That(controller.StartDialogue(script), Is.True);
            Assert.That(controller.IsShowingDialogue, Is.True);
            Assert.That(controller.IsInLobby, Is.False);
            Assert.That(controller.StartCampaignStage(1), Is.False,
                "Dialogue must block lobby actions while its modal is visible.");
            Assert.That(controller.Campaign.Phase, Is.EqualTo(CampaignPhase.Lobby));
            Assert.That(controller.Campaign.Currency, Is.EqualTo(currency));
            Assert.That(controller.ContinueDialogue(), Is.True);
            Assert.That(controller.CurrentDialogueLine.Text, Is.EqualTo("둘째 줄"));
            Assert.That(controller.ContinueDialogue(), Is.False);
            Assert.That(controller.IsShowingDialogue, Is.False);
            Assert.That(controller.IsInLobby, Is.True);
            Assert.That(controller.Campaign.Currency, Is.EqualTo(currency));
        }

        [UnityTest]
        public IEnumerator Controller_EnterAdvancesAndFinalEnterDoesNotLeakIntoLobby()
        {
            yield return null;
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            controller.ReturnToLobby();
            controller.LobbyHud.ShowTab(LobbyTab.Stages);
            Assert.That(controller.StartDialogue(DialogueScriptParser.Parse("keyboard-test", "첫 줄\n둘째 줄")), Is.True);

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.enterKey);
            yield return null;
            Assert.That(controller.IsShowingDialogue, Is.True);
            Assert.That(controller.CurrentDialogueLine.Text, Is.EqualTo("둘째 줄"));
            Assert.That(controller.Campaign.Phase, Is.EqualTo(CampaignPhase.Lobby));
            Release(keyboard.enterKey);
            yield return null;

            Press(keyboard.enterKey);
            yield return null;
            Assert.That(controller.IsShowingDialogue, Is.False);
            Assert.That(controller.Campaign.Phase, Is.EqualTo(CampaignPhase.Lobby),
                "The Enter that closes dialogue must not start the selected stage in the same frame.");
            Release(keyboard.enterKey);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Controller_EscapeClosesOnlyDialogueWithoutChangingCampaignState()
        {
            yield return null;
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            controller.ReturnToLobby();
            int stage = controller.Campaign.StageNumber;
            int currency = controller.Campaign.Currency;
            Assert.That(controller.StartDialogue(DialogueScriptParser.Parse("escape-test", "닫기 검사")), Is.True);

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.escapeKey);
            yield return null;
            Release(keyboard.escapeKey);
            yield return null;

            Assert.That(controller.IsShowingDialogue, Is.False);
            Assert.That(controller.IsInLobby, Is.True);
            Assert.That(controller.Campaign.Phase, Is.EqualTo(CampaignPhase.Lobby));
            Assert.That(controller.Campaign.StageNumber, Is.EqualTo(stage));
            Assert.That(controller.Campaign.Currency, Is.EqualTo(currency));
        }

        [UnityTest]
        public IEnumerator DialogueHud_DrawsAThoughtInParenthesesInTheMonologueColour_WithOrWithoutASpeaker()
        {
            yield return null;
            var parent = new GameObject("Dialogue Monologue Test");
            DialogueHud hud = null;
            try
            {
                hud = new DialogueHud(parent.transform, new LegacyDuelArt(), null, null);
                DialogueScript script = DialogueScriptParser.Parse("monologue-test",
                    "(눈앞이 밝아졌다.)\n@left 엘리사 | ???\n(이 검은…)\n윽…");
                Text body = Label(hud.Root, "Dialogue Body");
                hud.Show(script.Lines[0], 0, 3);
                Assert.That(body.text, Is.EqualTo("(눈앞이 밝아졌다.)"), "The parentheses stay.");
                Assert.That(body.color, Is.EqualTo(DialogueHud.MonologueColor), "Narration can be a thought.");
                hud.Show(script.Lines[1], 1, 3);
                Assert.That(body.color, Is.EqualTo(DialogueHud.MonologueColor));
                Assert.That(Label(hud.Root, "Dialogue Speaker").text, Is.EqualTo("엘리사"));
                hud.Show(script.Lines[2], 2, 3);
                Assert.That(body.color, Is.EqualTo(DuelVisualTheme.Foreground), "Speech is back in the usual colour.");
                Assert.That(DialogueHud.MonologueColor, Is.Not.EqualTo(DuelVisualTheme.Foreground));
            }
            finally
            {
                hud?.Dispose();
                Object.Destroy(parent);
            }
        }

        private static DialogueScript Script()
            => DialogueScriptParser.Parse("hud-test",
                "나레이션 문장\n@left 왼쪽 화자 | 왼쪽 역할\n왼쪽 대사\n@right 오른쪽 화자 | 오른쪽 역할\n오른쪽 대사");

        private static GameObject Named(GameObject root, string name)
        {
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == name) return item.gameObject;
            Assert.Fail($"Could not find '{name}'.");
            return null;
        }

        private static Text Label(GameObject root, string name) => Named(root, name).GetComponent<Text>();
        private static Button Button(GameObject root, string name) => Named(root, name).GetComponent<Button>();

        private static void AssertActiveTint(Image portrait)
        {
            Assert.That(portrait.color.r, Is.EqualTo(1f).Within(.001f));
            Assert.That(portrait.color.g, Is.EqualTo(1f).Within(.001f));
            Assert.That(portrait.color.b, Is.EqualTo(1f).Within(.001f));
            Assert.That(portrait.color.a, Is.EqualTo(1f).Within(.001f));
        }

        private static void AssertInactiveTint(Image portrait)
        {
            Assert.That(portrait.color.r, Is.EqualTo(.42f).Within(.001f));
            Assert.That(portrait.color.g, Is.EqualTo(.42f).Within(.001f));
            Assert.That(portrait.color.b, Is.EqualTo(.42f).Within(.001f));
            Assert.That(portrait.color.a, Is.EqualTo(1f).Within(.001f));
        }
    }
}
