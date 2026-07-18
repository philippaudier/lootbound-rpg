using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Lootbound.Gameplay.Combat;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// Pure roaming tests: pursuit/territory rules, wander pacing and
    /// candidate selection (fake NavMesh sampler), patrol traversal.
    /// </summary>
    public class EnemyRoamingTests
    {
        private static readonly Vector3 Home = new Vector3(100f, 5f, 200f);

        private static bool AcceptAllSampler(Vector3 position, float maxDistance, out Vector3 sampled)
        {
            sampled = position;
            return true;
        }

        private static bool RejectAllSampler(Vector3 position, float maxDistance, out Vector3 sampled)
        {
            sampled = default;
            return false;
        }

        private static EnemyWanderSettings DefaultSettings(int attempts = 4)
        {
            return new EnemyWanderSettings(
                wanderRadius: 10f,
                idleDurationMin: 1f,
                idleDurationMax: 3f,
                destinationAttempts: attempts,
                sampleDistance: 2f);
        }

        #region Pursuit rules

        [Test]
        public void Leash_IsEvaluatedAgainstHome_NotAgainstTheEnemy()
        {
            // Player 45m from home -> abandon, regardless of enemy position.
            Assert.IsTrue(EnemyPursuitRules.ShouldAbandonChase(
                targetDistanceFromHome: 45f, maxChaseDistanceFromHome: 40f,
                timeSinceTargetSeen: 0f, loseSightDelay: 3f));

            Assert.IsFalse(EnemyPursuitRules.ShouldAbandonChase(
                targetDistanceFromHome: 35f, maxChaseDistanceFromHome: 40f,
                timeSinceTargetSeen: 0f, loseSightDelay: 3f));
        }

        [Test]
        public void ProlongedLossOfSight_AbandonsChase()
        {
            Assert.IsTrue(EnemyPursuitRules.ShouldAbandonChase(
                targetDistanceFromHome: 10f, maxChaseDistanceFromHome: 40f,
                timeSinceTargetSeen: 3.5f, loseSightDelay: 3f));

            Assert.IsFalse(EnemyPursuitRules.ShouldAbandonChase(
                targetDistanceFromHome: 10f, maxChaseDistanceFromHome: 40f,
                timeSinceTargetSeen: 2.5f, loseSightDelay: 3f));
        }

        [Test]
        public void Hysteresis_PreventsBoundaryOscillation()
        {
            const float max = 40f;
            const float hysteresis = 4f;

            // Player sitting exactly on the abandon boundary: the chase
            // stops there and may NOT restart until the player comes back
            // inside the hysteresis margin.
            Assert.IsTrue(EnemyPursuitRules.ShouldAbandonChase(40.5f, max, 0f, 3f));
            Assert.IsFalse(EnemyPursuitRules.CanStartChase(40.5f, max, hysteresis));
            Assert.IsFalse(EnemyPursuitRules.CanStartChase(38f, max, hysteresis), "Still inside the hysteresis band");
            Assert.IsTrue(EnemyPursuitRules.CanStartChase(35f, max, hysteresis));
        }

        [Test]
        public void ReacquireCooldown_BlocksThenReleases()
        {
            float blockedUntil = 100f + 2f;

            Assert.IsFalse(EnemyPursuitRules.CanReacquireTarget(101f, blockedUntil));
            Assert.IsTrue(EnemyPursuitRules.CanReacquireTarget(102f, blockedUntil));
        }

        [Test]
        public void Arrival_UsesTolerance()
        {
            Assert.IsTrue(EnemyPursuitRules.HasArrived(1.2f, 1.5f));
            Assert.IsFalse(EnemyPursuitRules.HasArrived(1.8f, 1.5f));
        }

        #endregion

        #region Defensive chase

        [Test]
        public void DefensiveChase_OpensOnce_AndExpires()
        {
            var defense = new EnemyDefensiveChase();

            Assert.IsFalse(defense.IsActive(10f));
            Assert.IsTrue(defense.TryStart(10f, duration: 5f));
            Assert.IsTrue(defense.IsActive(14.9f));
            Assert.IsFalse(defense.IsActive(15.1f), "The window must expire after its duration");
        }

        [Test]
        public void DefensiveChase_SuccessiveHits_NeverExtendTheWindow()
        {
            var defense = new EnemyDefensiveChase();
            defense.TryStart(10f, duration: 5f); // active until 15

            Assert.IsFalse(defense.TryStart(13f, duration: 5f), "A hit during an active window must not restart it");
            Assert.IsFalse(defense.IsActive(15.1f),
                "The window must still end at the ORIGINAL expiry despite later hits - no infinite pursuit by poking");

            // Once expired, a new hit opens a fresh window.
            Assert.IsTrue(defense.TryStart(16f, duration: 5f));
            Assert.IsTrue(defense.IsActive(20f));
        }

        [Test]
        public void DefensiveChase_SuppressesSightRequirement_ButNeverTheLeash()
        {
            // The brain feeds timeSinceSeen = 0 while the window is active:
            // sight loss cannot abandon the chase, the territorial leash can.
            Assert.IsFalse(EnemyPursuitRules.ShouldAbandonChase(
                targetDistanceFromHome: 20f, maxChaseDistanceFromHome: 40f,
                timeSinceTargetSeen: 0f, loseSightDelay: 3f));

            Assert.IsTrue(EnemyPursuitRules.ShouldAbandonChase(
                targetDistanceFromHome: 45f, maxChaseDistanceFromHome: 40f,
                timeSinceTargetSeen: 0f, loseSightDelay: 3f),
                "The leash applies even during a defensive chase");
        }

        [Test]
        public void DefensiveChase_Clear_EndsTheWindow()
        {
            var defense = new EnemyDefensiveChase();
            defense.TryStart(10f, 5f);
            defense.Clear();

            Assert.IsFalse(defense.IsActive(11f));
            Assert.IsTrue(defense.TryStart(11f, 5f), "After Clear a new window can open immediately");
        }

        [Test]
        public void AttackedWhileReturning_ReasonExists()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(EnemyTransitionReason),
                EnemyTransitionReason.AttackedWhileReturning));
        }

        #endregion

        #region Wander

        [Test]
        public void Wander_DoesNotMoveBeforeIdleElapsed()
        {
            var wander = new EnemyWanderBehaviour(DefaultSettings(), new System.Random(1), AcceptAllSampler);
            wander.OnRoamingResumed(now: 0f);

            // Idle windows are at least IdleDurationMin (1s).
            Assert.IsFalse(wander.TryGetNextDestination(Home, Home, now: 0.5f, out _));
        }

        [Test]
        public void Wander_CandidatesStayWithinRadius_AroundHome()
        {
            var wander = new EnemyWanderBehaviour(DefaultSettings(), new System.Random(7), AcceptAllSampler);
            wander.OnRoamingResumed(0f);

            // Current position far from home: candidates must still be
            // centered on HOME (the drift back to territory).
            Vector3 farFromHome = Home + new Vector3(50f, 0f, 0f);

            int moves = 0;
            float now = 0f;
            for (int i = 0; i < 300 && moves < 20; i++)
            {
                now += 0.5f;
                if (wander.TryGetNextDestination(Home, farFromHome, now, out Vector3 destination))
                {
                    moves++;
                    float distance = Vector3.Distance(destination, Home);
                    Assert.LessOrEqual(distance, 10f + 0.001f,
                        "Wander destinations must stay inside WanderRadius around HomePosition");
                    wander.OnDestinationReached(now);
                }
            }

            Assert.Greater(moves, 0, "The enemy must eventually move");
        }

        [Test]
        public void Wander_SpendsMoreTimeRestingThanWalking()
        {
            // 40% of decisions must keep the enemy in place: over many
            // decisions, a significant share yields no destination even
            // though the sampler accepts everything.
            var wander = new EnemyWanderBehaviour(DefaultSettings(), new System.Random(42), AcceptAllSampler);
            wander.OnRoamingResumed(0f);

            int decisions = 0;
            int stays = 0;
            float now = 0f;
            for (int i = 0; i < 2000 && decisions < 200; i++)
            {
                now += 4f; // beyond any idle window: every poll is a decision
                if (wander.TryGetNextDestination(Home, Home, now, out _))
                {
                    decisions++;
                    wander.OnDestinationReached(now);
                }
                else
                {
                    decisions++;
                    stays++;
                }
            }

            float stayRatio = stays / (float)decisions;
            Assert.Greater(stayRatio, 0.25f, "A meaningful share of decisions must be 'stay put'");
            Assert.Less(stayRatio, 0.60f, "The enemy must still move regularly");
        }

        [Test]
        public void Wander_SamplerAttempts_AreBounded()
        {
            int calls = 0;
            bool CountingRejectSampler(Vector3 p, float d, out Vector3 s)
            {
                calls++;
                s = default;
                return false;
            }

            var wander = new EnemyWanderBehaviour(DefaultSettings(attempts: 3), new System.Random(3), CountingRejectSampler);
            wander.OnRoamingResumed(0f);

            // Poll once past the idle window with a non-stay roll guaranteed
            // by trying several decision windows.
            float now = 0f;
            for (int i = 0; i < 20; i++)
            {
                now += 4f;
                int before = calls;
                wander.TryGetNextDestination(Home, Home, now, out _);
                Assert.LessOrEqual(calls - before, 3, "Sampler calls per decision must be bounded by DestinationAttempts");
            }
        }

        [Test]
        public void Wander_FailsCleanly_WhenNoPointIsNavigable()
        {
            var wander = new EnemyWanderBehaviour(DefaultSettings(), new System.Random(5), RejectAllSampler);
            wander.OnRoamingResumed(0f);

            float now = 0f;
            for (int i = 0; i < 50; i++)
            {
                now += 4f;
                Assert.IsFalse(wander.TryGetNextDestination(Home, Home, now, out _),
                    "With no navigable point the behaviour must fail cleanly and keep resting");
            }
        }

        [Test]
        public void Wander_DifferentSeeds_ProduceDifferentSchedules()
        {
            var a = new EnemyWanderBehaviour(DefaultSettings(), new System.Random(1000), AcceptAllSampler);
            var b = new EnemyWanderBehaviour(DefaultSettings(), new System.Random(2000), AcceptAllSampler);
            a.OnRoamingResumed(0f);
            b.OnRoamingResumed(0f);

            // Two enemies must not take exactly the same decisions at the
            // same times over a long window.
            bool diverged = false;
            float now = 0f;
            for (int i = 0; i < 100 && !diverged; i++)
            {
                now += 0.7f;
                bool movedA = a.TryGetNextDestination(Home, Home, now, out Vector3 destA);
                bool movedB = b.TryGetNextDestination(Home, Home, now, out Vector3 destB);
                if (movedA) a.OnDestinationReached(now);
                if (movedB) b.OnDestinationReached(now);

                if (movedA != movedB || (movedA && movedB && (destA - destB).sqrMagnitude > 0.01f))
                {
                    diverged = true;
                }
            }

            Assert.IsTrue(diverged, "Instances with different seeds must desynchronize");
        }

        #endregion

        #region Patrol

        private static List<Vector3> Route() => new List<Vector3>
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(10f, 0f, 0f),
            new Vector3(10f, 0f, 10f),
            new Vector3(0f, 0f, 10f)
        };

        private static EnemyPatrolBehaviour CreatePatrol(List<Vector3> points, bool pingPong,
            NavigationSampleDelegate sampler = null)
        {
            return new EnemyPatrolBehaviour(points, pingPong, dwellSeconds: 0.5f, sampleDistance: 2f,
                new System.Random(9), sampler ?? AcceptAllSampler);
        }

        private static int[] CollectVisitOrder(EnemyPatrolBehaviour patrol, int count)
        {
            var visits = new List<int>();
            float now = 0f;
            patrol.OnRoamingResumed(now);

            for (int i = 0; i < count * 20 && visits.Count < count; i++)
            {
                now += 1f;
                if (patrol.TryGetNextDestination(Vector3.zero, Vector3.zero, now, out _))
                {
                    visits.Add(patrol.CurrentIndex);
                    patrol.OnDestinationReached(now);
                }
            }

            return visits.ToArray();
        }

        [Test]
        public void Patrol_Loop_AdvancesAndWraps()
        {
            var patrol = CreatePatrol(Route(), pingPong: false);

            var visits = CollectVisitOrder(patrol, 6);

            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 0, 1 }, visits);
        }

        [Test]
        public void Patrol_PingPong_ReversesAtEnds()
        {
            var patrol = CreatePatrol(Route(), pingPong: true);

            var visits = CollectVisitOrder(patrol, 8);

            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 2, 1, 0, 1 }, visits);
        }

        [Test]
        public void Patrol_InvalidPoints_AreSkipped_WithoutInfiniteLoop()
        {
            var points = Route();
            bool SkipIndexOneSampler(Vector3 p, float d, out Vector3 s)
            {
                // Point (10,0,0) is never navigable.
                if ((p - points[1]).sqrMagnitude < 0.01f)
                {
                    s = default;
                    return false;
                }
                s = p;
                return true;
            }

            var patrol = CreatePatrol(points, pingPong: false, SkipIndexOneSampler);
            var visits = CollectVisitOrder(patrol, 6);

            CollectionAssert.AreEqual(new[] { 0, 2, 3, 0, 2, 3 }, visits,
                "The invalid point must be skipped, the rest of the route preserved");
        }

        [Test]
        public void Patrol_AllPointsInvalid_FailsCleanly()
        {
            var patrol = CreatePatrol(Route(), pingPong: false, RejectAllSampler);
            patrol.OnRoamingResumed(0f);

            float now = 0f;
            for (int i = 0; i < 30; i++)
            {
                now += 2f;
                Assert.IsFalse(patrol.TryGetNextDestination(Vector3.zero, Vector3.zero, now, out _));
            }
        }

        [Test]
        public void Patrol_EmptyRoute_NeverMoves()
        {
            var patrol = CreatePatrol(new List<Vector3>(), pingPong: false);
            patrol.OnRoamingResumed(0f);

            Assert.IsFalse(patrol.TryGetNextDestination(Vector3.zero, Vector3.zero, 10f, out _));
        }

        #endregion
    }
}
