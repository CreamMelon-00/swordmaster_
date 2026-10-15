using System;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>결정적 한 수, the decisive-moment replay (2026-10-15, the read rewards). While a battle resolves, the slots the
    /// director rates as candidates are recorded as a short flipbook of small pictures of the arena camera alone (no HUD):
    /// the camera is rendered into this replay's own render textures (a <see cref="RenderPipeline.StandardRequest"/> submitted
    /// to the render pipeline in URP, or <see cref="Camera.Render"/> into its target texture otherwise), at <see cref="LegacyReplayTuning.FramesPerSecond"/> on real time, only while a candidate slot (and its
    /// tail) plays. <see cref="LegacyReplayRecorder"/> keeps the battle's most important one. On the result
    /// (<see cref="Play"/>) it plays once over the result card, slower than recorded, with the two skills' icons under it,
    /// then fades and the card is there; a click on it (or the controller's Enter, Space or Escape: <see cref="Skip"/>)
    /// ends it at once. Memory: both clips' pictures together stay within <see cref="LegacyReplayTuning.MemoryCapBytes"/>
    /// (320 × 180 RGBA, 15 a second for 2.5 s: about 17.5 MB); the pictures not kept are released when the battle ends
    /// (<see cref="StopRecording"/>), and everything when it has played or is skipped, and on any reset
    /// (<see cref="Clear"/>). Where capturing is not possible (no graphics device, no RGBA render textures, a render that
    /// throws) nothing is recorded and the result comes as ever; so it does when switched off
    /// (<see cref="DuelPresentationSettings.ReplayEnabled"/>).</summary>
    public sealed class DuelDecisiveReplay : IDisposable
    {
        public const string RootName = "Decisive Replay", PictureName = "Decisive Replay Picture",
            TitleName = "Decisive Replay Title", PlayerSkillName = "Decisive Replay Player Skill",
            EnemySkillName = "Decisive Replay Enemy Skill", HintName = "Decisive Replay Hint";
        public const string Title = "결정적 한 수", Hint = "클릭 · Enter  넘기기";
        /// <summary>Over the result card (400), under the versus exit question (410) and a level-up (450).</summary>
        public const int SortingOrder = 405;
        public const float FadeInSeconds = .2f, FadeOutSeconds = .3f;
        private const float PictureWidth = 960f, IconCardSize = 92f, IconSize = 66f;
        private readonly LegacyDuelArt art;
        private readonly Func<DuelPresentationSettings> settings;
        private readonly RectTransform root, frame;
        private readonly CanvasGroup group;
        private readonly RawImage picture;
        private readonly Image playerRing, enemyRing, playerIcon, enemyIcon;
        private readonly Text title, hint;
        private LegacyReplayRecorder recorder;
        private RenderTexture[] pictures;
        private int pictureWidth, pictureHeight;
        private bool captureFailed, playing, disposed;
        private float elapsed;
        // URP's render request, made once and reused for every picture (only its destination changes), and whether the
        // pipeline takes it for the battle's camera (asked once per battle, as its first candidate slot begins).
        private RenderPipeline.StandardRequest renderRequest;
        private bool requestSupported;

        public DuelDecisiveReplay(Transform parent, LegacyDuelArt art, Func<DuelPresentationSettings> settings)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            root = Rect(RootName, parent, Vector2.zero, Vector2.zero);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = SortingOrder;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            group = root.gameObject.AddComponent<CanvasGroup>();

            // The whole screen takes the click that skips it (the result card under it waits).
            var veil = Panel("Decisive Replay Veil", root, Vector2.zero, Vector2.zero,
                new Color(DuelVisualTheme.Track.r, DuelVisualTheme.Track.g, DuelVisualTheme.Track.b, .86f));
            veil.rectTransform.anchorMin = Vector2.zero;
            veil.rectTransform.anchorMax = Vector2.one;
            veil.rectTransform.sizeDelta = Vector2.zero;
            veil.raycastTarget = true;
            var skip = veil.gameObject.AddComponent<Button>();
            skip.transition = Selectable.Transition.None;
            skip.navigation = new Navigation { mode = Navigation.Mode.None };
            skip.onClick.AddListener(() =>
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                Skip();
            });

            frame = Panel("Decisive Replay Frame", root, new Vector2(0f, 10f), new Vector2(PictureWidth + 8f, 548f),
                DuelVisualTheme.Border).rectTransform;
            picture = new GameObject(PictureName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage))
                .GetComponent<RawImage>();
            picture.rectTransform.SetParent(frame, false);
            picture.rectTransform.anchorMin = picture.rectTransform.anchorMax = picture.rectTransform.pivot = Vector2.one * .5f;
            picture.raycastTarget = false;
            picture.color = Color.white;
            title = Label(TitleName, frame, Vector2.zero, new Vector2(600f, 52f), 36, DuelVisualTheme.Accent);
            title.text = Title;
            hint = Label(HintName, frame, Vector2.zero, new Vector2(400f, 28f), 18, DuelVisualTheme.Muted);
            hint.text = Hint;
            playerRing = IconCard(PlayerSkillName, out playerIcon);
            enemyRing = IconCard(EnemySkillName, out enemyIcon);
            root.gameObject.SetActive(false);
        }

        public GameObject Root => root.gameObject;
        /// <summary>Whether the replay is on screen now (over the result card).</summary>
        public bool IsPlaying => !disposed && playing;
        /// <summary>Whether a candidate slot (or its tail) is being recorded now.</summary>
        public bool IsRecording => !disposed && recorder != null && recorder.IsRecording;
        /// <summary>Whether a clip is kept for the result.</summary>
        public bool HasClip => !disposed && recorder != null && recorder.HasClip;
        /// <summary>The battle's bookkeeping (null between battles and before its first candidate slot).</summary>
        public LegacyReplayRecorder Recorder => recorder;
        /// <summary>Whether capturing failed or is not possible here (nothing is recorded for the rest of the session).</summary>
        public bool CaptureFailed => captureFailed;
        /// <summary>Render textures alive now (0 once released).</summary>
        public int AllocatedPictureCount
        {
            get
            {
                if (pictures == null) return 0;
                int count = 0;
                foreach (RenderTexture texture in pictures)
                    if (texture != null) count++;
                return count;
            }
        }
        /// <summary>Pictures taken this battle so far.</summary>
        public int CapturedPictureCount { get; private set; }
        /// <summary>The clip's picture shown now (-1 while not playing).</summary>
        public int ShownIndex { get; private set; } = -1;
        /// <summary>Real seconds the replay has played (0 while not playing).</summary>
        public float PlayElapsed => playing ? elapsed : 0f;
        /// <summary>The picture's size in pixels (0 before the battle's first candidate slot).</summary>
        public Vector2Int PictureSize => new Vector2Int(pictureWidth, pictureHeight);

        /// <summary>A slot begins, rated <paramref name="start"/> by the director: a candidate starts recording (the battle's
        /// numbers are taken at its first one). Nothing while switched off, playing, or where capturing is not possible.</summary>
        public void BeginSlot(LegacySlotRank start, Camera camera)
        {
            if (disposed || playing || captureFailed || camera == null) return;
            DuelPresentationSettings tuningSource = settings();
            LegacyReplayTuning tuning = tuningSource != null ? tuningSource.ReplayTuning : LegacyReplayTuning.Default;
            if (recorder == null)
            {
                if (!tuning.Enabled) return;
                if (!CanCapture() || !PrepareRenderRequest(camera))
                {
                    captureFailed = true;
                    return;
                }
                float aspect = camera.pixelHeight > 0 ? camera.pixelWidth / (float)camera.pixelHeight : 16f / 9f;
                pictureWidth = tuning.Width;
                pictureHeight = tuning.HeightFor(aspect);
                recorder = new LegacyReplayRecorder(tuning, tuning.FramesPerClip(pictureHeight));
                pictures = new RenderTexture[recorder.FrameCount];
                CapturedPictureCount = 0;
            }
            recorder.BeginSlot(start);
        }

        /// <summary>The recording's frame (real time, after the arena moved this frame): a picture of the arena camera when one
        /// is due.</summary>
        public void Capture(float realDelta, Camera camera)
        {
            if (disposed || playing || recorder == null || camera == null || !recorder.IsRecording) return;
            if (!recorder.Advance(realDelta, out int frame) || frame < 0 || frame >= pictures.Length) return;
            RenderTexture target = pictures[frame];
            if (target == null)
            {
                target = CreatePicture(frame);
                pictures[frame] = target;
            }
            if (target == null || !Render(camera, target))
            {
                captureFailed = true;
                Clear();
                return;
            }
            CapturedPictureCount++;
        }

        /// <summary>The slot being recorded completed, rated <paramref name="final"/> by the director, with these skills on the
        /// player's side and the opponent's (the replay's icons).</summary>
        public void EndSlot(LegacySlotRank final, LegacySkill playerSkill, LegacySkill enemySkill)
        {
            if (disposed || recorder == null) return;
            recorder.EndSlot(final, playerSkill, enemySkill);
        }

        /// <summary>The battle ended (or a scene interrupts it): the recording ends (a tail is kept as far as it got) and every
        /// picture not in the kept clip is released. Nothing kept: everything is released.</summary>
        public void StopRecording()
        {
            if (disposed || recorder == null) return;
            recorder.Stop();
            if (!recorder.HasClip)
            {
                if (!playing) Clear();
                return;
            }
            for (int frame = 0; frame < pictures.Length; frame++)
                if (pictures[frame] != null && !recorder.IsKeptFrame(frame)) Release(ref pictures[frame]);
        }

        /// <summary>Plays the kept clip once over the result card (call it right after the result is shown). False, with
        /// everything released, when there is none, when switched off or when a picture is missing: the result stands as
        /// ever.</summary>
        public bool Play()
        {
            if (disposed) return false;
            StopRecording();
            DuelPresentationSettings tuningSource = settings();
            if (recorder == null || !recorder.HasClip || tuningSource != null && !tuningSource.ReplayEnabled)
            {
                Clear();
                return false;
            }
            for (int index = 0; index < recorder.ClipFrameCount; index++)
            {
                if (pictures[recorder.ClipFrame(index)] != null) continue;
                Clear();
                return false;
            }
            float height = PictureWidth * pictureHeight / Mathf.Max(1f, pictureWidth);
            picture.rectTransform.sizeDelta = new Vector2(PictureWidth, height);
            frame.sizeDelta = new Vector2(PictureWidth + 8f, height + 8f);
            title.rectTransform.anchoredPosition = new Vector2(0f, height * .5f + 40f);
            hint.rectTransform.anchoredPosition = new Vector2(0f, -height * .5f - 34f);
            PlaceIcon(playerRing, playerIcon, recorder.ClipPlayerSkill, new Vector2(-PictureWidth * .5f + 70f, -height * .5f));
            PlaceIcon(enemyRing, enemyIcon, recorder.ClipEnemySkill, new Vector2(PictureWidth * .5f - 70f, -height * .5f));
            playing = true;
            elapsed = 0f;
            ShowPicture(0);
            group.alpha = 0f;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            return true;
        }

        /// <summary>The replay's frame on real time: its pictures at the clip's playback speed, the last held, then it fades
        /// and everything is released.</summary>
        public void Tick(float realDelta)
        {
            if (disposed || !playing) return;
            elapsed += realDelta > 0f && !float.IsInfinity(realDelta) ? realDelta : 0f;
            LegacyReplayTuning tuning = recorder.Tuning;
            int count = recorder.ClipFrameCount;
            ShowPicture(LegacyReplayRecorder.PlaybackIndex(elapsed, count, tuning));
            float end = LegacyReplayRecorder.PlaybackSeconds(count, tuning) + tuning.HoldSeconds;
            group.alpha = elapsed < FadeInSeconds ? elapsed / FadeInSeconds
                : elapsed > end ? Mathf.Clamp01(1f - (elapsed - end) / FadeOutSeconds) : 1f;
            if (elapsed >= end + FadeOutSeconds) Clear();
        }

        /// <summary>Ends the replay at once (a click, or the controller's key): the result card is there. False while none
        /// plays.</summary>
        public bool Skip()
        {
            if (disposed || !playing) return false;
            Clear();
            return true;
        }

        /// <summary>Releases everything: the recording, the clip, its pictures and the replay on screen (a reset, a new battle,
        /// the lobby, the replay over).</summary>
        public void Clear()
        {
            if (disposed) return;
            playing = false;
            elapsed = 0f;
            ShownIndex = -1;
            picture.texture = null;
            root.gameObject.SetActive(false);
            recorder?.Clear();
            recorder = null;
            if (pictures != null)
                for (int frame = 0; frame < pictures.Length; frame++) Release(ref pictures[frame]);
            pictures = null;
            pictureWidth = pictureHeight = 0;
            CapturedPictureCount = 0;
            requestSupported = false;
            if (renderRequest != null) renderRequest.destination = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            Clear();
            disposed = true;
            if (root != null) Object.Destroy(root.gameObject);
        }

        private void ShowPicture(int index)
        {
            ShownIndex = index;
            picture.texture = pictures[recorder.ClipFrame(index)];
        }

        private static bool CanCapture()
            => SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null &&
               SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32);

        private RenderTexture CreatePicture(int frame)
        {
            try
            {
                var texture = new RenderTexture(pictureWidth, pictureHeight, 0, RenderTextureFormat.ARGB32)
                {
                    name = "Decisive Replay " + frame,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                    antiAliasing = 1,
                };
                if (!texture.Create())
                {
                    Object.Destroy(texture);
                    return null;
                }
                return texture;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("결정적 한 수: the replay's picture could not be made (" + exception.Message + ").");
                return null;
            }
        }

        // Whether the render pipeline takes this replay's render request for the battle's camera, asked once per battle (the
        // request itself is made once and reused: a picture makes no new request object, so recording adds no garbage of
        // ours). False only when asking threw: no capturing this session.
        private bool PrepareRenderRequest(Camera camera)
        {
            requestSupported = false;
            if (GraphicsSettings.currentRenderPipeline == null) return true;
            try
            {
                if (renderRequest == null) renderRequest = new RenderPipeline.StandardRequest();
                requestSupported = RenderPipeline.SupportsRenderRequest(camera, renderRequest);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("결정적 한 수: the arena could not be captured; no replay this session (" +
                    exception.Message + ").");
                return false;
            }
        }

        // The arena camera alone into the picture: URP's render request where the pipeline takes it (the one request,
        // pointed at this picture), else the camera's own render into its target texture (put back after).
        private bool Render(Camera camera, RenderTexture target)
        {
            try
            {
                if (requestSupported && renderRequest != null)
                {
                    renderRequest.destination = target;
                    RenderPipeline.SubmitRenderRequest(camera, renderRequest);
                    return true;
                }
                RenderTexture previous = camera.targetTexture;
                camera.targetTexture = target;
                try { camera.Render(); }
                finally { camera.targetTexture = previous; }
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("결정적 한 수: the arena could not be captured; no replay this session (" +
                    exception.Message + ").");
                return false;
            }
        }

        private static void Release(ref RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release();
            Object.Destroy(texture);
            texture = null;
        }

        private void PlaceIcon(Image ring, Image icon, LegacySkill skill, Vector2 position)
        {
            ring.rectTransform.anchoredPosition = position;
            ring.color = skill != null && skill.LaneIndex >= 0 && skill.LaneIndex <= 2
                ? SkillLaneStyle.Edge(skill.LaneIndex) : DuelVisualTheme.Border;
            Sprite sprite = skill != null ? art.GetSkillIcon(skill.IconId) : null;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        private Image IconCard(string name, out Image icon)
        {
            Image ring = Panel(name, frame, Vector2.zero, Vector2.one * IconCardSize, DuelVisualTheme.Border);
            Image surface = Panel("Surface", ring.transform, Vector2.zero, Vector2.one * (IconCardSize - 6f), DuelVisualTheme.Card);
            DuelVisualTheme.Frame(surface);
            icon = Panel("Icon", ring.transform, Vector2.zero, Vector2.one * IconSize, Color.white);
            icon.preserveAspect = true;
            return ring;
        }

        private Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = art.UIFont;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
