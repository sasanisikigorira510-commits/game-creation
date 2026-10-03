using UnityEngine;

namespace WitchTower.Home
{
    /// <summary>Time-based shot choreography. Re-sampling does not integrate or alter any state.</summary>
    public static class TenPullFilmMotion
    {
        public struct Pose
        {
            public float CameraScale, BodyRotation, ArmRotation, HairRotation, CapeRotation, Energy;
            public Vector2 CameraOffset, ActorOffset;
        }

        // Quintic ease has zero velocity and acceleration at each end. Poses remain continuous
        // at every display refresh and when previewing with a different frame rate.
        public static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        public static Pose Opening(float t, float cameraPush)
        {
            t = Mathf.Clamp01(t);
            float lift = Smooth(Mathf.InverseLerp(0.08f, 0.91f, t));
            float follow = Smooth(Mathf.InverseLerp(0.18f, 1f, t));
            return new Pose
            {
                CameraScale = 1.01f + cameraPush * Smooth(t),
                CameraOffset = new Vector2(-2.5f * Smooth(t), -3f * Smooth(t)) * Mathf.Clamp01(cameraPush / 0.045f),
                ActorOffset = new Vector2(-1.5f * lift, 2f * lift),
                BodyRotation = -0.8f + 1.3f * lift,
                // The cloak already contains the shoulder silhouette. Keep the sleeve's
                // rotation modest so its overlapping contour does not slide across the torso.
                ArmRotation = Mathf.Lerp(-8f, 2f, lift),
                HairRotation = -1.9f * follow,
                CapeRotation = -2.8f * follow,
                Energy = Smooth(Mathf.InverseLerp(0.08f, 0.96f, t))
            };
        }

        public static Pose Casting(float t, float cameraPush)
        {
            t = Mathf.Clamp01(t);
            float release = Smooth(Mathf.InverseLerp(0.06f, 0.64f, t));
            float settle = Smooth(Mathf.InverseLerp(0.45f, 1f, t));
            float follow = Smooth(Mathf.InverseLerp(0.16f, 0.90f, t));
            return new Pose
            {
                CameraScale = 1.018f + cameraPush * (0.35f + 0.65f * release - 0.12f * settle),
                CameraOffset = new Vector2(-3f * release + settle, -2f * release) * Mathf.Clamp01(cameraPush / 0.045f),
                ActorOffset = new Vector2(-1.5f * release + 0.7f * settle, -1f * release),
                BodyRotation = -0.45f * release + 0.2f * settle,
                // The hand is drawn in perspective. Settle its shoulder without enlarging a flat hand.
                ArmRotation = 2.4f - 3.3f * release + 0.8f * settle,
                HairRotation = -2.6f * follow + 0.7f * settle,
                CapeRotation = -3.6f * follow + 0.9f * settle,
                Energy = 0.34f + 0.66f * release
            };
        }
    }
}
