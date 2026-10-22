using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyArenaView
    {
        public const float ResistanceImpactDuration = .24f;
        public const float ResistanceImpactContactTime = .10f;
        private const float ResistanceImpactWindup = .025f;
        private const float ResistanceImpactSettle = .145f;
        private const float ResistanceImpactChestGap = 1.25f;
        private const float ResistanceImpactMaximumRush = 5f;
        private bool resistanceImpactPlaying, resistanceImpactContact;
        private bool playerResistanceChecks, enemyResistanceChecks;
        private float resistanceImpactTime, resistanceImpactDistance;
        private Vector3 playerResistanceBaseOffset, enemyResistanceBaseOffset;
        private Transform playerResistanceShadow, enemyResistanceShadow;
        private Vector3 playerResistanceShadowLocal, enemyResistanceShadowLocal;

        /// <summary>A real-time body check before a use effect is revealed. Combat and skill clocks are held by the caller.</summary>
        public bool IsResistanceImpactPlaying => resistanceImpactPlaying;
        /// <summary>Retains the contact edge after completion until this phase is cleared or a new one begins.</summary>
        public bool HasResistanceImpactContact => resistanceImpactContact;

        /// <summary>Braces, then drives the checking fighter forward without moving combat feet or aligning ground depth.
        /// The dummy can receive a check, but cannot perform one. Returns false when nobody can check.</summary>
        public bool BeginResistanceImpact(bool playerChecks, bool enemyChecks)
        {
            ClearResistanceImpact();
            if (disposed || suspension != null) return false;
            playerResistanceChecks = playerChecks && !player.Defeated;
            enemyResistanceChecks = enemyChecks && !enemy.Defeated && !EnemyIsDummy;
            if (!playerResistanceChecks && !enemyResistanceChecks) return false;
            resistanceImpactPlaying = true;
            playerResistanceBaseOffset = player.AttackVisualOffset;
            enemyResistanceBaseOffset = enemy.AttackVisualOffset;
            playerResistanceShadow = player.GroundShadow != null ? player.GroundShadow.transform : null;
            enemyResistanceShadow = enemy.GroundShadow != null ? enemy.GroundShadow.transform : null;
            if (playerResistanceShadow != null) playerResistanceShadowLocal = playerResistanceShadow.localPosition;
            if (enemyResistanceShadow != null) enemyResistanceShadowLocal = enemyResistanceShadow.localPosition;
            // Sprite pivots include empty space beside the torso. Measure the drawn chests, including mirroring,
            // rather than the floor roots, so the shoulder check actually reaches the opposing body.
            Vector3 chestDifference = ResistanceImpactBodyPoint(enemy) + enemyResistanceBaseOffset -
                ResistanceImpactBodyPoint(player) - playerResistanceBaseOffset;
            float availableGap = Mathf.Max(0f,
                arenaRoot.transform.InverseTransformVector(chestDifference).x - ResistanceImpactChestGap);
            resistanceImpactDistance = Mathf.Min(ResistanceImpactMaximumRush,
                availableGap / (playerResistanceChecks && enemyResistanceChecks ? 2f : 1f));
            SampleActor(player);
            SampleActor(enemy);
            RefreshDepthPresentation();
            return true;
        }

        /// <summary>Advances only this cosmetic phase on real time. Returns true exactly once when contact is crossed,
        /// including a long frame crossing the whole phase. The caller reveals the already-resolved use effect on that edge.</summary>
        public bool AdvanceResistanceImpact(float realSeconds)
        {
            if (!resistanceImpactPlaying || disposed || suspension != null || !(realSeconds > 0f) ||
                float.IsInfinity(realSeconds)) return false;
            resistanceImpactTime = Mathf.Min(ResistanceImpactDuration, resistanceImpactTime + realSeconds);
            bool contact = !resistanceImpactContact && resistanceImpactTime >= ResistanceImpactContactTime;
            if (contact)
            {
                resistanceImpactContact = true;
                // Use the actual contact picture even if a long frame has crossed its recovery as well.
                Vector3 center = (ResistanceImpactBodyPoint(player) + ResistanceImpactDrawOffset(player, ResistanceImpactContactTime) +
                    ResistanceImpactBodyPoint(enemy) + ResistanceImpactDrawOffset(enemy, ResistanceImpactContactTime)) * .5f;
                impactGlow.Emit(center, false, true, .8f);
                float direction = playerResistanceChecks == enemyResistanceChecks ? 0f : playerResistanceChecks ? 1f : -1f;
                cameraJolt = new Vector3(direction, .35f, 0f).normalized * settings.ImpactCameraShake * 1.25f;
                cameraJoltTime = .10f;
            }
            if (resistanceImpactTime >= ResistanceImpactDuration)
            {
                resistanceImpactPlaying = false;
                RestoreResistanceImpactShadows();
            }
            SampleActor(player);
            SampleActor(enemy);
            RefreshDepthPresentation();
            return contact;
        }

        /// <summary>Releases the temporary braced poses and draw offsets; it never rewinds either skill's clock.</summary>
        public void ClearResistanceImpact()
        {
            bool hadPhase = resistanceImpactPlaying || resistanceImpactContact;
            resistanceImpactPlaying = resistanceImpactContact = false;
            playerResistanceChecks = enemyResistanceChecks = false;
            resistanceImpactTime = resistanceImpactDistance = 0f;
            playerResistanceBaseOffset = enemyResistanceBaseOffset = Vector3.zero;
            RestoreResistanceImpactShadows();
            if (disposed || !hadPhase) return;
            SampleActor(player);
            SampleActor(enemy);
            RefreshDepthPresentation();
        }

        // The floor marks follow only this check's added draw travel. Ordinary sword lunges and the authoritative
        // ground positions/depth order keep their existing meaning.
        private void RefreshResistanceImpactCircles()
        {
            if (!resistanceImpactPlaying) return;
            if (playerGroundCircle != null)
                playerGroundCircle.Root.position += player.AttackVisualOffset - playerResistanceBaseOffset;
            if (enemyGroundCircle != null)
                enemyGroundCircle.Root.position += enemy.AttackVisualOffset - enemyResistanceBaseOffset;
            if (playerResistanceShadow != null)
                playerResistanceShadow.localPosition = playerResistanceShadowLocal +
                    playerResistanceShadow.parent.InverseTransformVector(player.AttackVisualOffset - playerResistanceBaseOffset);
            if (enemyResistanceShadow != null)
                enemyResistanceShadow.localPosition = enemyResistanceShadowLocal +
                    enemyResistanceShadow.parent.InverseTransformVector(enemy.AttackVisualOffset - enemyResistanceBaseOffset);
        }

        private void RestoreResistanceImpactShadows()
        {
            if (playerResistanceShadow != null) playerResistanceShadow.localPosition = playerResistanceShadowLocal;
            if (enemyResistanceShadow != null) enemyResistanceShadow.localPosition = enemyResistanceShadowLocal;
            playerResistanceShadow = enemyResistanceShadow = null;
        }

        // This overlay owns only the drawn pose. Logical feet, motion velocities, hit counts, reaction timers,
        // guard variants and attack clocks keep their existing values throughout the check.
        private bool TrySampleResistanceImpact(Actor actor)
        {
            if (!resistanceImpactPlaying || suspension != null || actor != player && actor != enemy) return false;
            if (actor == enemy && EnemyIsDummy)
            {
                // The fixed dummy sways only on an actual hit; no hurt or movement is fabricated here.
                actor.Renderer.sprite = dummyAnimations.GetIdle(actor.IdleClock);
            }
            else if (actor == player || actor == enemy && EnemyIsElisa)
                actor.Renderer.sprite = mobAnimations.GetBlockPose(actor.GuardVariant);
            else
                actor.Renderer.sprite = AnimationsFor(actor).GetGuard(actor.GuardVariant);
            SetAttackDrawOffset(actor, ResistanceImpactDrawOffset(actor, resistanceImpactTime));
            return true;
        }

        private Vector3 ResistanceImpactDrawOffset(Actor actor, float time)
        {
            bool checks = actor == player ? playerResistanceChecks : enemyResistanceChecks;
            bool receives = actor == player ? enemyResistanceChecks : playerResistanceChecks;
            float direction = actor == player ? 1f : -1f;
            float distance = checks ? resistanceImpactDistance * ResistanceImpactRush(time)
                : receives ? -.17f * ResistanceImpactRecoil(time) : 0f;
            if (actor == enemy && EnemyIsDummy) distance = 0f;
            Vector3 baseline = actor == player ? playerResistanceBaseOffset : enemyResistanceBaseOffset;
            return baseline + arenaRoot.transform.TransformVector(Vector3.right * (direction * distance));
        }

        private Vector3 ResistanceImpactBodyPoint(Actor actor)
        {
            // These authored guard poses place the chest just right of the SwordGirl pivot and directly over the
            // student/cadet pivot. The step HUD's targeting offset belongs to a different silhouette.
            Vector3 local = new Vector3(actor == player || actor == enemy && EnemyIsElisa ? .25f : 0f, -.25f, 0f);
            if (actor.Renderer.flipX) local.x = -local.x;
            if (actor.Renderer.flipY) local.y = -local.y;
            return actor.Renderer.transform.TransformPoint(local);
        }

        private static float ResistanceImpactRush(float time)
        {
            if (time < ResistanceImpactWindup)
                return -.07f * Mathf.SmoothStep(0f, 1f, time / ResistanceImpactWindup);
            if (time < ResistanceImpactContactTime)
                return Mathf.Lerp(-.07f, 1f, Mathf.SmoothStep(0f, 1f,
                    (time - ResistanceImpactWindup) / (ResistanceImpactContactTime - ResistanceImpactWindup)));
            if (time < ResistanceImpactSettle)
                return Mathf.Lerp(1f, .88f, Mathf.SmoothStep(0f, 1f,
                    (time - ResistanceImpactContactTime) / (ResistanceImpactSettle - ResistanceImpactContactTime)));
            return .88f * (1f - Mathf.SmoothStep(0f, 1f,
                (time - ResistanceImpactSettle) / (ResistanceImpactDuration - ResistanceImpactSettle)));
        }

        private static float ResistanceImpactRecoil(float time)
        {
            if (time < ResistanceImpactContactTime) return 0f;
            if (time < ResistanceImpactSettle)
                return Mathf.SmoothStep(0f, 1f,
                    (time - ResistanceImpactContactTime) / (ResistanceImpactSettle - ResistanceImpactContactTime));
            return 1f - Mathf.SmoothStep(0f, 1f,
                (time - ResistanceImpactSettle) / (ResistanceImpactDuration - ResistanceImpactSettle));
        }
    }
}
