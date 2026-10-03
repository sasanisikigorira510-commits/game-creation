using UnityEngine;

namespace WitchTower.Battle
{
    // Session-only QA playback control. Never changes combat stats, save data,
    // Unity's global clock, network deadlines, or store transactions.
    public static class BattlePlaybackSpeed
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly float[] Speeds = { .5f, 1f, 2f, 3f, 5f, 10f };
        private static int selected = 1;
        public static float Multiplier => Speeds[selected];
        public static void Cycle() => selected = (selected + 1) % Speeds.Length;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => selected = 1;
#else
        public static float Multiplier => 1f;
#endif
        public static int StepsPerFrame => Mathf.CeilToInt(Multiplier);
        public static float StepDelta(float frameDelta) => frameDelta * Multiplier / StepsPerFrame;
        public static string Label => $"検証用ゲーム速度：{Multiplier:0.#}倍";
    }
}
