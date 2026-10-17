using UnityEngine;
using UnityEngine.Rendering;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyArenaView
    {
        // All scenery stays outside these groups: the forest path is -5 and its foreground ferns are +1.
        // Rank the two complete figures only within that band, keeping every layered body and attached aura together.
        private const int FrontActorDepthOrder = 0, RearActorDepthOrder = -1;
        private SortingGroup playerDepthGroup, enemyDepthGroup;
        private DuelGroundCircle playerGroundCircle, enemyGroundCircle;
        private bool depthPresentationVisible;

        /// <summary>The physical feet, including the actor's current ground-depth movement. Shader lunges never move them.</summary>
        public Vector3 PlayerGroundPosition => GroundPosition(player);
        public Vector3 EnemyGroundPosition => GroundPosition(enemy);
        /// <summary>The two cached world-space floor circles. They follow the figures and never intercept input.</summary>
        public Transform PlayerGroundCircle => playerGroundCircle?.Root;
        public Transform EnemyGroundCircle => enemyGroundCircle?.Root;
        public int PlayerDepthSortingOrder => playerDepthGroup != null ? playerDepthGroup.sortingOrder : 0;
        public int EnemyDepthSortingOrder => enemyDepthGroup != null ? enemyDepthGroup.sortingOrder : 0;

        private float LowestGroundVisualY
        {
            get
            {
                float bottom = FeetY - .34f;
                if (player.GroundShadow != null && player.GroundShadow.enabled)
                    bottom = Mathf.Min(bottom, player.GroundShadow.bounds.min.y);
                if (enemy.GroundShadow != null && enemy.GroundShadow.enabled)
                    bottom = Mathf.Min(bottom, enemy.GroundShadow.bounds.min.y);
                for (int i = 0; i < enemyFigures.Count; i++)
                {
                    Actor actor = enemyFigures[i].Actor;
                    if (!actor.Defeated && actor.GroundShadow != null && actor.GroundShadow.enabled)
                        bottom = Mathf.Min(bottom, actor.GroundShadow.bounds.min.y);
                }
                return bottom;
            }
        }

        private static Vector3 GroundPosition(Actor actor) => actor.Renderer.transform.TransformPoint(
            new Vector3(0f, MobStudentAnimationSet.GroundOffset, 0f));

        private void BuildDepthPresentation()
        {
            if (playerDepthGroup != null) return;
            playerDepthGroup = AddDepthGroup(player.Renderer);
            enemyDepthGroup = AddDepthGroup(enemy.Renderer);
            playerGroundCircle = new DuelGroundCircle("Player Ground Circle", arenaRoot.transform,
                ArenaLayer, player.Renderer.sortingLayerID, new Color(.90f, .70f, .38f, .68f));
            enemyGroundCircle = new DuelGroundCircle("Enemy Ground Circle", arenaRoot.transform,
                ArenaLayer, enemy.Renderer.sortingLayerID, new Color(.48f, .68f, .81f, .62f));
            RefreshDepthPresentation();
        }

        private static SortingGroup AddDepthGroup(SpriteRenderer body)
        {
            var group = body.gameObject.AddComponent<SortingGroup>();
            group.sortingLayerID = body.sortingLayerID;
            return group;
        }

        /// <summary>Battle-only circles and whole-figure depth sorting. Cutscenes recover their authored renderer order
        /// while this is hidden; restoring it rereads the actual ground positions immediately.</summary>
        public void SetDepthPresentationVisible(bool visible)
        {
            if (disposed) return;
            depthPresentationVisible = visible;
            RefreshDepthPresentation();
        }

        private void RefreshDepthPresentation()
        {
            if (playerDepthGroup == null || enemyDepthGroup == null) return;
            bool visible = depthPresentationVisible && suspension == null;
            if (enemyFigures.Count > 1)
            {
                RefreshRosterDepth(visible);
                return;
            }
            playerDepthGroup.enabled = enemyDepthGroup.enabled = visible;
            // Global floor order stays under both actors. A front actor's ring can never paint across a rear actor's body.
            playerGroundCircle.Root.position = PlayerGroundPosition;
            enemyGroundCircle.Root.position = EnemyGroundPosition;
            // A fixed tie makes an equal-depth exchange stable. Body/lower/shadow/aura orders stay local and unchanged.
            float playerY = PlayerGroundPosition.y, enemyY = EnemyGroundPosition.y;
            bool playerInFront = Mathf.Approximately(playerY, enemyY) ? depthLocalPlayer == 0 : playerY < enemyY;
            playerDepthGroup.sortingOrder = playerInFront ? FrontActorDepthOrder : RearActorDepthOrder;
            enemyDepthGroup.sortingOrder = playerInFront ? RearActorDepthOrder : FrontActorDepthOrder;
            playerGroundCircle.SetVisible(visible && player.Renderer.enabled && player.Renderer.gameObject.activeInHierarchy);
            enemyGroundCircle.SetVisible(visible && enemy.Renderer.enabled && enemy.Renderer.gameObject.activeInHierarchy);
        }

        private void DisposeDepthPresentation()
        {
            playerGroundCircle?.Dispose();
            enemyGroundCircle?.Dispose();
        }

        private void RefreshRosterDepth(bool visible)
        {
            playerDepthGroup.enabled = visible;
            playerDepthGroup.sortingOrder = -RosterDepthRank(player, 0);
            playerGroundCircle.Root.position = PlayerGroundPosition;
            playerGroundCircle.SetVisible(visible && player.Renderer.enabled && player.Renderer.gameObject.activeInHierarchy);
            for (int i = 0; i < enemyFigures.Count; i++)
            {
                EnemyFigure figure = enemyFigures[i];
                figure.DepthGroup.enabled = visible;
                figure.DepthGroup.sortingOrder = -RosterDepthRank(figure.Actor, i + 1);
                figure.Circle.Root.position = GroundPosition(figure.Actor);
                figure.Circle.SetVisible(visible && !figure.Actor.Defeated && figure.Actor.Renderer.enabled &&
                    figure.Actor.Renderer.gameObject.activeInHierarchy);
            }
        }

        private int RosterDepthRank(Actor actor, int identity)
        {
            float y = GroundPosition(actor).y;
            int rank = 0;
            if (actor != player && IsInFront(player, 0, y, identity)) rank++;
            for (int i = 0; i < enemyFigures.Count; i++)
                if (enemyFigures[i].Actor != actor && IsInFront(enemyFigures[i].Actor, i + 1, y, identity)) rank++;
            return rank;
        }

        private static bool IsInFront(Actor other, int otherIdentity, float y, int identity)
        {
            float otherY = GroundPosition(other).y;
            return Mathf.Approximately(otherY, y) ? otherIdentity < identity : otherY < y;
        }
    }
}
