using System;

namespace WitchTower.Data
{
    public sealed class StoryDialogueLine
    {
        public StoryDialogueLine(string speaker, string text)
        {
            Speaker = speaker ?? string.Empty;
            Text = text ?? string.Empty;
        }

        public string Speaker { get; }
        public string Text { get; }
    }

    public sealed class StoryDialogueDefinition
    {
        public StoryDialogueDefinition(string eventId, string title, string subtitle, string summary,
            int unlockFloor, StoryDialogueLine[] lines)
        {
            EventId = eventId ?? string.Empty;
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            Summary = summary ?? string.Empty;
            UnlockFloor = unlockFloor;
            Lines = lines ?? Array.Empty<StoryDialogueLine>();
        }

        public string EventId { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public string Summary { get; }
        public int UnlockFloor { get; }
        public StoryDialogueLine[] Lines { get; }
    }

    // Verbatim dialogue from STORY_FIRST_SUMMON_IONA and STORY_CHAPTER_01..06,
    // All dialogue uses the approved 2026-09-20 manuscripts.
    // Stage directions stay in the source manuscripts.
    public static class StoryDialogueCatalog
    {
        public const string IntroBeforeId = "story_first_summon_iona_before";
        public const string GuardianIntroId = "story_guardian_trial_intro";
        public const string IntroAfterId = "story_first_summon_iona_after";

