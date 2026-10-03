using System;
using System.Collections.Generic;
using System.Linq;
using WitchTower.Save;

namespace WitchTower.Data
{
    public sealed partial class PlayerProfile
    {
        public List<OwnedGuardianData> OwnedGuardians { get; private set; }
        public List<string> GuardianCoreIds { get; private set; }
        public string EquippedGuardianId { get; set; }
        public List<string> GuardianOathIds { get; private set; }
        public List<string> SeenGuardianDialogueIds { get; private set; }

        private void InitializeGuardians(PlayerSaveData save)
        {
            var legacyGuardians = (save.OwnedGuardians ?? new List<OwnedGuardianData>())
                .Where(g => g != null && GuardianService.Find(g.Id) != null)
                .ToList();
            // Preserve each original guardian's own progress. Only saves actually
            // written by the shared-growth version inherit its value once; using
            // the greater value also avoids discarding a valid individual record.
            int inheritedSharedExp = !save.GuardianIndividualProgressInitialized && save.GuardianSharedProgressInitialized
                ? GuardianService.CumulativeExperience(save.GuardianSharedLevel, save.GuardianSharedExp) : 0;
            OwnedGuardians = legacyGuardians.GroupBy(g => g.Id)
                .Select(group => group.OrderByDescending(g => GuardianService.CumulativeExperience(g.Level, g.Exp)).First())
                .Select(g =>
                {
                    var guardian = new OwnedGuardianData { Id = g.Id,
                        ContractId = g.ContractId == GuardianService.AlternateContract ? GuardianService.AlternateContract : GuardianService.BasicContract };
                    GuardianService.SetIndividualExperience(guardian, Math.Max(inheritedSharedExp,
                        GuardianService.CumulativeExperience(g.Level, g.Exp)));
                    return guardian;
                }).ToList();
            GuardianCoreIds = (save.GuardianCoreIds ?? new List<string>())
                .Where(id => GuardianService.Find(id) != null && !OwnedGuardians.Any(g => g.Id == id)).Distinct().ToList();
            EquippedGuardianId = OwnedGuardians.Any(g => g.Id == save.EquippedGuardianId) ? save.EquippedGuardianId : string.Empty;
            GuardianOathIds = (save.GuardianOathIds ?? new List<string>())
                .Where(id => OwnedGuardians.Any(g => g.Id == id)).Distinct().ToList();
            SeenGuardianDialogueIds = (save.SeenGuardianDialogueIds ?? new List<string>())
                .Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        }
    }
}
