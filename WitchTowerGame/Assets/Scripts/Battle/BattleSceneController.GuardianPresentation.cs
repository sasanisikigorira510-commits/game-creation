using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WitchTower.Data;
using WitchTower.Managers;

namespace WitchTower.Battle
{
    public sealed partial class BattleSceneController
    {
        private const string GuardianArtRoot = "UI/GuardiansReborn/";
        private const float GuardianDivineDuration = .8f;
        private Image guardianHalo;
        private Image guardianDivineEffect;
        private float guardianDivineRemaining;
        private string guardianDivineId;
        private Vector2 guardianDivineTarget;
        private readonly List<Image> guardianAllyMarks = new List<Image>();
        private readonly List<Image> guardianEnemyMarks = new List<Image>();
        private readonly Dictionary<string, Sprite> guardianSpriteCache = new Dictionary<string, Sprite>();
        private readonly Dictionary<string, Sprite[]> guardianEffectFrameCache = new Dictionary<string, Sprite[]>();
        private readonly Dictionary<string, AudioClip> guardianSoundCache = new Dictionary<string, AudioClip>();
        private Sprite[] guardianDivineFrames;
        private bool guardianCommonArtLoaded;
        private Sprite guardianContractSealSprite;
        private Sprite guardianBarrierSprite;
        private Sprite guardianBurnSprite;
        private Sprite guardianMarkSprite;
        // Reserve space above the 451px body / 475px halo for the enemy-count HUD.
        // Only presentation moves: guardian scale and simulation's rear anchor stay unchanged.
        private static Vector2 GuardianPresentationAnchor => MapBattlefieldAnchor(new Vector2(.19f, .68f));

        private static string GuardianArtName(string id) => string.IsNullOrEmpty(id) ? "Seiryu" :
            char.ToUpperInvariant(id[0]) + id.Substring(1);

        private static void ApplyGuardianPreviewVisualLayout(Image image, Vector2 size)
        {
            if (image == null) return;
            // Bodies keep the original 384-pixel drawing scale. A breath frame can
            // extend its canvas to the right without shrinking or recentering the
            // body. The sprite's registered body pivot also survives facing flips.
            var rect = image.rectTransform;
            var sprite = image.sprite;
            float width = sprite != null ? sprite.rect.width : 384f;
            float height = sprite != null ? sprite.rect.height : 384f;
            rect.sizeDelta = new Vector2(size.x * width / 384f, size.y);
            rect.pivot = sprite != null && width > 0f && height > 0f
                ? new Vector2(sprite.pivot.x / width, sprite.pivot.y / height)
                : new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            image.preserveAspect = true;
        }

        private Sprite GuardianSprite(string resource)
        {
            if (!guardianSpriteCache.TryGetValue(resource, out var sprite))
            {
                sprite = Resources.Load<Sprite>(GuardianArtRoot + resource);
                // Cache missing assets too, so an import error cannot cause a
                // Resources lookup for every unit on every presentation frame.
                guardianSpriteCache[resource] = sprite;
            }
            return sprite;
        }

        private void EnsureGuardianCommonArt()
        {
            if (guardianCommonArtLoaded) return;
            guardianCommonArtLoaded = true;
            guardianContractSealSprite = GuardianSprite("ContractSeal");
            guardianBarrierSprite = GuardianSprite("Effects/Genbu_2");
            guardianBurnSprite = GuardianSprite("Effects/Suzaku_0");
            guardianMarkSprite = GuardianSprite("Effects/Byakko_0");
        }

        private Sprite[] GuardianEffectFrames(string id)
        {
            if (!guardianEffectFrameCache.TryGetValue(id, out var frames))
            {
                frames = new Sprite[4];
                string prefix = "Effects/" + GuardianArtName(id) + "_";
                for (int i = 0; i < frames.Length; i++) frames[i] = GuardianSprite(prefix + i);
                guardianEffectFrameCache[id] = frames;
            }
            return frames;
        }

        private AudioClip GuardianSound(string id)
        {
            if (!guardianSoundCache.TryGetValue(id, out var sound))
            {
                sound = Resources.Load<AudioClip>("Audio/SE/GuardiansReborn/" + id);
                guardianSoundCache[id] = sound;
            }
            return sound;
        }

