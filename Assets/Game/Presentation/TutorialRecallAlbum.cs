using System;
using System.Collections;
using System.Collections.Generic;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>What the cutscene's tutorial recall (<c>@recall</c>) can bring back (<see cref="TutorialRecall"/>). While a
    /// coached beat of the 서막's first missions is on screen, the controller asks for it (<see cref="RequestCapture"/>):
    /// once the card has settled, the screen is captured at the end of a frame, shrunk to at most
    /// <see cref="MaximumWidth"/> wide and partly drained of colour, and kept, one per beat, at most
    /// <see cref="TutorialRecall.MaximumScreens"/> for the session. A recall picks one of them at random
    /// (<see cref="TryPick"/>); with none, a random coached beat to redraw as a card. The album owns its screens: it
    /// destroys those it drops, and all of them on <see cref="Clear"/> (title, new game) and <see cref="Dispose"/>.
    /// Nothing is captured in batch mode or without a graphics device, and a beat whose capture failed is not tried
    /// again that session.</summary>
    public sealed class TutorialRecallAlbum : IDisposable
    {
        /// <summary>The widest a kept screen is, in pixels.</summary>
        public const int MaximumWidth = 960;
        /// <summary>How long a beat's card stays up before its screen is captured, in real seconds.</summary>
        public const float CaptureDelay = .35f;
        /// <summary>How much colour a kept screen keeps (1 = all), so it already reads as a memory.</summary>
        public const float MemorySaturation = .6f;

        private readonly MonoBehaviour host;
        private readonly List<Texture2D> screens = new List<Texture2D>(TutorialRecall.MaximumScreens);
        // Every beat offered this session, kept or not: each is captured once.
        private readonly HashSet<string> offeredKeys = new HashSet<string>();
        // Every beat whose capture failed this session: not tried again, and no part of what was offered.
        private readonly HashSet<string> failedKeys = new HashSet<string>();
        private System.Random random;
        private Coroutine pending;
        private string pendingKey;
        private bool disposed;

        /// <param name="host">Runs the captures; null for an album that is only handed screens.</param>
        /// <param name="random">Every choice the album makes; seed it to fix them. A fresh source when null.</param>
        public TutorialRecallAlbum(MonoBehaviour host, System.Random random = null)
        {
            this.host = host;
            this.random = random ?? new System.Random();
        }

        /// <summary>The source of every choice (which screens are kept, which one is recalled); replace it to fix them.</summary>
        public System.Random Random
        {
            get => random;
            set => random = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>The kept screens, at most <see cref="TutorialRecall.MaximumScreens"/>.</summary>
        public IReadOnlyList<Texture2D> Screens => screens;
        /// <summary>How many beats were offered this session, kept or not.</summary>
        public int OfferedCount => offeredKeys.Count;
        /// <summary>Whether a capture is waiting for its beat's card to settle.</summary>
        public bool IsCapturing => pending != null;
        /// <summary>The key of the beat waiting to be captured, or null.</summary>
        public string PendingKey => pendingKey;
        /// <summary>Whether this album can capture the screen at all (a host, a graphics device, not batch mode).</summary>
        public bool CapturesScreens => !disposed && host != null && !Application.isBatchMode &&
            SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

        public bool HasOffered(string key) => key != null && offeredKeys.Contains(key);

        /// <summary>Whether beat <paramref name="key"/> was offered this session or its capture failed: either way it is
        /// not captured again.</summary>
        public bool HasTried(string key) => HasOffered(key) || (key != null && failedKeys.Contains(key));

        /// <summary>Captures the beat on screen (<paramref name="key"/>, <see cref="TutorialRecall.Key"/>) once its card has
        /// settled, if <paramref name="stillShown"/> then says the same beat is still up. A beat already tried
        /// (<see cref="HasTried"/>), or already waiting, is not asked again; a different one replaces the one waiting.
        /// Returns whether it waits now.</summary>
        public bool RequestCapture(string key, Func<bool> stillShown)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("A beat key is required.", nameof(key));
            if (stillShown == null) throw new ArgumentNullException(nameof(stillShown));
            if (!CapturesScreens || HasTried(key) || !host.gameObject.activeInHierarchy) return false;
            if (pendingKey == key) return true;
            StopPending();
            pendingKey = key;
            pending = host.StartCoroutine(CaptureWhenSettled(key, stillShown));
            return true;
        }

        /// <summary>Captures beat <paramref name="key"/>'s screen now: <paramref name="grab"/> returns it as a readable
        /// texture (the game's is <c>ScreenCapture.CaptureScreenshotAsTexture</c>), which is made into a memory
        /// (<see cref="MakeMemory"/>) and offered (<see cref="Offer"/>), then destroyed. If it returns nothing or throws,
        /// the beat has failed: one warning, and it is not tried again this session (a recall shows another screen or
        /// redraws a card). A beat already tried is left alone. Returns whether the screen was kept.</summary>
        public bool CaptureNow(string key, Func<Texture2D> grab)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("A beat key is required.", nameof(key));
            if (grab == null) throw new ArgumentNullException(nameof(grab));
            if (disposed || HasTried(key)) return false;
            Texture2D shot = null, memory = null;
            string failure = null;
            bool kept = false;
            try
            {
                shot = grab();
                if (shot == null) failure = "no screen came back";
                else
                {
                    memory = MakeMemory(shot, "Tutorial Recall " + key);
                    kept = Offer(key, memory);
                    memory = null; // The album's now, kept or destroyed.
                }
            }
            catch (Exception exception)
            {
                failure = exception.Message;
            }
            finally
            {
                // The shot always, and the memory if it never reached the album.
                Destroy(shot);
                Destroy(memory);
            }
            if (failure == null) return kept;
            failedKeys.Add(key);
            // A recall without this screen shows another or redraws a card instead; the lesson itself goes on.
            Debug.LogWarning($"Tutorial recall: the screen of coach beat {key} could not be captured ({failure}).");
            return false;
        }

        /// <summary>Offers <paramref name="screen"/> as the memory of beat <paramref name="key"/>, taking ownership of it: it
        /// is kept while there is room, then in a random place or not at all, so every beat offered has the same chance
        /// (<see cref="TutorialRecall.Slot"/>); a screen dropped or replaced is destroyed. A beat already offered keeps
        /// what it had. Returns whether <paramref name="screen"/> was kept.</summary>
        public bool Offer(string key, Texture2D screen)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("A beat key is required.", nameof(key));
            if (screen == null) throw new ArgumentNullException(nameof(screen));
            if (disposed || !offeredKeys.Add(key))
            {
                Destroy(screen);
                return false;
            }
            int slot = TutorialRecall.Slot(random, offeredKeys.Count, TutorialRecall.MaximumScreens);
            if (slot < 0)
            {
                Destroy(screen);
                return false;
            }
            if (slot == screens.Count) screens.Add(screen);
            else
            {
                Destroy(screens[slot]);
                screens[slot] = screen;
            }
            return true;
        }

        /// <summary>What a recall shows: a kept screen at random, or, with none, a coached beat at random to redraw as a
        /// card (<paramref name="screen"/> null). False when there is neither.</summary>
        public bool TryPick(out Texture2D screen, out TutorialRecallBeat beat)
        {
            screen = null;
            beat = null;
            if (disposed) return false;
            if (screens.Count > 0)
            {
                screen = screens[TutorialRecall.Pick(random, screens.Count)];
                return true;
            }
            IReadOnlyList<TutorialRecallBeat> beats = TutorialRecall.Beats;
            if (beats.Count == 0) return false;
            beat = beats[TutorialRecall.Pick(random, beats.Count)];
            return true;
        }

        /// <summary>Forgets the session: the capture waiting stops, every kept screen is destroyed, and every beat may be
        /// captured again (a failed one too).</summary>
        public void Clear()
        {
            StopPending();
            foreach (Texture2D screen in screens) Destroy(screen);
            screens.Clear();
            offeredKeys.Clear();
            failedKeys.Clear();
        }

        public void Dispose()
        {
            if (disposed) return;
            Clear();
            disposed = true;
        }

        /// <summary>A captured screen made into a memory: shrunk by a whole factor (box filtered) to at most
        /// <see cref="MaximumWidth"/> wide, its colour drawn toward grey to <see cref="MemorySaturation"/>. A new texture,
        /// not readable, owned by the caller; <paramref name="shot"/> must be readable and is left as it was.</summary>
        public static Texture2D MakeMemory(Texture2D shot, string name)
        {
            if (shot == null) throw new ArgumentNullException(nameof(shot));
            int factor = Mathf.Max(1, Mathf.CeilToInt(shot.width / (float)MaximumWidth));
            int width = Mathf.Max(1, shot.width / factor), height = Mathf.Max(1, shot.height / factor);
            Color32[] source = shot.GetPixels32();
            var pixels = new Color32[width * height];
            float samples = factor * factor;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int red = 0, green = 0, blue = 0;
                    for (int dy = 0; dy < factor; dy++)
                    {
                        int row = (y * factor + dy) * shot.width + x * factor;
                        for (int dx = 0; dx < factor; dx++)
                        {
                            Color32 colour = source[row + dx];
                            red += colour.r;
                            green += colour.g;
                            blue += colour.b;
                        }
                    }
                    float r = red / samples, g = green / samples, b = blue / samples;
                    float grey = r * .299f + g * .587f + b * .114f;
                    pixels[y * width + x] = new Color32(Channel(grey, r), Channel(grey, g), Channel(grey, b), 255);
                }
            }
            var memory = new Texture2D(width, height, TextureFormat.RGB24, false)
            {
                name = name, hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            memory.SetPixels32(pixels);
            memory.Apply(false, true);
            return memory;
        }

        private static byte Channel(float grey, float value)
            => (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(grey, value, MemorySaturation)), 0, 255);

        // After the card has settled, and at the end of a frame, when the screen holds what was drawn.
        private IEnumerator CaptureWhenSettled(string key, Func<bool> stillShown)
        {
            yield return new WaitForSecondsRealtime(CaptureDelay);
            yield return new WaitForEndOfFrame();
            pending = null;
            pendingKey = null;
            if (disposed || HasTried(key) || !stillShown()) yield break;
            CaptureNow(key, ScreenCapture.CaptureScreenshotAsTexture);
        }

        private void StopPending()
        {
            if (pending != null && host != null) host.StopCoroutine(pending);
            pending = null;
            pendingKey = null;
        }

        private static void Destroy(Texture2D texture)
        {
            if (texture == null) return;
            if (Application.isPlaying) Object.Destroy(texture);
            else Object.DestroyImmediate(texture);
        }
    }
}
