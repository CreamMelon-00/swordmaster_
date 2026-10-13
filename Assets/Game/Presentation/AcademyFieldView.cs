using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>
    /// Connected school and forest fields between story scenes and battles. Combat keeps its own camera and
    /// actors; this view only borrows their loaded sprite sets and leaves the duel untouched.
    /// </summary>
    public sealed class AcademyFieldView : IDisposable
    {
        private const int FieldLayer = 29;
        private const float GroundY = -1.45f;
        private const float StartingX = -29f;
        private const float TopBound = -0.55f;
        private const float BottomBound = -2.3f;
        private const float WalkSpeed = 4.5f;
        private const float InteractionRange = 2.8f;
        private const float CameraSize = 6f;
        private const float TransitionHalfDuration = .24f;
        // TEMPORARY field pickup test (Docs/FieldPickups.md): a technique lying in the field is its skill icon floating
        // over a small shadow, in a soft pale-brass halo. Heights are measured up from the ground line the actors stand on.
        private const float PickupIconSize = 1.1f;
        private const float PickupFloatHeight = 1.3f;
        private const float PickupBob = .09f;
        private const float PickupLabelHeight = 2.3f;
        private const float PickupHaloSize = 2.3f;
        private const float PickupShadowWidth = .9f;
        private const float PickupShadowHeight = .27f;
        private const float PickupFlourishDuration = .45f;
        // Brass itself is the forest path's own colour and vanishes on it; the glow is the same hue, paler, like lamplight.
        private static readonly Color PickupLight = new Color(1f, .93f, .67f, 1f);
        private static readonly Color Paper = new Color(.25f, .19f, .13f, 1f);
        private static readonly Color Brass = new Color(.77f, .61f, .35f, 1f);
        private static readonly Color Ink = new Color(.95f, .89f, .74f, 1f);

        private enum TargetKind { Stage, Story, Training, Portal, Pickup }

        private sealed class Target
        {
            public TargetKind Kind;
            public int StageNumber;
            public Transform Root;
            public SpriteRenderer Renderer;
            public Text Label;
            public string Name;
            public bool CanInteract;
            // TEMPORARY field pickup test only (Kind == Pickup): the saved pickup id and its glow pieces.
            public string PickupId;
            public SpriteRenderer Halo;
            public SpriteRenderer GroundGlow;
            public SpriteRenderer Shadow;
            public float Phase;
        }

        private readonly GameObject root;
        private readonly Camera camera;
        private readonly ExplorationFieldBackdrop backdrop;
        private readonly SpriteRenderer player;
        private readonly Transform playerGround;
        private readonly MobStudentAnimationSet playerAnimations;
        private readonly EnemyStudentAnimationSet cadetA;
        private readonly EnemyStudentAnimationSet cadetB;
        private readonly TrainingDummyAnimationSet dummyAnimations;
        private readonly RectTransform uiRoot;
        private readonly Text prompt;
        private readonly Text arrivalText;
        private readonly Text locationText;
        private readonly Image transitionDim;
        private readonly Target storyBoard;
        private readonly Target trainingDummy;
        private readonly Target portal;
        private readonly List<Target> stageTargets = new List<Target>();
        private readonly List<Target> pickupTargets = new List<Target>();
        private readonly Action<int> onStage;
        private readonly Action onStory;
        private readonly Action onTraining;
        private readonly Action onMenu;
        private readonly Action<string> onPickup;
        private readonly LegacyDuelArt art;
        private readonly Font font;
        private readonly Sprite white;
        private readonly Material spriteMaterial;
        private readonly Texture2D glowTexture;
        private readonly Sprite glowSprite;
        private readonly Transform pickupFlourish;
        private readonly SpriteRenderer flourishIcon;
        private readonly SpriteRenderer flourishHalo;
        private CampaignRun run;
        private string storyStatus;
        private float animationTime;
        private float moveTime;
        private float arrivalRemaining;
        private float flourishRemaining;
        private float transitionElapsed;
        private bool hasEntered;
        private bool trainingAvailable;
        private bool facingRight = true;
        private bool disposed;

        public bool IsVisible => !disposed && root.activeSelf;
        public ExplorationFieldMap CurrentMap => backdrop.CurrentMap;
        public bool IsTransitioning { get; private set; }
        public Camera FieldCamera => camera;
        public Vector2 PlayerPosition => playerGround.localPosition;
        public Vector2 PortalPosition => portal.Root.localPosition;
        public int VisibleStageCount { get; private set; }
        public Vector2 StagePosition(int stageNumber)
        {
            foreach (Target target in stageTargets)
                if (target.StageNumber == stageNumber && target.Root.gameObject.activeSelf)
                    return target.Root.localPosition;
            throw new ArgumentOutOfRangeException(nameof(stageNumber));
        }

        /// <summary>The bottom prompt as shown now: <c>Space · …</c> for the nearest target, a locked reason, or empty.</summary>
        public string InteractionPrompt => prompt.text;
        /// <summary>The top-right notice while it is on screen, else empty.</summary>
        public string ArrivalNotice => arrivalRemaining > 0f ? arrivalText.text : string.Empty;
        /// <summary>TEMPORARY field pickup test: the pickups drawn on the current map (the ones the run has not taken).</summary>
        public int VisiblePickupCount { get; private set; }
        /// <summary>TEMPORARY field pickup test: whether this pickup lies on the visible map, not yet taken.</summary>
        public bool IsPickupVisible(string pickupId)
        {
            Target target = FindPickupTarget(pickupId);
            return IsVisible && target != null && target.Root.gameObject.activeSelf;
        }
        /// <summary>TEMPORARY field pickup test: where the pickup lies, in the walker's ground coordinates.</summary>
        public Vector2 PickupPosition(string pickupId)
        {
            Target target = FindPickupTarget(pickupId);
            if (target == null) throw new ArgumentOutOfRangeException(nameof(pickupId));
            return target.Root.localPosition;
        }

        /// <param name="onPickup">TEMPORARY field pickup test (Docs/FieldPickups.md): Space or a click beside a technique
        /// lying in the field passes its pickup id (<see cref="CampaignFieldPickups"/>). The callback takes it through the
        /// run (<see cref="CampaignRun.TryCollectFieldPickup(string, out CampaignOwnedSkill)"/>); the field then hides
        /// every pickup the run has taken.</param>
        public AcademyFieldView(Transform parent, LegacyDuelArt art, LegacyArenaView arena,
            Action<int> onStage, Action onStory, Action onTraining, Action onMenu, Action<string> onPickup = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (art == null) throw new ArgumentNullException(nameof(art));
            if (arena == null) throw new ArgumentNullException(nameof(arena));
            this.onStage = onStage;
            this.onStory = onStory;
            this.onTraining = onTraining;
            this.onMenu = onMenu;
            this.onPickup = onPickup;
            this.art = art;
            font = art.UIFont;
            white = Resources.Load<Sprite>("LegacyHud/white");
            spriteMaterial = arena.SpriteMaterial;
            playerAnimations = arena.PlayerAnimations;
            dummyAnimations = arena.DummyAnimations;
            cadetA = new EnemyStudentAnimationSet(EnemyStudentAnimationSet.CadetAResourceRoot, "field-cadet-a");
            cadetB = new EnemyStudentAnimationSet(EnemyStudentAnimationSet.CadetBResourceRoot, "field-cadet-b");

            root = new GameObject("Academy Field") { layer = FieldLayer };
            root.transform.SetParent(parent, false);
            var cameraObject = new GameObject("Academy Field Camera") { layer = FieldLayer };
            cameraObject.transform.SetParent(root.transform, false);
            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.16f, .13f, .09f, 1f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100f;
            camera.depth = 101f;
            camera.cullingMask = 1 << FieldLayer;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.transform.localPosition = new Vector3(StartingX, -1.5f, -10f);

            backdrop = new ExplorationFieldBackdrop(root.transform, arena.SpriteMaterial, FieldLayer);
            playerGround = Child("Elisa Ground", root.transform);
            player = Sprite("Elisa", playerGround, playerAnimations.GetIdleUpper(0f), 35, arena.SpriteMaterial);
            CreateShadow(playerGround, arena.SpriteMaterial);

            uiRoot = Rect("Academy Field HUD", parent);
            var canvas = uiRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 150;
            var scaler = uiRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            locationText = Label("Field Location", uiRoot, "도미니코 학원 · 복도", 26, Ink);
            Anchor(locationText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(400f, 42f), new Vector2(32f, -28f));
            locationText.alignment = TextAnchor.MiddleLeft;
            arrivalText = Label("Field Arrival Notice", uiRoot, string.Empty, 19, Brass);
            Anchor(arrivalText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(680f, 70f), new Vector2(-32f, -25f));
            arrivalText.alignment = TextAnchor.UpperRight;
            arrivalText.horizontalOverflow = HorizontalWrapMode.Wrap;
            arrivalText.gameObject.SetActive(false);
            Text controls = Label("Field Controls", uiRoot, "WASD 이동   ·   Space / 클릭 상호작용   ·   Esc 메뉴", 18, Ink);
            Anchor(controls.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(660f, 36f), new Vector2(32f, 30f));
            controls.alignment = TextAnchor.MiddleLeft;
            prompt = Label("Field Interaction Hint", uiRoot, string.Empty, 22, Brass);
            Anchor(prompt.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                new Vector2(620f, 42f), new Vector2(0f, 68f));
            prompt.alignment = TextAnchor.MiddleCenter;

            storyBoard = CreateBoard(arena.SpriteMaterial);
            trainingDummy = CreateTrainingDummy(arena.SpriteMaterial);
            portal = CreatePortal(arena.SpriteMaterial);
            // TEMPORARY field pickup test: one soft radial falloff, tinted brass, lights every pickup.
            glowSprite = CreateGlowSprite(out glowTexture);
            for (int index = 0; index < CampaignFieldPickups.All.Count; index++)
                pickupTargets.Add(CreatePickup(CampaignFieldPickups.All[index], index, arena.SpriteMaterial));
            pickupFlourish = Child("Field Pickup Flourish", root.transform);
            flourishHalo = Sprite("Flourish Halo", pickupFlourish, glowSprite, 0, arena.SpriteMaterial);
            flourishHalo.transform.localPosition = new Vector3(0f, 0f, .01f);
            flourishIcon = Sprite("Flourish Icon", pickupFlourish, null, 0, arena.SpriteMaterial);
            pickupFlourish.gameObject.SetActive(false);
            RectTransform fade = Rect("Field Map Transition", uiRoot);
            // A nested canvas keeps the fade above labels created later when more fights unlock.
            var fadeCanvas = fade.gameObject.AddComponent<Canvas>();
            fadeCanvas.overrideSorting = true;
            fadeCanvas.sortingOrder = 190;
            transitionDim = fade.gameObject.AddComponent<Image>();
            transitionDim.color = Color.clear;
            transitionDim.raycastTarget = false;
            transitionDim.gameObject.SetActive(false);
            ApplyMap();
            Hide();
        }

        /// <summary>
        /// Return from a battle or story to the map and position Elisa left. Opponents are repositioned on
        /// each return; starting a new journey explicitly resets exploration to the school entrance.
        /// </summary>
        public void Show(CampaignRun campaign, bool storyAvailable, bool trainingAvailable,
            string unavailableStoryStatus = null, string entryNotice = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(AcademyFieldView));
            run = campaign ?? throw new ArgumentNullException(nameof(campaign));
            storyStatus = string.IsNullOrWhiteSpace(unavailableStoryStatus)
                ? "선행 전투를 완료하면 다음 임무가 열립니다." : unavailableStoryStatus;
            root.SetActive(true);
            uiRoot.gameObject.SetActive(true);
            if (!hasEntered)
            {
                backdrop.SetMap(ExplorationFieldMap.Corridor);
                playerGround.localPosition = new Vector3(StartingX, GroundY, 0f);
                facingRight = true;
                hasEntered = true;
            }
            player.flipX = !facingRight;
            animationTime = moveTime = 0f;
            // The board stays in the school even when its next mission is waiting for a battle.
            storyBoard.CanInteract = storyAvailable;
            storyBoard.Name = "스토리 게시판";
            this.trainingAvailable = trainingAvailable;
            trainingDummy.CanInteract = trainingAvailable && run.CanStartTraining;
            // The sheet can be reloaded between visits; names and icons follow its current rows.
            foreach (Target target in pickupTargets) RefreshPickupArt(target);
            PositionStages();
            ApplyMap();
            UpdateCamera(0f, true);
            SetArrivalNotice(entryNotice);
            UpdateLabels();
        }

        /// <summary>Clear temporary field context when a new game or a different save is opened.</summary>
        public void ResetExploration()
        {
            if (disposed) return;
            Hide();
            hasEntered = false;
            run = null;
            backdrop.SetMap(ExplorationFieldMap.Corridor);
            playerGround.localPosition = new Vector3(StartingX, GroundY, 0f);
            facingRight = true;
            ApplyMap();
        }

        /// <summary>Show a short first-arrival note without replacing the navigable field.</summary>
        public void SetArrivalNotice(string notice)
        {
            if (disposed) return;
            arrivalText.text = notice ?? string.Empty;
            arrivalRemaining = string.IsNullOrWhiteSpace(notice) ? 0f : 5.5f;
            arrivalText.color = Brass;
            arrivalText.gameObject.SetActive(arrivalRemaining > 0f);
        }

        /// <summary>Return from the ESC overlay without respawning opponents or moving Elisa.</summary>
        public void Resume()
        {
            if (disposed) throw new ObjectDisposedException(nameof(AcademyFieldView));
            if (run == null) return;
            root.SetActive(true);
            uiRoot.gameObject.SetActive(true);
            foreach (Target target in stageTargets)
                if (target.Root.gameObject.activeSelf) target.CanInteract = run.CanStartStage(target.StageNumber);
            trainingDummy.CanInteract = trainingDummy.Root.gameObject.activeSelf && run.CanStartTraining;
            RefreshPickups();
            UpdateLabels();
        }

        public void Hide()
        {
            if (disposed) return;
            IsTransitioning = false;
            transitionElapsed = 0f;
            transitionDim.gameObject.SetActive(false);
            StopPickupFlourish();
            prompt.text = string.Empty;
            root.SetActive(false);
            uiRoot.gameObject.SetActive(false);
        }

        /// <summary>Call only while the field is accepting input. Menus, dialogue and combat own their own keys.</summary>
        public void Tick(float realDelta, Keyboard keyboard, Mouse mouse)
        {
            if (!IsVisible || run == null) return;
            float dt = Mathf.Clamp(realDelta, 0f, .1f);
            if (IsTransitioning)
            {
                TickTransition(dt);
                return;
            }
            foreach (Target target in stageTargets)
                if (target.Root.gameObject.activeSelf) target.CanInteract = run.CanStartStage(target.StageNumber);
            trainingDummy.CanInteract = trainingDummy.Root.gameObject.activeSelf && run.CanStartTraining;
            RefreshPickups();
            TickPickupFlourish(dt);
            if (arrivalRemaining > 0f)
            {
                arrivalRemaining = Mathf.Max(0f, arrivalRemaining - dt);
                Color color = Brass;
                color.a = Mathf.Clamp01(arrivalRemaining / .7f);
                arrivalText.color = color;
                if (arrivalRemaining <= 0f) arrivalText.gameObject.SetActive(false);
            }
            animationTime += dt;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                onMenu?.Invoke();
                return;
            }

            Vector2 motion = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed) motion.x -= 1f;
                if (keyboard.dKey.isPressed) motion.x += 1f;
                if (keyboard.sKey.isPressed) motion.y -= 1f;
                if (keyboard.wKey.isPressed) motion.y += 1f;
            }
            if (motion.sqrMagnitude > 1f) motion.Normalize();
            if (motion.sqrMagnitude > .001f)
            {
                Vector3 position = playerGround.localPosition + (Vector3)(motion * WalkSpeed * dt);
                Rect bounds = backdrop.MapBounds;
                // The forest's end fence occupies the last part of the path rather than the canvas edge.
                float rightBound = CurrentMap == ExplorationFieldMap.Forest
                    ? ExplorationFieldBackdrop.AuthoredToWorld(new Vector2(3700f, 0f)).x : bounds.xMax - 1.5f;
                position.x = Mathf.Clamp(position.x, bounds.xMin + 1.5f, rightBound);
                position.y = Mathf.Clamp(position.y, BottomBound, TopBound);
                playerGround.localPosition = position;
                if (motion.x < -.01f) facingRight = false;
                else if (motion.x > .01f) facingRight = true;
                moveTime += dt;
                player.sprite = playerAnimations.GetMove();
                player.transform.localPosition = new Vector3(0f, Mathf.Sin(moveTime * 13f) * .04f, 0f);
            }
            else
            {
                player.sprite = playerAnimations.GetIdleUpper(animationTime);
                player.transform.localPosition = Vector3.zero;
            }
            player.flipX = !facingRight;
            player.sortingOrder = SortAt(playerGround.position.y);

            UpdateCamera(dt);
            trainingDummy.Renderer.sprite = dummyAnimations.GetIdle(animationTime);
            foreach (Target target in stageTargets)
            {
                if (!target.Root.gameObject.activeSelf) continue;
                EnemyStudentAnimationSet animations = (target.StageNumber & 1) == 1 ? cadetA : cadetB;
                target.Renderer.sprite = animations.GetIdle(animationTime + target.StageNumber * .17f);
            }

            Target nearest = FindNearest();
            AnimatePickups(nearest);
            prompt.text = nearest == null ? string.Empty
                : nearest.Kind == TargetKind.Pickup ? nearest.CanInteract ? "Space · " + nearest.Name + " 습득" : string.Empty
                : nearest.CanInteract ? "Space · " + nearest.Name
                : nearest.Kind == TargetKind.Story ? storyStatus
                : run.HasLoadoutChanges ? "편성 변경을 먼저 저장하세요"
                : "선행 임무를 완료하면 도전할 수 있습니다";
            UpdateLabels(nearest);

            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                Interact(nearest);
                return;
            }
            if (mouse != null && mouse.leftButton.wasPressedThisFrame &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                Vector3 world = camera.ScreenToWorldPoint(mouse.position.ReadValue());
                Target clicked = HitTarget(world);
                Interact(clicked);
            }
        }

        /// <summary>Use for an explicit input or PlayMode test. Nearness and progression gates still apply.</summary>
        public bool TryInteractNearest() => IsVisible && !IsTransitioning && Interact(FindNearest());

        public void Dispose()
        {
            if (disposed) return;
            Hide();
            disposed = true;
            cadetA.Dispose();
            cadetB.Dispose();
            Destroy(root);
            Destroy(uiRoot.gameObject);
            Destroy(glowSprite);
            Destroy(glowTexture);
        }

        /// <summary>TEMPORARY field pickup test: the field notice for a technique just taken. It names the technique's
        /// lane only while that lane is open; a closed lane stays unnamed (StoryUnlocks.md: locked things do not show
        /// themselves).</summary>
        /// <param name="newlyOwned">False when the run already owned the technique, so the pickup granted nothing new.</param>
        public static string PickupNotice(LegacySkill skill, bool newlyOwned, bool laneOpen)
        {
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            if (!newlyOwned) return $"{skill.Name} · 이미 익힌 기술입니다.";
            return laneOpen
                ? $"새 기술 · {skill.Name}({SkillLaneStyle.Key(skill.LaneIndex)} {SkillLaneStyle.Name(skill.LaneIndex)})\n" +
                  "Esc 메뉴의 편성에서 장착할 수 있습니다."
                : $"새 기술 · {skill.Name}\n이 기술의 열이 열리면 편성할 수 있습니다.";
        }

        private void PositionStages()
        {
            var available = new List<int>();
            for (int stage = 1; stage <= run.StageCount && stage <= run.HighestUnlockedStage; stage++)
                if (stage <= run.StageLimit) available.Add(stage);
            for (int i = available.Count - 1; i > 0; i--)
            {
                int other = UnityEngine.Random.Range(0, i + 1);
                int swap = available[i];
                available[i] = available[other];
                available[other] = swap;
            }
            while (stageTargets.Count < available.Count)
                stageTargets.Add(CreateStageTarget(stageTargets.Count));
            VisibleStageCount = CurrentMap == ExplorationFieldMap.Corridor ? available.Count : 0;
            for (int i = 0; i < stageTargets.Count; i++)
            {
                Target target = stageTargets[i];
                bool availableTarget = i < available.Count;
                bool visible = availableTarget && CurrentMap == ExplorationFieldMap.Corridor;
                target.Root.gameObject.SetActive(visible);
                target.Label.gameObject.SetActive(visible);
                if (!availableTarget) { target.StageNumber = 0; continue; }
                int stageNumber = available[i];
                target.StageNumber = stageNumber;
                target.Name = $"{stageNumber:00}  {run.GetStage(stageNumber).Name}";
                target.Label.text = target.Name;
                target.CanInteract = run.CanStartStage(stageNumber);
                float x = -14f + i * 4.2f + UnityEngine.Random.Range(-.55f, .55f);
                float y = UnityEngine.Random.Range(-1.85f, -.55f);
                // Returning from a fight should not put a newly shuffled cadet directly over Elisa.
                if (Vector2.Distance(new Vector2(x, y), PlayerPosition) < 2f) x += 2.5f;
                target.Root.localPosition = new Vector3(x, y, 0f);
                target.Renderer.sprite = ((stageNumber & 1) == 1 ? cadetA : cadetB).GetIdle(0f);
                target.Renderer.sortingOrder = SortAt(y);
                target.Renderer.flipX = (stageNumber & 1) == 0;
            }
        }

        private Target CreateStageTarget(int index)
        {
            Transform ground = Child("Stage NPC " + index, root.transform);
            SpriteRenderer renderer = Sprite("Cadet", ground, cadetA.GetIdle(0f), 25, spriteMaterial);
            CreateShadow(ground, spriteMaterial);
            Text label = FloatingLabel("Stage NPC Label " + index);
            return new Target { Kind = TargetKind.Stage, Root = ground, Renderer = renderer, Label = label };
        }

        private Target CreateBoard(Material material)
        {
            Transform ground = Child("Story Board", root.transform);
            ground.localPosition = new Vector3(-26f, GroundY, 0f);
            SpriteRenderer frame = Sprite("Story Board Frame", ground, white, 15, material);
            Size(frame, 1.85f, 2.05f);
            // The corridor actors' feet and shadows are 2.23 units below their logical anchor.
            // A 2.05-unit board stands on that same line when its centre is 1.205 units lower.
            frame.transform.localPosition = new Vector3(0f, -1.205f, 0f);
            frame.color = Brass;
            SpriteRenderer paper = Sprite("Story Board Paper", ground, white, 16, material);
            Size(paper, 1.65f, 1.85f);
            paper.transform.localPosition = frame.transform.localPosition;
            paper.color = Paper;
            SpriteRenderer noticeTop = Sprite("Posted Notice Top", ground, white, 17, material);
            Size(noticeTop, 1.32f, .62f);
            noticeTop.transform.localPosition = new Vector3(-.02f, -.82f, 0f);
            noticeTop.color = new Color(.81f, .73f, .55f, 1f);
            SpriteRenderer noticeBottom = Sprite("Posted Notice Bottom", ground, white, 17, material);
            Size(noticeBottom, 1.22f, .48f);
            noticeBottom.transform.localPosition = new Vector3(.08f, -1.48f, 0f);
            noticeBottom.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            noticeBottom.color = new Color(.68f, .62f, .48f, 1f);
            Text label = FloatingLabel("Story Board Label");
            label.text = "새 이야기";
            return new Target
            {
                Kind = TargetKind.Story, Root = ground, Renderer = frame, Label = label,
                Name = "스토리 게시판", CanInteract = true
            };
        }

        private Target CreateTrainingDummy(Material material)
        {
            Transform ground = Child("Training Dummy", root.transform);
            ground.localPosition = new Vector3(-21f, GroundY, 0f);
            SpriteRenderer renderer = Sprite("Dummy", ground, dummyAnimations.GetIdle(0f), 24, material);
            CreateShadow(ground, material);
            Text label = FloatingLabel("Training Dummy Label");
            label.text = "수련";
            return new Target
            {
                Kind = TargetKind.Training, Root = ground, Renderer = renderer, Label = label,
                Name = "수련 허수아비", CanInteract = true
            };
        }

        private Target CreatePortal(Material material)
        {
            Transform ground = Child("Field Door", root.transform);
            // The door is painted in the fixed background. This invisible rect only supplies its click bounds.
            SpriteRenderer hitArea = Sprite("Field Door Hit Area", ground, white, 0, material);
            hitArea.color = Color.clear;
            Size(hitArea, 4.1f, 5.6f);
            hitArea.transform.localPosition = new Vector3(0f, .6f, 0f);
            return new Target
            {
                Kind = TargetKind.Portal, Root = ground, Renderer = hitArea,
                Label = FloatingLabel("Field Door Label"), CanInteract = true
            };
        }

        // ---- TEMPORARY field pickup test (Docs/FieldPickups.md) ------------------------------------------------------
        // Placeholder look and flow for techniques lying in the field, until the field gets its real interactables.
        // Where they lie is Runtime data (CampaignFieldPickups); whether one is taken is the run's (and the save's).

        private Target CreatePickup(CampaignFieldPickup pickup, int index, Material material)
        {
            Transform ground = Child("Field Pickup " + pickup.Id, root.transform);
            ground.localPosition = new Vector3(pickup.X, pickup.Y, 0f);
            float groundLine = MobStudentAnimationSet.GroundOffset;
            int order = SortAt(pickup.Y);
            SpriteRenderer shadow = CreateShadow(ground, material, PickupShadowWidth, PickupShadowHeight, .27f);
            // A flattened pool of light on the path, just above the shadows (order 5) and below every standing sprite.
            SpriteRenderer pool = Sprite("Pickup Ground Glow", ground, glowSprite, 6, material);
            pool.transform.localPosition = new Vector3(0f, groundLine, .02f);
            pool.transform.localScale = new Vector3(1.9f, .55f, 1f);
            pool.color = WithAlpha(PickupLight, .36f);
            // The halo shares the walkers' depth sort: one step under the icon, at a slightly farther z, so the icon
            // always draws over its own halo and Elisa passes in front of or behind both like any actor.
            SpriteRenderer halo = Sprite("Pickup Halo", ground, glowSprite, order - 1, material);
            halo.transform.localPosition = new Vector3(0f, groundLine + PickupFloatHeight, .01f);
            halo.transform.localScale = Vector3.one * PickupHaloSize;
            // Tick animates both; these are the resting values for frames drawn before it runs (e.g. during a door fade).
            halo.color = WithAlpha(PickupLight, .53f);
            SpriteRenderer icon = Sprite("Pickup Icon", ground, null, order, material);
            icon.transform.localPosition = new Vector3(0f, groundLine + PickupFloatHeight, 0f);
            var target = new Target
            {
                Kind = TargetKind.Pickup, PickupId = pickup.Id, Root = ground, Renderer = icon,
                Label = FloatingLabel("Field Pickup Label " + pickup.Id), Halo = halo, GroundGlow = pool,
                Shadow = shadow, Phase = index * 1.7f
            };
            RefreshPickupArt(target);
            ground.gameObject.SetActive(false);
            return target;
        }

        /// <summary>The technique's current sheet name and its skill icon (the sheet's 아이콘 column).</summary>
        private void RefreshPickupArt(Target target)
        {
            CampaignFieldPickup pickup = CampaignFieldPickups.Find(target.PickupId);
            LegacySkill skill = pickup == null ? null : LegacySkillDefinitions.Find(pickup.SkillId)?.Skill;
            target.Name = skill != null ? skill.Name : "기술";
            target.Label.text = target.Name;
            target.Renderer.sprite = skill != null ? art.GetSkillIcon(skill.IconId) : null;
            Size(target.Renderer, PickupIconSize, PickupIconSize);
        }

        /// <summary>Shows the current map's pickups the run has not taken, and only those.</summary>
        private void RefreshPickups()
        {
            CampaignFieldArea area = AreaOf(CurrentMap);
            VisiblePickupCount = 0;
            foreach (Target target in pickupTargets)
            {
                CampaignFieldPickup pickup = CampaignFieldPickups.Find(target.PickupId);
                bool visible = run != null && pickup != null && pickup.Area == area &&
                    !run.IsFieldPickupCollected(target.PickupId);
                if (target.Root.gameObject.activeSelf != visible) target.Root.gameObject.SetActive(visible);
                target.CanInteract = visible && run.CanCollectFieldPickup(target.PickupId);
                if (visible) VisiblePickupCount++;
            }
        }

        /// <summary>Each icon bobs on its own phase while its halo breathes; the nearest one glows brighter.</summary>
        private void AnimatePickups(Target nearest)
        {
            float iconY = MobStudentAnimationSet.GroundOffset + PickupFloatHeight;
            foreach (Target target in pickupTargets)
            {
                if (!target.Root.gameObject.activeSelf) continue;
                float lift = Mathf.Sin(animationTime * 2.4f + target.Phase) * PickupBob;
                float pulse = .5f + .5f * Mathf.Sin(animationTime * 3.1f + target.Phase * 1.3f);
                bool near = target == nearest;
                target.Renderer.transform.localPosition = new Vector3(0f, iconY + lift, 0f);
                target.Halo.transform.localPosition = new Vector3(0f, iconY + lift, .01f);
                target.Halo.transform.localScale = Vector3.one * (PickupHaloSize * (1f + .05f * pulse));
                target.Halo.color = WithAlpha(PickupLight, (near ? .64f : .46f) + .14f * pulse);
                target.GroundGlow.color = WithAlpha(PickupLight, (near ? .44f : .32f) + .08f * pulse);
                // The contact shadow tightens a little as the icon rises.
                float shadowScale = 1f - lift * 1.2f;
                target.Shadow.transform.localScale =
                    new Vector3(PickupShadowWidth * shadowScale, PickupShadowHeight * shadowScale, 1f);
            }
        }

        private Target FindPickupTarget(string pickupId)
        {
            foreach (Target target in pickupTargets)
                if (target.PickupId == pickupId) return target;
            return null;
        }

        /// <summary>A copy of the taken icon rises and fades inside a widening halo.</summary>
        private void PlayPickupFlourish(Target target)
        {
            pickupFlourish.localPosition = target.Root.localPosition +
                new Vector3(0f, MobStudentAnimationSet.GroundOffset + PickupFloatHeight, 0f);
            flourishIcon.sprite = target.Renderer.sprite;
            flourishIcon.transform.localScale = target.Renderer.transform.localScale;
            // In front of everything standing on the path, still behind the forest's foreground bushes (100).
            flourishIcon.sortingOrder = 60;
            flourishHalo.sortingOrder = 59;
            flourishRemaining = PickupFlourishDuration;
            pickupFlourish.gameObject.SetActive(true);
            TickPickupFlourish(0f);
        }

        private void TickPickupFlourish(float dt)
        {
            if (flourishRemaining <= 0f) return;
            flourishRemaining = Mathf.Max(0f, flourishRemaining - dt);
            float t = 1f - flourishRemaining / PickupFlourishDuration;
            float eased = 1f - (1f - t) * (1f - t);
            flourishIcon.transform.localPosition = new Vector3(0f, .6f * eased, 0f);
            flourishIcon.color = WithAlpha(Color.white, 1f - t);
            flourishHalo.transform.localScale = Vector3.one * Mathf.Lerp(PickupHaloSize, PickupHaloSize * 1.8f, eased);
            flourishHalo.color = WithAlpha(PickupLight, .75f * (1f - t));
            if (flourishRemaining <= 0f) pickupFlourish.gameObject.SetActive(false);
        }

        private void StopPickupFlourish()
        {
            flourishRemaining = 0f;
            pickupFlourish.gameObject.SetActive(false);
        }

        private static CampaignFieldArea AreaOf(ExplorationFieldMap map)
            => map == ExplorationFieldMap.Forest ? CampaignFieldArea.Forest : CampaignFieldArea.Corridor;

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        /// <summary>A white radial falloff (opaque centre, smoothstep to a clear rim) for tinted glows, one world unit
        /// wide.</summary>
        private static Sprite CreateGlowSprite(out Texture2D texture)
        {
            const int size = 64;
            var pixels = new Color[size * size];
            float centre = (size - 1) * .5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - centre, dy = y - centre;
                    float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / centre);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, falloff * falloff * (3f - 2f * falloff));
                }
            }
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Field Pickup Glow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f,
                size, 0, SpriteMeshType.FullRect);
            sprite.name = "Field Pickup Glow";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
        // ---- end of the temporary field pickup test ---------------------------------------------------------------

        private void ApplyMap()
        {
            bool corridor = CurrentMap == ExplorationFieldMap.Corridor;
            locationText.text = corridor ? "도미니코 학원 · 복도" : "도미니코 학원 · 바깥 숲";
            storyBoard.Root.gameObject.SetActive(corridor);
            trainingDummy.Root.gameObject.SetActive(corridor && trainingAvailable);
            VisibleStageCount = 0;
            foreach (Target target in stageTargets)
            {
                bool visible = corridor && target.StageNumber > 0;
                target.Root.gameObject.SetActive(visible);
                if (visible) VisibleStageCount++;
            }
            Vector2 door = ExplorationFieldBackdrop.AuthoredToWorld(new Vector2(corridor ? 3720f : 352f, 488f));
            door.y -= MobStudentAnimationSet.GroundOffset;
            portal.Root.localPosition = door;
            portal.Name = corridor ? "숲으로 나가기" : "학원으로 들어가기";
            portal.Label.text = portal.Name;
            StopPickupFlourish();
            RefreshPickups();
            prompt.text = string.Empty;
        }

        private void UpdateCamera(float dt, bool snap = false)
        {
            float targetX = backdrop.ClampCameraX(camera, playerGround.localPosition.x);
            Vector3 position = camera.transform.localPosition;
            position.x = snap ? targetX : Mathf.Lerp(position.x, targetX, 1f - Mathf.Exp(-6f * dt));
            position.x = backdrop.ClampCameraX(camera, position.x);
            position.y = -1.5f;
            camera.transform.localPosition = position;
            backdrop.Tick(camera);
        }

        private void BeginTransition()
        {
            IsTransitioning = true;
            transitionElapsed = 0f;
            transitionDim.color = Color.clear;
            transitionDim.gameObject.SetActive(true);
            player.sprite = playerAnimations.GetIdleUpper(animationTime);
            player.transform.localPosition = Vector3.zero;
            prompt.text = string.Empty;
        }

        private void TickTransition(float dt)
        {
            float before = transitionElapsed;
            transitionElapsed += dt;
            bool changingMap = before < TransitionHalfDuration && transitionElapsed >= TransitionHalfDuration;
            if (changingMap)
            {
                // Always present one fully covered frame, even if a slow frame skips past the fade midpoint.
                transitionElapsed = TransitionHalfDuration;
                backdrop.SetMap(CurrentMap == ExplorationFieldMap.Corridor
                    ? ExplorationFieldMap.Forest : ExplorationFieldMap.Corridor);
                ApplyMap();
                bool enteringForest = CurrentMap == ExplorationFieldMap.Forest;
                playerGround.localPosition = (Vector3)(PortalPosition + new Vector2(enteringForest ? 2.2f : -2.2f, 0f));
                facingRight = enteringForest;
                player.flipX = !facingRight;
                player.sortingOrder = SortAt(playerGround.position.y);
                if (!enteringForest) PositionStages();
                UpdateCamera(0f, true);
                UpdateLabels();
            }
            float alpha = transitionElapsed < TransitionHalfDuration
                ? transitionElapsed / TransitionHalfDuration
                : 1f - (transitionElapsed - TransitionHalfDuration) / TransitionHalfDuration;
            transitionDim.color = new Color(.04f, .03f, .025f, Mathf.Clamp01(alpha));
            if (transitionElapsed >= TransitionHalfDuration * 2f)
            {
                IsTransitioning = false;
                transitionDim.gameObject.SetActive(false);
            }
        }

        private Target FindNearest()
        {
            Target best = null;
            float bestDistance = InteractionRange * InteractionRange;
            Consider(storyBoard, ref best, ref bestDistance);
            Consider(trainingDummy, ref best, ref bestDistance);
            Consider(portal, ref best, ref bestDistance);
            foreach (Target target in stageTargets) Consider(target, ref best, ref bestDistance);
            foreach (Target target in pickupTargets) Consider(target, ref best, ref bestDistance);
            return best;
        }

        private void Consider(Target target, ref Target best, ref float bestDistance)
        {
            if (!target.Root.gameObject.activeSelf) return;
            Vector2 difference = target.Root.localPosition - playerGround.localPosition;
            float distance = difference.sqrMagnitude;
            if (distance > InteractionRange * InteractionRange) return;
            if (best != null && best.CanInteract && !target.CanInteract) return;
            if (best == null || target.CanInteract && !best.CanInteract || distance < bestDistance)
            {
                bestDistance = distance;
                best = target;
            }
        }

        private Target HitTarget(Vector3 world)
        {
            Target best = null;
            float bestDistance = float.MaxValue;
            ConsiderHit(storyBoard, world, ref best, ref bestDistance);
            ConsiderHit(trainingDummy, world, ref best, ref bestDistance);
            ConsiderHit(portal, world, ref best, ref bestDistance);
            foreach (Target target in stageTargets) ConsiderHit(target, world, ref best, ref bestDistance);
            foreach (Target target in pickupTargets) ConsiderHit(target, world, ref best, ref bestDistance);
            return best;
        }

        private void ConsiderHit(Target target, Vector3 world, ref Target best, ref float bestDistance)
        {
            if (!target.Root.gameObject.activeSelf) return;
            Vector2 distance = target.Root.localPosition - playerGround.localPosition;
            if (distance.sqrMagnitude > InteractionRange * InteractionRange) return;
            Bounds bounds = target.Renderer.bounds;
            // Character texture frames have transparent canvas margins. An expanded ground-centred zone makes their
            // silhouettes easy to click while still requiring Elisa to stand nearby.
            bool hit = bounds.Contains(new Vector3(world.x, world.y, bounds.center.z)) ||
                Vector2.Distance(new Vector2(world.x, world.y), new Vector2(bounds.center.x, bounds.center.y)) < 1.1f;
            if (!hit) return;
            float score = distance.sqrMagnitude;
            if (score < bestDistance) { best = target; bestDistance = score; }
        }

        private bool Interact(Target target)
        {
            if (!IsVisible || IsTransitioning) return false;
            if (target == null || !target.CanInteract || !target.Root.gameObject.activeSelf) return false;
            Vector2 distance = target.Root.localPosition - playerGround.localPosition;
            if (distance.sqrMagnitude > InteractionRange * InteractionRange) return false;
            switch (target.Kind)
            {
                case TargetKind.Stage: onStage?.Invoke(target.StageNumber); break;
                case TargetKind.Story: onStory?.Invoke(); break;
                case TargetKind.Training: onTraining?.Invoke(); break;
                case TargetKind.Portal: BeginTransition(); break;
                case TargetKind.Pickup: CollectPickup(target); break;
            }
            return true;
        }

        /// <summary>TEMPORARY field pickup test: the owner takes the pickup through the run; once the run has it, it
        /// leaves the map with a short rising flash.</summary>
        private void CollectPickup(Target target)
        {
            onPickup?.Invoke(target.PickupId);
            if (disposed || run == null || !run.IsFieldPickupCollected(target.PickupId)) return;
            PlayPickupFlourish(target);
            RefreshPickups();
            prompt.text = string.Empty;
            UpdateLabels();
        }

        private void UpdateLabels(Target nearest = null)
        {
            UpdateLabel(storyBoard, nearest);
            UpdateLabel(trainingDummy, nearest);
            UpdateLabel(portal, nearest);
            foreach (Target target in stageTargets) UpdateLabel(target, nearest);
            foreach (Target target in pickupTargets) UpdateLabel(target, nearest);
        }

        private void UpdateLabel(Target target, Target nearest)
        {
            if (!target.Root.gameObject.activeSelf)
            {
                target.Label.enabled = false;
                return;
            }
            Vector3 world = target.Kind == TargetKind.Story
                ? target.Root.position + new Vector3(0f, .3f, 0f)
                : target.Kind == TargetKind.Portal ? target.Root.position + new Vector3(0f, 4.2f, 0f)
                : target.Kind == TargetKind.Pickup
                    ? target.Root.position + new Vector3(0f, MobStudentAnimationSet.GroundOffset + PickupLabelHeight, 0f)
                : target.Root.position + new Vector3(0f, 2.45f, 0f);
            Vector3 viewport = camera.WorldToViewportPoint(world);
            bool visible = viewport.z > 0f && viewport.x > -.04f && viewport.x < 1.04f &&
                viewport.y > -.04f && viewport.y < 1.04f;
            target.Label.enabled = visible;
            if (!visible) return;
            Vector2 screen = camera.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(uiRoot, screen, null, out Vector2 local);
            target.Label.rectTransform.anchoredPosition = local;
            target.Label.color = target == nearest ? Brass : target.CanInteract ? Ink : new Color(.64f, .61f, .54f, 1f);
            target.Label.text = target.Kind == TargetKind.Story
                ? target.CanInteract ? "스토리 게시판 · 새 임무" : "스토리 게시판 · 대기"
                : target.Name;
        }

        private Text FloatingLabel(string name)
        {
            Text text = Label(name, uiRoot, string.Empty, 20, Ink);
            text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 15;
            text.resizeTextMaxSize = 20;
            text.rectTransform.sizeDelta = new Vector2(260f, 34f);
            return text;
        }

        private SpriteRenderer CreateShadow(Transform ground, Material material,
            float width = 2.2f, float height = .7f, float alpha = .34f)
        {
            Sprite[] shadows = Resources.LoadAll<Sprite>("LegacyArena/Shadow/Circle");
            // Without the art the renderer stays empty, so callers can still size and tint it.
            SpriteRenderer shadow = Sprite("Ground Shadow", ground, shadows.Length > 0 ? shadows[0] : null, 5, material);
            shadow.color = new Color(.14f, .11f, .08f, alpha);
            shadow.transform.localPosition = new Vector3(0f, MobStudentAnimationSet.GroundOffset, 0f);
            shadow.transform.localScale = new Vector3(width, height, 1f);
            return shadow;
        }

        private static int SortAt(float y) => 30 + Mathf.RoundToInt(-y * 3f);

        private static Transform Child(string name, Transform parent)
        {
            var obj = new GameObject(name) { layer = FieldLayer };
            obj.transform.SetParent(parent, false);
            return obj.transform;
        }

        private static SpriteRenderer Sprite(string name, Transform parent, Sprite sprite, int order, Material material)
        {
            Transform child = Child(name, parent);
            var renderer = child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            if (material != null) renderer.sharedMaterial = material;
            return renderer;
        }

        private static void Size(SpriteRenderer renderer, float width, float height)
        {
            Vector2 source = renderer.sprite != null ? renderer.sprite.bounds.size : Vector2.one;
            renderer.transform.localScale = new Vector3(width / Mathf.Max(.01f, source.x),
                height / Mathf.Max(.01f, source.y), 1f);
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private Text Label(string name, Transform parent, string value, int size, Color color)
        {
            RectTransform rect = Rect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(240f, 34f);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.08f, .06f, .04f, .85f);
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        private static void Anchor(RectTransform rect, Vector2 anchors, Vector2 pivot, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = rect.anchorMax = anchors;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
        }

        private static void Destroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }
    }
}

