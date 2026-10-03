using System;
using System.Linq;

namespace WitchTower.Data
{
    public sealed class GuardianDialogueDefinition
    {
        public GuardianDialogueDefinition(string guardianId, string stage, string title, params StoryDialogueLine[] lines)
        {
            GuardianId = guardianId;
            Stage = stage;
            EventId = "guardian_" + guardianId + "_" + stage;
            Title = title;
            Lines = lines ?? Array.Empty<StoryDialogueLine>();
        }
        public string GuardianId { get; }
        public string Stage { get; }
        public string EventId { get; }
        public string Title { get; }
        public StoryDialogueLine[] Lines { get; }
    }

    // Optional sanctuary conversations. Main-story manuscripts and their read
    // flags stay independent from these small, individual contracts.
    public static class GuardianDialogueCatalog
    {
        public static GuardianDialogueDefinition[] All { get; } = new[]
        {
            new GuardianDialogueDefinition("seiryu", "birth", "同じ空、違う歩幅",
                new StoryDialogueLine("ルシェ", "青龍との契約が結ばれました。命令ではなく、互いの呼びかけで続く約束です。"),
                new StoryDialogueLine("青龍", "風を急がせることはできる。だが、歩く者の足まで急がせるつもりはない。汝はどこへ向かう？"),
                new StoryDialogueLine("契約師", "まずは仲間と帰れる道へ。早い者も、遅い者も、一緒に進みたい。"),
                new StoryDialogueLine("イオナ", "では、全員を同じ速さに揃える式はいらないわね。風の通り道だけ、開けておく。")),
            new GuardianDialogueDefinition("seiryu", "bond", "追い越さない風",
                new StoryDialogueLine("ルシェ", "青龍様の風が吹くと、仲間の一歩が軽くなるそうです。置いていかれる風ではない、と。"),
                new StoryDialogueLine("青龍", "背を押すか、次の一撃へ風を渡すか。同じ力にも、預け方はある。"),
                new StoryDialogueLine("イオナ", "契約の型も、一度決めたら終わりではないのね。次の道に合わせて、話し直せる。"),
                new StoryDialogueLine("契約師", "今日は背を押してほしい。皆が追いついたら、次のことを一緒に決めよう。")),
            new GuardianDialogueDefinition("seiryu", "oath", "戻るための翼",
                new StoryDialogueLine("青龍", "試練を越えても、汝は仲間の足音を確かめていた。その歩みなら、風を預けられる。"),
                new StoryDialogueLine("契約師", "先へ進めることと、一人で進むことは違うから。これからも、帰り道まで頼みたい。"),
                new StoryDialogueLine("イオナ", "誓いの印を残すわ。誰かに従わせるためではなく、一緒に戻ったことの印として。"),
                new StoryDialogueLine("ルシェ", "おかえりなさい。青龍様の分は……杯より、まず窓を開けましょうか。")),
            new GuardianDialogueDefinition("suzaku", "birth", "迎え火",
                new StoryDialogueLine("イオナ", "火の道は整ったわ。朱雀、こちらの声は届いている？"),
                new StoryDialogueLine("朱雀", "届いているとも。燃やしたい敵の名より、照らしたい道を聞かせておくれ。"),
                new StoryDialogueLine("契約師", "迷っている仲間が、帰る場所を見つけられる道を。"),
                new StoryDialogueLine("ルシェ", "では、この炉の灯も消しません。遠くからでも、ここだと分かるように。")),
            new GuardianDialogueDefinition("suzaku", "bond", "火の行き先",
                new StoryDialogueLine("朱雀", "広く灯せば、いくつもの影が見える。一点へ注げば、厚い闇にも穴が開く。"),
                new StoryDialogueLine("契約師", "強い火を出すだけでは足りないんだね。仲間が何を狙っているかも見たい。"),
                new StoryDialogueLine("イオナ", "炎海と孤炎。どちらの式にも、続く仲間の声が通る余地を残したわ。"),
                new StoryDialogueLine("ルシェ", "次に向かう場所を見て選びましょう。帰ってから変えても、約束を破ったことにはなりません。")),
            new GuardianDialogueDefinition("suzaku", "oath", "消さずに残す灯",
                new StoryDialogueLine("朱雀", "炎の向こうでも、汝は仲間を見失わなかった。勝ち急ぐ火より、長く灯る火になったね。"),
                new StoryDialogueLine("イオナ", "無事に戻った声が、別々に聞こえる。揃えなくても、こんなに確かに。"),
                new StoryDialogueLine("契約師", "朱雀が照らしてくれたから見つけられた。これからも、迎え火を頼むよ。"),
                new StoryDialogueLine("ルシェ", "炉のそばを少し空けておきました。皆で帰った日の灯を、ここにも。")),
            new GuardianDialogueDefinition("byakko", "birth", "牙を向ける先",
                new StoryDialogueLine("ルシェ", "白虎様が応えてくださいました。……契約師様、少し緊張していらっしゃいますか。"),
                new StoryDialogueLine("白虎", "恐れてよい。牙を持つ者を、確かめず信じる必要はない。何を守り、何を討つ？"),
                new StoryDialogueLine("契約師", "仲間の帰り道を塞ぐものを。力を見せるためだけには、あなたを呼ばない。"),
                new StoryDialogueLine("イオナ", "その言葉を契約に残すわ。白虎の返事も、あなたの返事と並べて。")),
            new GuardianDialogueDefinition("byakko", "bond", "一撃のあとで",
                new StoryDialogueLine("白虎", "厚い守りを砕くだけでは、道は開ききらぬ。続く者が、こちらの狙いを知ってこそだ。"),
                new StoryDialogueLine("イオナ", "牙の印を仲間へ渡すか、弱った相手を追うか。どちらも、次の一手につなぐ型ね。"),
                new StoryDialogueLine("契約師", "白虎だけに任せきりにはしない。印が見えたら、皆でそこを開こう。"),
                new StoryDialogueLine("ルシェ", "戦いの記録にも、続いた仲間の働きが残っています。一撃だけの勝利ではなかったのですね。")),
            new GuardianDialogueDefinition("byakko", "oath", "並び立つ者",
                new StoryDialogueLine("白虎", "我が牙を借りても、汝は仲間を盾にしなかった。並び立つ相手として、ここに誓おう。"),
                new StoryDialogueLine("契約師", "ありがとう。強くなることを、誰かを置いていく理由にはしない。"),
                new StoryDialogueLine("ルシェ", "戻られた皆様の席があります。白虎様にも、炉のそばを空けておきました。"),
                new StoryDialogueLine("イオナ", "今は牙を休めていいわ。続きの道は、皆で休んでから。")),
            new GuardianDialogueDefinition("genbu", "birth", "待てる岸辺",
                new StoryDialogueLine("玄武", "呼び声を聞いた。急がずともよい。ここは、返事を待てる岸だ。"),
                new StoryDialogueLine("契約師", "守ってほしい仲間がいる。でも、あなた一人に耐えてもらう約束にはしたくない。"),
                new StoryDialogueLine("イオナ", "支える側の声も聞く。障壁の式にも、そのための余白を作りましょう。"),
                new StoryDialogueLine("ルシェ", "帰ってきたら、皆様で休んでください。守る方にも、帰る場所は必要です。")),
            new GuardianDialogueDefinition("genbu", "bond", "盾の内側",
                new StoryDialogueLine("ルシェ", "障壁の内側で、仲間が次の一歩を選べたそうです。守られるだけの場所ではないのですね。"),
                new StoryDialogueLine("玄武", "厚く守る日もあれば、薄く支えて押し返す日もある。盾を張る前に、皆の顔を見よ。"),
                new StoryDialogueLine("契約師", "今日は無理をさせたくない。堅守で進んで、余裕があれば反攻も試そう。"),
                new StoryDialogueLine("イオナ", "変えられる約束なら、苦しいまま我慢しなくていい。出発前に、何度でも確かめて。")),
            new GuardianDialogueDefinition("genbu", "oath", "皆の帰る岸",
                new StoryDialogueLine("玄武", "盾の陰に誰かを隠したままにせず、皆で帰ってきた。その足音を、誓いの証としよう。"),
                new StoryDialogueLine("イオナ", "守り切った数だけではなく、今ここにいる声を確かめる。最後まで、それでよかったのね。"),
                new StoryDialogueLine("契約師", "玄武も戻ってきた一人だよ。次は誰かが守る側になれるように、ここで休もう。"),
                new StoryDialogueLine("ルシェ", "ええ。待つ場所だけでなく、皆が休める岸にしましょう。おかえりなさい。")),
        };

        public static GuardianDialogueDefinition Find(string eventId) => All.FirstOrDefault(d => d.EventId == eventId);
        public static GuardianDialogueDefinition[] ForGuardian(string id) => All.Where(d => d.GuardianId == id).ToArray();
        public static bool CanRead(PlayerProfile p, string eventId)
        {
            var dialogue = Find(eventId);
            if (p == null || dialogue == null || GuardianService.Owned(p, dialogue.GuardianId) == null) return false;
            return dialogue.Stage == "birth" ||
                dialogue.Stage == "bond" && GuardianService.Level(p, dialogue.GuardianId) >= GuardianService.BondDialogueLevel ||
                dialogue.Stage == "oath" && GuardianService.OathCleared(p, dialogue.GuardianId);
        }
        public static bool HasSeen(PlayerProfile p, string eventId) => p?.SeenGuardianDialogueIds.Contains(eventId) == true;
        public static bool MarkSeen(PlayerProfile p, string eventId)
        {
            if (!CanRead(p, eventId) || HasSeen(p, eventId)) return false;
            p.SeenGuardianDialogueIds.Add(eventId);
            return true;
        }
    }
}
