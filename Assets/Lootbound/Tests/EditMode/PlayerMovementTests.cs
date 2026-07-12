using NUnit.Framework;
using UnityEngine;
using Lootbound.Gameplay.Player;

namespace Lootbound.Tests.EditMode
{
    public class PlayerMovementTests
    {
        private PlayerMovementConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            if (config != null)
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void CalculateJumpVelocity_ReturnsPositiveValue()
        {
            float jumpVelocity = config.CalculateJumpVelocity();

            Assert.Greater(jumpVelocity, 0f, "Jump velocity should be positive");
        }

        [Test]
        public void CalculateJumpVelocity_ProducesCorrectHeight()
        {
            // Using physics formula: v = sqrt(2 * g * h)
            // After jumping with velocity v under gravity g,
            // max height should be approximately jumpHeight

            float jumpVelocity = config.CalculateJumpVelocity();
            float gravity = Mathf.Abs(config.Gravity);

            // Calculate expected max height: h = v² / (2 * g)
            float calculatedHeight = (jumpVelocity * jumpVelocity) / (2f * gravity);

            Assert.AreEqual(config.JumpHeight, calculatedHeight, 0.01f,
                "Calculated height should match configured jump height");
        }

        [Test]
        public void GetTargetSpeed_WhenCrouching_ReturnsCrouchSpeed()
        {
            float speed = config.GetTargetSpeed(isCrouching: true, isSprinting: false);

            Assert.AreEqual(config.CrouchSpeed, speed);
        }

        [Test]
        public void GetTargetSpeed_WhenSprinting_ReturnsSprintSpeed()
        {
            float speed = config.GetTargetSpeed(isCrouching: false, isSprinting: true);

            Assert.AreEqual(config.SprintSpeed, speed);
        }

        [Test]
        public void GetTargetSpeed_WhenWalking_ReturnsWalkSpeed()
        {
            float speed = config.GetTargetSpeed(isCrouching: false, isSprinting: false);

            Assert.AreEqual(config.WalkSpeed, speed);
        }

        [Test]
        public void GetTargetSpeed_CrouchingTakesPriorityOverSprinting()
        {
            // When both crouching and sprinting, crouching should take priority
            float speed = config.GetTargetSpeed(isCrouching: true, isSprinting: true);

            Assert.AreEqual(config.CrouchSpeed, speed,
                "Crouching should take priority over sprinting");
        }

        [Test]
        public void DefaultValues_AreReasonable()
        {
            // Verify default values are within expected ranges
            Assert.Greater(config.WalkSpeed, 0f);
            Assert.Greater(config.SprintSpeed, config.WalkSpeed);
            Assert.Less(config.CrouchSpeed, config.WalkSpeed);

            Assert.Less(config.Gravity, 0f, "Gravity should be negative");
            Assert.Less(config.TerminalVelocity, config.Gravity,
                "Terminal velocity should be more negative than gravity");

            Assert.Greater(config.JumpHeight, 0f);
            Assert.Greater(config.CoyoteTime, 0f);
            Assert.Greater(config.JumpBufferTime, 0f);

            Assert.Greater(config.StandingHeight, config.CrouchingHeight);

            Assert.Less(config.MinPitch, 0f);
            Assert.Greater(config.MaxPitch, 0f);
        }

        [Test]
        public void PitchClampValues_AreValid()
        {
            // MinPitch should be negative (looking down)
            // MaxPitch should be positive (looking up)
            // The range should not exceed 180 degrees total

            float range = config.MaxPitch - config.MinPitch;
            Assert.LessOrEqual(range, 180f,
                "Pitch range should not exceed 180 degrees");

            Assert.GreaterOrEqual(config.MinPitch, -90f,
                "MinPitch should not be less than -90");
            Assert.LessOrEqual(config.MaxPitch, 90f,
                "MaxPitch should not exceed 90");
        }

        [Test]
        public void CharacterControllerParams_ArePositive()
        {
            Assert.Greater(config.ControllerRadius, 0f);
            Assert.Greater(config.SkinWidth, 0f);
            Assert.GreaterOrEqual(config.StepOffset, 0f);
            Assert.Greater(config.MaxSlopeAngle, 0f);
            Assert.LessOrEqual(config.MaxSlopeAngle, 90f);
        }
    }
}