        private Image GuardianImage(string name, string resource)
        {
            if (monsterPreviewRoot == null) return null;
            var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            node.transform.SetParent(monsterPreviewRoot.transform, false);
            var image = node.GetComponent<Image>();
            image.sprite = GuardianSprite(resource);
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        private static void PlaceGuardianImage(Image image, Vector2 anchor, float size, float alpha)
        {
            if (image == null) return;
            image.gameObject.SetActive(alpha > .001f && image.sprite != null);
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.one * size;
            image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }

        private void PlayGuardianDivinePresentation()
        {
            var simulator = stateMachine?.Simulator;
            if (simulator == null || string.IsNullOrEmpty(simulator.GuardianId)) return;
            guardianDivineId = simulator.GuardianId;
            if (IsDirectedGuardian(guardianDivineId))
            {
                // Every resolved hit already carries its own target-bound attack.
                guardianDivineRemaining = 0f;
                if (guardianDivineEffect != null) guardianDivineEffect.gameObject.SetActive(false);
                AudioManager.Instance?.PlaySe(GuardianSound(guardianDivineId));
                return;
            }
            guardianDivineFrames = GuardianEffectFrames(guardianDivineId);
            guardianDivineRemaining = GuardianDivineDuration;
            guardianDivineTarget = GuardianPresentationAnchor;
            if (guardianDivineId != "genbu")
                guardianDivineTarget = MapBattlefieldAnchor(simulator.LastGuardianSkillTargetAnchor);
            if (guardianDivineEffect == null)
                guardianDivineEffect = GuardianImage("GuardianDivineSkill", "Effects/" + GuardianArtName(guardianDivineId) + "_0");
            if (GuardianService.BattleSlot < allyAttackVisualRemainings.Count)
                allyAttackVisualRemainings[GuardianService.BattleSlot] = AttackVisualDuration;
            AudioManager.Instance?.PlaySe(GuardianSound(guardianDivineId));
        }

        private void UpdateGuardianDivinePresentation(float dt)
        {
            var simulator = stateMachine?.Simulator;
            if (simulator == null || string.IsNullOrEmpty(simulator.GuardianId) || monsterPreviewRoot == null)
            {
                HideGuardianPresentation();
                return;
            }
            EnsureGuardianCommonArt();
            string id = simulator.GuardianId;
            string name = GuardianArtName(id);
            if (guardianHalo == null)
            {
                guardianHalo = GuardianImage("GuardianContractHalo", "ContractSeal");
                guardianHalo.transform.SetAsFirstSibling();
            }
            float resonance = simulator.GuardianResonanceNormalized;
            PlaceGuardianImage(guardianHalo, GuardianPresentationAnchor, 475f,
                simulator.IsAllyAlive(GuardianService.BattleSlot) ? .18f + .28f * resonance : 0f);
            if (guardianHalo != null) guardianHalo.rectTransform.localRotation = Quaternion.Euler(0,0,battlePresentationClock * 3f);

            guardianDivineRemaining = Mathf.Max(0f, guardianDivineRemaining - Mathf.Max(0f, dt));
            if (guardianDivineEffect != null && guardianDivineFrames != null)
            {
                float t = 1f - guardianDivineRemaining / GuardianDivineDuration;
                int frame = Mathf.Clamp(Mathf.FloorToInt(t * 4),0,3);
                guardianDivineEffect.sprite = guardianDivineFrames[frame];
                float alpha = guardianDivineRemaining > 0 ? Mathf.Min(1f, t * 8f, (1f-t)*5f) : 0;
                PlaceGuardianImage(guardianDivineEffect, guardianDivineTarget, guardianDivineId == "suzaku" ? 550f : 470f, alpha);
            }

            for (int i=0;i<=GuardianService.BattleSlot;i++)
            {
                while (guardianAllyMarks.Count<=i) guardianAllyMarks.Add(GuardianImage("GuardianAllyEffect_"+i,"ContractSeal"));
                bool shield = simulator.GuardianHasBarrier(i);
                bool support = simulator.GuardianSupportsAlly(i);
                var mark = guardianAllyMarks[i];
                if(mark==null) continue;
                mark.sprite=shield ? guardianBarrierSprite : guardianContractSealSprite;
                Vector2 anchor=i==GuardianService.BattleSlot ? GuardianPresentationAnchor : MapBattlefieldAnchor(simulator.GetAllyPositionAnchor(i));
                PlaceGuardianImage(mark,anchor,shield ? (i==GuardianService.BattleSlot ? 470f : 265f) : 165f,shield ? .52f : support ? .62f : 0f);
            }
            int count=simulator.CurrentActiveEnemyCount;
            while(guardianEnemyMarks.Count<count)
                guardianEnemyMarks.Add(GuardianImage("GuardianEnemyEffect_"+guardianEnemyMarks.Count,"Effects/"+name+"_1"));
            for(int i=0;i<guardianEnemyMarks.Count;i++)
            {
                var mark=guardianEnemyMarks[i]; if(mark==null) continue;
                bool burn=i<count && simulator.GuardianIsBurning(i);
                bool marked=i<count && simulator.GuardianHasMark(i);
                mark.sprite=burn ? guardianBurnSprite : guardianMarkSprite;
                Vector2 anchor=i<count ? MapBattlefieldAnchor(simulator.GetEnemyPositionAnchor(i)) : Vector2.zero;
                PlaceGuardianImage(mark,anchor,160f,burn ? .72f : marked ? .80f : 0f);
            }
        }

        private void HideGuardianPresentation()
        {
            if(guardianHalo!=null) guardianHalo.gameObject.SetActive(false);
            if(guardianDivineEffect!=null) guardianDivineEffect.gameObject.SetActive(false);
            foreach(var image in guardianAllyMarks) if(image!=null) image.gameObject.SetActive(false);
            foreach(var image in guardianEnemyMarks) if(image!=null) image.gameObject.SetActive(false);
        }
    }
}
