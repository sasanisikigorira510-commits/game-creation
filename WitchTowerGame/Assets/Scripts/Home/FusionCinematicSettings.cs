using UnityEngine;

namespace WitchTower.Home
{
    [CreateAssetMenu(menuName = "WitchTower/Fusion Cinematic Settings")]
    public sealed class FusionCinematicSettings : ScriptableObject
    {
        public const string ResourcePath = "UI/FusionPage/Cinematic/FusionCinematicSettings";
        [Min(3f)] public float NormalSeconds = 7f;
        [Min(3f)] public float UpperSeconds = 9f;
        [Range(0f, 1f)] public float NormalGlow = .70f;
        [Range(0f, 1f)] public float UpperGlow = 1f;
        [Range(0, 48)] public int NormalParticles = 20;
        [Range(0, 48)] public int UpperParticles = 36;
        [Range(0f, 12f)] public float UpperShakePixels = 5f;
        [Range(600f, 1600f)] public float IonaHeightPixels = 1200f;
        public bool UpperCutInEnabled = true;
        public Color NormalAccent = new Color(.58f, .92f, 1f);
        public Color UpperAccent = new Color(1f, .83f, .40f);
    }
}
