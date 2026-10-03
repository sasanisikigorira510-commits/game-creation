using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WitchTower.Home
{
    // A display-only snapshot. No inventory references, random calls or award methods.
    [Serializable]
    public sealed class SummonPresentationResult
    {
        public string MonsterId;
        public string InstanceId;
        public string DisplayName;
        public int ClassRank;
        public int IndividualValue;
        public bool IsNew;
        public SummonPresentationResult Copy() => (SummonPresentationResult)MemberwiseClone();
    }

    public enum TenPullPhase { Introduction, Materialization, Focus, Crack, CutIn, Burst, Reveal, Summary }

    public sealed class TenPullSequence
    {
        private readonly ReadOnlyCollection<SummonPresentationResult> results;
        private readonly TenPullPresentationSettings settings;
        public TenPullPhase Phase { get; private set; } = TenPullPhase.Introduction;
        public int Index { get; private set; }
        public float Elapsed { get; private set; }
        public bool AutoPlay { get; set; }
        public bool IsComplete => Phase == TenPullPhase.Summary;
        public int Count => results.Count;
        public int Class4Count { get; }
        public bool HasClass4 => Class4Count > 0;
        public SummonPresentationResult Current => results[Index].Copy();
        public SummonPresentationResult GetResult(int index) => results[index].Copy();
        public int OpenedCount => IsComplete ? Count : Index + (Phase == TenPullPhase.Reveal ? 1 : 0);
        public bool CanAdvance => !IsComplete &&
            (Phase == TenPullPhase.Focus && Elapsed >= Duration || Phase == TenPullPhase.Reveal && Elapsed >= 0.12f);
        public event Action PhaseChanged;
        public event Action Completed;

        public TenPullSequence(IReadOnlyList<SummonPresentationResult> confirmed, TenPullPresentationSettings configuration)
        {
            if (confirmed == null || (confirmed.Count != 1 && confirmed.Count != 10)) throw new ArgumentException("Summon presentation requires one or ten confirmed results.");
            settings = configuration ?? throw new ArgumentNullException(nameof(configuration));
            var copied = new List<SummonPresentationResult>(confirmed.Count);
            foreach (var result in confirmed)
            {
                if (result == null || string.IsNullOrEmpty(result.MonsterId)) throw new ArgumentException("Every result must identify an already-drawn monster.");
                copied.Add(result.Copy());
                if (result.ClassRank == 4) Class4Count++;
            }
            results = copied.AsReadOnly();
            AutoPlay = settings.AutoPlay;
        }

        public float Duration
        {
            get
            {
                var tier = settings.ForClass(results[Index].ClassRank);
                switch (Phase)
                {
                    case TenPullPhase.Introduction: return Math.Max(1f, Count == 1 ? settings.SingleIntroductionSeconds : settings.IntroductionSeconds);
                    case TenPullPhase.Materialization: return settings.GetMaterializationDuration(Count);
                    case TenPullPhase.Focus: return Math.Max(0.12f, tier.FocusSeconds + (Count > 1 && Index == 0 ? settings.GridTransitionSeconds : 0f));
                    case TenPullPhase.Crack: return Math.Max(0.1f, tier.CrackSeconds);
                    case TenPullPhase.CutIn: return Math.Max(0.1f, tier.CutInSeconds);
                    case TenPullPhase.Burst: return Math.Max(0.1f, tier.BurstSeconds);
                    case TenPullPhase.Reveal: return Math.Max(0.2f, tier.RevealSeconds);
                    default: return 1f;
                }
            }
        }

        public void Tick(float unscaledDelta)
        {
            if (IsComplete || float.IsNaN(unscaledDelta) || float.IsInfinity(unscaledDelta)) return;
            // Large resume deltas do not silently rush through several results.
            Elapsed += Math.Max(0f, Math.Min(0.1f, unscaledDelta));
            if (!AutoPlay && (Phase == TenPullPhase.Focus || Phase == TenPullPhase.Reveal)) return;
            if (Elapsed >= Duration) NextPhase();
        }

        public void Advance() { if (CanAdvance) NextPhase(); }
        public void Skip()
        {
            if (IsComplete) return;
            Phase = TenPullPhase.Summary;
            Elapsed = 0f;
            PhaseChanged?.Invoke();
            Completed?.Invoke();
        }

        private void NextPhase()
        {
            switch (Phase)
            {
                case TenPullPhase.Introduction: Phase = TenPullPhase.Materialization; break;
                case TenPullPhase.Materialization: Phase = TenPullPhase.Focus; break;
                case TenPullPhase.Focus: Phase = TenPullPhase.Crack; break;
                case TenPullPhase.Crack: Phase = settings.HasCutIn(results[Index].ClassRank) ? TenPullPhase.CutIn : TenPullPhase.Burst; break;
                case TenPullPhase.CutIn: Phase = TenPullPhase.Burst; break;
                case TenPullPhase.Burst: Phase = TenPullPhase.Reveal; break;
                case TenPullPhase.Reveal:
                    if (Index + 1 >= Count) { Skip(); return; }
                    Index++;
                    Phase = TenPullPhase.Focus;
                    break;
                default: return;
            }
            Elapsed = 0f;
            PhaseChanged?.Invoke();
        }
    }
}