        public static StoryDialogueDefinition[] All { get; } = new[]
        {
            new StoryDialogueDefinition(IntroBeforeId, "最初の仲間", "初回召喚・儀式の前",
                "ルシェに召喚の儀式を支えるイオナを紹介された。人とともに戦うモンスターを仲間に迎えるため、最初の召喚に臨む。", 0, new[]
                {
                    new StoryDialogueLine("ルシェ", "冒険者さん。迷宮へ向かう前に紹介させてください。召喚の儀式を支えてくれるイオナです。"),
                    new StoryDialogueLine("イオナ", "よろしく。モンスターを見ても全部敵だと思わないでね。これからあなたの背中を守ってくれる相手にもなるんだから。"),
                    new StoryDialogueLine("主人公", "一緒に戦ってくれるんだね。それなら心強い。どんな仲間に会えるか楽しみだ。"),
                    new StoryDialogueLine("イオナ", "見た目だけで決めつけないこと。何が得意でどう戦うか。そういうのは一緒に過ごして覚えていけばいい。"),
                    new StoryDialogueLine("ルシェ", "イオナは厳しそうに見えますけど仲間のことになるととても心配性なんですよ。"),
                    new StoryDialogueLine("イオナ", "そこは紹介しなくていいの。……さあ準備はできたわ。最初の挨拶はあなたからお願い。"),
                }),
            new StoryDialogueDefinition(IntroAfterId, "最初の仲間", "初回召喚・儀式の後",
                "召喚した仲間に一緒に村を守ってほしいと伝えた。イオナは仲間とともに無事に帰るよう願い、ルシェと村で待つと約束した。", 0, new[]
                {
                    new StoryDialogueLine("主人公", "来てくれてありがとう。これからよろしく。一緒にこの村を守ってほしい。"),
                    new StoryDialogueLine("イオナ", "うん、それでいい。あなたも仲間もちゃんと帰ってくること。ここではルシェと私が待っているから。"),
                }),
            new StoryDialogueDefinition(StoryTutorialService.StoryChapter2Unlocked, "おかえりと焦げたパン", "第1話・見習いの五門洞",
                "五門洞を攻略して村の近くに平穏が戻った。しかし廃工廠では村を襲うための兵器が作られていると分かる。", 10, new[]
                {
                    new StoryDialogueLine("", "ホームへ戻るとルシェが駆け寄ってきた。その後ろでイオナが黒く焼けたパンの皿を持っている。"),
                    new StoryDialogueLine("ルシェ", "おかえりなさい！　冒険者さん。お怪我はありませんか？　仲間の皆さんも無事ですか？"),
                    new StoryDialogueLine("主人公", "無事だよ。五門洞の奥まで片づいた。……それよりそのパン。ずいぶん黒いね。"),
                    new StoryDialogueLine("ルシェ", "帰る頃に焼き上がるようにしたんですけど門のほうが気になって。少しだけ目を離してしまいました。"),
                    new StoryDialogueLine("イオナ", "少しじゃないわ。何度も様子を見に行くから。村へ知らせる前にパンが煙で知らせてくれたのよ。"),
                    new StoryDialogueLine("主人公", "中は食べられそうだ。……うん、おいしい。待っていてくれたんだね。二人ともありがとう。"),
                    new StoryDialogueLine("ルシェ", "五門洞が静まれば村の人たちも畑へ出られます。皆さんきっと喜びますよ。本当にありがとうございます。"),
                    new StoryDialogueLine("", "食卓の脇で五門洞から得た道の記録を広げる。点灯した小門の先には獣影の廃工廠へ続く転移路があった。"),
                    new StoryDialogueLine("イオナ", "でも喜んでばかりはいられない。廃工廠は魔王軍の兵器工場よ。ここに村へ運ぶ予定まで書いてある。"),
                    new StoryDialogueLine("主人公", "五門洞の敵を倒してももっと大勢で攻めてくるつもりだったんだね。"),
                    new StoryDialogueLine("ルシェ", "この道なら兵器が運び出される前に工場へ入れます。村へ来るのを待つより先に止められます。"),
                    new StoryDialogueLine("イオナ", "目当ては暴走した生産炉。止めれば魔王軍へ新しい兵器は渡らない。次はあそこを黙らせましょう。"),
                    new StoryDialogueLine("主人公", "分かった。仲間と準備を整えて向かう。せっかく畑へ出られるようになったのにまた閉じこもらせたくない。"),
                    new StoryDialogueLine("ルシェ", "お願いします。でも今日は休んでください。帰ってきたばかりの方をそのまま送り出したくありません。"),
                    new StoryDialogueLine("主人公", "そうするよ。次に戻ったときもパンをお願いしていい？　できればもう少し明るい色で。"),
                    new StoryDialogueLine("イオナ", "注文が増えたわね。次は私が窯を見るからルシェは門の前で好きなだけ待っていなさい。"),
                }),
            new StoryDialogueDefinition(StoryTutorialService.StoryChapter3Unlocked, "守るだけでは終わらない", "第2話・獣影の廃工廠",
                "兵器の生産を止めたが魔王軍は襲撃を繰り返すつもりだった。主人公たちは魔王自身を倒すために居場所を調べると決める。", 20, new[]
                {
                    new StoryDialogueLine("", "ホームへ戻るとイオナが大きな袋を肩にかけていた。ルシェはその前に立って何かを言いかけている。"),
                    new StoryDialogueLine("主人公", "ただいま。二人ともどうしたの？　そんな荷物を持ってこれからどこかへ行くところ？"),
                    new StoryDialogueLine("イオナ", "道具の整理よ。たまたま全部袋に入れただけ。あなたたちを迎えに行こうとしたわけじゃないから。"),
                    new StoryDialogueLine("ルシェ", "さっきは少し様子を見てくるだけだと。……よかったです。入れ違いになるところでした。"),
                    new StoryDialogueLine("主人公", "心配をかけたね。生産炉は止まったよ。もうあそこで新しい兵器が作られることはない。"),
                    new StoryDialogueLine("イオナ", "そう。よくやったわ。……無事ならそれでいいの。さあ荷物を下ろして。話は座ってから。"),
                    new StoryDialogueLine("", "机に広げた廃工廠の作戦書にはいくつもの村の名が並んでいた。その一つを見つけたルシェの表情が曇る。"),
                    new StoryDialogueLine("ルシェ", "この村もあります。一度攻めて終わりではなく降伏するまで何度でも軍を送るつもりだったようです。"),
                    new StoryDialogueLine("主人公", "工場を止めても別の場所から来るのか。それなら守っているだけでは終わらない。魔王を倒そう。"),
                    new StoryDialogueLine("イオナ", "言うのは簡単よ。相手は魔王ガルザ。一人で乗り込んでどうにかなる敵じゃない。"),
                    new StoryDialogueLine("主人公", "一人で行くつもりはないよ。仲間もいる。ルシェもイオナもここで支えてくれるんでしょう？"),
                    new StoryDialogueLine("イオナ", "……そうだったわね。昔の私とは違う。助けを当てにしてくれるならこちらも張り合いがあるわ。"),
                    new StoryDialogueLine("ルシェ", "生産炉が止まって古契約の地下書庫への道が開きました。古い地図なら魔王の居場所を調べられるかもしれません。"),
                    new StoryDialogueLine("主人公", "次はその書庫へ行こう。魔王が来るのを待つんじゃなくこちらから会いに行くために。"),
                    new StoryDialogueLine("イオナ", "分かった。私はここで召喚の準備を整えておく。道具袋もほどくから救援に出る前に帰ってきてよね。"),
                    new StoryDialogueLine("ルシェ", "ではまず食事です。二人とも話しながら立ち上がらないでください。冒険も準備も食べてからです。"),
                }),
            new StoryDialogueDefinition(StoryTutorialService.StoryChapter4Unlocked, "残る人の戦い", "第3話・古契約の地下書庫",
                "書庫の地図から魔王の居城へ向かう道が分かった。ルシェは村に残って帰還する場所を守る役目を引き受ける。", 30, new[]
                {
                    new StoryDialogueLine("", "ホームに帰還するとルシェの足元に旅支度の袋が置かれていた。机には書庫から得た大きな地図が広がっている。"),
                    new StoryDialogueLine("ルシェ", "おかえりなさい。魔王への道が見つかりましたね。……冒険者さん。次は私も一緒に連れていってください。"),
                    new StoryDialogueLine("主人公", "ルシェも？　急にどうしたの。ここで調べ物をしてくれるだけでもずいぶん助かっているよ。"),
                    new StoryDialogueLine("ルシェ", "皆さんが危険な場所へ行くたびに私は安全なところで待っています。それで仲間だと言っていいのか分からなくて。"),
                    new StoryDialogueLine("イオナ", "残るのは逃げることじゃないわ。こちらが留守の間に敵が来たら村の人をどこへ逃がす？"),
                    new StoryDialogueLine("ルシェ", "……南の道です。北の橋は古いので大勢で渡ると危険です。歩くのが遅い方は先に荷車へ。"),
                    new StoryDialogueLine("主人公", "そんなことまで考えてくれていたんだ。ここを任せられるから安心して先へ進める。それはルシェの力だよ。"),
                    new StoryDialogueLine("ルシェ", "本当に任せてもらえますか？　冒険に出られないから仕方なく残るのではなくて。"),
                    new StoryDialogueLine("主人公", "うん。村を守ってほしい。それから帰ったら迎えてほしい。ほかの誰かじゃなくてルシェに。"),
                    new StoryDialogueLine("", "ルシェは旅支度の袋を椅子の下へ置いて地図の端へ避難路を書き込んだ。それから迷宮へ続く道を指でなぞる。"),
                    new StoryDialogueLine("ルシェ", "では私はここを守ります。こちらを見てください。魔王の居城へは紅蓮竜道を越える道がつながっています。"),
                    new StoryDialogueLine("イオナ", "火口と溶岩だらけの道ね。その先には星鉱の巨殿。魔王軍の大きな砦がある。楽な旅にはならないわ。"),
                    new StoryDialogueLine("主人公", "でもどこへ進めばいいかは分かった。道があるなら仲間と一つずつ越えていこう。"),
                    new StoryDialogueLine("ルシェ", "私も休めそうな場所や遠回りの道を調べます。敵のいる場所を知るだけが案内役の仕事ではありませんから。"),
                    new StoryDialogueLine("イオナ", "頼もしくなったわね。じゃあ私はあなたたちが無茶をしないよう見張る係も引き受ける。"),
                    new StoryDialogueLine("ルシェ", "それなら私はイオナを見張ります。救援に出ようとした方も忘れず休んでくださいね。"),
                }),
            new StoryDialogueDefinition(GuardianIntroId, "ともに帰る力", "第3話のあと・神獣の試練",
                "書庫の地図から神獣の聖域が見つかった。仲間と力を合わせて試練に挑み、新たな仲間を迎えることにした。", 0, new[]
                {
                    new StoryDialogueLine("ルシェ", "冒険者さん。書庫の地図にもう一つ道がありました。青龍たち四柱の神獣がいる聖域です。"),
                    new StoryDialogueLine("主人公", "神獣も一緒に戦ってくれるの？　これから先の旅で力を貸してもらえたら心強いね。"),
                    new StoryDialogueLine("イオナ", "ただ頼むだけではだめよ。まずは試練で力を示すの。今の仲間と協力して立ち向かいなさい。"),
                    new StoryDialogueLine("ルシェ", "勝つと神核が手に入ります。それを持ち帰れば神獣を仲間に迎えられます。まずは一柱を選びましょう。"),
                    new StoryDialogueLine("主人公", "分かった。仲間と挑んでみるよ。ルシェたちが守ってくれるこの村へ、みんなで帰るために。"),
                    new StoryDialogueLine("イオナ", "準備ができたらホームの『神獣の聖域』へ。負けてもまた挑めるわ。急がなくていいからね。"),
                }),
            new StoryDialogueDefinition(StoryTutorialService.StoryChapter5Unlocked, "誰も置いていかない", "第4話・紅蓮竜道",
                "村は襲撃を受けたが二人の働きで村人は助かった。主人公たちは一人で犠牲を引き受けずに協力して魔王軍の砦を落とすと決める。", 40, new[]
                {
                    new StoryDialogueLine("", "ホームへ戻ると入口の柱に焼け跡が残っていた。イオナが腕を背中へ隠す。その手首には白い布が巻かれている。"),
                    new StoryDialogueLine("主人公", "何があったの？　敵が来た？　イオナ……その腕。村の人たちは無事なの？"),
                    new StoryDialogueLine("イオナ", "大丈夫。少し擦りむいただけよ。敵も追い払ったしあなたたちが帰ってくる頃には片づけるつもりだった。"),
                    new StoryDialogueLine("ルシェ", "少しではありません。一人で入口に立って全部止めようとするからです。見つけたときは本当に驚きました。"),
                    new StoryDialogueLine("主人公", "間に合わなくてごめん。村を守るために進んでいたのにこっちで二人が危ない目に遭っていたなんて。"),
                    new StoryDialogueLine("ルシェ", "謝らないでください。皆さんが竜道を通れるようにしてくれたからこちらも魔王の砦へ近づけるんです。"),
                    new StoryDialogueLine("イオナ", "村人は全員無事よ。ルシェが南の道へ逃がした。小さな子も歩けない人もちゃんと確かめていたわ。"),
                    new StoryDialogueLine("ルシェ", "道を調べておいてよかったです。声は震えましたけど行き先だけは間違えずに伝えられました。"),
                    new StoryDialogueLine("主人公", "ありがとう。村を守ってくれたんだね。……でもイオナまで倒れていたら無事に守れたとは言えないよ。"),
                    new StoryDialogueLine("イオナ", "分かってる。昔も一人で敵に立ち向かって仲間に運ばれて帰ったの。あれで懲りたつもりだったんだけど。"),
                    new StoryDialogueLine("ルシェ", "でしたら今回は私にも守らせてください。入口に立つだけが戦いではないと教えてくれたのはイオナでしょう？"),
                    new StoryDialogueLine("イオナ", "……自分で言ったことがそのまま返ってきたわね。分かった。まず座る。それからちゃんと休むわ。"),
                    new StoryDialogueLine("", "ルシェがイオナの前へ温かい食事を置いた。机の反対側では紅蓮竜道の奥で見つかった鉱脈の道を確認している。"),
                    new StoryDialogueLine("ルシェ", "星のように光る鉱脈が星鉱の巨殿まで続いています。村を襲った敵もあの砦から送られてきたようです。"),
                    new StoryDialogueLine("主人公", "それなら次は砦を落とそう。村へ来る敵を減らして魔王までの道も開く。一度に両方進められる。"),
                    new StoryDialogueLine("イオナ", "私は村で備えを整えるわ。今度は一人で抱え込まない。ルシェも一緒に逃がす道と守る場所を決めて。"),
                    new StoryDialogueLine("ルシェ", "はい。誰かだけが残らなくても済むように。冒険者さんも仲間を頼ってください。イオナと同じ約束です。"),
                    new StoryDialogueLine("主人公", "約束する。みんなで守ってみんなで帰ろう。……そのためにも今は一緒に食べようか。"),
                }),
            new StoryDialogueDefinition(StoryTutorialService.StoryChapter6Unlocked, "帰ってきたらみんなで", "第5話・星鉱の巨殿",
                "魔王の居城へ続く道が開いた。三人は帰還門への警戒と村の避難を決めて討伐後に全員で食卓を囲むと約束する。", 50, new[]
                {
                    new StoryDialogueLine("", "ホームへ戻ると村のほうから喜ぶ声が聞こえた。ルシェはその声を聞きながら地図の最後の道を指でなぞっている。"),
                    new StoryDialogueLine("ルシェ", "おかえりなさい。砦が落ちた知らせが届きました。村の見張りも敵の姿が減ったと喜んでいます。"),
                    new StoryDialogueLine("イオナ", "兵器工場を止めて竜道を抜けて砦まで落とした。魔王軍も好きなようには動けなくなったわね。"),
                    new StoryDialogueLine("主人公", "仲間が頑張ってくれたからだよ。二人もありがとう。ここまで来られた。……魔王への道は？"),
                    new StoryDialogueLine("ルシェ", "星鉱の転移路が復旧しました。この先が深淵魔導回廊。魔王ガルザはそこを居城にしています。"),
                    new StoryDialogueLine("イオナ", "残るのは魔王。だけど近づくほど敵も必死になる。帰るときまで油断しないでほしいの。"),
                    new StoryDialogueLine("主人公", "うん。倒したつもりで背中を向けない。帰還門に入るまで仲間と一緒に気をつけるよ。"),
                    new StoryDialogueLine("イオナ", "門の中もよ。道がつながれば敵が追ってくるかもしれない。こちら側でも受け止める準備をしておく。"),
                    new StoryDialogueLine("ルシェ", "村の人たちには門から離れた安全な場所へ移ってもらいます。何かあっても慌てずに動けるように。"),
                    new StoryDialogueLine("主人公", "助かる。村のことは任せるね。魔王を倒してここへ帰ってくる。それを最後の目標にしよう。"),
                    new StoryDialogueLine("ルシェ", "はい。帰ってきたらごはんにしましょう。皆さんの分を用意します。今度はパンも焦がしません。"),
                    new StoryDialogueLine("イオナ", "そこは自信を持ちすぎないほうが。……私は食卓に着く前に道具の片づけを始めないようにするわ。"),
                    new StoryDialogueLine("主人公", "それじゃ約束だね。魔王を倒してみんなで食べよう。誰かだけ働いているのはなしで。"),
                    new StoryDialogueLine("ルシェ", "冒険者さん。怖くないと言ったら嘘になります。でも今はそれより帰ってくる姿を信じたいです。"),
                    new StoryDialogueLine("イオナ", "二人で待っているわ。召喚も準備もいつもどおり手伝う。あなたもいつもどおり仲間を頼りなさい。"),
                    new StoryDialogueLine("主人公", "うん。準備ができたら出発しよう。帰ってきたときの話をここでするために。"),
                }),
            new StoryDialogueDefinition(StoryTutorialService.StoryFirstArcComplete, "魔王を倒して帰ろう", "第6話・深淵魔導回廊",
                "帰還門を追ってきた魔王ガルザを全員で力を合わせて倒した。食卓を囲んだ一同は魔王を倒して帰るという約束を果たす。", 60, new[]
                {
                    new StoryDialogueLine("", "ホームへ帰還した直後に背後の門が大きく揺れた。閉じかけた光を押し広げて巨大な黒い影が踏み込んでくる。"),
                    new StoryDialogueLine("ルシェ", "おかえりなさ……冒険者さん！　後ろです！　皆さんは門から離れてください！"),
                    new StoryDialogueLine("魔王ガルザ", "我が城を踏み荒らして無事に帰れると思ったか。貴様らが守るこの村から焼き払ってやろう。"),
                    new StoryDialogueLine("イオナ", "魔王ガルザ……本当に追ってきたのね。ルシェは予定どおりに。私はここで攻撃を止める。"),
                    new StoryDialogueLine("ルシェ", "村の人たちは先に避難してもらっています。門の近くには誰も残していません。ここで食い止めましょう。"),
                    new StoryDialogueLine("主人公", "この村は壊させない。みんなもう少しだけ力を貸して。ここで魔王を倒して一緒に帰ろう。"),
                    new StoryDialogueLine("魔王ガルザ", "魔物に頼むだと？　道具に声をかける必要などない。力で従わせて盾にすればよいものを。"),
                    new StoryDialogueLine("主人公", "道具じゃない。ここまで一緒に戦ってきた仲間だ。だから一方的に盾になれなんて言わない。"),
                    new StoryDialogueLine("", "魔王の腕から黒い炎が広がる。イオナが前へ出ると光の盾が炎を受け止めた。足元の石が熱で赤く染まる。"),
                    new StoryDialogueLine("イオナ", "長くは止められないわ。今度は一人で頑張るつもりなんてないからね。みんな頼んだわよ！"),
                    new StoryDialogueLine("魔王ガルザ", "ならばまとめて消してやる！　その小さな盾ごと村の端まで焼き尽くしてくれる！"),
                    new StoryDialogueLine("ルシェ", "魔王が腕を上げています！　攻撃する前に止めてください！　イオナはこちらまで下がれます！"),
                    new StoryDialogueLine("主人公", "今だ！　力を合わせよう！　イオナが止めてくれているうちに一気に押し返す！"),
                    new StoryDialogueLine("", "主人公の合図に応えて仲間の攻撃が魔王へ集中する。炎が途切れた瞬間にルシェがイオナを安全な場所へ引いた。"),
                    new StoryDialogueLine("魔王ガルザ", "なぜだ……なぜ逃げぬ！　我が力を前にしてなぜまだ立ち向かってこられる！"),
                    new StoryDialogueLine("主人公", "一人で戦っていないからだ。みんなもう一度！　これで終わらせよう！"),
                    new StoryDialogueLine("", "仲間の一撃が魔王を打ち倒した。黒い炎が消えて巨体が崩れる。ガルザの影は灰となり帰還門も静かに閉じた。"),
                    new StoryDialogueLine("ルシェ", "終わった……んですね。魔王を倒したんですね。冒険者さんもイオナも皆さんも……ここにいますね。"),
                    new StoryDialogueLine("イオナ", "ええ。ちゃんといるわ。今度は一人で倒れずに済んだ。ルシェも引っ張ってくれてありがとう。少し腕は痛かったけど。"),
                    new StoryDialogueLine("主人公", "二人がここを守ってくれたから最後まで戦えた。みんなで勝ったんだ。……約束のごはんにしようか。"),
                    new StoryDialogueLine("", "その夜、拠点に焼きたてのパンの匂いが広がった。人にもモンスターにも食事が並び村に久しぶりの笑い声が戻る。"),
                    new StoryDialogueLine("イオナ", "今日は道具の片づけも後回し。私もおかわりをもらうわ。ルシェが焼いた今度のパンはちゃんとパンの色ね。"),
                    new StoryDialogueLine("主人公", "ただいま。ルシェもイオナも待っていてくれてありがとう。これからもみんなでこの村を守ろう。"),
                    new StoryDialogueLine("ルシェ", "おかえりなさい、私たちの勇者様。さあ冷めないうちに。今日は誰も一人で食べさせません。"),
                }),
        };

        public static StoryDialogueDefinition Find(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return null;
            foreach (var definition in All)
                if (string.Equals(definition.EventId, eventId, StringComparison.Ordinal)) return definition;
            return null;
        }
    }
}
