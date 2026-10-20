using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    /// <summary>
    /// KI für das Deck „Sun God“ (The Winged Dragon of Ra, Immortal Phoenix).
    /// Grundplan: Helpoemer/Makyura suchen und Ra in den Friedhof bringen, dann mit
    /// „The Immortal Sun God“ den „Sun God of Darkness“ beschwören und gegnerische Zonen leeren.
    /// Spielt bevorzugt als Zweiter.
    /// </summary>
    [Deck("SunGod", "AI_SunGod")]
    public class SunGodExecutor : DefaultExecutor
    {
        private const int SetSunGod = 0x1e6;
        private const int ImmortalSunGodCopies = 2;

        // Karten werden zusätzlich per Name gesucht, damit Prerelease- und spätere offizielle IDs funktionieren.
        private readonly int[] Helpoemer = Find(new[] { "The Sun God's Poet - Helpoemer", "Helpoemer, Chanter of the Sun God" }, 101403001);
        private readonly int[] Makyura = Find(new[] { "The Sun God's Destructor - Makyura", "Makyura, Destructor of the Sun God" }, 101403002);
        private readonly int[] GilGarth = Find(new[] { "The Sun God's Terror Machine - Gil Garth", "Gil Garth, Exterminator of the Sun God" }, 101403003);
        private readonly int[] Viser = Find(new[] { "The Sun God's Torture Device - Viser Des Shock", "Viser Des Shock, Tormentor of the Sun God" }, 101403004);
        private readonly int[] LavaGolem = Find(new[] { "The Sun God's Steelcage - Lava Golem", "Lava Golem, Steelcage of the Sun God" }, 101403031);
        private readonly int[] ImmortalSunGod = Find("The Immortal Sun God", 101403051);
        private readonly int[] LeadingDown = Find("The Sun God Leading Down into Darkness", 101403052);
        private readonly int[] GodSlimeTrap = Find(new[] { "The Sun God's Protector - Egyptian God Slime", "Egyptian God Slime, Protector of the Sun God" }, 101403073);
        private readonly int[] SunGodDarkness = Find(new[] { "The Sun God of Darkness - The Winged Dragon of Ra", "The Winged Dragon of Ra, Sun God of Darkness" }, 101403130, 101403030);
        private readonly int[] TrueSunGod = Find("The True Sun God", 11587414);
        private readonly int[] Ra = Find("The Winged Dragon of Ra", 10000012, 10000010);
        private readonly int[] Sphere = Find("The Winged Dragon of Ra - Sphere Mode", 10000080);
        private readonly int[] Phoenix = Find("The Winged Dragon of Ra - Immortal Phoenix", 10000090);
        private readonly int[] AncientChant = Find("Ancient Chant", 78665705);
        private readonly int[] GuardianSlime = Find("Guardian Slime", 15771991);
        private readonly int[] Dangers = Find("Dangers of the Divine", 22082432);
        private readonly int[] EgyptianGodSlime = Find("Egyptian God Slime", 42166000);
        private readonly int[] CalledByTheGrave = Find("Called by the Grave", 24224830);
        private readonly int[] TripleTactics = Find("Triple Tactics Talent", 25311006);
        private readonly int[] DominusImpulse = Find("Dominus Impulse", 40366667);
        private readonly int[] Impermanence = Find("Infinite Impermanence", 10045474);
        private readonly int[] Varudras = Find("Varudras, the Final Bringer of the End Times", 70636044);
        private readonly int[] Gustav = Find("Superdreadnought Rail Cannon Gustav Max", 56910167);
        private readonly int[] Rabbit = Find("Silhouhatte Rabbit", 1528054);
        private readonly int[] WeeWitch = Find("Wee Witch's Apprentice", 71384012);
        private readonly int[] Ash = Find("Ash Blossom & Joyous Spring", 14558127);
        private readonly int[] Reinforcement = Find("Reinforcement of the Army", 32807846);
        private readonly int[] FoolishBurialGoods = Find("Foolish Burial Goods", 35726888);
        private readonly int[] Domination = Find("Sun God Domination", 101403072);

        private const int GodSlimeTrapCopies = 3;
        private bool _chantApplied;
        private bool _slimeUsedThisTurn;
        private bool _immortalRetrievedThisTurn;
        private bool _leadingDownActivatedThisTurn;
        private bool _helpoemerSummonedThisTurn;
        private bool _gilGarthSummonedThisTurn;
        private bool _immortalActivatedThisTurn;
        private int _immortalInGraveAtTurnStart;
        private List<int> _darknessZones = new List<int>(); // geplante gegnerische Main-Zonen für Darkness
        private int _darknessDiscards = 1;                     // so viele Sun-God-Karten wirft Darkness ab
        private bool _darknessOtk;                             // Abwurf für den OTK: stärkste Monster zuerst
        private bool _multiZone;                               // WindBot kann mehrere Zonen melden (Hook aktiv)
        private bool _hookTried;
        private readonly HashSet<ClientCard> _darknessFree = new HashSet<ClientCard>(); // kommen in der End Phase zurück

        // Reihenfolge = Priorität. Die Combo folgt Ketos Spielweise (Replay vs. Swordsoul, 2026-10-04):
        // Ancient Chant → Makyura (setzt Slime) → Slime → Leading Down → Helpoemer (Guardian Slime abwerfen)
        // → True Sun God → Silhouhatte Rabbit → Immortal Sun God → Immortal aus dem Friedhof zurück und setzen.
        public SunGodExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // Unterbrechungen und Reaktionen
            Add(ExecutorType.Activate, Impermanence, ImpermanenceEffect);
            Add(ExecutorType.Activate, Ash, DefaultAshBlossomAndJoyousSpring);
            Add(ExecutorType.Activate, CalledByTheGrave, CalledByTheGraveEffect);
            Add(ExecutorType.Activate, DominusImpulse, DominusImpulseEffect);
            Add(ExecutorType.Activate, Varudras, VarudrasEffect);
            Add(ExecutorType.Activate, GuardianSlime, () => true);
#if !OHNE_VISERZIEL
            Add(ExecutorType.Activate, Viser, ViserEffect);
#else
            Add(ExecutorType.Activate, Viser, () => Card.Location != CardLocation.Hand || RoomForFusionAfterSummon() || Duel.Player == 1);
#endif
            Add(ExecutorType.Activate, Phoenix, PhoenixEffect);
            Add(ExecutorType.Activate, Ra, RaEffect);
            Add(ExecutorType.Activate, Dangers, DangersEffect);
            Add(ExecutorType.Activate, TripleTactics, () => true);

            // Feldräumung als Zweiter
            Add(ExecutorType.Summon, Sphere, SphereSummon);
            Add(ExecutorType.Activate, Sphere, () => Card.Controller == 0);

            // Patch 13 (Claude vs Swordsoul 2026-10-06): Leading Down sofort aktivieren – Ra liegt dann im Friedhof,
            // bevor der Gegner stört. Ein gesetztes Leading Down hat Swordsoul Blackout zerstört (Duell 2).
            Add(ExecutorType.Activate, LeadingDown, LeadingDownEffect);
            // Combo-Teile in Ketos Reihenfolge (v2: zuerst Immortal Sun God in den Friedhof und Makyura holen)
            Add(ExecutorType.Activate, FoolishBurialGoods, () => ImmortalSunGodInDeck() > 0);
            Add(ExecutorType.Activate, Reinforcement, () => true);
            Add(ExecutorType.Activate, AncientChant, AncientChantEffect);
            Add(ExecutorType.Summon, Makyura, RoomForFusionAfterSummon);
            Add(ExecutorType.Activate, Makyura, () => true);
            Add(ExecutorType.Activate, GodSlimeTrap, GodSlimeTrapEffect);
            Add(ExecutorType.Activate, Helpoemer, HelpoemerEffect);
            Add(ExecutorType.Activate, TrueSunGod, TrueSunGodEffect);
            Add(ExecutorType.Activate, GilGarth, GilGarthEffect);
            Add(ExecutorType.SpSummon, WeeWitch, WeeWitchSummon);
            Add(ExecutorType.SpSummon, Rabbit, RabbitSummon);
            Add(ExecutorType.Activate, Rabbit, RabbitEffect);
            Add(ExecutorType.Activate, ImmortalSunGod, ImmortalSunGodEffect);
            Add(ExecutorType.Activate, SunGodDarkness, SunGodDarknessEffect);
            Add(ExecutorType.Activate, Domination, DominationEffect);
            Add(ExecutorType.Summon, Ra, RaSummon);
            Add(ExecutorType.SpSummon, EgyptianGodSlime, () => Bot.GetMonsters().Any(m => Is(m, GuardianSlime) && m.IsFaceup()));
            Add(ExecutorType.SpSummon, LavaGolem, LavaGolemSummon);
            Add(ExecutorType.Summon, GilGarth, NormalBeater);
            Add(ExecutorType.Summon, Viser, NormalBeater);
            Add(ExecutorType.Summon, Helpoemer, NormalBeater);
            // Bewusst keine Xyz-Beschwörung (Varudras/Gustav) aus Sun God of Darkness und Slime (Keto: schlecht):
            // Darkness bleibt als Störung im Spiel bzw. geht für die Rückholungen in den Friedhof, Slime bleibt als Wand.

            // Für den gegnerischen Zug setzen
            Add(ExecutorType.SpellSet, ImmortalSunGod, () => true);
            Add(ExecutorType.SpellSet, Dangers, () => true);
            Add(ExecutorType.SpellSet, GodSlimeTrap, () => true);
            Add(ExecutorType.SpellSet, Domination, () => true);
            Add(ExecutorType.SpellSet, DominusImpulse, () => true);
            Add(ExecutorType.SpellSet, Impermanence, () => true);
            Add(ExecutorType.SpellSet, CalledByTheGrave, () => true);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
        }

        // --- Hilfsfunktionen ------------------------------------------------------------

        private static int[] Find(string name, params int[] known)
        {
            return Find(new[] { name }, known);
        }

        /// <summary>Prerelease- und offizielle Namen (z. B. „Helpoemer, Chanter of the Sun God“).</summary>
        private static int[] Find(string[] names, params int[] known)
        {
            var ids = new HashSet<int>(known);
            try
            {
                foreach (NamedCard card in NamedCardsManager.GetAllCards())
                    if (names.Contains(card.Name))
                        ids.Add(card.Id);
            }
            catch (Exception) { }
            return ids.ToArray();
        }

        private void Add(ExecutorType type, int[] ids, Func<bool> func)
        {
            foreach (int id in ids)
#if !OHNE_LOG
                AddExecutor(type, id, Logged(type, func));
#else
                AddExecutor(type, id, func);
#endif
        }

#if !OHNE_LOG
        // --- Protokoll (WindBot\SunGodAI.log, 2026-10-17) -------------------------------------------
        // Wie bei LUNALIGHT/SACRED BEAST: Der Simulator hängt das Log an jedes Duell. „Auswahl … STANDARD aus:“ zeigt
        // Auswahlen, die keine eigene Regel trifft (WindBot nimmt dann die erste Karte). Ändert keine Entscheidung.
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SunGodAI.log");

        private void Log(string text)
        {
            try
            {
                var info = new FileInfo(LogPath);
                if (info.Exists && info.Length > 2_000_000) info.Delete();
                File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss} Z{Duel.Turn} {Duel.Phase} {text}{Environment.NewLine}");
            }
            catch (Exception) { }
        }

        private static string Name(ClientCard c) => c == null ? "-" : (c.Name ?? c.Id.ToString());

        private Func<bool> Logged(ExecutorType type, Func<bool> func)
        {
            if (func == null) return null;
            return () =>
            {
                bool result = func();
                if (result) Log($"{type} {Name(Card)} @{Card?.Location}");
#if !OHNE_HANDKETTE
                if (result && type == ExecutorType.Activate && Card != null) _aktivId = Card.Id;
#endif
                return result;
            };
        }

        private string KetteText()
        {
            var teile = new List<string>();
            for (int i = 0; i < Duel.CurrentChain.Count; i++)
            {
                ClientCard c = Duel.CurrentChain[i];
                string n = Name(c) + (c != null && c.Controller == 1 ? "/G" : "");
#if !OHNE_HANDKETTE
                if (_handGlied.TryGetValue(i, out int echt) && !Is(c, new[] { echt })) n += $" (echt: {Name(new ClientCard(echt, CardLocation.Hand, -1, 0))})";
#endif
                teile.Add(n);
            }
            return string.Join(", ", teile);
        }

        private static string HintText(long hint) => hint >= (1L << 20) ? $"{hint >> 20}:{hint & 0xFFFFF}" : hint.ToString();
