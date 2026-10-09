using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    public sealed partial class LocalVersusHud
    {
        private readonly RectTransform[] onlineFootMarkers = new RectTransform[2];
        private readonly Image[] onlineFootPlates = new Image[2];
        private readonly Text[] onlineFootLabels = new Text[2];
        private readonly DuelVersusTurnRingGraphic[] onlineTurnRings = new DuelVersusTurnRingGraphic[2];

        private void BuildTurnMarkers()
        {
            for (int side = 0; side < 2; side++)
            {
                RectTransform marker = Rect("Versus Foot Marker " + (side + 1) + "P",
                    root, Vector2.zero, new Vector2(200f, 68f));
                marker.gameObject.SetActive(false);
                onlineFootMarkers[side] = marker;
                RectTransform ring = Rect("Versus Turn Ring", marker,
                    new Vector2(0f, 13f), new Vector2(166f, 42f));
                onlineTurnRings[side] = ring.gameObject.AddComponent<DuelVersusTurnRingGraphic>();
                onlineTurnRings[side].raycastTarget = false;
                onlineTurnRings[side].gameObject.SetActive(false);
                Image plate = Panel("Versus Foot Identity", marker,
                    new Vector2(0f, -17f), new Vector2(190f, 29f), DuelVisualTheme.Surface);
                DuelVisualTheme.Frame(plate);
                onlineFootPlates[side] = plate;
                Text label = Label("Versus Foot Identity Label", plate.transform,
                    Vector2.zero, new Vector2(180f, 25f), 16, DuelVisualTheme.Foreground);
                label.alignment = TextAnchor.MiddleCenter;
                onlineFootLabels[side] = label;
            }
        }

        private void SetTurnMarkersEnabled(bool online)
        {
            for (int side = 0; side < 2; side++)
                onlineFootMarkers[side].gameObject.SetActive(online &&
                    actorCamera != null && actors[side] != null);
        }

        private void RefreshTurnMarkers(LocalVersusMatch current, bool planning)
        {
            if (!onlineLayout) return;
            Vector2 half = root.rect.size * .5f;
            if (half.x < 1f || half.y < 1f) half = new Vector2(960f, 540f);
            for (int side = 0; side < 2; side++)
            {
                bool visible = actorCamera != null && actors[side] != null;
                RectTransform marker = onlineFootMarkers[side];
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

                bool own = side == localPlayer;
                bool acting = planning && current.CurrentPlanner == side;
                onlineFootPlates[side].color = acting
                    ? own ? DuelVisualTheme.Accent : DuelVisualTheme.Danger
                    : own ? DuelVisualTheme.Paper : DuelVisualTheme.Surface;
                onlineFootLabels[side].color = acting || own
                    ? DuelVisualTheme.Ink : DuelVisualTheme.Foreground;
                onlineFootLabels[side].text = acting
                    ? own ? "내 행동 차례" : "상대 행동 차례"
                    : own ? "내 캐릭터" : "상대";
                DuelVersusTurnRingGraphic ring = onlineTurnRings[side];
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
