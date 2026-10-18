using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Each fighter's foot marker: both fighters are 엘리사, so a plate at her feet says whose she is (내 캐릭터 /
    /// 상대, or 내 예약 차례 / 상대 예약 차례 while she holds the planning turn, with a pulsing ring). The viewer's own fighter
    /// is always on the arena's left, as in single play. The markers belong to the arena, so they draw under the duel HUD
    /// (<see cref="FootMarkerSortingOrder"/>): its log, a held skill's explanation and the Tab card cover them as they cover
    /// the fighters.</summary>
    public sealed partial class LocalVersusHud
    {
        /// <summary>The foot markers' own canvas, under the duel HUD (<see cref="LegacyCombatHud.SortingOrder"/>); the rest of
        /// the overlay stays over it (<see cref="SortingOrder"/>).</summary>
        public const int FootMarkerSortingOrder = 90;
        public const string FootMarkerLayerName = "Versus Foot Markers";
        public const string OwnMarkerName = "Versus Own Foot Marker", OpponentMarkerName = "Versus Opponent Foot Marker";
        public const string OwnText = "내 캐릭터", OpponentText = "상대", OwnActingText = "내 예약 차례",
            OpponentActingText = "상대 예약 차례";
        // 0: the viewer's own fighter (the arena's left), 1: the opponent's (the right).
        private readonly RectTransform[] footMarkers = new RectTransform[2];
        private readonly Image[] footPlates = new Image[2];
        private readonly Text[] footLabels = new Text[2];
        private readonly DuelVersusTurnRingGraphic[] turnRings = new DuelVersusTurnRingGraphic[2];
        private readonly Transform[] actors = new Transform[2];
        private Camera actorCamera;

        /// <summary>The arena's camera and its two fighters: the viewer's own on the left, the opponent's on the right.</summary>
        public void SetActorAnchors(Camera camera, Transform ownActor, Transform opponentActor)
        {
            actorCamera = camera;
            actors[0] = ownActor;
            actors[1] = opponentActor;
        }

        private void BuildTurnMarkers()
        {
            // Their own canvas under the duel HUD. Stretched over the battle layer, it keeps the overlay's centre and units,
            // which RefreshTurnMarkers places them in; nothing on it takes a click, so it needs no raycaster.
            RectTransform layer = Rect(FootMarkerLayerName, battleLayer, Vector2.zero, Vector2.zero);
            Stretch(layer);
            var layerCanvas = layer.gameObject.AddComponent<Canvas>();
            layerCanvas.overrideSorting = true;
            layerCanvas.sortingOrder = FootMarkerSortingOrder;
            for (int side = 0; side < 2; side++)
            {
                RectTransform marker = Rect(side == 0 ? OwnMarkerName : OpponentMarkerName,
                    layer, Vector2.zero, new Vector2(200f, 68f));
                marker.gameObject.SetActive(false);
                footMarkers[side] = marker;
                RectTransform ring = Rect("Versus Turn Ring", marker,
                    new Vector2(0f, 13f), new Vector2(166f, 42f));
                turnRings[side] = ring.gameObject.AddComponent<DuelVersusTurnRingGraphic>();
                turnRings[side].raycastTarget = false;
                turnRings[side].gameObject.SetActive(false);
                Image plate = Panel("Versus Foot Identity", marker,
                    new Vector2(0f, -17f), new Vector2(190f, 29f), DuelVisualTheme.Surface);
                DuelVisualTheme.Frame(plate);
                footPlates[side] = plate;
                Text label = Label("Versus Foot Identity Label", plate.transform,
                    Vector2.zero, new Vector2(180f, 25f), 16, DuelVisualTheme.Foreground);
                label.alignment = TextAnchor.MiddleCenter;
                footLabels[side] = label;
            }
        }

        private void RefreshTurnMarkers(LocalVersusMatch current, int localPlayer, bool planning)
        {
            Vector2 half = root.rect.size * .5f;
            if (half.x < 1f || half.y < 1f) half = new Vector2(960f, 540f);
            for (int side = 0; side < 2; side++)
            {
                bool visible = actorCamera != null && actors[side] != null;
                RectTransform marker = footMarkers[side];
                marker.gameObject.SetActive(visible);
                if (!visible) continue;
                Vector3 screen = actorCamera.WorldToScreenPoint(
                    actors[side].TransformPoint(new Vector3(0f, .05f, 0f)));
                if (screen.z <= 0f ||
                    !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        root, screen, null, out Vector2 point))
                {
                    marker.gameObject.SetActive(false);
                    continue;
                }
                point.y -= 39f;
                point.x = Mathf.Clamp(point.x, -half.x + 110f, half.x - 110f);
                point.y = Mathf.Clamp(point.y, -half.y + 245f, half.y - 210f);
                marker.anchoredPosition = point;

                bool own = side == 0;
                int player = own ? localPlayer : 1 - localPlayer;
                bool acting = planning && current.CurrentPlanner == player;
                footPlates[side].color = acting
                    ? own ? DuelVisualTheme.Accent : DuelVisualTheme.Danger
                    : own ? DuelVisualTheme.Paper : DuelVisualTheme.Surface;
                footLabels[side].color = acting || own
                    ? DuelVisualTheme.Ink : DuelVisualTheme.Foreground;
                footLabels[side].text = acting
                    ? own ? OwnActingText : OpponentActingText
                    : own ? OwnText : OpponentText;
                DuelVersusTurnRingGraphic ring = turnRings[side];
                ring.gameObject.SetActive(acting);
                if (acting)
                {
                    ring.color = own ? DuelVisualTheme.Accent : DuelVisualTheme.Danger;
                    float pulse = 1f + .065f * Mathf.Sin(Time.unscaledTime * 6f);
                    ring.rectTransform.localScale = Vector3.one * pulse;
                }
            }
        }
    }

    /// <summary>A small, non-interactive ring at the feet of the player whose turn is active.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DuelVersusTurnRingGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect bounds = rectTransform.rect;
            float x = bounds.width * .46f;
            float y = bounds.height * .34f;
            if (x < 1f || y < 1f) return;
            const int segments = 48;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float b = (i + 1) * Mathf.PI * 2f / segments;
                Vector2 ai = new Vector2(Mathf.Cos(a) * x * .78f, Mathf.Sin(a) * y * .78f);
                Vector2 ao = new Vector2(Mathf.Cos(a) * x, Mathf.Sin(a) * y);
                Vector2 bi = new Vector2(Mathf.Cos(b) * x * .78f, Mathf.Sin(b) * y * .78f);
                Vector2 bo = new Vector2(Mathf.Cos(b) * x, Mathf.Sin(b) * y);
                int start = vertices.currentVertCount;
                vertices.AddVert(ai, color, Vector2.zero);
                vertices.AddVert(bi, color, Vector2.zero);
                vertices.AddVert(bo, color, Vector2.zero);
                vertices.AddVert(ao, color, Vector2.zero);
                vertices.AddTriangle(start, start + 1, start + 2);
                vertices.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
