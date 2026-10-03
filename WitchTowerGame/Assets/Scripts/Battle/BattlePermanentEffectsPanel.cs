using UnityEngine;
using UnityEngine.UI;
using WitchTower.Data;
using WitchTower.Managers;

namespace WitchTower.Battle
{
    // No purchases in this panel. Only already-owned effects may be toggled.
    public sealed class BattlePermanentEffectsPanel : MonoBehaviour
    {
        private BattleSceneController owner;
        private GameObject modal;
        private Text summary, repeatCondition, capacity;
        private readonly Text[] states = new Text[3];
        private readonly Text[] details = new Text[3];
        private readonly Button[] toggles = new Button[3];
        private Sprite frame;
        public bool IsOpen => modal != null && modal.activeSelf;
        private PlayerProfile Profile => GameManager.Instance?.PlayerProfile;
        private static readonly Color OnColor = new Color(.35f, 1f, .65f);
        private static readonly Color OffColor = new Color(.75f,.78f,.83f);

        public void Initialize(BattleSceneController controller)
        {
            owner = controller;
            if (summary != null) return;
            frame = Resources.Load<Sprite>("UI/BattlePermanentEffects/Panel");
            var compact = Button("PermanentEffectsSummary", transform, new Vector2(530,156), new Vector2(1,1), new Vector2(-28,-16), Open);
            compact.GetComponent<RectTransform>().pivot = Vector2.one;
            compact.image.pixelsPerUnitMultiplier = 2f;
            summary = Text("Summary",compact.transform,"",30,new Vector2(464,134),Vector2.zero);
            summary.lineSpacing = 1.12f;
            BuildModal(); Refresh();
        }
        private void LateUpdate()
        {
            if (owner == null) return;
            Refresh();
            // Keep the controls available during the result countdown, but never
            // cover the separate retire confirmation or intercept its input.
            if (!owner.IsPermanentEffectsInputBlocked) transform.SetAsLastSibling();
        }
        private void OnDisable() { if (modal != null) modal.SetActive(false); }
        public void Open()
        {
            if (owner == null || owner.IsPermanentEffectsInputBlocked) return;
            modal.SetActive(true); transform.SetAsLastSibling(); Refresh();
        }
        public void Close() { modal.SetActive(false); }
        private void BuildModal()
        {
            modal = new GameObject("EffectsModal",typeof(RectTransform),typeof(Image));
            modal.transform.SetParent(transform,false);
            var rect = (RectTransform)modal.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            modal.GetComponent<Image>().color = new Color(0,0,0,.82f);
            var card = Panel("SettingsCard",modal.transform,new Vector2(960,1400),new Vector2(.5f,.5f),Vector2.zero);
            Text("Title",card.transform,"永続効果の設定",44,new Vector2(850,70),new Vector2(0,554));
            Text("Paused",card.transform,"設定中はバトル・自動再挑戦を一時停止",27,new Vector2(850,48),new Vector2(0,492));
            string[] titles = {"自動周回（同じ階層）","装備の自動売却","モンスターの自動逃がし"};
            for (int i=0;i<3;i++)
            {
                int effect = i;
                float y = 318-i*278;
                var row = Panel("Effect"+i,card.transform,new Vector2(872,256),new Vector2(.5f,.5f),new Vector2(0,y));
                Text("Name",row.transform,titles[i],32,new Vector2(760,48),new Vector2(0,62));
                details[i] = Text("Details",row.transform,"",27,new Vector2(490,140),new Vector2(-114,-36));
                details[i].alignment = TextAnchor.MiddleLeft;
                toggles[i] = Button("Toggle"+i,row.transform,new Vector2(238,134),new Vector2(.5f,.5f),new Vector2(292,-24),()=>Toggle(effect));
                states[i] = Text("State",toggles[i].transform,"",28,new Vector2(170,78),Vector2.zero);
            }
            repeatCondition = Text("RepeatRules",card.transform,"",25,new Vector2(824,100),new Vector2(0,-431));
            capacity = Text("Capacity",card.transform,"",24,new Vector2(824,42),new Vector2(0,-506));
            var close = Button("CloseEffects",card.transform,new Vector2(540,104),new Vector2(.5f,.5f),new Vector2(0,-603),Close);
            Text("CloseLabel",close.transform,"閉じてバトルに戻る",32,new Vector2(490,76),Vector2.zero);
            modal.SetActive(false);
        }
        private bool Owned(int i) => Profile != null && (i==0 ? Profile.HasAutoRepeatFloorUpgrade : i==1 ? Profile.HasAutoSellEquipmentUpgrade : Profile.HasAutoReleaseMonsterUpgrade);
        private bool Enabled(int i) => Owned(i) && (i==0 ? owner.IsAutoRepeatSameFloorActive() && Profile.IsAutoRepeatFloorUpgradeEnabled : i==1 ? Profile.IsAutoSellEquipmentUpgradeEnabled : Profile.IsAutoReleaseMonsterUpgradeEnabled);
        public void Toggle(int effect)
        {
            if (!IsOpen || effect<0 || effect>2 || !Owned(effect)) return;
            var p = Profile;
            bool enable = !Enabled(effect);
            if (effect==0 && !owner.SetBattleAutoRepeatEnabled(enable)) return;
            if (effect==1) p.IsAutoSellEquipmentUpgradeEnabled = enable;
            if (effect==2) p.IsAutoReleaseMonsterUpgradeEnabled = enable;
            SaveManager.Instance?.SaveCurrentGame(); Refresh();
        }
        private string ShortState(int i) => !Owned(i) ? "—" : Enabled(i) ? "ON" : "OFF";
        public void Refresh()
        {
            if (summary == null || owner == null) return;
            summary.text = $"永続効果・設定 ▸\n周回 {ShortState(0)}　売却 {ShortState(1)}\n逃がし {ShortState(2)}";
            var p = Profile;
            for(int i=0;i<3;i++)
            {
                bool owned=Owned(i), active=Enabled(i);
                states[i].text = !owned ? "未購入" : active ? "ON\n停止する" : "OFF\n有効にする";
                states[i].color = active ? OnColor : OffColor;
                toggles[i].image.color = active ? new Color(.55f,1f,.7f) : Color.white;
                toggles[i].interactable = owned && (i!=0 || active || owner.CanEnableBattleAutoRepeat);
            }
            details[0].text = !Owned(0) ? "永続強化の商店で購入すると\n使用できます。" :
                !owner.CanEnableBattleAutoRepeat ? "このバトルでは自動周回を\n開始できません。" : Enabled(0) ?
                "周回予約中／周回中\n勝利・敗北後に同じ階層へ\n自動で再挑戦します。" :
                "購入済み・現在は停止中\nONにすると今の戦闘終了後\nから同じ階層を周回します。";
            int quality = Mathf.Clamp(p?.AutoSellEquipmentQualityThreshold ?? 3,1,5);
            string qualityName = new[]{"コモン","アンコモン","レア","エピック","レジェンダリー"}[quality-1];
            details[1].text = !Owned(1) ? "永続強化の商店で購入すると\n使用できます。" : quality==1 ?
                "基準：コモン以上を残す\n現在は売却対象なし\n基準変更：ホームの永続強化" :
                $"今後獲得する装備が対象\n{qualityName}未満を自動売却\n基準変更：ホームの永続強化";
            details[2].text = !Owned(2) ? "永続強化の商店で購入すると\n使用できます。" :
                $"今後仲間になる個体が対象\n平均個体値 {p.AutoReleaseMonsterIndividualValueThreshold} 未満を逃がす\n基準変更：ホームの永続強化";
            repeatCondition.text = "自動周回は次の階層へ進みません。\nホームへ戻る・次の階層へ進む・初回ステージクリアで停止。\nチュートリアル・神獣試練では開始できません。";
            capacity.text = p==null ? "" : $"所持枠は常時有効：モンスター {p.MonsterStorageLimit} ／ 装備 {p.EquipmentStorageLimit}";
        }
        private GameObject Panel(string name,Transform parent,Vector2 size,Vector2 anchor,Vector2 position)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
            var r=(RectTransform)go.transform; r.anchorMin=r.anchorMax=anchor; r.sizeDelta=size; r.anchoredPosition=position;
            var image=go.GetComponent<Image>(); image.sprite=frame; image.type=Image.Type.Sliced; image.color=Color.white;
            return go;
        }
        private Button Button(string name,Transform parent,Vector2 size,Vector2 anchor,Vector2 position,UnityEngine.Events.UnityAction action)
        {
            var go=Panel(name,parent,size,anchor,position); var button=go.AddComponent<Button>();
            button.targetGraphic=go.GetComponent<Image>(); button.onClick.AddListener(action); return button;
        }
        private static Text Text(string name,Transform parent,string value,int fontSize,Vector2 size,Vector2 position)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text)); go.transform.SetParent(parent,false);
            var text=go.GetComponent<Text>(); text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text=value; text.fontSize=fontSize; text.color=Color.white; text.alignment=TextAnchor.MiddleCenter;
            text.raycastTarget=false; text.rectTransform.sizeDelta=size; text.rectTransform.anchoredPosition=position;
            return text;
        }
    }
}
