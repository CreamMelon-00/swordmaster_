using System;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>One lane's gear on a loadout screen (<see cref="CampaignLoadoutHud"/> and <see cref="VersusLoadoutHud"/>):
    /// the battle dock's brass wheel (<see cref="AcademyUiSkin.GearWheel"/>) with one socket per lane slot round its
    /// rim, each the battle's socket picture (<see cref="LegacyCombatHud.SocketTexture"/>). A socket shows only its skill's
    /// icon and ACT cost; an empty one is the bare ring. The lane's order runs round the gear the way the battle's gears
    /// turn (<see cref="LegacySkillGear.LoadoutSocketAngle"/>): the first at the top, where the battle shows the current
    /// skill, the next a socket against the turn, so the order learned here is the order seen in battle. A small curved
    /// arrow beside the top (<see cref="LoadoutGearArc"/>) shows that turn (<see cref="LegacySkillGear.TurnDirection"/>).
    /// <para>The gear keeps no state: its owner says what each socket holds, which socket is selected and which may take a
    /// dragged skill, and wires input. The wheel and every socket are buttons (a click on the wheel, one on a socket), and
    /// the owner puts its drag and drop handlers on the sockets. Only the sockets light up under the pointer: they are the
    /// wheel's children, so a tint of the wheel's own would light the whole wheel under any socket. The pointer reaches the
    /// gear only on its drawn wheel or a socket, never in its picture's empty corners (<see cref="LoadoutGearHitArea"/>).
    /// Nodes are named from <c>prefix</c>: the gear
    /// <c>"&lt;prefix&gt; Gear Q"</c>, its sockets <c>"&lt;prefix&gt; Slot Q 1"</c>.., the glow round a socket a dragged skill
    /// may go into <c>"&lt;prefix&gt; Slot Target Q 1"</c>.., and inside each socket <c>Slot Icon</c>, <c>Slot ACT Plate</c>,
    /// <c>Slot ACT</c> (its only text) and <c>Slot Selection</c>.</para></summary>
    public sealed class LoadoutGear
    {
        /// <summary>The wheel's picture (its tips a little inside, <see cref="LegacySkillGear.TipShare"/>).</summary>
        public const float WheelSize = 240f;
        /// <summary>How far from its centre the wheel takes the pointer: its tooth tips (the rest of the square picture is
        /// its soft edge and empty corners). A socket takes it over its whole rect, even where it reaches past.</summary>
        public const float HitRadius = WheelSize * .5f * LegacySkillGear.TipShare;
        /// <summary>The sockets' centres from the gear's.</summary>
        public const float SocketRadius = 86f;
        /// <summary>The first socket (the top) and the others: the first larger, as the battle's current skill.</summary>
        public const float FirstSocketSize = 74f, SocketSize = 66f;
        /// <summary>How much smaller than its socket the icon is: the brass ring stays visible round it.</summary>
        public const float IconInset = 12f;
        /// <summary>The turn arrow: its radius, the angle it starts at beside the top socket, how far it sweeps the way the
        /// gears turn, its line and its head.</summary>
        public const float ArrowRadius = 130f, ArrowStart = 70f, ArrowSweep = 56f, ArrowWidth = 3f, ArrowHead = 13f;

        private const float SelectionPad = 8f, TargetPad = 16f, PlateHeight = 18f;

        private readonly Socket[] sockets;

        private sealed class Socket
        {
            public Button Button;
            public Image Ring, Icon, Plate;
            public Text Cost;
            public LoadoutGearArc Selection, Target;
        }

        /// <param name="parent">Where the gear goes; <paramref name="centre"/> is its centre there.</param>
        /// <param name="socketSprite">The socket picture, owned by the caller (it releases it).</param>
        /// <param name="clickSocket">A socket was clicked (its index, 0 the top).</param>
        /// <param name="clickWheel">The wheel was clicked off its sockets.</param>
        public LoadoutGear(Transform parent, Font font, string prefix, int lane, int socketCount, Vector2 centre,
            Sprite socketSprite, Action<int> clickSocket, Action clickWheel)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (socketCount < 1) throw new ArgumentOutOfRangeException(nameof(socketCount));
            Lane = lane;
            string key = SkillLaneStyle.Key(lane);
            Root = Rect(prefix + " Gear " + key, parent, centre, Vector2.one * WheelSize);
            var wheel = Root.gameObject.AddComponent<Image>();
            wheel.sprite = AcademyUiSkin.GearWheel;
            wheel.preserveAspect = true;
            // Without the shared picture the wheel still takes its clicks, unseen.
            wheel.color = wheel.sprite != null ? Color.white : Color.clear;
            wheel.raycastTarget = true;
            Wheel = Root.gameObject.AddComponent<Button>();
            Wheel.targetGraphic = wheel;
            Wheel.navigation = new Navigation { mode = Navigation.Mode.None };
            // No tint: the sockets are its children, so the pointer entering one enters the wheel too and would light it
            // all. The sockets keep theirs.
            Wheel.transition = Selectable.Transition.None;
            Wheel.onClick.AddListener(() =>
            {
                ClearFocus();
                clickWheel?.Invoke();
            });

            TurnArrow = Arc("Turn Arrow", Root, Vector2.one * (ArrowRadius + ArrowHead) * 2f);
            TurnArrow.Configure(ArrowRadius, ArrowWidth, ArrowStart, LegacySkillGear.TurnDirection * ArrowSweep, ArrowHead);
            TurnArrow.color = Color.Lerp(SkillLaneStyle.Edge(lane), DuelVisualTheme.Accent, .35f);

            sockets = new Socket[socketCount];
            for (int index = 0; index < socketCount; index++)
            {
                int selected = index;
                float size = SizeOf(index);
                Vector2 at = SocketOffset(index, socketCount);
                var socket = new Socket();
                // The glow first, so it sits behind its socket.
                socket.Target = Arc(prefix + " Slot Target " + key + " " + (index + 1), Root, Vector2.one * (size + TargetPad + 8f));
                socket.Target.rectTransform.anchoredPosition = at;
                socket.Target.Configure((size + TargetPad) * .5f, 5f);
                socket.Target.color = Color.Lerp(SkillLaneStyle.Edge(lane), DuelVisualTheme.Accent, .45f);
                socket.Target.gameObject.SetActive(false);

                RectTransform node = Rect(prefix + " Slot " + key + " " + (index + 1), Root, at, Vector2.one * size);
                socket.Ring = node.gameObject.AddComponent<Image>();
                socket.Ring.sprite = socketSprite;
                socket.Ring.color = Color.white;
                socket.Ring.raycastTarget = true;
                socket.Button = node.gameObject.AddComponent<Button>();
                socket.Button.targetGraphic = socket.Ring;
                socket.Button.navigation = new Navigation { mode = Navigation.Mode.None };
                Tint(socket.Button);
                socket.Button.onClick.AddListener(() =>
                {
                    ClearFocus();
                    clickSocket?.Invoke(selected);
                });

                socket.Selection = Arc("Slot Selection", node, Vector2.one * (size + SelectionPad + 4f));
                socket.Selection.Configure((size + SelectionPad) * .5f, 3f);
                socket.Selection.color = DuelVisualTheme.Accent;
                socket.Selection.gameObject.SetActive(false);

                socket.Icon = Rect("Slot Icon", node, Vector2.zero, Vector2.one * (size - IconInset)).gameObject.AddComponent<Image>();
                socket.Icon.preserveAspect = true;
                socket.Icon.raycastTarget = false;
                socket.Icon.enabled = false;

                // The cost on a small dark plate across the socket's lower rim, under the icon (the battle's window carries
                // it on the hub, under the current skill).
                RectTransform plate = Rect("Slot ACT Plate", node, new Vector2(0f, -size * .5f - 1f), new Vector2(54f, PlateHeight));
                socket.Plate = plate.gameObject.AddComponent<Image>();
                socket.Plate.color = new Color(DuelVisualTheme.Track.r, DuelVisualTheme.Track.g, DuelVisualTheme.Track.b, .94f);
                socket.Plate.raycastTarget = false;
                socket.Cost = Rect("Slot ACT", plate, Vector2.zero, new Vector2(58f, PlateHeight)).gameObject.AddComponent<Text>();
                socket.Cost.font = font;
                socket.Cost.fontSize = 13;
                socket.Cost.alignment = TextAnchor.MiddleCenter;
                socket.Cost.color = DuelVisualTheme.Foreground;
                socket.Cost.supportRichText = false;
                socket.Cost.raycastTarget = false;
                socket.Cost.horizontalOverflow = HorizontalWrapMode.Overflow;
                socket.Cost.verticalOverflow = VerticalWrapMode.Truncate;
                plate.gameObject.SetActive(false);
                sockets[index] = socket;
            }
            var socketRects = new RectTransform[socketCount];
            for (int index = 0; index < socketCount; index++) socketRects[index] = sockets[index].Button.GetComponent<RectTransform>();
            HitArea = Root.gameObject.AddComponent<LoadoutGearHitArea>();
            HitArea.Configure(HitRadius, socketRects);
        }

        public RectTransform Root { get; }
        /// <summary>The wheel's button (the gear's root): a click off the sockets.</summary>
        public Button Wheel { get; }
        /// <summary>The arrow showing which way the gear turns (<see cref="LegacySkillGear.TurnDirection"/>).</summary>
        public LoadoutGearArc TurnArrow { get; }
        /// <summary>Where the gear takes the pointer: its drawn wheel and its sockets.</summary>
        public LoadoutGearHitArea HitArea { get; }
        public int Lane { get; }
        public int SocketCount => sockets.Length;

        public Button SocketButton(int index) => sockets[index].Button;

        /// <summary>Socket <paramref name="index"/> holds a skill: its icon and its ACT cost, nothing else.</summary>
        public void Show(int index, Sprite icon, int cost)
        {
            Socket socket = sockets[index];
            socket.Icon.sprite = icon;
            socket.Icon.enabled = icon != null;
            string text = "ACT " + cost;
            if (socket.Cost.text != text) socket.Cost.text = text;
            if (!socket.Plate.gameObject.activeSelf) socket.Plate.gameObject.SetActive(true);
        }

        /// <summary>Socket <paramref name="index"/> is empty: the bare ring.</summary>
        public void ShowEmpty(int index)
        {
            Socket socket = sockets[index];
            socket.Icon.sprite = null;
            socket.Icon.enabled = false;
            socket.Cost.text = string.Empty;
            if (socket.Plate.gameObject.activeSelf) socket.Plate.gameObject.SetActive(false);
        }

        /// <summary>Rings the selected socket (-1: none).</summary>
        public void SetSelected(int index)
        {
            for (int socket = 0; socket < sockets.Length; socket++)
            {
                GameObject ring = sockets[socket].Selection.gameObject;
                if (ring.activeSelf != (socket == index)) ring.SetActive(socket == index);
            }
        }

        /// <summary>Glows round a socket a dragged skill may go into.</summary>
        public void SetTarget(int index, bool target)
        {
            GameObject glow = sockets[index].Target.gameObject;
            if (glow.activeSelf != target) glow.SetActive(target);
        }

        /// <summary>The size of socket <paramref name="index"/>: the top one larger.</summary>
        public static float SizeOf(int index) => index == 0 ? FirstSocketSize : SocketSize;

        /// <summary>Where socket <paramref name="index"/> of <paramref name="count"/> sits from the gear's centre (y up):
        /// round the rim the way the battle's gears turn, the first at the top.</summary>
        public static Vector2 SocketOffset(int index, int count)
        {
            float angle = LegacySkillGear.LoadoutSocketAngle(index, count);
            return new Vector2(LegacySkillGear.SlotX(angle, SocketRadius), LegacySkillGear.SlotY(angle, SocketRadius));
        }

        // A button's tint as the theme's (DuelVisualTheme.StyleButton) without its rectangular trim: the sockets are
        // pictures with their own round silhouettes.
        private static void Tint(Button button)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.10f, 1.06f, .95f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.80f, .75f, .65f, 1f);
            colors.disabledColor = new Color(.63f, .60f, .55f, 1f);
            colors.fadeDuration = .10f;
            button.colors = colors;
        }

        private static void ClearFocus()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private static LoadoutGearArc Arc(string name, Transform parent, Vector2 size)
        {
            RectTransform rect = Rect(name, parent, Vector2.zero, size);
            rect.gameObject.AddComponent<CanvasRenderer>();
            var arc = rect.gameObject.AddComponent<LoadoutGearArc>();
            arc.raycastTarget = false;
            return arc;
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
    }

    /// <summary>Where a loadout gear (<see cref="LoadoutGear"/>) takes the pointer: within <see cref="Radius"/> of its centre
    /// (the drawn wheel) or on one of its sockets, never in its square picture's empty corners, where a click would turn
    /// the rack to the gear's lane with nothing visible clicked. Unity asks a graphic's raycast filters and every
    /// ancestor's, so this filter on the gear also governs its sockets (its children): it lets each socket's whole rect
    /// through, also where the top socket reaches past the wheel's tips.</summary>
    public sealed class LoadoutGearHitArea : MonoBehaviour, ICanvasRaycastFilter
    {
        private float radius;
        private RectTransform[] sockets = Array.Empty<RectTransform>();

        /// <summary>How far from the gear's centre the wheel takes the pointer.</summary>
        public float Radius => radius;

        internal void Configure(float wheelRadius, RectTransform[] socketRects)
        {
            radius = Mathf.Max(0f, wheelRadius);
            sockets = socketRects ?? Array.Empty<RectTransform>();
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            var rect = (RectTransform)transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, eventCamera, out Vector2 local) &&
                (local - rect.rect.center).sqrMagnitude <= radius * radius) return true;
            foreach (RectTransform socket in sockets)
                if (socket != null && socket.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(socket, screenPoint, eventCamera)) return true;
            return false;
        }
    }

    /// <summary>A brass arc drawn as its own mesh, no texture: a loadout gear's turn arrow (a head at its end), the ring
    /// round its selected socket and the glow round the sockets a dragged skill may go into. Angles are degrees
    /// counter-clockwise from the right, from the rect's centre; a negative sweep runs clockwise. Never takes input.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LoadoutGearArc : MaskableGraphic
    {
        private float radius = 10f, thickness = 2f, startAngle = 90f, sweep = 360f, head;

        public float Radius => radius;
        public float Thickness => thickness;
        public float StartAngle => startAngle;
        /// <summary>How far it runs from <see cref="StartAngle"/>: negative clockwise.</summary>
        public float Sweep => sweep;
        public float EndAngle => startAngle + sweep;
        /// <summary>The arrowhead's length (0: a plain arc or ring).</summary>
        public float Head => head;
        /// <summary>Which way an arrow points, as the sign of its sweep (-1 clockwise, 1 counter-clockwise); 0 without a
        /// head.</summary>
        public int Direction => head > 0f && sweep != 0f ? (sweep < 0f ? -1 : 1) : 0;
        /// <summary>The arrow's tip (its end on the arc), from the rect's centre.</summary>
        public Vector2 Tip => Point(EndAngle, radius);

        public void Configure(float radius, float thickness, float startAngle = 90f, float sweep = 360f, float head = 0f)
        {
            this.radius = Mathf.Max(0f, radius);
            this.thickness = Mathf.Max(0f, thickness);
            this.startAngle = startAngle;
            this.sweep = Mathf.Clamp(sweep, -360f, 360f);
            this.head = Mathf.Max(0f, head);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!(radius > 0f) || !(thickness > 0f) || sweep == 0f) return;
            Color32 tint = color;
            float sign = sweep < 0f ? -1f : 1f;
            // The head takes its length off the end of the line, so the tip lands on the end angle.
            float headDegrees = head > 0f ? Mathf.Min(Mathf.Abs(sweep), head / radius * Mathf.Rad2Deg) : 0f;
            float body = sweep - sign * headDegrees;
            float inner = radius - thickness * .5f, outer = radius + thickness * .5f;
            if (Mathf.Abs(body) > .01f)
            {
                int segments = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(body) / 4f));
                for (int step = 0; step <= segments; step++)
                {
                    float angle = startAngle + body * step / segments;
                    mesh.AddVert(Point(angle, inner), tint, Vector2.zero);
                    mesh.AddVert(Point(angle, outer), tint, Vector2.zero);
                }
                for (int step = 0; step < segments; step++)
                {
                    int at = step * 2;
                    mesh.AddTriangle(at, at + 1, at + 3);
                    mesh.AddTriangle(at, at + 3, at + 2);
                }
            }
            if (headDegrees > 0f)
            {
                int start = mesh.currentVertCount;
                float baseAngle = EndAngle - sign * headDegrees, half = Mathf.Max(thickness * 2f, head * .5f);
                mesh.AddVert(Point(baseAngle, radius - half), tint, Vector2.zero);
                mesh.AddVert(Point(baseAngle, radius + half), tint, Vector2.zero);
                mesh.AddVert(Point(EndAngle, radius), tint, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2);
            }
        }

        private static Vector2 Point(float degrees, float distance)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians) * distance, Mathf.Sin(radians) * distance);
        }
    }
}
