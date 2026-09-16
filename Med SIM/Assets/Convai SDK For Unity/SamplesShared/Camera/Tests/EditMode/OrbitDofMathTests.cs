// Copyright (c) Convai. Licensed under the Convai SDK license. See LICENSE in the package root.

using Convai.Sample.Camera;
using NUnit.Framework;

namespace Convai.Tests.SamplesShared.Camera.EditMode
{
    public class OrbitDofMathTests
    {
        [Test]
        public void Evaluate_WithMidRangeDistance_ReturnsBlendedAperture()
        {
            // Arrange
            float cameraDistance = 1.64f; // Midpoint between 0.78 and 2.5
            float closeDistance = 0.78f;
            float farDistance = 2.5f;
            float closeAperture = 1.4f;
            float farAperture = 8.0f;

            // Act
            OrbitDofMath.AdaptiveDofResult result = OrbitDofMath.Evaluate(
                cameraDistance,
                closeDistance,
                farDistance,
                closeAperture,
                farAperture);

            // Assert
            // Should be between close and far aperture (smoothstep affects exact value)
            Assert.Greater(result.Aperture, closeAperture);
            Assert.Less(result.Aperture, farAperture);
        }

        [Test]
        public void Evaluate_WithCloseDistance_ReturnsCloseAperture()
        {
            // Arrange
            float cameraDistance = 0.5f; // Below close threshold
            float closeDistance = 0.78f;
            float farDistance = 2.5f;
            float closeAperture = 1.4f;
            float farAperture = 8.0f;

            // Act
            OrbitDofMath.AdaptiveDofResult result = OrbitDofMath.Evaluate(
                cameraDistance,
                closeDistance,
                farDistance,
                closeAperture,
                farAperture);

            // Assert
            Assert.AreEqual(closeAperture, result.Aperture, 0.01f);
        }

        [Test]
        public void Evaluate_WithFarDistance_ReturnsFarAperture()
        {
            // Arrange
            float cameraDistance = 3.0f; // Above far threshold
            float closeDistance = 0.78f;
            float farDistance = 2.5f;
            float closeAperture = 1.4f;
            float farAperture = 8.0f;

            // Act
            OrbitDofMath.AdaptiveDofResult result = OrbitDofMath.Evaluate(
                cameraDistance,
                closeDistance,
                farDistance,
                closeAperture,
                farAperture);

            // Assert
            Assert.AreEqual(farAperture, result.Aperture, 0.01f);
        }

        [Test]
        public void Evaluate_FocusDistanceMatchesCameraDistance()
        {
            // Arrange
            float cameraDistance = 1.5f;
            float closeDistance = 0.78f;
            float farDistance = 2.5f;
            float closeAperture = 1.4f;
            float farAperture = 8.0f;

            // Act
            OrbitDofMath.AdaptiveDofResult result = OrbitDofMath.Evaluate(
                cameraDistance,
                closeDistance,
                farDistance,
                closeAperture,
                farAperture);

            // Assert
            Assert.AreEqual(cameraDistance, result.FocusDistance, 0.01f);
        }

        [Test]
        public void Evaluate_WithZeroDistance_ReturnsMinimumFocusDistance()
        {
            // Arrange - edge case
            float cameraDistance = 0f;
            float closeDistance = 0.78f;
            float farDistance = 2.5f;
            float closeAperture = 1.4f;
            float farAperture = 8.0f;

            // Act
            OrbitDofMath.AdaptiveDofResult result = OrbitDofMath.Evaluate(
                cameraDistance,
                closeDistance,
                farDistance,
                closeAperture,
                farAperture);

            // Assert
            Assert.Greater(result.FocusDistance, 0f, "Focus distance sanitized to positive");
            Assert.AreEqual(0.1f, result.FocusDistance, 0.01f);
        }

        [Test]
        public void Evaluate_WithNegativeDistance_ClampsToPositive()
        {
            // Arrange - edge case
            float cameraDistance = -1f;
            float closeDistance = 0.78f;
            float farDistance = 2.5f;
            float closeAperture = 1.4f;
            float farAperture = 8.0f;

            // Act
            OrbitDofMath.AdaptiveDofResult result = OrbitDofMath.Evaluate(
                cameraDistance,
                closeDistance,
                farDistance,
                closeAperture,
                farAperture);

            // Assert
            Assert.Greater(result.FocusDistance, 0f);
            Assert.Greater(result.Aperture, 0f);
        }

        [Test]
        public void Evaluate_ApertureNeverBelowMinimum()
        {
            // Arrange - test with very low aperture values
            float cameraDistance = 1.0f;
            float closeDistance = 0.78f;
            float farDistance = 2.5f;
            float closeAperture = 0.1f; // Below physical minimum
            float farAperture = 0.5f;

            // Act
            OrbitDofMath.AdaptiveDofResult result = OrbitDofMath.Evaluate(
                cameraDistance,
                closeDistance,
                farDistance,
                closeAperture,
                farAperture);

            // Assert
            Assert.GreaterOrEqual(result.Aperture, 0.7f, "Aperture clamped to minimum f/0.7");
        }
    }
}
