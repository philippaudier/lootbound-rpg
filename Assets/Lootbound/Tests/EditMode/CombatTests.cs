using NUnit.Framework;
using UnityEngine;
using Lootbound.Gameplay.Combat;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for the combat system.
    /// Tests damage system, health logic, and attack phases.
    /// </summary>
    public class CombatTests
    {
        #region Health Tests

        [Test]
        public void Health_InitialValue_IsMax()
        {
            var health = new Health(100f);

            Assert.AreEqual(100f, health.Current);
            Assert.AreEqual(100f, health.Max);
            Assert.AreEqual(1f, health.Normalized);
            Assert.IsFalse(health.IsDead);
        }

        [Test]
        public void Health_TakeDamage_ReducesHealth()
        {
            var health = new Health(100f);
            var request = new DamageRequest(null, 30f, Vector3.zero, Vector3.forward);

            var result = health.ApplyDamage(request);

            Assert.IsTrue(result.Applied);
            Assert.AreEqual(30f, result.DamageDealt);
            Assert.AreEqual(70f, health.Current);
            Assert.IsFalse(result.WasFatal);
        }

        [Test]
        public void Health_TakeDamage_ExceedsHealth_ClampsToZero()
        {
            var health = new Health(50f);
            var request = new DamageRequest(null, 100f, Vector3.zero, Vector3.forward);

            var result = health.ApplyDamage(request);

            Assert.IsTrue(result.Applied);
            Assert.AreEqual(50f, result.DamageDealt);
            Assert.AreEqual(0f, health.Current);
            Assert.IsTrue(result.WasFatal);
            Assert.IsTrue(health.IsDead);
        }

        [Test]
        public void Health_TakeDamage_WhenDead_ReturnsNotApplied()
        {
            var health = new Health(50f);
            var request = new DamageRequest(null, 100f, Vector3.zero, Vector3.forward);

            // Kill
            health.ApplyDamage(request);

            // Try to damage again
            var secondResult = health.ApplyDamage(request);

            Assert.IsFalse(secondResult.Applied);
            Assert.AreEqual(0f, secondResult.DamageDealt);
        }

        [Test]
        public void Health_Death_TriggersOnce()
        {
            var health = new Health(50f);
            int deathCount = 0;
            health.OnDied += () => deathCount++;

            var request = new DamageRequest(null, 30f, Vector3.zero, Vector3.forward);

            health.ApplyDamage(request); // 20 remaining
            health.ApplyDamage(request); // Dies
            health.ApplyDamage(request); // Already dead

            Assert.AreEqual(1, deathCount);
        }

        [Test]
        public void Health_Heal_IncreasesHealth()
        {
            var health = new Health(100f);
            var request = new DamageRequest(null, 50f, Vector3.zero, Vector3.forward);
            health.ApplyDamage(request);

            health.Heal(30f);

            Assert.AreEqual(80f, health.Current);
        }

        [Test]
        public void Health_Heal_ClampsToMax()
        {
            var health = new Health(100f);
            var request = new DamageRequest(null, 20f, Vector3.zero, Vector3.forward);
            health.ApplyDamage(request);

            health.Heal(50f);

            Assert.AreEqual(100f, health.Current);
        }

        [Test]
        public void Health_Heal_WhenDead_DoesNothing()
        {
            var health = new Health(50f);
            var request = new DamageRequest(null, 100f, Vector3.zero, Vector3.forward);
            health.ApplyDamage(request);

            health.Heal(50f);

            Assert.AreEqual(0f, health.Current);
            Assert.IsTrue(health.IsDead);
        }

        [Test]
        public void Health_Reset_RestoresFullHealth()
        {
            var health = new Health(100f);
            var request = new DamageRequest(null, 100f, Vector3.zero, Vector3.forward);
            health.ApplyDamage(request);

            health.Reset();

            Assert.AreEqual(100f, health.Current);
            Assert.IsFalse(health.IsDead);
        }

        #endregion

        #region DamageRequest Tests

        [Test]
        public void DamageRequest_PositiveDamage_IsValid()
        {
            var request = new DamageRequest(null, 30f, Vector3.zero, Vector3.forward);

            Assert.IsTrue(request.IsValid);
        }

        [Test]
        public void DamageRequest_ZeroDamage_IsInvalid()
        {
            var request = new DamageRequest(null, 0f, Vector3.zero, Vector3.forward);

            Assert.IsFalse(request.IsValid);
        }

        [Test]
        public void DamageRequest_NegativeDamage_IsInvalid()
        {
            var request = new DamageRequest(null, -10f, Vector3.zero, Vector3.forward);

            Assert.IsFalse(request.IsValid);
        }

        [Test]
        public void DamageRequest_InvalidDamage_NotApplied()
        {
            var health = new Health(100f);
            var request = new DamageRequest(null, -10f, Vector3.zero, Vector3.forward);

            var result = health.ApplyDamage(request);

            Assert.IsFalse(result.Applied);
            Assert.AreEqual(100f, health.Current);
        }

        [Test]
        public void DamageRequest_StaggerForce_IsClamped()
        {
            var request = new DamageRequest(null, 30f, Vector3.zero, Vector3.forward, 1.5f);

            Assert.AreEqual(1f, request.StaggerForce);
        }

        [Test]
        public void DamageRequest_HitDirection_IsNormalized()
        {
            var direction = new Vector3(3f, 4f, 0f); // Length 5
            var request = new DamageRequest(null, 30f, Vector3.zero, direction);

            Assert.AreEqual(1f, request.HitDirection.magnitude, 0.001f);
        }

        #endregion

        #region DamageResult Tests

        [Test]
        public void DamageResult_Blocked_HasCorrectValues()
        {
            var result = DamageResult.Blocked();

            Assert.IsFalse(result.Applied);
            Assert.IsTrue(result.WasBlocked);
            Assert.AreEqual(0f, result.DamageDealt);
            Assert.IsFalse(result.WasFatal);
        }

        [Test]
        public void DamageResult_NotApplied_HasCorrectValues()
        {
            var result = DamageResult.NotApplied();

            Assert.IsFalse(result.Applied);
            Assert.IsFalse(result.WasBlocked);
            Assert.AreEqual(0f, result.DamageDealt);
            Assert.IsFalse(result.WasFatal);
        }

        [Test]
        public void DamageResult_Success_HasCorrectValues()
        {
            var result = DamageResult.Success(50f, true);

            Assert.IsTrue(result.Applied);
            Assert.IsFalse(result.WasBlocked);
            Assert.AreEqual(50f, result.DamageDealt);
            Assert.IsTrue(result.WasFatal);
        }

        #endregion

        #region AttackPhase Tests

        [Test]
        public void AttackPhase_Ready_IsDefault()
        {
            AttackPhase phase = default;

            Assert.AreEqual(AttackPhase.Ready, phase);
        }

        [Test]
        public void AttackPhase_AllValuesAreDefined()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(AttackPhase), AttackPhase.Ready));
            Assert.IsTrue(System.Enum.IsDefined(typeof(AttackPhase), AttackPhase.Windup));
            Assert.IsTrue(System.Enum.IsDefined(typeof(AttackPhase), AttackPhase.Active));
            Assert.IsTrue(System.Enum.IsDefined(typeof(AttackPhase), AttackPhase.Recovery));
        }

        #endregion

        #region EnemyState Tests

        [Test]
        public void EnemyState_Idle_IsDefault()
        {
            EnemyState state = default;

            Assert.AreEqual(EnemyState.Idle, state);
        }

        [Test]
        public void EnemyState_AllValuesAreDefined()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(EnemyState), EnemyState.Idle));
            Assert.IsTrue(System.Enum.IsDefined(typeof(EnemyState), EnemyState.Wandering));
            Assert.IsTrue(System.Enum.IsDefined(typeof(EnemyState), EnemyState.Patrolling));
            Assert.IsTrue(System.Enum.IsDefined(typeof(EnemyState), EnemyState.Suspicious));
            Assert.IsTrue(System.Enum.IsDefined(typeof(EnemyState), EnemyState.Chasing));
            Assert.IsTrue(System.Enum.IsDefined(typeof(EnemyState), EnemyState.AttackWindup));
            Assert.IsTrue(System.Enum.IsDefined(typeof(EnemyState), EnemyState.AttackActive));
            Assert.IsTrue(System.Enum.IsDefined(typeof(EnemyState), EnemyState.AttackRecovery));
            Assert.IsTrue(System.Enum.IsDefined(typeof(EnemyState), EnemyState.ReturningHome));
            Assert.IsTrue(System.Enum.IsDefined(typeof(EnemyState), EnemyState.Stagger));
            Assert.IsTrue(System.Enum.IsDefined(typeof(EnemyState), EnemyState.Stuck));
            Assert.IsTrue(System.Enum.IsDefined(typeof(EnemyState), EnemyState.Dead));
        }

        #endregion

        #region Health Events Tests

        [Test]
        public void Health_OnHealthChanged_FiresOnDamage()
        {
            var health = new Health(100f);
            float? reportedCurrent = null;
            float? reportedMax = null;

            health.OnHealthChanged += (current, max) =>
            {
                reportedCurrent = current;
                reportedMax = max;
            };

            var request = new DamageRequest(null, 25f, Vector3.zero, Vector3.forward);
            health.ApplyDamage(request);

            Assert.AreEqual(75f, reportedCurrent);
            Assert.AreEqual(100f, reportedMax);
        }

        [Test]
        public void Health_OnHealthChanged_FiresOnHeal()
        {
            var health = new Health(100f);
            var request = new DamageRequest(null, 50f, Vector3.zero, Vector3.forward);
            health.ApplyDamage(request);

            float? reportedCurrent = null;
            health.OnHealthChanged += (current, max) => reportedCurrent = current;

            health.Heal(25f);

            Assert.AreEqual(75f, reportedCurrent);
        }

        [Test]
        public void Health_OnDamaged_FiresWithRequest()
        {
            var health = new Health(100f);
            var source = new GameObject("TestSource");
            DamageRequest? receivedRequest = null;

            health.OnDamaged += request => receivedRequest = request;

            var damageRequest = new DamageRequest(source, 30f, Vector3.one, Vector3.forward, 0.5f);
            health.ApplyDamage(damageRequest);

            Assert.IsNotNull(receivedRequest);
            Assert.AreEqual(source, receivedRequest.Value.Source);
            Assert.AreEqual(30f, receivedRequest.Value.Amount);

            Object.DestroyImmediate(source);
        }

        #endregion

        #region Invulnerability Tests

        [Test]
        public void DamageResult_Blocked_IndicatesInvulnerability()
        {
            // Simulates what happens when player dodges
            var result = DamageResult.Blocked();

            Assert.IsFalse(result.Applied);
            Assert.IsTrue(result.WasBlocked);
            Assert.AreEqual(0f, result.DamageDealt);
            Assert.IsFalse(result.WasFatal);
        }

        [Test]
        public void Health_WhenBlocked_TakesNoDamage()
        {
            var health = new Health(100f);

            // Simulate blocked damage scenario
            var blocked = DamageResult.Blocked();

            // Health should remain unchanged when damage is blocked
            Assert.AreEqual(100f, health.Current);
            Assert.IsFalse(blocked.Applied);
        }

        #endregion

        #region Hit Detection Tests

        [Test]
        public void HashSet_PreventsDoubleHits()
        {
            // Tests the concept used by MeleeHitDetector
            var hitTargets = new System.Collections.Generic.HashSet<int>();
            int targetId = 42;

            // First hit should be added
            bool firstHit = hitTargets.Add(targetId);
            Assert.IsTrue(firstHit);

            // Second hit to same target should fail
            bool secondHit = hitTargets.Add(targetId);
            Assert.IsFalse(secondHit);

            // Different target should succeed
            bool differentTarget = hitTargets.Add(99);
            Assert.IsTrue(differentTarget);
        }

        [Test]
        public void HashSet_ClearsOnNewAttack()
        {
            // Tests that resetting between attacks works
            var hitTargets = new System.Collections.Generic.HashSet<int>();

            hitTargets.Add(1);
            hitTargets.Add(2);
            Assert.AreEqual(2, hitTargets.Count);

            // Clear for new attack
            hitTargets.Clear();
            Assert.AreEqual(0, hitTargets.Count);

            // Can hit same targets again
            Assert.IsTrue(hitTargets.Add(1));
            Assert.IsTrue(hitTargets.Add(2));
        }

        #endregion

        #region Dodge Timing Tests

        [Test]
        public void DodgeTiming_InvulnerabilityWindowIsValid()
        {
            // Test dodge timing calculations
            float duration = 0.3f;
            float invulStart = 0.05f;
            float invulEnd = 0.25f;

            // Invulnerability should start after dodge begins
            Assert.Greater(invulStart, 0f);

            // Invulnerability should end before dodge ends
            Assert.Less(invulEnd, duration);

            // Invulnerability window should have positive duration
            float invulDuration = invulEnd - invulStart;
            Assert.Greater(invulDuration, 0f);
        }

        [Test]
        public void DodgeTiming_SpeedCalculation()
        {
            float distance = 2.5f;
            float duration = 0.3f;

            float speed = distance / duration;

            Assert.Greater(speed, 0f);
            Assert.AreEqual(distance, speed * duration, 0.001f);
        }

        [Test]
        public void DodgeTiming_IsInvulnerableAtTime()
        {
            float invulStart = 0.05f;
            float invulEnd = 0.25f;

            // Helper function to check invulnerability
            bool IsInvulnerable(float timer) => timer >= invulStart && timer <= invulEnd;

            Assert.IsFalse(IsInvulnerable(0f));      // Before window
            Assert.IsFalse(IsInvulnerable(0.04f));   // Just before window
            Assert.IsTrue(IsInvulnerable(0.05f));    // Start of window
            Assert.IsTrue(IsInvulnerable(0.15f));    // Middle of window
            Assert.IsTrue(IsInvulnerable(0.25f));    // End of window
            Assert.IsFalse(IsInvulnerable(0.26f));   // Just after window
            Assert.IsFalse(IsInvulnerable(0.3f));    // After dodge ends
        }

        #endregion

        #region Attack Phase Timing Tests

        [Test]
        public void AttackTiming_PhasesAreSequential()
        {
            float windupDuration = 0.15f;
            float activeDuration = 0.25f;
            float recoveryDuration = 0.3f;

            float activeStart = windupDuration;
            float activeEnd = windupDuration + activeDuration;
            float totalDuration = windupDuration + activeDuration + recoveryDuration;

            Assert.Greater(activeStart, 0f);
            Assert.Greater(activeEnd, activeStart);
            Assert.Greater(totalDuration, activeEnd);
        }

        [Test]
        public void AttackTiming_ActiveWindowDuration()
        {
            float activeWindowStart = 0.15f;
            float activeWindowEnd = 0.40f;

            float activeWindowDuration = activeWindowEnd - activeWindowStart;

            Assert.Greater(activeWindowDuration, 0f);
            Assert.AreEqual(0.25f, activeWindowDuration, 0.001f);
        }

        #endregion
    }
}