#endif

#if !OHNE_HANDKETTE
        // 2026-10-17 (Fallstrick aus SB-Runde 2): WindBot holt das Kettenglied über Ort + Index aus der Kettennachricht. Wird
        // die Karte als Kosten bewegt (Gil Garth opfert sich), steht eine FALSCHE Karte in Duel.CurrentChain (A/B-Log 17.10.:
        // „Kette: Viser Des Shock“ – eine Friedhofskarte) → ResolvingSource fand Gil Garth nicht, seine Auflösung schickte die
        // erste Deckkarte (z. B. Sun God Domination) statt Viser/Makyura. Darum merken wir uns die zuletzt selbst aktivierte
        // Karte und ersetzen damit jedes eigene Glied, das nicht passt.
        private int _aktivId;
        private readonly Dictionary<int, int> _handGlied = new Dictionary<int, int>();   // Kettenindex → echte Karten-ID

        public override void OnChainEnd()
        {
            _handGlied.Clear();
            base.OnChainEnd();
        }
#endif

        // Training 2026-10-10: DefaultCalledByTheGrave kennt nur 7 Handfallen (Maxx C, Ash, Veiler …). Tearlaments' Herald of
        // Orange Light negierte Black Sheep, Called by the Grave blieb auf der Hand → jeder gegnerische Monstereffekt,
        // dessen Karte jetzt im Friedhof liegt (Handfalle als Kosten abgeworfen, Friedhofseffekt), wird beantwortet.
        private bool CalledByTheGraveEffect()
        {
            if (DefaultCalledByTheGrave()) return true;
            ClientCard last = Util.GetLastChainCard();
            if (Duel.LastChainPlayer != 1 || last == null || !last.IsMonster() || last.Controller != 1
                || last.Location != CardLocation.Grave) return false;
            AI.SelectCard(last);
            return true;
        }

        private static bool Is(ClientCard card, int[] ids)
        {
            // Nicht card.Alias verwenden: EDOPros WindBot lässt beim Neuzuweisen einer Deckkarte den alten Alias stehen.
            // Ein Platzhalter, der einmal eine Ra (Alias 10000010) war, galt sonst auch als Sphere Mode noch als „Ra“.
            if (card == null || card.Id == 0) return false;
            if (ids.Contains(card.Id)) return true;
            int alias = card.Data != null ? card.Data.Alias : 0;
            return alias != 0 && ids.Contains(alias);
        }

        private int DescIndex => ActivateDescription >= (1L << 20) ? (int)(ActivateDescription & 0xFFFFF) : -1;
        private bool MainPhase => Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2;
        private bool IsSunGod(ClientCard c) => c != null && c.HasSetcode(SetSunGod);
        private bool RaInGrave => Bot.Graveyard.Any(c => Is(c, Ra)) || Bot.Banished.Any(c => Is(c, Ra) && c.IsFaceup());
        private bool HasImmortalReady => Bot.HasInHand(ImmortalSunGod) || Bot.HasInSpellZone(ImmortalSunGod);
        private bool PhoenixInGrave => Bot.Graveyard.Any(c => Is(c, Phoenix));

        /// <summary>
        /// Immortal Sun God braucht Ra (oder Phoenix) im Friedhof. Ohne Ra dort ist Immortal tot
        /// (Replays 21:19 und 21:24: zwei gesetzte Immortal, aber kein Ra im Friedhof → Fusion erst Züge später).
        /// Ra auf der Hand zählt, wenn Helpoemer ihn heute noch abwerfen kann.
        /// </summary>
        private bool NeedRa => !RaInGrave && !PhoenixInGrave
                               && !(Bot.HasInHand(Ra) && Bot.HasInHand(Helpoemer) && !_helpoemerSummonedThisTurn);

        /// <summary>Leading Down kann in diesem Zug noch aktiviert werden (legt Ra in den Friedhof und sucht ein Monster).</summary>
        private bool LeadingDownUsableNow => Duel.Player == 0 && MainPhase && !_leadingDownActivatedThisTurn;

        /// <summary>Helpoemer und Gil Garth kommen in der End Phase zurück, wenn Immortal Sun God diesen Zug in den Friedhof ging.</summary>
        private bool ImmortalSentThisTurn => _immortalActivatedThisTurn
                                             || Bot.Graveyard.Count(c => Is(c, ImmortalSunGod)) > _immortalInGraveAtTurnStart;

        private int SunGodAttackInGrave()
        {
            return Bot.Graveyard.Where(c => IsSunGod(c) && c.IsMonster()).Sum(c => Math.Max(0, c.Attack));
        }

        private int ImmortalSunGodInDeck()
        {
            int seen = Bot.Hand.Concat(Bot.SpellZone.Where(c => c != null)).Concat(Bot.Graveyard).Concat(Bot.Banished)
                .Count(c => Is(c, ImmortalSunGod));
            return Math.Max(0, ImmortalSunGodCopies - seen);
        }

        /// <summary>Niedriger Wert = eher abwerfen/opfern.</summary>
        private int DiscardScore(ClientCard c)
        {
            int score;
            // Keto wirft bevorzugt Guardian Slime (sucht dann eine Ra-Karte) und Makyura (setzt eine Karte) ab
            // Ra (Original) und Phoenix gehören in den Friedhof: dann kann Immortal Sun God sofort die Fusion holen
            if (Is(c, Ra)) score = RaInGrave ? 8 : -2;
            else if (Is(c, Phoenix)) score = -1;
            else if (Is(c, GuardianSlime)) score = 0;
            else if (Is(c, Makyura)) score = 1;
            else if (Is(c, Viser)) score = 3;
            else if (Is(c, GilGarth)) score = 4;
            else if (Is(c, Sphere)) score = Enemy.GetMonsterCount() >= 3 ? 12 : 5;
            else if (Is(c, AncientChant)) score = 6;
            else if (Is(c, TripleTactics)) score = 7;
            else if (Is(c, Dangers)) score = 9;
            else if (Is(c, Impermanence) || Is(c, DominusImpulse)) score = 10;
            else if (Is(c, CalledByTheGrave)) score = 11;
            else if (Is(c, TrueSunGod)) score = 12;
            else if (Is(c, LeadingDown)) score = 13;
            else if (Is(c, ImmortalSunGod)) score = 14;
            else if (Is(c, Helpoemer)) score = 15;
            else score = 6;
            if (Bot.Hand.Count(h => h.Id == c.Id) >= 2) score -= 1;
            return score;
        }

        private IList<ClientCard> PickByScore(IList<ClientCard> cards, int min, int max, Func<ClientCard, int> score, int want = -1)
        {
            int n = want < 0 ? min : Math.Max(min, Math.Min(max, want));
            var chosen = cards.OrderBy(score).Take(n).ToList();
            return Util.CheckSelectCount(chosen, cards, min, max);
        }

        /// <summary>Wählt nach Prioritätsliste (Gruppen von IDs), Karten aus dem Deck bevorzugt.</summary>
        private IList<ClientCard> PickByOrder(IList<ClientCard> cards, int min, int max, params int[][] order)
        {
            var chosen = new List<ClientCard>();
            foreach (int[] group in order)
            {
                var match = cards.Where(c => Is(c, group) && !chosen.Contains(c))
                    .OrderBy(c => c.Location == CardLocation.Deck ? 0 : 1).FirstOrDefault();
                if (match != null)
                    chosen.Add(match);
                if (chosen.Count >= Math.Max(1, min))
                    break;
            }
            return Util.CheckSelectCount(chosen, cards, min, max);
        }

        private ClientCard BestEnemyMonster(IEnumerable<ClientCard> cards)
        {
            return cards.Where(c => c.Controller == 1)
                .OrderByDescending(c => (c.IsFacedown() ? 1500 : Math.Max(c.Attack, c.Defense)) + (c.IsExtraCard() ? 2000 : 0))
                .FirstOrDefault();
        }

        // --- Grundverhalten ---------------------------------------------------------------

        public override bool OnSelectHand()
        {
            return false; // Sun God ist ein Zweitspieler-Deck
        }

        // Training 2026-10-10: Infinite Impermanence negiert bis Zugende alle anderen Zauber/Fallen ihrer Spalte – auch
        // nachdem sie im Friedhof liegt. Lunalight legte Super Duper Poly und Poly genau dorthin (beide verpufft).
        private readonly HashSet<int> _impColumns = new HashSet<int>();   // eigene Zauberzonen-Indizes

        private void TrackImpermanence(ClientCard card)
        {
            try
            {
                if (card != null && card.Id == 10045474 && card.Location == CardLocation.SpellZone && card.Sequence < 5)
                    _impColumns.Add(card.Controller == 0 ? card.Sequence : 4 - card.Sequence);
            }
            catch (Exception) { }
        }

        public override void OnNewTurn()
        {
            _impColumns.Clear();
            _chantApplied = false;
            _slimeUsedThisTurn = false;
            _immortalRetrievedThisTurn = false;
            _leadingDownActivatedThisTurn = false;
            _helpoemerSummonedThisTurn = false;
            _gilGarthSummonedThisTurn = false;
            _immortalActivatedThisTurn = false;
            _immortalInGraveAtTurnStart = Bot.Graveyard.Count(c => Is(c, ImmortalSunGod));
            _darknessZones.Clear();
            _darknessFree.Clear();
            _darknessDiscards = 1;
            _darknessOtk = false;
            HookMultiZone();
            base.OnNewTurn();
        }

        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            int strongest = StrongestEnemyAttack;
            if (SunGodDarkness.Contains(cardId))
            {
                // Eigener Zug: Angriffsposition für den OTK (Keto).
                // Gegnerzug: Verteidigung. Sein Effekt bei Angriffen gibt ATK UND DEF, er schützt also auch dort;
                // ohne Zahlen nimmt die KI so 0 Schaden (Replay 20:59: 14.000 Schaden in Angriffsposition = verloren).
                return Duel.Player == 0 ? CardPosition.FaceUpAttack : CardPosition.FaceUpDefence;
            }
            if (GuardianSlime.Contains(cardId) || Viser.Contains(cardId) || Sphere.Contains(cardId))
                return CardPosition.FaceUpDefence;
            // Gegnerzug oder wenig LP: verteidigen (Replay 20:33: Phoenix in Angriffsposition kostete das Spiel)
            if (Duel.Player == 1 || Bot.LifePoints <= 2000)
                return CardPosition.FaceUpDefence;
            NamedCard data = NamedCard.Get(cardId);
            if (Duel.Turn == 1 || (data != null && data.Attack <= strongest))
                return CardPosition.FaceUpDefence;
            return CardPosition.FaceUpAttack;
        }

        // --- Mehrere Zonen für Sun God of Darkness ---------------------------------------------
        // EDOPros WindBot meldet bei der Zonenwahl immer genau eine Zone. Wirft Darkness 2+ Karten ab, verlangt
        // das Spiel genauso viele Zonen; mit nur einer Antwort käme MSG_RETRY und die KI flöge aus dem Duell.
        // Darum ersetzt die KI zur Laufzeit (per Reflection, ohne EDOPro-Dateien zu ändern) die Behandlung von
        // SelectPlace/SelectDisfield durch eine Version, die mehrere Zonen meldet. Klappt das nicht, bleibt es bei 1 Karte.
        private void HookMultiZone()
        {
            if (_hookTried) return;
            _hookTried = true;
            try
            {
                const BindingFlags bf = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                object client = null;
                foreach (FieldInfo f in typeof(GameAI).GetFields(bf))
                {
                    Delegate d = f.GetValue(AI) as Delegate;
                    if (d != null && d.Target != null && d.Target.GetType().Name == "GameClient") { client = d.Target; break; }
                }
                if (client == null) return;
                FieldInfo behaviorField = client.GetType().GetField("_behavior", bf);
                object behavior = behaviorField != null ? behaviorField.GetValue(client) : null;
                if (behavior == null) return;
                Type bt = behavior.GetType();
                FieldInfo messagesField = bt.GetField("_messages", bf);
                var messages = messagesField != null ? messagesField.GetValue(behavior) as IDictionary<GameMessage, Action<BinaryReader>> : null;
                PropertyInfo connProp = bt.GetProperty("Connection", bf);
                object connection = connProp != null ? connProp.GetValue(behavior, null) : null;
                MethodInfo send = connection != null ? connection.GetType().GetMethod("Send", new[] { typeof(BinaryWriter) }) : null;
                MethodInfo getLocal = bt.GetMethod("GetLocalPlayer", bf);
                FieldInfo hintField = bt.GetField("_select_hint", bf);
                if (messages == null || send == null || getLocal == null) return;
                var originals = new Dictionary<GameMessage, Action<BinaryReader>>();
                foreach (GameMessage msg in new[] { GameMessage.SelectPlace, GameMessage.SelectDisfield })
                {
                    Action<BinaryReader> original;
                    if (!messages.TryGetValue(msg, out original)) return;
                    originals[msg] = original;
                }
                foreach (var kv in originals)
                {
                    Action<BinaryReader> original = kv.Value;
                    messages[kv.Key] = packet => OnSelectPlaceMulti(packet, original, behavior, connection, send, getLocal, hintField);
                }
                _multiZone = true;
            }
            catch (Exception)
            {
                _multiZone = false;
            }
        }

        private void OnSelectPlaceMulti(BinaryReader packet, Action<BinaryReader> original, object behavior, object connection,
                                        MethodInfo send, MethodInfo getLocal, FieldInfo hintField)
        {
            long start = packet.BaseStream.Position;
            packet.ReadByte(); // Spieler
            int count = packet.ReadByte();
            int field = ~packet.ReadInt32();
            packet.BaseStream.Position = start;
            if (count <= 1)
            {
                original(packet); // normaler Weg über OnSelectPlace
                return;
            }

            var picks = new List<int[]>(); // { Spieler (0 = KI, 1 = Gegner), Ort, Zone }
            int oppMain = (field >> 16) & 0x1f;
            if (oppMain != 0)
            {
                // Sun God of Darkness: geplante Zonen zuerst, dann die gefährlichsten Monster
                List<int> threat;
                try { threat = EnemyMainZonesByThreat(); }
                catch (Exception) { threat = new List<int>(); } // Feld noch nicht bekannt: nach Index
                foreach (int z in _darknessZones.Concat(threat).Concat(Enumerable.Range(0, 5)).Distinct())
                    if ((oppMain & (1 << z)) != 0 && picks.Count < count)
                        picks.Add(new[] { 1, (int)CardLocation.MonsterZone, z });
            }
            _darknessZones.Clear();
            // andere Karten mit Mehrfach-Zonenwahl: freie Zonen der Reihe nach
            var groups = new[]
            {
                new[] { 0, (int)CardLocation.MonsterZone, field & 0x7f },
                new[] { 0, (int)CardLocation.SpellZone, (field >> 8) & 0x1f },
                new[] { 1, (int)CardLocation.MonsterZone, (field >> 16) & 0x7f },
                new[] { 1, (int)CardLocation.SpellZone, (field >> 24) & 0x1f }
            };
            foreach (int[] g in groups)
                for (int z = 0; z < 7 && picks.Count < count; z++)
                    if ((g[2] & (1 << z)) != 0 && !picks.Any(x => x[0] == g[0] && x[1] == g[1] && x[2] == z))
                        picks.Add(new[] { g[0], g[1], z });
            if (picks.Count < count)
            {
                original(packet);
                return;
            }

            if (hintField != null) hintField.SetValue(behavior, 0L);
            var writer = new BinaryWriter(new MemoryStream());
            writer.Write((byte)1); // CtosMessage.Response
            foreach (int[] x in picks)
            {
                writer.Write((byte)(int)getLocal.Invoke(behavior, new object[] { x[0] }));
                writer.Write((byte)x[1]);
                writer.Write((byte)x[2]);
            }
            send.Invoke(connection, new object[] { writer });
        }

        public override void OnChaining(int player, ClientCard card)
        {
#if !OHNE_HANDKETTE
            if (Duel.CurrentChain.Count == 0) _handGlied.Clear();
            if (player == 0 && card != null)
            {
#if !OHNE_LOG
                if (_aktivId != 0 && !Is(card, new[] { _aktivId }))
                    Log($"  Kettenglied falsch: zeigt {Name(card)}@{card.Location}, aktiviert {Name(new ClientCard(_aktivId, CardLocation.Hand, -1, 0))}");
#endif
                // Jedes eigene Glied merken: das Kettenobjekt kann während der Kette eine andere Identität bekommen
                // (Gegner kettet Mudora, Gil Garth geht zurück ins Deck → WindBot vergibt die Deck-ID neu, „Kette: Viser“).
                _handGlied[Duel.CurrentChain.Count] = _aktivId != 0 ? _aktivId : card.Id;
                _aktivId = 0;
            }
#endif
            TrackImpermanence(card);
            base.OnChaining(player, card);
        }

        public override int OnSelectPlace(long cardId, int player, CardLocation location, int available)
        {
            // Eigene Zauber/Fallen: Spalten meiden, in denen der Gegner verdeckte Karten hat. Eine gesetzte
            // Infinite Impermanence annulliert alle anderen Zauber/Fallen ihrer Spalte (so verlor Keto Ultimate Fusion).
            if (player == 0 && location == CardLocation.SpellZone)
            {
                int best = -1, bestScore = int.MaxValue;
                foreach (int i in new[] { 2, 1, 3, 0, 4 })
                {
                    if ((available & (1 << i)) == 0) continue;
                    ClientCard across = Enemy.SpellZone[4 - i];
                    int score = (across == null ? 0 : (across.IsFacedown() ? 10 : (Is(across, Impermanence) ? 10 : 1))) + (_impColumns.Contains(i) ? 50 : 0);
                    if (score < bestScore) { best = i; bestScore = score; }
                }
                if (best >= 0)
                    return 1 << best;
            }
            // Sun God of Darkness: gegnerische Zone mit dem stärksten Monster wählen
            if (player == 1 && location == CardLocation.MonsterZone)
            {
                int planned = _darknessZones.Count > 0 ? _darknessZones[0] : -1;
                _darknessZones.Clear();
                if (planned >= 0 && (available & (1 << planned)) != 0 && Enemy.MonsterZone[planned] != null)
                    return 1 << planned;
                int best = -1, bestPower = -1;
                for (int i = 0; i < 5; i++)
                {
                    ClientCard m = Enemy.MonsterZone[i];
                    if (m == null || (available & (1 << i)) == 0) continue;
                    int power = ThreatPower(m);
                    if (power > bestPower) { best = i; bestPower = power; }
                }
                if (best >= 0)
                    return 1 << best;
            }
            return base.OnSelectPlace(cardId, player, location, available);
        }

        public override int OnSelectOption(IList<long> options)
        {
            // Triple Tactics Talent: 2 Karten ziehen
            int draw = options.IndexOf(Util.GetStringId(TripleTactics[0], 0));
            if (draw >= 0 && Is(Card, TripleTactics))
                return draw;
            return base.OnSelectOption(options);
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, long hint, bool cancelable)
        {
            try
            {
#if !OHNE_LOG
                IList<ClientCard> result = SelectCardInner(cards, min, max, hint, cancelable);
                if (result != null)
                    Log($"  Auswahl {HintText(hint)} ({min}-{max}): {string.Join(", ", result.Select(c => Name(c) + (c.Controller == 1 ? "/G" : "")))}");
                else
                    Log($"  Auswahl {HintText(hint)} ({min}-{max}) STANDARD aus: {string.Join(", ", cards.Select(c => $"{Name(c)}@{c.Location}{(c.Controller == 1 ? "/G" : "")}"))}  [Kette: {KetteText()}]");
                return result;
#else
                return SelectCardInner(cards, min, max, hint, cancelable);
#endif
            }
            finally
            {
                // Nach dem Mischen stimmen die gemerkten IDs der Deck-Positionen nicht mehr. Zurücksetzen, damit die
                // nächste Auswahl die echten IDs aus der Server-Nachricht übernimmt (sonst wählt die KI „Ra“ und bekommt Sphere Mode).
                foreach (ClientCard c in Bot.Deck)
                    if (c != null && c.Id != 0)
                        c.SetId(0);
            }
        }

        private IList<ClientCard> SelectCardInner(IList<ClientCard> cards, int min, int max, long hint, bool cancelable)
        {
            // Achtung: EDOPro meldet eine Aktivierung schon VOR dem Bezahlen der Kosten. Kosten und Ziele werden
            // deshalb an Karte + Hinweis + Ort der angebotenen Karten erkannt, nicht daran, ob die Karte schon in der Kette steht.
            ClientCard source = ResolvingSource(hint) ?? Card;
            bool allInGrave = cards.Count > 0 && cards.All(c => c.Location == CardLocation.Grave);

#if !OHNE_VISERZIEL
            // 2026-10-17: Viser Des Shock gibt „1 andere Karte auf dem Feld“ zurück – die Liste enthält Karten BEIDER Seiten.
            // Viser wird aus der Hand beschworen (Kettenglied zeigt eine falsche Handkarte, s. _handGlied) → nicht an der Kette,
            // sondern am Auswahl-Hinweis erkennen: Visers eigener Text (Stringid(id, 2)).
            if (hint >= (1L << 20) && Viser.Contains((int)(hint >> 20)))
            {
                if (cards.Any(c => c.Controller == 1))
                    return ViserZiel(cards, min, max);
                // Gegner ohne Karten auf dem Feld: Viser MUSS eine eigene zurückgeben (A/B-Log 17.10.: 14 von 88, Standardwahl =
                // erste eigene Karte, oft Sun God of Darkness ins Extra Deck) → die billigste.
                return Util.CheckSelectCount(new List<ClientCard> { cards.OrderBy(ViserEigenScore).First() }, cards, min, max);
            }
#endif
#if !OHNE_HANDLIMIT
            // 2026-10-17 (A/B-Logs fs-*): Abwurf über das Handlimit am Zugende war ungesteuert (Standard = erste Handkarten, auch
            // Helpoemer/Leading Down). Ohne offene Kette ist es kein Karteneffekt (Kosten/Effekte stehen schon in der Kette).
            if (hint == HintMsg.Discard && Duel.Phase == DuelPhase.End && Duel.CurrentChain.Count == 0
                && cards.All(c => c.Location == CardLocation.Hand))
                return PickByScore(cards, min, max, DiscardScore);
#endif

            if (Card != null)
            {
                if (Is(Card, Helpoemer) && hint == HintMsg.Discard)
                    return PickByScore(cards, min, max, DiscardScore);
                if (Is(Card, SunGodDarkness) && hint == HintMsg.Discard)
                {
                    // Ohne aktiven Mehrfach-Hook immer nur 1 Karte, sonst fliegt die KI mit MSG_RETRY raus
                    int want = _multiZone ? Math.Max(1, _darknessDiscards) : 1;
                    return PickByScore(cards, min, Math.Min(max, want),
                        _darknessOtk ? (Func<ClientCard, int>)OtkDiscardScore : DarknessDiscardScore, want);
                }
                if (Is(Card, ImmortalSunGod) && hint == HintMsg.Discard)
                    return PickByScore(cards, min, max, DiscardScore, 1);
#if !OHNE_LDKOSTEN
                // 2026-10-17 (A/B-Logs): Leading Down darf auch einen offenen Ra auf dem Feld schicken – steht Sun God of Darkness,
                // ist er mit in der Liste, „alle sind Ra/Phoenix/Sphere“ griff nicht und die Standardwahl schickte die erste Karte
                // (meist Sphere Mode aus dem Deck statt Ra → Immortal Sun God holte danach keinen Darkness). 13× in 355 Duellen.
                if (hint == HintMsg.ToGrave && cards.Count > 0 && cards.All(c => Is(c, Ra) || Is(c, Phoenix) || Is(c, Sphere) || Is(c, SunGodDarkness)))
                    return PickByScore(cards, min, max, c => Is(c, SunGodDarkness) ? 99 : LeadingDownCostScore(c));
#endif
                if (hint == HintMsg.ToGrave && cards.Count > 0 && cards.All(c => Is(c, Ra) || Is(c, Phoenix) || Is(c, Sphere)))
                    return PickByScore(cards, min, max, LeadingDownCostScore);
                if (Is(Card, LavaGolem))
                    return LavaGolemMaterials(cards, min, max);
                if (Is(Card, Sphere) && hint == HintMsg.Release)
                    return PickByScore(cards, min, max, c => c.Controller == 1 ? -Math.Max(c.Attack, c.Defense) : 10000);
                if (Is(Card, Ra) && hint == HintMsg.Release)
                    return PickByScore(cards, min, max, c => Is(c, EgyptianGodSlime) ? -1 : (c.Controller == 0 ? Math.Max(0, c.Attack) : 10000));
                if (Is(Card, Domination) && hint == HintMsg.Release)
                    return PickByScore(cards, min, max, c => -Math.Max(0, c.Attack));
                if ((Is(Card, Dangers) || Is(Card, EgyptianGodSlime)) && hint == HintMsg.Release)
                    return PickByScore(cards, min, max, c => Is(c, GuardianSlime) ? -1 : (Is(c, SunGodDarkness) || Is(c, Phoenix) ? 9999 : Math.Max(0, c.Attack)));
                if (Is(Card, GilGarth) && allInGrave)
                    return GilGarthTarget(cards, min, max);
                if (Is(Card, WeeWitch) && hint == HintMsg.LinkMaterial)
                    return PickByScore(cards, min, max, WeeWitchMaterialScore, 2);
                // Keto linkt Rabbit aus Helpoemer + Egyptian God Slime (die Falle hat ihre Suche schon gemacht)
                if (Is(Card, Rabbit) && hint == HintMsg.LinkMaterial)
                    return PickByScore(cards, min, max, c => Is(c, SunGodDarkness) ? 9999 : (IsSunGod(c) ? 0 : 10) + Math.Max(0, c.Attack) / 1000);
                if (Is(Card, Rabbit) && cards.All(c => c.Controller == 1 && c.Location == CardLocation.SpellZone))
                {
                    ClientCard target = cards.OrderBy(c => Duel.CurrentChain.Contains(c) ? -1 : (c.IsFacedown() ? 0 : 1)).FirstOrDefault();
                    if (target != null)
                        return Util.CheckSelectCount(new List<ClientCard> { target }, cards, min, max);
                }
                if ((Is(Card, Ra) || Is(Card, Phoenix) || Is(Card, Varudras)) && cards.Any(c => c.Controller == 1) && !allInGrave)
                {
                    ClientCard target = BestEnemyMonster(cards) ?? cards.FirstOrDefault(c => c.Controller == 1);
                    if (target != null)
                        return Util.CheckSelectCount(new List<ClientCard> { target }, cards, min, max);
                }
            }

            if (source == null)
                return base.OnSelectCard(cards, min, max, hint, cancelable);

            // Effekte während der Auflösung
            if (Is(source, Helpoemer) && hint == HintMsg.AddToHand)
                return PickByOrder(cards, min, max, HelpoemerSearchOrder());
            if (Is(source, LeadingDown) && hint == HintMsg.AddToHand)
                return PickByOrder(cards, min, max, LeadingDownSearchOrder());
            if (Is(source, GodSlimeTrap) && hint == HintMsg.AddToHand)
                return PickByOrder(cards, min, max, Duel.Player == 1 ? new[] { Helpoemer, Makyura, GilGarth, Viser } : SlimeSearchOrder());
            if (Is(source, GuardianSlime) && hint == HintMsg.AddToHand)
                return PickByOrder(cards, min, max, GuardianSearchOrder());
            if (Is(source, TrueSunGod) && hint == HintMsg.AddToHand)
                return PickByOrder(cards, min, max, TrueSunGodSearchOrder());
            if (Is(source, AncientChant))
                return PickByOrder(cards, min, max, Ra);
            if (Is(source, Reinforcement))
                return PickByOrder(cards, min, max, Makyura);
            if (Is(source, FoolishBurialGoods))
                return PickByOrder(cards, min, max, ImmortalSunGod, Domination, LeadingDown);
            if (Is(source, Domination))
            {
                // aus meinem Friedhof Helpoemer (sucht erneut) oder das stärkste Monster, aus dem gegnerischen das stärkste
                ClientCard pick = cards.FirstOrDefault(c => c.Controller == 0 && Is(c, Helpoemer))
                    ?? cards.OrderByDescending(c => Math.Max(0, c.Attack) + (c.IsExtraCard() ? 500 : 0)).FirstOrDefault();
                if (pick != null)
                    return Util.CheckSelectCount(new List<ClientCard> { pick }, cards, min, max);
            }
            if (Is(source, Makyura) && NeedRa && LeadingDownUsableNow && !Bot.HasInHand(LeadingDown) && !LeadingDownActive)
                return PickByOrder(cards, min, max, LeadingDown, GodSlimeTrap, ImmortalSunGod, Domination); // Patch 13: erst Ra in den Friedhof
            if (Is(source, Makyura))
                return PickByOrder(cards, min, max, (!_slimeUsedThisTurn || Duel.Player == 1 || HasImmortalReady)
                    ? new[] { GodSlimeTrap, ImmortalSunGod, Domination, LeadingDown }
                    : new[] { ImmortalSunGod, GodSlimeTrap, Domination, LeadingDown });
            if (Is(source, Rabbit))
                return PickByOrder(cards, min, max, GodSlimeTrap);
            if (Is(source, GilGarth))
            {
                if (Bot.GetMonsters().Any(m => Is(m, SunGodDarkness)) && Duel.Player == 0)
                    return PickByOrder(cards, min, max, Helpoemer, GilGarth, Makyura, Viser); // OTK: möglichst viel ATK in den Friedhof
                if (WantImmortalMill)
                    return PickByOrder(cards, min, max, ImmortalSunGod, Viser, Makyura, Helpoemer); // per Abwurf zurückholen
                return PickByOrder(cards, min, max, Viser, Makyura, Helpoemer, ImmortalSunGod);
            }
            if (Is(source, ImmortalSunGod) || Is(source, Sphere) || Is(source, Dangers))
                return PickByOrder(cards, min, max, SunGodDarkness, Ra, Phoenix, Sphere);
            if (Is(source, TrueSunGod))
                return PickByOrder(cards, min, max, Phoenix, Ra);
            if (Is(source, Viser))
            {
                // Patch 13: Monster in der Extra-Monsterzone (z. B. Monk of the Tenyi) erreicht Darkness nicht → Viser
                // schickt es zurück ins Extra Deck. Gilt, sobald Darkness steht oder Immortal bereitliegt.
                ClientCard emz = null;
                if (Bot.GetMonsters().Any(m => Is(m, SunGodDarkness)) || HasImmortalReady)
                    emz = cards.Where(c => c.Controller == 1 && c.Location == CardLocation.MonsterZone && c.Sequence >= 5)
                        .OrderByDescending(c => Math.Max(0, c.Attack)).FirstOrDefault();
                ClientCard target = emz ?? BestEnemyMonster(cards) ?? cards.FirstOrDefault(c => c.Controller == 1);
                if (target != null)
                    return Util.CheckSelectCount(new List<ClientCard> { target }, cards, min, max);
            }

            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        /// <summary>
        /// Welche eigene Karte in der Kette diese Auswahl gerade verlangt (die EDOPro-Version des
        /// WindBot kennt keinen Zeiger auf das auflösende Kettenglied).
        /// </summary>
        private ClientCard ResolvingSource(long hint)
        {
            int[][] candidates;
            switch (hint)
            {
                case HintMsg.AddToHand:
                    candidates = new[] { Helpoemer, LeadingDown, GodSlimeTrap, GuardianSlime, TrueSunGod, AncientChant, Reinforcement };
                    break;
                case HintMsg.Set:
                    candidates = new[] { Makyura, Rabbit };
                    break;
                case HintMsg.ToGrave:
                    candidates = new[] { GilGarth, TrueSunGod, FoolishBurialGoods };
                    break;
                case HintMsg.SpSummon:
                    candidates = new[] { ImmortalSunGod, Sphere, Dangers, GodSlimeTrap, Domination };
                    break;
                default:
                    candidates = new[] { Viser, ImmortalSunGod, Dangers, Sphere, GilGarth };
                    break;
            }
            for (int i = Duel.CurrentChain.Count - 1; i >= 0; i--)
            {
                ClientCard link = Duel.CurrentChain[i];
#if !OHNE_HANDKETTE
                if (link != null && link.Controller == 0 && _handGlied.TryGetValue(i, out int echt))
                    link = new ClientCard(echt, CardLocation.Hand, -1, 0);   // Stellvertreter mit der echten Karte
#endif
                if (link != null && link.Controller == 0 && candidates.Any(ids => Is(link, ids)))
                    return link;
            }
            return null;
        }

#if !OHNE_VISERZIEL
        /// <summary>Viser: gegnerische Karte zurück (Extra-Monsterzone, wenn Darkness steht/kommt, sonst das stärkste Monster).</summary>
        private IList<ClientCard> ViserZiel(IList<ClientCard> cards, int min, int max)
        {
            ClientCard emz = null;
            if (Bot.GetMonsters().Any(m => Is(m, SunGodDarkness)) || HasImmortalReady)
                emz = cards.Where(c => c.Controller == 1 && c.Location == CardLocation.MonsterZone && c.Sequence >= 5)
                    .OrderByDescending(c => Math.Max(0, c.Attack)).FirstOrDefault();
            ClientCard target = emz ?? BestEnemyMonster(cards) ?? cards.First(c => c.Controller == 1);   // wie der Viser-Zweig unten
            return Util.CheckSelectCount(new List<ClientCard> { target }, cards, min, max);
        }

        /// <summary>
        /// Eigene Karte für Viser (niedrig = zuerst): offene, schon benutzte Leading Down/True Sun God (nächsten Zug neu
        /// aktivieren = neue Suche), dann Monster, die von der Hand wiederkommen; gesetzte Karten und Bosse zuletzt.
        /// </summary>
        private int ViserEigenScore(ClientCard c)
        {
            if (c.Location == CardLocation.SpellZone)
            {
                if (c.IsFaceup() && Is(c, LeadingDown)) return 0;
                if (c.IsFaceup() && Is(c, TrueSunGod)) return 1;
                return Is(c, ImmortalSunGod) ? 15 : 10;
            }
            if (Is(c, Helpoemer) || Is(c, Makyura) || Is(c, GilGarth)) return 4;
            if (Is(c, GuardianSlime)) return 5;
            if (Is(c, GodSlimeTrap) || Is(c, Sphere)) return 6;
            if (Is(c, Ra)) return 8;
            if (Is(c, Rabbit) || Is(c, WeeWitch)) return 9;
            if (Is(c, SunGodDarkness)) return 20;
            return 12;
        }

        /// <summary>
        /// Viser aus der Hand nur, wenn er eine gegnerische Karte zurückgeben kann – oder eine billige eigene (sonst ging
        /// z. B. Sun God of Darkness zurück ins Extra Deck). Rückholung aus dem Friedhof wie bisher.
        /// </summary>
        private bool ViserEffect()
        {
            if (Card.Location != CardLocation.Hand) return true;
            if (!(RoomForFusionAfterSummon() || Duel.Player == 1)) return false;
            if (Enemy.GetFieldCount() > 0) return true;
            var eigene = Bot.GetMonsters().Concat(Bot.GetSpells()).Where(c => c != null && c != Card).ToList();
            return eigene.Count > 0 && eigene.Min(ViserEigenScore) <= 1;
        }
#endif

        private bool LeadingDownActive => Bot.GetSpells().Any(s => Is(s, LeadingDown) && s.IsFaceup());

        /// <summary>Helpoemer: Immortal Sun God, sonst Gil Garth (Replay: Zug 1 Immortal, Zug 3 Gil Garth).</summary>
        private int[][] HelpoemerSearchOrder()
        {
            var order = new List<int[]>();
            // Ohne Ra im Friedhof bringt Immortal nichts: Leading Down legt Ra hinein und sucht noch ein Monster
            if (NeedRa && LeadingDownUsableNow && !Bot.HasInHand(LeadingDown))
                order.Add(LeadingDown);
            if (!HasImmortalReady)
                order.Add(ImmortalSunGod);
            if (!Bot.HasInHand(GilGarth))
                order.Add(GilGarth);
            if (!Bot.HasInHand(LeadingDown) && !LeadingDownActive)
                order.Add(LeadingDown);
            if (!Bot.HasInHand(Makyura))
                order.Add(Makyura);
            order.Add(GilGarth);
            order.Add(GodSlimeTrap);
            order.Add(Viser);
            order.Add(LeadingDown);
            order.Add(ImmortalSunGod);
            return order.ToArray();
        }

        /// <summary>Slime im eigenen Zug: Leading Down, sonst Helpoemer.</summary>
        private int[][] SlimeSearchOrder()
        {
            var order = new List<int[]>();
            if (!Bot.HasInHand(LeadingDown) && !LeadingDownActive)
                order.Add(LeadingDown);
            if (!Bot.HasInHand(Helpoemer))
                order.Add(Helpoemer);
            order.AddRange(HelpoemerSearchOrder());
            return order.ToArray();
        }

        /// <summary>Guardian Slime: True Sun God, dann Leading Down, dann Immortal Sun God.</summary>
        private int[][] GuardianSearchOrder()
        {
            var order = new List<int[]>();
            if (NeedRa && !Bot.HasInHand(LeadingDown) && (LeadingDownUsableNow || Duel.Player == 1))
                order.Add(LeadingDown);
            else if (!HasImmortalReady && !NeedRa)
                order.Add(ImmortalSunGod);
            if (!Bot.HasInHand(TrueSunGod) && !Bot.HasInSpellZone(TrueSunGod))
                order.Add(TrueSunGod);
            if (!Bot.HasInHand(LeadingDown) && !LeadingDownActive)
                order.Add(LeadingDown);
            order.AddRange(RaSpellOrder());
            return order.ToArray();
        }

        /// <summary>Leading Down: Helpoemer (Replay), sonst Gil Garth/Makyura.</summary>
        private int[][] LeadingDownSearchOrder()
        {
            var order = new List<int[]>();
            if (!Bot.HasInHand(Helpoemer) && !_helpoemerSummonedThisTurn)
                order.Add(Helpoemer);
            if (!Bot.HasInHand(GilGarth) && !_gilGarthSummonedThisTurn)
                order.Add(GilGarth);
            if (!Bot.HasInHand(Makyura))
                order.Add(Makyura);
            order.Add(Helpoemer); // Abwurf für Darkness: 2000 ATK mehr im Friedhof
            order.Add(Viser);
            order.Add(Makyura);
            order.Add(GilGarth);
            return order.ToArray();
        }

        /// <summary>
        /// True Sun God: fehlt Ra im Friedhof, zuerst Leading Down (Ra in den Friedhof + Monster suchen),
        /// sonst Immortal Sun God. Guardian Slime nur, wenn Helpoemer ihn heute abwerfen kann
        /// (Replay 21:24: Guardian Slime gesucht und nie benutzt, Fusion erst zwei Züge später).
        /// </summary>
        private int[][] TrueSunGodSearchOrder()
        {
            var order = new List<int[]>();
            if (NeedRa && LeadingDownUsableNow && !Bot.HasInHand(LeadingDown))
                order.Add(LeadingDown);
            if (!HasImmortalReady && !NeedRa)
                order.Add(ImmortalSunGod);
            if (!Bot.HasInHand(GuardianSlime) && Bot.HasInHand(Helpoemer) && !_helpoemerSummonedThisTurn)
                order.Add(GuardianSlime);
            if (NeedRa && !Bot.HasInHand(LeadingDown))
                order.Add(LeadingDown);
            order.AddRange(RaSpellOrder());
            order.Add(GuardianSlime);
            order.Add(Ra);
            return order.ToArray();
        }

        private int[][] RaSpellOrder()
        {
            var order = new List<int[]>();
            if (!HasImmortalReady)
                order.Add(ImmortalSunGod);
            if (Enemy.GetMonsterCount() >= 2)
                order.Add(Dangers);
            order.Add(LeadingDown);
            order.Add(ImmortalSunGod);
            order.Add(Dangers);
            order.Add(TrueSunGod);
            order.Add(AncientChant);
            return order.ToArray();
        }

        private bool IsGilGarthJunk(ClientCard c)
        {
            return Is(c, AncientChant) || Is(c, TripleTactics) || Is(c, CalledByTheGrave) || Is(c, Impermanence)
                || Is(c, DominusImpulse) || Is(c, Dangers) || Is(c, TrueSunGod) || Is(c, GuardianSlime)
                || (Is(c, LeadingDown) && Bot.Graveyard.Count(g => Is(g, LeadingDown)) > 0);
        }

        /// <summary>Immortal fehlt, Ra liegt schon im Friedhof: Gil Garth legt Immortal in den Friedhof, die KI holt ihn per Abwurf zurück.</summary>
        private bool WantImmortalMill => !HasImmortalReady && !Bot.Graveyard.Any(c => Is(c, ImmortalSunGod))
                                         && ImmortalSunGodInDeck() > 0 && (RaInGrave || PhoenixInGrave)
                                         && Bot.Hand.Any(c => IsSunGod(c) && !Is(c, Helpoemer));

        private bool HasGoodGilGarthTarget()
        {
            return Bot.Graveyard.Any(c => Is(c, SunGodDarkness)) || Enemy.Graveyard.Any() || Bot.Graveyard.Any(IsGilGarthJunk);
        }

        private IList<ClientCard> GilGarthTarget(IList<ClientCard> cards, int min, int max)
        {
            // Ketos Trick: den eigenen Sun God of Darkness aus dem Friedhof zurück ins Extra Deck holen
            ClientCard target = cards.FirstOrDefault(c => c.Controller == 0 && Is(c, SunGodDarkness))
                ?? cards.Where(c => c.Controller == 1).OrderByDescending(c => c.IsMonster() ? Math.Max(c.Attack, 1000) : 500).FirstOrDefault();
            if (target == null)
            {
                target = cards.FirstOrDefault(c => c.Controller == 0 && IsGilGarthJunk(c))
                    ?? cards.Where(c => c.Controller == 0 && !IsSunGod(c) && !Is(c, Ra) && !Is(c, Phoenix)).FirstOrDefault()
                    ?? cards.Where(c => c.Controller == 0 && !Is(c, Ra) && !Is(c, Phoenix) && !Is(c, ImmortalSunGod))
                            .OrderBy(c => c.IsMonster() ? Math.Max(0, c.Attack) : 0).FirstOrDefault();
            }
            return target != null
                ? Util.CheckSelectCount(new List<ClientCard> { target }, cards, min, max)
                : base.OnSelectCard(cards, min, max, 0, false);
        }

        private IList<ClientCard> LavaGolemMaterials(IList<ClientCard> cards, int min, int max)
        {
            var chosen = new List<ClientCard>();
            var mine = cards.Where(c => c.Controller == 0 && IsSunGod(c)).OrderBy(c => c.Attack).FirstOrDefault();
            var theirs = cards.Where(c => c.Controller == 1).OrderByDescending(c => Math.Max(c.Attack, c.Defense)).ToList();
            if (theirs.Count > 0)
                chosen.AddRange(theirs.Take(Math.Max(1, max)));
            else if (mine != null)
                chosen.Add(mine);
            return Util.CheckSelectCount(chosen.Take(max).ToList(), cards, min, max);
        }

        // --- Kampf ------------------------------------------------------------------------

        private int DarknessPotential => Bot.LifePoints - 1 + SunGodAttackInGrave();

        /// <summary>Sun God Domination im Friedhof: Darkness nach dem Angriff tributieren und die gezahlten LP zurückholen.</summary>
        private bool DominationRefundReady => Duel.Player == 0 && Bot.LifePoints > 1 && Bot.Graveyard.Any(c => Is(c, Domination));

        /// <summary>Größter Schaden, den Darkness mit „LP auf 1“ anrichten kann (direkt oder gegen das schwächste Angriffsmonster).</summary>
        private int BestDarknessHit(IList<ClientCard> defenders, int potential)
        {
            if (defenders.Count == 0) return potential;
            return defenders.Where(d => d.IsFaceup() && d.IsAttack()).Select(d => potential - d.Attack).DefaultIfEmpty(0).Max();
        }

        public override BattlePhaseAction OnSelectAttackTarget(ClientCard attacker, IList<ClientCard> defenders)
        {
            if (Is(attacker, SunGodDarkness))
            {
                // Training 2026-10-10: Regel „Crusadia Avramax nie angreifen, Darkness wartet“ getestet und
                // wieder entfernt – Darkness blieb dann ganz stehen (vs Kashtira 60 % → 50 %, vs Swordsoul 75 % → 58 %).
                // Nur angreifen, wenn „LP auf 1 zahlen“ den Sieg bringt
                foreach (ClientCard defender in defenders)
                    if (defender.IsAttack() && DarknessPotential - defender.Attack >= Enemy.LifePoints)
                        return AI.Attack(attacker, defender);
                if (attacker.CanDirectAttack && DarknessPotential >= Enemy.LifePoints)
                    return AI.Attack(attacker, null);
                // Keto (Replay 20:48): auch ohne Sieg zuschlagen, wenn Sun God Domination im Friedhof liegt –
                // Darkness danach tributieren, LP in Höhe seiner ATK zurück (Gegner 8000 → 1, Keto 1 → 8000)
                if (DominationRefundReady)
                {
                    if (attacker.CanDirectAttack && DarknessPotential >= Enemy.LifePoints / 2)
                        return AI.Attack(attacker, null);
                    ClientCard weakest = defenders.Where(d => d.IsFaceup() && d.IsAttack() && DarknessPotential - d.Attack >= Enemy.LifePoints / 2)
                        .OrderBy(d => d.Attack).FirstOrDefault();
                    if (weakest != null)
                        return AI.Attack(attacker, weakest);
                }
                return null;
            }
            return base.OnSelectAttackTarget(attacker, defenders);
        }

        // --- Effekte ----------------------------------------------------------------------

        private bool DominusImpulseEffect()
        {
            return Duel.LastChainPlayer == 1;
        }

        private bool VarudrasEffect()
        {
            return Duel.LastChainPlayer == 1 || Duel.Player == 0;
        }

        private bool SunGodDarknessEffect()
        {
            if (DescIndex == 1 || !MainPhase) // Angriffsansage: LP auf 1 zahlen (Beschreibung fehlt oft, daher Phase prüfen)
            {
                if (Duel.Player != 0)
                {
                    // Gegner greift Darkness an: zahlen, wenn er den Angreifer schlägt und der Schaden sonst wehtut
                    ClientCard attacker = Enemy.BattlingMonster;
                    if (attacker == null || Bot.LifePoints <= 1 || DarknessPotential <= attacker.Attack) return false;
                    ClientCard self = Bot.BattlingMonster;
                    if (self != null && self.IsDefense())
                        return DarknessPotential - attacker.Attack >= Enemy.LifePoints; // Rückschaden gewinnt das Spiel
                    return attacker.Attack >= Bot.LifePoints || attacker.Attack >= 2500 || Bot.LifePoints <= 4000;
                }
                ClientCard defender = Enemy.BattlingMonster;
                int need = DominationRefundReady ? Enemy.LifePoints / 2 : Enemy.LifePoints;
                if (defender == null) return DarknessPotential >= need;
                return defender.IsAttack() && DarknessPotential - defender.Attack >= need;
            }
            // Quick Effect: Sun-God-Karten abwerfen, genauso viele gegnerische Main-Zonen leeren
            _darknessZones.Clear();
            _darknessDiscards = 1;
            _darknessOtk = false;
            var targets = Enemy.GetMonstersInMainZone();
            if (!targets.Any() || !MainPhase) return false;
            // Immortal Sun God ist die Motorkarte und wird nur im eigenen Zug für einen tödlichen Angriff geopfert
            bool fodder = Bot.Hand.Any(c => IsSunGod(c) && !Is(c, ImmortalSunGod));
            if (Duel.Player == 0)
                return DarknessOwnTurnQuick(targets, fodder);
            if (!fodder) return false;
            // Gegnerzug (Keto): nicht sofort, sondern gezielt
            bool beforeBattleOrEnd = Duel.MainPhaseEnd;                       // Gegner verlässt die Main Phase
            bool dangerous = targets.Any(m => m.IsFaceup() && (m.IsMonsterDangerous() || m.IsFloodgate()));
            bool bigBoard = targets.Count >= 4 && targets.Count(m => m.IsExtraCard()) >= 2; // sonst Combo-Zwischenstand
            // Gegner aktiviert gerade den Effekt eines Extra-Deck- oder gefährlichen Monsters → entfernen, bevor er auflöst
            bool responding = Duel.LastChainPlayer == 1
                              && targets.Any(m => Duel.CurrentChain.Contains(m) && (m.IsExtraCard() || IsDangerousMonster(m)));
            bool lethalThreat = Enemy.GetMonsters().Where(m => m.IsFaceup() && m.IsAttack()).Sum(m => Math.Max(0, m.Attack)) >= Bot.LifePoints
                                && Duel.Phase == DuelPhase.Main1 && Duel.MainPhaseEnd;
            if (!(beforeBattleOrEnd || dangerous || bigBoard || lethalThreat || responding)) return false;
            // Patch 13: Liegt noch ein Empfänger mit Material auf dem Feld, kommt gleich eine Synchro-Beschwörung
            // (Swordsoul: Spielmarke + Longyuan → Chengying). Erst danach abwerfen, dann trifft Darkness die Bosse.
            bool synchroPending = !beforeBattleOrEnd && !lethalThreat && !dangerous
                                  && targets.Any(m => m.IsFaceup() && m.HasType(CardType.Tuner))
                                  && targets.Any(m => m.IsFaceup() && !m.HasType(CardType.Tuner) && !m.IsExtraCard());
            if (synchroPending) return false;
            PlanDarknessOpponentTurn(lethalThreat);
            return true;
        }

        /// <summary>
        /// Gegnerzug: mehrere Zonen auf einmal leeren (Ketos Linie: „2 Karten abwerfen → 2 Zonen leer“).
        /// Die erste Zone kostet irgendeine Sun-God-Karte außer Immortal, jede weitere nur billige Karten
        /// (Helpoemer/Gil Garth kommen nach Immortal in der End Phase zurück) – in Not auch teurere.
        /// </summary>
        private void PlanDarknessOpponentTurn(bool emergency)
        {
            var zones = EnemyMainZonesByThreat();
            _darknessFree.Clear();
            var fodderList = Bot.Hand.Where(c => IsSunGod(c) && !Is(c, ImmortalSunGod)).OrderBy(DarknessDiscardScore).ToList();
            // Nach Immortal kommen Helpoemer, Gil Garth, Makyura und Viser in der End Phase zurück – je Name 1 Karte.
            // Diese Abwürfe kosten nichts, also auch Verteidiger entfernen (Replay 19:58: nur 1 Karte abgeworfen,
            // Gil Garth behalten → Gegner baute aus den übrigen Stufe-10-Monstern Gustav Max und Gustav Rocket).
            if (ImmortalSentThisTurn)
                foreach (var g in fodderList.Where(ReturnsInEndPhase).GroupBy(c => c.Id))
                    _darknessFree.Add(g.First());
            int n = 1;
            if (_multiZone)
            {
                int limit = emergency ? 13 : 4;
                int worth = zones.Count(i => WorthRemoving(Enemy.MonsterZone[i]));
                int free = _darknessFree.Count;
                int cheap = fodderList.Count(c => !_darknessFree.Contains(c) && DarknessDiscardScore(c) <= limit);
                n = Math.Min(zones.Count, Math.Max(1, free + Math.Min(Math.Max(0, worth - free), cheap)));
            }
            _darknessDiscards = n;
            _darknessZones = zones.Take(n).ToList();
        }

        /// <summary>Diese Monster kommen in der End Phase zurück auf die Hand, wenn Immortal diesen Zug in den Friedhof ging.</summary>
        private bool ReturnsInEndPhase(ClientCard c)
        {
            return Is(c, Helpoemer) || Is(c, GilGarth) || Is(c, Makyura) || Is(c, Viser);
        }

        private static bool IsDangerousMonster(ClientCard m)
        {
            return m != null && m.IsFaceup() && (m.IsMonsterDangerous() || m.IsFloodgate());
        }

        /// <summary>Lohnt es, dieses Monster mit Darkness zu entfernen?</summary>
        private static bool WorthRemoving(ClientCard m)
        {
            if (m == null) return false;
            return IsDangerousMonster(m) || m.IsExtraCard() || (m.IsFaceup() && m.IsAttack() && m.Attack >= 1500);
        }

        private static int ThreatPower(ClientCard m)
        {
            return (m.IsFacedown() ? 1500 : Math.Max(m.Attack, m.Defense))
                   + (IsDangerousMonster(m) ? 5000 : 0)
                   + (m.IsAttack() ? 500 : 0)
                   + (m.IsExtraCard() ? 1000 : 0);
        }

        /// <summary>Besetzte gegnerische Main-Zonen (0–4), gefährlichste zuerst.</summary>
        private List<int> EnemyMainZonesByThreat()
        {
            var mons = Enemy.GetMonsters();
            return Enumerable.Range(0, 5).Where(i => Enemy.MonsterZone[i] != null)
                .OrderByDescending(i => ThreatPower(Enemy.MonsterZone[i]) + (IsXyzPair(Enemy.MonsterZone[i], mons) ? 1500 : 0)
                                        + (Duel.CurrentChain.Contains(Enemy.MonsterZone[i]) ? 20000 : 0)).ToList();
        }

        /// <summary>Gegnerzug: ein weiteres Monster gleicher Stufe auf dem Feld = mögliches Xyz-Material.</summary>
        private bool IsXyzPair(ClientCard m, IList<ClientCard> all)
        {
            if (Duel.Player != 1 || m.IsFacedown() || m.IsExtraCard() || m.Level <= 0) return false;
            return all.Any(o => o != m && o.IsFaceup() && !o.IsExtraCard() && o.Level == m.Level);
        }

        private static int AtkOf(ClientCard c)
        {
            return c.IsMonster() ? Math.Max(0, c.Attack) : 0;
        }

        /// <summary>Abwurf für den OTK: Monster mit viel ATK zuerst (zählen im Friedhof), Immortal zuletzt.</summary>
        private int OtkDiscardScore(ClientCard c)
        {
            return c.IsMonster() ? -Math.Max(0, c.Attack) : (Is(c, ImmortalSunGod) ? 200 : 100);
        }

        /// <summary>
        /// Eigener Zug (Keto: nicht sofort, sondern gezielt). Main Phase 1: nur, wenn das Leeren einer Zone
        /// den tödlichen Angriff erst möglich macht, oder gegen ein gefährliches Monster (Replay 21:24: eine
        /// Verteidigungs-Karte entfernt, obwohl der Angriff ohnehin gewann). Main Phase 2: Darkness geht in der
        /// End Phase in den Friedhof, also den stärksten Angreifer vor dem Gegnerzug entfernen – mit billigem Abwurf.
        /// </summary>
        private bool DarknessOwnTurnQuick(IList<ClientCard> targets, bool fodder)
        {
            var sunGodHand = Bot.Hand.Where(IsSunGod).OrderBy(OtkDiscardScore).ToList();
            if (!sunGodHand.Any()) return false;
            var nonImmortal = Bot.Hand.Where(c => IsSunGod(c) && !Is(c, ImmortalSunGod)).ToList();
            if (Duel.Phase == DuelPhase.Main1)
            {
                var all = Enemy.GetMonsters();
                if (CanWinByAttack(all, DarknessPotential)) return false; // gewinnt auch ohne Effekt
                // a) Eine Zone leeren reicht: der Abwurf (stärkstes Sun-God-Monster) zählt zusätzlich im Friedhof
                int bonus1 = AtkOf(sunGodHand[0]);
                for (int i = 0; i < 5; i++)
                {
                    ClientCard m = Enemy.MonsterZone[i];
                    if (m == null) continue;
                    if (CanWinByAttack(all.Where(x => x != m).ToList(), DarknessPotential + bonus1))
                    {
                        _darknessZones = new List<int> { i }; // Immortal darf hier abgeworfen werden: der Angriff gewinnt
                        _darknessOtk = true;
                        return true;
                    }
                }
                // b) Alle Main-Zonen leeren → direkter Angriff (nur ohne Monster in der Extra-Monsterzone)
                var mains = Enumerable.Range(0, 5).Where(i => Enemy.MonsterZone[i] != null).ToList();
                if (_multiZone && mains.Count >= 2 && mains.Count <= sunGodHand.Count && all.Count == mains.Count
                    && DarknessPotential + sunGodHand.Take(mains.Count).Sum(c => AtkOf(c)) >= Enemy.LifePoints)
                {
                    _darknessZones = mains;
                    _darknessDiscards = mains.Count;
                    _darknessOtk = true;
                    return true;
                }
                // d) Antwort auf einen gegnerischen Monstereffekt (Keto, Replay 20:41: zwei Gil Garth abgeworfen,
                //    Masquerena samt Link-Beschwörung weg). Im eigenen Zug zählen die Abwürfe danach im Friedhof für den Angriff.
                bool enemyActivating = Duel.LastChainPlayer == 1
                    && Enumerable.Range(0, 5).Any(i => Enemy.MonsterZone[i] != null && Duel.CurrentChain.Contains(Enemy.MonsterZone[i]));
                if (enemyActivating && nonImmortal.Count > 0)
                {
                    var zones = EnemyMainZonesByThreat();
                    _darknessDiscards = _multiZone ? Math.Min(zones.Count, nonImmortal.Count) : 1;
                    _darknessZones = zones.Take(_darknessDiscards).ToList();
                    _darknessOtk = true; // stärkste Monster zuerst abwerfen
                    return true;
                }
                // c) kein OTK: nur gefährliche Monster (Floodgates, Negierer) vor dem Kampf entfernen
                var danger = EnemyMainZonesByThreat().Where(i => IsDangerousMonster(Enemy.MonsterZone[i])).ToList();
                if (!fodder || danger.Count == 0) return false;
                _darknessDiscards = _multiZone ? Math.Max(1, Math.Min(danger.Count, nonImmortal.Count)) : 1;
                _darknessZones = danger.Take(_darknessDiscards).ToList();
                return true;
            }
            // Main Phase 2: Darkness geht in der End Phase ohnehin in den Friedhof → Angreifer vor dem Gegnerzug entfernen
            var cheap = Bot.Hand.Where(c => IsSunGod(c) && DarknessDiscardScore(c) <= 4).ToList();
            var worthZones = EnemyMainZonesByThreat().Where(i => WorthRemoving(Enemy.MonsterZone[i])).ToList();
            if (cheap.Count == 0 || worthZones.Count == 0) return false;
            _darknessDiscards = _multiZone ? Math.Min(worthZones.Count, cheap.Count) : 1;
            _darknessZones = worthZones.Take(_darknessDiscards).ToList();
            return true;
        }

        private bool CanWinByAttack(IList<ClientCard> defenders, int potential)
        {
            if (defenders.Count == 0) return potential >= Enemy.LifePoints;
            return defenders.Any(d => d.IsFaceup() && d.IsAttack() && potential - d.Attack >= Enemy.LifePoints);
        }

        /// <summary>Abwurf für Darkness außerhalb des OTK: nie Immortal; Helpoemer/Gil Garth sind gratis, wenn Immortal heute in den Friedhof ging.</summary>
        private int DarknessDiscardScore(ClientCard c)
        {
            if (Is(c, ImmortalSunGod)) return 999;
            if (_darknessFree.Contains(c)) return -10;
            if (ImmortalSentThisTurn && ReturnsInEndPhase(c)) return -5; // kommen in der End Phase zurück
            return DiscardScore(c);
        }

        private bool ImmortalSunGodEffect()
        {
            if (Card.Location == CardLocation.Grave)
            {
                if (HasImmortalReady || !MainPhase) return false;
                var fodder = Bot.Hand.Where(c => IsSunGod(c) && !Is(c, Helpoemer)).OrderBy(DiscardScore).FirstOrDefault();
                if (fodder == null) return false;
                AI.SelectCard(fodder);
                _immortalRetrievedThisTurn = true;
                return true;
            }

            bool raReady = RaInGrave || Bot.Graveyard.Any(c => Is(c, Phoenix));
            if (!raReady || !MainPhase || FreeMainZones < 1) return false;
            bool canDiscard = Bot.Hand.Count(IsSunGod) >= (Card.Location == CardLocation.Hand ? 2 : 1);

            bool go = ImmortalActivationWanted(canDiscard);
            if (go) _immortalActivatedThisTurn = true;
            return go;
        }

        private bool ImmortalActivationWanted(bool canDiscard)
        {
            if (Duel.Player == 1)
            {
                // Keto (Replay 20:20): nicht beim ersten Gegnerzug-Effekt, sondern wenn ein Monster liegt, das Darkness
                // sofort per Abwurf entfernen kann. Ohne Abwurf ist Darkness nur eine 0-DEF-Wand (Replay 20:12: Immortal
                // auf die erste Aktion verbraucht, danach baute der Gegner ungestört Gustav und Juggernaut Liebe).
                bool fodder = Bot.Hand.Any(c => IsSunGod(c) && !Is(c, ImmortalSunGod));
                bool targeted = Duel.ChainTargets != null && Duel.ChainTargets.Contains(Card);
                // Replay 2026-10-05 21:50 (Rokket): Immortal schon auf Rokket Loader verbraucht, Darkness räumte mitten in der
                // Combo drei kleine Monster weg, die sofort wiederkamen. Darum warten, bis 2+ Monster oder ein Extra-Deck-Monster
                // in den Main-Zonen stehen, oder bis der Gegner die Main Phase verlässt.
                var mains = Enemy.GetMonstersInMainZone();
                bool worth = mains.Count >= 2 || mains.Any(m => m.IsExtraCard() || IsDangerousMonster(m)) || (Duel.MainPhaseEnd && mains.Any());
                return targeted || Bot.UnderAttack || (fodder && worth);
            }
            if (Duel.Phase != DuelPhase.Main1) return false;
            // Erster Zug (Keto): erst alle Monster als Link-Material verbrauchen, dann Immortal → in der End Phase
            // kommen Helpoemer, Makyura, Gil Garth und Viser zurück. Ohne Monster im Friedhof bringt das nichts.
            if (Duel.Turn == 1)
                return Bot.Hand.Any(c => IsSunGod(c) && c != Card) && !CanStillLinkRabbit() && !CanLinkWeeWitch()
                       && ReturnerNamesInGrave() >= 2;
            // Sun God of Darkness ist unbeeinflussbar → die Angriffssperre von True Sun God gilt für ihn nicht (Replay)
            // Replay 2026-10-05 19:58: Immortal zweimal gesetzt (Zug 3 und 7), obwohl Darkness sofort gewonnen hätte
            if (DarknessOtkAfterImmortal()) return true;
            // Going second (Keto, Replays 20:41 und 20:48): Immortal als Antwort, wenn der Gegner in meinem Zug
            // unterbricht (ABC-Dragon Buster, Chixiao). Darkness ist unbeeinflussbar und räumt danach die Main-Zonen;
            // jede abgeworfene Sun-God-Karte erhöht zusätzlich den Angriff.
            if (Duel.LastChainPlayer == 1 && Enemy.GetMonstersInMainZone().Any() && Bot.Hand.Any(c => IsSunGod(c) && c != Card))
                return true;
            // Sonst nur für die Rückholung von 2+ Monstern, und nur, wenn Immortal heute noch zurück auf die Hand kann
            return ReturnerNamesInGrave() >= 2 && !_immortalRetrievedThisTurn
                   && Bot.Hand.Any(c => IsSunGod(c) && c != Card);
        }

        /// <summary>
        /// Gewinnt Darkness diesen Zug, wenn Immortal jetzt aktiviert wird? „LP auf 1 zahlen“ + ATK der Sun-God-Monster
        /// im Friedhof, gegen ein Angriffsmonster oder direkt; dazu der Quick Effect, falls Sun-God-Karten zum Abwerfen da sind.
        /// </summary>
        private bool DarknessOtkAfterImmortal()
        {
            if (Duel.Turn <= 1 || Bot.LifePoints <= 1) return false;
            int pot = DarknessPotential;
            var all = Enemy.GetMonsters();
            if (CanWinByAttack(all, pot)) return true;
            if (DominationRefundReady && BestDarknessHit(all, pot) >= Enemy.LifePoints / 2) return true;
            var fodder = Bot.Hand.Where(c => IsSunGod(c) && !Is(c, ImmortalSunGod)).OrderBy(OtkDiscardScore).ToList();
            if (fodder.Count == 0) return false;
            var mains = Enemy.GetMonstersInMainZone();
            foreach (ClientCard m in mains)
                if (CanWinByAttack(all.Where(x => x != m).ToList(), pot + AtkOf(fodder[0])))
                    return true;
            return _multiZone && mains.Count <= fodder.Count && all.Count == mains.Count
                   && pot + fodder.Take(mains.Count).Sum(c => AtkOf(c)) >= Enemy.LifePoints;
        }

        /// <summary>
        /// Infinite Impermanence: nie Effekte annullieren, die dem Gegner selbst schaden (Replay 19:58, Zug 6:
        /// Lava Golem hätte dem Gegner 1000 Schaden gemacht – die KI hat das verhindert und die Falle verschwendet).
        /// </summary>
        private bool ImpermanenceEffect()
        {
            if (Duel.CurrentChain.Count > 0 && Is(Duel.CurrentChain[Duel.CurrentChain.Count - 1], LavaGolem))
                return false;
            if (Duel.CurrentChain.Count == 0 && Duel.Phase == DuelPhase.Standby && Enemy.GetMonsters().Any(m => Is(m, LavaGolem)))
                return false;
            return DefaultInfiniteImpermanence();
        }

        /// <summary>
        /// Sun God Domination: mit einem Ra-Monster auf dem Feld je 1 Monster aus beiden Friedhöfen holen
        /// (mit echter Ra zusätzlich alle gegnerischen Monster übernehmen). Aus dem Friedhof: ein Ra-Monster
        /// tributieren und LP in Höhe seiner ATK gewinnen – ideal in Main Phase 2 nach „LP auf 1“.
        /// </summary>
        private bool DominationEffect()
        {
            if (Card.Location == CardLocation.Grave)
            {
                var raMonster = Bot.GetMonsters().Where(m => (Is(m, SunGodDarkness) || Is(m, Ra) || Is(m, Phoenix)) && m.IsFaceup())
                    .OrderByDescending(m => m.Attack).FirstOrDefault();
                return raMonster != null && Duel.Player == 0 && Duel.Phase == DuelPhase.Main2
                    && Bot.LifePoints <= 4000 && raMonster.Attack >= 2000;
            }
            if (!MainPhase) return false;
            bool enemyGrave = Enemy.Graveyard.Any(c => c.IsMonster());
            return FreeMainZones >= 2 && (enemyGrave || Bot.Graveyard.Any(c => Is(c, Helpoemer)));
        }

        private bool PhoenixEffect()
        {
            if (DescIndex != 1) return true; // Pflichteffekte
            ClientCard target = BestEnemyMonster(Enemy.GetMonsters());
            if (target == null || Bot.LifePoints <= 2000) return false;
            bool worth = target.IsExtraCard() || target.IsFacedown() || target.Attack >= 2000 || target.Defense >= 2500;
            if (!worth) return false;
            AI.SelectCard(target);
            return true;
        }

        private bool RaEffect()
        {
            // EDOPro schickt Auslöse-Effekte oft ohne echte Beschreibung (221/0). Nur Index 1 ist sicher der
            // Zerstörungseffekt; alles andere ist der Auslöser „LP auf 100 zahlen“ (Replay 20:33: 4900 LP verschenkt).
            if (DescIndex != 1)
            {
                int afterPay = Math.Max(0, Card.Attack) + Bot.LifePoints - 100;
                return Duel.Player == 0 && Duel.Turn > 1 && Duel.Phase == DuelPhase.Main1 && Bot.LifePoints > 100
                    && Enemy.GetMonsterCount() == 0 && afterPay >= Enemy.LifePoints; // nur, wenn es diesen Zug gewinnt
            }
            // 1000 LP zahlen, 1 Monster zerstören
            ClientCard target = BestEnemyMonster(Enemy.GetMonsters());
            if (target == null || Bot.LifePoints <= 2000) return false;
            if (target.IsFaceup() && target.Attack < 1500 && Enemy.GetMonsterCount() > 1) return false;
            AI.SelectCard(target);
            return true;
        }

        private bool GodSlimeTrapEffect()
        {
            if (Card.Location == CardLocation.MonsterZone)
                return true; // Suche nach der Beschwörung
            bool go;
            bool rabbitReady = Bot.GetMonsters().Any(m => Is(m, Rabbit) && m.IsFaceup());
            if (Duel.Player == 1 && rabbitReady)
                // Keto (Replay 20:20): Slime als Antwort auf eine gegnerische Zauberkarte → Rabbit zerstört sie
                go = Duel.Phase == DuelPhase.End || Bot.UnderAttack || (MainPhase && Enemy.GetSpells().Any()
                     && (Duel.LastChainPlayer == 1 || Enemy.GetSpells().Any(c => c.IsFaceup())));
            else if (Duel.Player == 1)
                go = Duel.Phase == DuelPhase.End || Bot.UnderAttack || (MainPhase && (Duel.LastChainPlayer == 1 || Enemy.GetMonsterCount() >= 1));
            else
                go = Duel.Phase == DuelPhase.Main1 && RoomForFusionAfterSummon(); // eigener Zug: für die Suche, Platz lassen
            if (go)
                _slimeUsedThisTurn = true;
            return go;
        }

        private bool DangersEffect()
        {
            if (Card.Location == CardLocation.Grave) return true;
            if (!Bot.HasInHand(Ra) || Bot.LifePoints < 2000) return false;
            var tribute = Bot.GetMonsters().Where(m => !Is(m, SunGodDarkness) && !Is(m, Phoenix)).OrderBy(m => m.Attack).FirstOrDefault();
            if (tribute == null) return false;
            bool darknessUp = Bot.GetMonsters().Any(m => Is(m, SunGodDarkness));
            // Dangers kostet die halben LP → halber Darkness-Bonus. Mit Darkness nur, wenn der Angriff danach noch gewinnt.
            int potentialAfterDangers = Bot.LifePoints / 2 - 1 + SunGodAttackInGrave();
            bool otk = darknessUp && Duel.Turn > 1 && potentialAfterDangers >= Enemy.LifePoints;
            bool worth = Duel.Player == 0
                ? Duel.Phase == DuelPhase.Main1 && Enemy.GetMonsterCount() >= 1 && (otk || (!darknessUp && Enemy.GetMonsterCount() >= 2))
                : Enemy.GetMonsterCount() >= 3 && MainPhase;
            if (!worth) return false;
            AI.SelectCard(tribute);
            return true;
        }

        private bool SphereSummon()
        {
            return Enemy.GetMonsterCount() >= 3;
        }

        private bool HelpoemerEffect()
        {
            if (Card.Location == CardLocation.Hand)
            {
                // Abwerfen und Spezialbeschwören – braucht eine andere Karte als Kosten und lässt Platz für die Fusion
                bool ok = Bot.Hand.Count(c => c != Card) >= 1 && RoomForFusionAfterSummon();
                if (ok) _helpoemerSummonedThisTurn = true;
                return ok;
            }
            return true; // Suche nach Spezialbeschwörung bzw. Rückholung in der End Phase
        }

        private bool LeadingDownEffect()
        {
            if (Card.Location == CardLocation.SpellZone && Card.IsFaceup())
                return true; // Suche bzw. Kampfschaden
            // Aktivierung: jede Kopie lohnt sich (Ra/Phoenix in den Friedhof + Monster suchen), einmal pro Zug
            if (_leadingDownActivatedThisTurn || Duel.Player != 0 || !MainPhase) return false;
            _leadingDownActivatedThisTurn = true;
            return true;
        }

        /// <summary>Kosten für Leading Down: ohne Ra im Friedhof Ra (aus dem Deck), sonst Phoenix/Sphere aus der Hand loswerden.</summary>
        private int LeadingDownCostScore(ClientCard c)
        {
            bool fromHand = c.Location == CardLocation.Hand;
            if (Is(c, Ra) && !RaInGrave) return c.Location == CardLocation.Deck ? 0 : 1;
            if (Is(c, Phoenix)) return fromHand ? 2 : 4;
            if (Is(c, Sphere)) return fromHand ? 3 : 5;
            if (Is(c, Ra)) return c.Location == CardLocation.Deck ? 6 : 7;
            return 9;
        }

        private bool GilGarthEffect()
        {
            if (Card.Location == CardLocation.Hand) // Gil Garth tributiert sich später selbst, gibt die Zone also frei
            {
                bool ok = FreeMainZones >= 2 || (FreeMainZones >= 1 && HasGoodGilGarthTarget());
                if (ok) _gilGarthSummonedThisTurn = true;
                return ok;
            }
            if (Card.Location == CardLocation.Grave)
                return true;
            // Tributieren: Immortal Sun God in den Friedhof (Rückholungen freischalten) oder gegnerischen Friedhof stören
            if (!MainPhase) return false;
#if !OHNE_RABBITZUERST
            // Training 2026-10-11 (r8): Gil Garth opferte sich in Zug 1 für das Mahlen, obwohl er mit Helpoemer Silhouhatte Rabbit
            // gelinkt hätte (Endfeld nur Helpoemer: 60 % Siege, mit Rabbit 80 %). Wird er als Rabbit-Material gebraucht: erst Rabbit.
            if (Duel.Player == 0 && Duel.Phase == DuelPhase.Main1 && CanStillLinkRabbit() && RabbitMaterials().Count == 2
                && RabbitMaterials().Contains(Card) && RabbitSummon())
                return false;
#endif
            return HasGoodGilGarthTarget()
                || (WantImmortalMill && Bot.Graveyard.Concat(Enemy.Graveyard).Any(c => c != Card && !Is(c, Ra) && !Is(c, Phoenix)));
        }

        private bool TrueSunGodEffect()
        {
            if (Card.Location != CardLocation.SpellZone || Card.IsFacedown())
                return true; // Aktivierung: Suche
            // Phoenix aus dem Deck + Ra vom Feld in den Friedhof → Phoenix (4000, unbeeinflussbar) steht auf.
            // Nur, wenn Ra schwächer als 4000 ist (Replay 20:33: aufgeladene Ra mit 7900 ATK weggeworfen).
            ClientCard ra = Bot.GetMonsters().FirstOrDefault(m => Is(m, Ra) && m.IsFaceup());
            return ra != null && ra.Attack < 4000 && PhoenixInDeck() && Duel.Player == 0 && MainPhase;
        }

        private bool AncientChantEffect()
        {
            if (Card.Location == CardLocation.Grave)
            {
                if (!Bot.HasInHand(Ra) || Duel.Phase != DuelPhase.Main1) return false;
                bool tributes = Bot.GetMonsterCount() >= 3 || Bot.GetMonsters().Any(m => Is(m, EgyptianGodSlime));
                if (!tributes) return false;
                _chantApplied = true;
                return true;
            }
            return Duel.Phase == DuelPhase.Main1 && (!Bot.HasInHand(Ra) || Bot.GetMonsterCount() >= 3);
        }

        private bool RaSummon()
        {
            return _chantApplied && Duel.Phase == DuelPhase.Main1;
        }

        private bool LavaGolemSummon()
        {
            var faceup = Enemy.GetMonsters().Where(m => m.IsFaceup()).ToList();
            if (faceup.Count < 2 || !Bot.GetMonsters().Any(m => IsSunGod(m) && m.IsFaceup())) return false;
            // Keto (Replay 20:48): Gegner auf 1 LP → Lava Golem zu ihm, er verliert in seiner Standby Phase
            if (Enemy.LifePoints <= 1000) return true;
            if (HasImmortalReady && RaInGrave && Duel.Phase == DuelPhase.Main1) return false; // Immortal Sun God hat Vorrang
            if (faceup.Max(m => m.Attack) < 1500) return false;
            // Lava Golem (3000 ATK) landet beim Gegner. Danach muss ich seinen Angriff plus die übrigen Angreifer überleben.
            int remaining = faceup.OrderByDescending(m => m.Attack).Skip(2).Where(m => m.IsAttack()).Sum(m => Math.Max(0, m.Attack));
            bool blockerLeft = Bot.GetMonsters().Count(m => m.IsFaceup() && m.Defense >= 3000) >= 1 && Bot.GetMonsterCount() >= 2;
            return blockerLeft || Bot.LifePoints > 3000 + remaining + 1000;
        }

        /// <summary>Immortal Sun God braucht eine freie Main-Monsterzone (für Ra, danach für die Fusion).</summary>
        private bool FusionPlanned => (HasImmortalReady && RaInGrave) || Bot.HasInHand(ImmortalSunGod) || Bot.HasInSpellZone(ImmortalSunGod);
        private int FreeMainZones => 5 - Bot.GetMonstersInMainZone().Count;

        /// <summary>false, wenn diese Beschwörung die letzte freie Zone belegen würde, solange die Fusion geplant ist.</summary>
        private bool RoomForFusionAfterSummon()
        {
            return !FusionPlanned || FreeMainZones >= 2;
        }

        private bool PhoenixInDeck()
        {
            return !Bot.Hand.Concat(Bot.Graveyard).Concat(Bot.Banished).Concat(Bot.GetMonsters()).Any(c => Is(c, Phoenix));
        }

        private int StrongestEnemyAttack => Enemy.GetMonsters().Where(m => m.IsFaceup()).Select(m => m.Attack).DefaultIfEmpty(0).Max();

        private int GodSlimeTrapInDeck()
        {
            int seen = Bot.Hand.Concat(Bot.SpellZone.Where(c => c != null)).Concat(Bot.MonsterZone.Where(c => c != null))
                .Concat(Bot.Graveyard).Concat(Bot.Banished).Count(c => Is(c, GodSlimeTrap));
            return Math.Max(0, GodSlimeTrapCopies - seen);
        }

        private List<ClientCard> RabbitMaterials()
        {
            return Bot.GetMonsters().Where(m => m.IsFaceup() && m.HasType(CardType.Effect) && !Is(m, WeeWitch)
                && !Is(m, SunGodDarkness) && !Is(m, Phoenix) && !Is(m, Ra)).ToList();
        }

        /// <summary>Monster mit End-Phase-Rückholung im Friedhof (je Name einmal).</summary>
        private int ReturnerNamesInGrave()
        {
            int n = 0;
            foreach (var ids in new[] { Helpoemer, GilGarth, Makyura, Viser })
                if (Bot.Graveyard.Any(c => Is(c, ids))) n++;
            return n;
        }

        private List<ClientCard> WeeWitchMaterials()
        {
            return Bot.GetMonsters().Where(m => m.IsFaceup() && m.HasAttribute(CardAttribute.Dark) && ReturnsInEndPhase(m)).ToList();
        }

        /// <summary>
        /// Ketos erster Zug (Replay 20:20): Gil Garth + Makyura → Wee Witch's Apprentice, Helpoemer + Slime → Rabbit,
        /// dann Immortal. Alle Sun-God-Monster gehen so in den Friedhof und kommen in der End Phase zurück.
        /// </summary>
        private bool CanLinkWeeWitch()
        {
            if (!Bot.HasInExtra(WeeWitch) || Bot.GetMonsters().Any(m => Is(m, WeeWitch))) return false;
            if (Duel.Player != 0 || Duel.Phase != DuelPhase.Main1) return false;
            if (!HasImmortalReady || !(RaInGrave || PhoenixInGrave)) return false; // nur, wenn Immortal sie heute zurückholt
            var mats = WeeWitchMaterials();
            if (mats.Count < 2) return false;
            // Helpoemer möglichst für Rabbit übrig lassen
            var forRabbit = RabbitMaterials().Except(mats.OrderBy(WeeWitchMaterialScore).Take(2)).Count();
            return forRabbit >= 2 || !Bot.HasInExtra(Rabbit) || mats.Count >= 2 && RabbitMaterials().Count < 2;
        }

        private int WeeWitchMaterialScore(ClientCard c)
        {
            if (Is(c, Makyura) || Is(c, GilGarth)) return 0;
            if (Is(c, Viser)) return 1;
            if (Is(c, Helpoemer)) return 3;
            return 10;
        }

        private bool WeeWitchSummon()
        {
            return CanLinkWeeWitch();
        }

        private bool CanStillLinkRabbit()
        {
            return Bot.HasInExtra(Rabbit) && RabbitMaterials().Count >= 2 && GodSlimeTrapInDeck() > 0
                && !Bot.GetMonsters().Any(m => Is(m, Rabbit));
        }

        /// <summary>Silhouhatte Rabbit aus zwei verbrauchten Sun-God-Monstern: setzt eine weitere Slime-Falle (Replay Zug 1).</summary>
        /// <summary>Schaden, den der Gegner über Rabbit (1500 ATK, unzerstörbar im Kampf) pro Zug machen könnte.</summary>
        private int ExpectedRabbitDamage()
        {
            return Enemy.GetMonsters().Where(m => m.IsFaceup()).Sum(m => Math.Max(0, m.Attack - 1500));
        }

        private bool RabbitSummon()
        {
            if (Duel.Phase != DuelPhase.Main1 || !CanStillLinkRabbit()) return false;
            // Link-Monster stehen immer in Angriffsposition, Rabbit wird im Kampf nicht zerstört → jeder Angriff trifft meine LP
            return ExpectedRabbitDamage() * 3 < Bot.LifePoints && Bot.LifePoints > 4000;
        }

        private bool RabbitEffect()
        {
            if (Card.Location != CardLocation.MonsterZone) return false;
            // Fallenmonster beschworen → gegnerische Zauber/Falle zerstören (Replay Zug 3)
            if (DescIndex == 1)
                return Enemy.GetSpells().Any();
            // Index 0 = Link-Beschwörung: Slime-Falle setzen. Fehlt die Beschreibung (Auslöser), sind beide Effekte nützlich.
            return true;
        }

        private bool NormalBeater()
        {
            if (Bot.HasInHand(Makyura) || !RoomForFusionAfterSummon()) return false;
            return DefaultMonsterSummon();
        }
    }
}
