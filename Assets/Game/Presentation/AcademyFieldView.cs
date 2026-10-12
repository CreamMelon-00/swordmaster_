using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Campaign;
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
        private static readonly Color Paper = new Color(.25f, .19f, .13f, 1f);
        private static readonly Color Brass = new Color(.77f, .61f, .35f, 1f);
        private static readonly Color Ink = new Color(.95f, .89f, .74f, 1f);

        private enum TargetKind { Stage, Story, Training, Portal }

        private sealed class Target
        {
            public TargetKind Kind;
            public int StageNumber;
            public Transform Root;
            public SpriteRenderer Renderer;
            public Text Label;
            public string Name;
            public bool CanInteract;
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
        private readonly Action<int> onStage;
        private readonly Action onStory;
        private readonly Action onTraining;
        private readonly Action onMenu;
        private readonly Font font;
        private readonly Sprite white;
        private readonly Material spriteMaterial;
        private CampaignRun run;
        private string storyStatus;
        private float animationTime;
        private float moveTime;
        private float arrivalRemaining;
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

        public AcademyFieldView(Transform parent, LegacyDuelArt art, LegacyArenaView arena,
            Action<int> onStage, Action onStory, Action onTraining, Action onMenu)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (art == null) throw new ArgumentNullException(nameof(art));
            if (arena == null) throw new ArgumentNullException(nameof(arena));
            this.onStage = onStage;
            this.onStory = onStory;
            this.onTraining = onTraining;
            this.onMenu = onMenu;
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
            UpdateLabels();
        }

        public void Hide()
        {
            if (disposed) return;
            IsTransitioning = false;
            transitionElapsed = 0f;
            transitionDim.gameObject.SetActive(false);
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
            prompt.text = nearest == null ? string.Empty
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
            }
            return true;
        }

        private void UpdateLabels(Target nearest = null)
        {
            UpdateLabel(storyBoard, nearest);
            UpdateLabel(trainingDummy, nearest);
            UpdateLabel(portal, nearest);
            foreach (Target target in stageTargets) UpdateLabel(target, nearest);
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

        private void CreateShadow(Transform ground, Material material)
        {
            Sprite[] shadows = Resources.LoadAll<Sprite>("LegacyArena/Shadow/Circle");
            if (shadows.Length == 0) return;
            SpriteRenderer shadow = Sprite("Ground Shadow", ground, shadows[0], 5, material);
            shadow.color = new Color(.14f, .11f, .08f, .34f);
            shadow.transform.localPosition = new Vector3(0f, MobStudentAnimationSet.GroundOffset, 0f);
            shadow.transform.localScale = new Vector3(2.2f, .7f, 1f);
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

        private static void Destroy(GameObject obj)
        {
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }
    }
}

