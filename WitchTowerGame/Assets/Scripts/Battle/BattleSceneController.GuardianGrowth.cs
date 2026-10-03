using WitchTower.Data;
using WitchTower.Managers;
using UnityEngine;

namespace WitchTower.Battle
{
    public sealed partial class BattleSceneController
    {
        private void NotifyGuardianGrowth(PlayerProfile profile, int previousLevel)
        {
            var guardian = GuardianService.Equipped(profile);
            if (guardian == null) return;
            int level = GuardianService.Level(profile, guardian.Id);
            if (level <= previousLevel) return;
            string name = GuardianService.Find(guardian.Id)?.DisplayName ?? guardian.Id;
            string message = $"{name}の共鳴が Lv.{level} に成長";
            if (previousLevel < GuardianService.AlternateContractLevel && level >= GuardianService.AlternateContractLevel)
                message += "\nもう一つの契約型を聖域で選べます";
            if (previousLevel < GuardianService.BondDialogueLevel && level >= GuardianService.BondDialogueLevel)
                message += "\n聖域の神獣に契約の紋章が付きました";
            if (previousLevel < GuardianService.MaxLevel && level >= GuardianService.MaxLevel)
                message += StoryTutorialService.HasSeenStory(profile, StoryTutorialService.StoryFirstArcComplete)
                    ? "\n聖域で誓約の試練に挑めます"
                    : "\n第一部完了後、聖域で誓約の試練に挑めます";
            EnqueueBattleAnnouncement(message, BattleAnnouncementTone.Gold);
            AudioManager.Instance?.PlaySe(Resources.Load<AudioClip>("Audio/SE/GuardiansReborn/growth"));
        }
    }
}
