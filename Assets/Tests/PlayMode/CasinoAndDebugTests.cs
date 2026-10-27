// TideWall 3D — play-mode tests for the Lucky Barnacle (the casino's five games: their rules and what they pay, played
// with stacked cards so the outcome is known) and the debug menu (every page builds, and the main switches work).
// Like the other tests, saves and the legacy record go to a temporary Storage.Root, never the player's files.
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TideWall.Tests
{
    public class CasinoAndDebugTests
    {
        readonly System.Collections.Generic.List<InputDevice> devices = new();
        GameManager gm;
        string storage;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1;
            storage = Path.Combine(Application.temporaryCachePath, "tidewall-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(storage);
            Storage.Root = storage;
            Settings.Reload();
            StoryCard.Disabled = true;
            DebugTools.ResetAll();
            SceneManager.LoadScene("TideWall");
            yield return null;
            yield return null;
            gm = GameManager.I;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Net.Shutdown(null);
            foreach (var d in devices) if (d.added) InputSystem.RemoveDevice(d);
            devices.Clear();
            DebugTools.ResetAll();
            GameManager.Pending = null;
            StoryCard.Disabled = false;
            Time.timeScale = 1;
            SceneManager.LoadScene("TideWall");
            yield return null;
            yield return null;
            try { Directory.Delete(storage, true); } catch (System.Exception) { }
        }

        Player JoinPad()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            devices.Add(pad);
            return gm.Players.Join(new InputDevice[] { pad });
        }

        static PlayingCard C(int rank, int suit = 0) => new(rank, suit);

        /// <summary>A run with one player and a known purse, in a phase where the casino deals.</summary>
        Player Seated(int purse = 1000)
        {
            var p = JoinPad();
            gm.TrySpend(gm.SandDollars);
            gm.AddSandDollars(purse);
            DebugTools.CasinoAlwaysOpen = true;
            return p;
        }

        // ------------------------------------------------------------------ the casino
        [Test]
        public void TheTownAndEveryStageHaveACasino()
        {
            Assert.IsNotNull(Object.FindAnyObjectByType<CasinoBooth>(), "the Lucky Barnacle's booth stands in town");
            Assert.IsNotNull(gm.World.casinoCrab, "the crab runs it");
        }

        [UnityTest]
        public IEnumerator TheCasinoIsClosedDuringPartOnesFights()
        {
            var p = JoinPad();
            yield return null;
            int fight = gm.Cycle.phases.FindIndex(ph => ph.waves > 0);
            Assert.GreaterOrEqual(fight, 0);
            gm.Phases.Enter(fight);
            Assert.IsNotNull(gm.Casino.Closed(), "closed while creatures come");
            int before = gm.SandDollars;
            gm.Casino.Seat<Slots>(p, Vector3.zero).Spin();
            Assert.AreEqual(before, gm.SandDollars, "no bets taken while it's closed");
            int calm = gm.Cycle.phases.FindIndex(ph => ph.shopOpen);
            gm.Phases.Enter(calm);
            Assert.IsNull(gm.Casino.Closed(), "open again between fights");
        }

        [UnityTest]
        public IEnumerator SlotsPayTheTableAndHighTideGivesAFreeDoubleSpin()
        {
            Assert.AreEqual(80, Slots.Payout(5, 5, 5), "three Glizzy Monsters: the jackpot");
            Assert.AreEqual(Slots.TwoShells, Slots.Payout(0, 3, 0), "two shells anywhere");
            Assert.AreEqual(0, Slots.Payout(0, 3, 4));
            for (int r = 0; r < 3; r++)
                for (int sym = 0; sym < Slots.Weights.Length; sym++)
                    Assert.AreEqual(Slots.Weights[sym], System.Array.FindAll(Slots.Strips[r], x => x == sym).Length, $"reel {r} carries symbol {sym} {Slots.Weights[sym]} times");
            Assert.That(Slots.SimulateReturn(400000, 7), Is.InRange(.88, .97), "with High Tide, the machine still keeps a few percent");

            var p = Seated();
            yield return null;
            var slots = gm.Casino.Seat<Slots>(p, Vector3.zero);
            slots.SpinTo(5, 5, 5);
            Assert.AreEqual(990, gm.SandDollars, "the 10 SD bet is taken");
            Assert.IsTrue(slots.Busy);
            slots.Tick(10);
            Assert.AreEqual(990 + 800, gm.SandDollars, "the jackpot pays 80× when the reels stop");

            // Ten losing spins raise the tide; the next spin is free and pays double
            for (int i = 0; i < Slots.TideFill; i++) { slots.SpinTo(1, 2, 3); slots.Tick(10); }
            Assert.IsTrue(slots.HighTide);
            int purse = gm.SandDollars;
            slots.SpinTo(4, 4, 4);
            Assert.AreEqual(purse, gm.SandDollars, "a High Tide spin is free");
            slots.Tick(10);
            Assert.AreEqual(purse + 10 * Slots.Three[4] * Slots.TideMultiplier, gm.SandDollars, "and pays double");
            Assert.IsFalse(slots.HighTide, "the tide goes back out");
        }

        [UnityTest]
        public IEnumerator BlackjackPaysThreeToTwoAndBustsLose()
        {
            var p = Seated();
            yield return null;
            var bj = gm.Casino.Seat<Blackjack>(p, Vector3.zero);
            bj.Shoe.CheckCut();
            bj.Shoe.Stack(C(1), C(9), C(13), C(7));   // you: A K (blackjack); dealer: 9 7
            bj.Deal();
            Assert.AreEqual(Blackjack.Phase.Over, bj.State, "a blackjack ends the hand at once");
            Assert.AreEqual(1000 + 15, gm.SandDollars, "paid 3 to 2");

            bj.Shoe.CheckCut();
            bj.Shoe.Stack(C(10), C(10), C(6), C(7), C(9));   // you: 10 6; dealer: 10 7; you hit a 9
            bj.Deal();
            Assert.AreEqual(Blackjack.Phase.Playing, bj.State);
            bj.Hit();
            Assert.AreEqual("Bust", bj.Hands[0].Result);
            Assert.AreEqual(1015 - 10, gm.SandDollars, "a bust loses the bet");

            Assert.AreEqual(21, Blackjack.Value(new[] { C(1), C(1), C(9) }, out bool soft), "A A 9 = 21");
            Assert.IsTrue(soft);
            Assert.AreEqual(12, Blackjack.Value(new[] { C(1), C(1), C(10) }, out _), "aces drop to 1 rather than bust");
        }

        [UnityTest]
        public IEnumerator RouletteLandsAndPaysEveryChip()
        {
            var p = Seated();
            yield return null;
            var r = gm.Casino.Seat<Roulette>(p, Vector3.zero);
            r.Place(Roulette.Kind.Red, 0);
            r.Place(Roulette.Kind.Number, 7);
            r.Place(Roulette.Kind.Even, 0);
            Assert.AreEqual(970, gm.SandDollars, "three 10 SD chips");
            r.SpinTo(7);   // 7 is red and odd
            r.Tick(10);
            Assert.AreEqual(970 + 20 + 360, gm.SandDollars, "red pays 1 to 1, the number 35 to 1, even loses");
            Assert.AreEqual(0, r.Chips.Count, "the table is cleared");
            Assert.AreEqual(0, Roulette.Return(new Roulette.Chip { Kind = Roulette.Kind.Red, Amount = 10 }, 0), "zero beats red");
            Assert.AreEqual(37, Roulette.Wheel.Length);
        }

        [UnityTest]
        public IEnumerator VideoPokerKnowsItsHands()
        {
            Assert.AreEqual(VideoPoker.Rank.RoyalFlush, VideoPoker.Evaluate(new[] { C(10, 1), C(11, 1), C(12, 1), C(13, 1), C(1, 1) }));
            Assert.AreEqual(VideoPoker.Rank.Straight, VideoPoker.Evaluate(new[] { C(1, 0), C(2, 1), C(3, 2), C(4, 3), C(5, 0) }), "ace-low straight");
            Assert.AreEqual(VideoPoker.Rank.FullHouse, VideoPoker.Evaluate(new[] { C(4, 0), C(4, 1), C(4, 2), C(9, 3), C(9, 0) }));
            Assert.AreEqual(VideoPoker.Rank.JacksOrBetter, VideoPoker.Evaluate(new[] { C(11, 0), C(11, 1), C(3, 2), C(7, 3), C(9, 0) }));
            Assert.AreEqual(VideoPoker.Rank.Nothing, VideoPoker.Evaluate(new[] { C(10, 0), C(10, 1), C(3, 2), C(7, 3), C(9, 0) }), "tens aren't enough");

            var p = Seated();
            yield return null;
            var vp = gm.Casino.Seat<VideoPoker>(p, Vector3.zero);
            vp.DealThese(C(4, 0), C(4, 1), C(4, 2), C(9, 3), C(9, 0));
            for (int i = 0; i < 5; i++) vp.Held[i] = true;
            vp.Draw();
            Assert.AreEqual(VideoPoker.Rank.FullHouse, vp.Last);
            Assert.AreEqual(990 + 90, gm.SandDollars, "a full house pays 9×");
        }

        [UnityTest]
        public IEnumerator FathomsFollowsBaccaratsRules()
        {
            // The Captain's (banker's) chart
            Assert.IsTrue(Fathoms.CaptainDraws(2, 9));
            Assert.IsFalse(Fathoms.CaptainDraws(3, 8));
            Assert.IsTrue(Fathoms.CaptainDraws(4, 2)); Assert.IsFalse(Fathoms.CaptainDraws(4, 8));
            Assert.IsTrue(Fathoms.CaptainDraws(6, 7)); Assert.IsFalse(Fathoms.CaptainDraws(6, 5));
            Assert.IsFalse(Fathoms.CaptainDraws(7, 6));
            // What bets return
            Assert.AreEqual(20, Fathoms.Return(Fathoms.Side.Sailor, Fathoms.Side.Sailor, 10));
            Assert.AreEqual(19, Fathoms.Return(Fathoms.Side.Captain, Fathoms.Side.Captain, 10), "less the 5% harbour fee");
            Assert.AreEqual(90, Fathoms.Return(Fathoms.Side.Slack, Fathoms.Side.Slack, 10), "Slack Water pays 8 to 1");
            Assert.AreEqual(10, Fathoms.Return(Fathoms.Side.Captain, Fathoms.Side.Slack, 10), "a tie returns the others' bets");
            Assert.AreEqual(5, Fathoms.Depth(new[] { C(7), C(8) }), "15 fathoms → 5");
            Assert.AreEqual(0, Fathoms.Depth(new[] { C(13), C(10) }));

            var p = Seated();
            yield return null;
            var f = gm.Casino.Seat<Fathoms>(p, Vector3.zero);
            // High Tide: the Sailor has 9 on two cards; nobody draws
            f.Pick = Fathoms.Side.Sailor;
            f.DealThese(C(9), C(13), C(13), C(5));   // Sailor 9 K, Captain K 5
            Assert.IsTrue(f.Busy, "the dealer shows it step by step");
            f.Tick(30);
            Assert.AreEqual(2, f.Sailor.Count); Assert.AreEqual(2, f.Captain.Count);
            Assert.AreEqual(Fathoms.Side.Sailor, f.Winner);
            Assert.AreEqual(990 + 20, gm.SandDollars);
            // Third cards: Sailor 2+3 = 5 draws a 6 (→ 1); Captain at 6 draws because that card was a 6, gets a K (→ 6)
            f.Pick = Fathoms.Side.Captain;
            f.DealThese(C(2), C(4), C(3), C(2), C(6), C(13));
            f.Tick(30);
            Assert.AreEqual(3, f.Sailor.Count); Assert.AreEqual(3, f.Captain.Count);
            Assert.AreEqual(1, Fathoms.Depth(f.Sailor)); Assert.AreEqual(6, Fathoms.Depth(f.Captain));
            Assert.AreEqual(Fathoms.Side.Captain, f.Winner);
            Assert.AreEqual(1010 - 10 + 19, gm.SandDollars, "the Captain's win, less the harbour fee");
        }

        [UnityTest]
        public IEnumerator OneButtonRepeatsTheWagerAndLeftRightChangesTheBet()
        {
            var p = Seated();
            yield return null;
            var slots = gm.Casino.Seat<Slots>(p, Vector3.zero);
            var m = Casino.Table(p, slots, Vector3.zero);
            Assert.AreEqual(10, gm.Casino.Bet(p));
            m.OnSide(1);
            Assert.AreEqual(25, gm.Casino.Bet(p), "left/right on any row changes the bet");
            Assert.AreEqual("spin", m.QuickLabel());
            m.Quick();
            Assert.AreEqual(975, gm.SandDollars, "Reload spins again at the new bet");
            slots.Tick(5);

            int before = gm.SandDollars;
            var r = gm.Casino.Seat<Roulette>(p, Vector3.zero);
            r.Place(Roulette.Kind.Black, 0); r.Place(Roulette.Kind.Number, 0);
            r.SpinTo(1);   // 1 is red: both chips lose
            r.Tick(10);
            Assert.AreEqual(before - 50, gm.SandDollars);
            Assert.AreEqual("re-bet & spin", Casino.Table(p, r, Vector3.zero).QuickLabel());
            r.Repeat();
            Assert.IsTrue(r.Busy, "the same chips go back down and the wheel spins");
            Assert.AreEqual(2, r.Chips.Count);
            Assert.AreEqual(before - 100, gm.SandDollars);
        }

        // ------------------------------------------------------------------ four at the casino
        /// <summary>Press these buttons on these pads in the same frame (null = nothing on that pad), then let go.</summary>
        static IEnumerator Press(Gamepad[] pads, params GamepadButton?[] buttons)
        {
            for (int i = 0; i < pads.Length; i++) InputSystem.QueueStateEvent(pads[i], buttons[i].HasValue ? new GamepadState().WithButton(buttons[i].Value) : new GamepadState());
            yield return null;
            foreach (var pad in pads) InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator FourPlayersGambleAtOnce()
        {
            var pads = new Gamepad[4];
            var players = new Player[4];
            for (int i = 0; i < 4; i++)
            {
                pads[i] = InputSystem.AddDevice<Gamepad>();
                devices.Add(pads[i]);
                players[i] = gm.Players.Join(new InputDevice[] { pads[i] });
            }
            yield return null;
            Assert.AreEqual(4, gm.Players.Count);
            gm.TrySpend(gm.SandDollars);
            gm.AddSandDollars(5000);
            var booth = Object.FindAnyObjectByType<CasinoBooth>().transform.position;
            for (int i = 0; i < 4; i++)
            {
                players[i].Place(booth + new Vector3(-3 + i * 2, 0, 3));
                players[i].OpenMenu(Casino.Lobby(players[i], booth));
            }
            yield return null;
            Assert.IsNull(gm.Casino.Closed(), "the first morning: the tables are open");

            // Everyone picks a different game from the lobby's cards, all at the same time, with their own controller:
            // Red slots, Blue blackjack, Green roulette, Gold video poker
            var R = GamepadButton.DpadRight; var D = GamepadButton.DpadDown; var A = GamepadButton.South; var X = GamepadButton.West;
            yield return Press(pads, null, R, R, D);
            yield return Press(pads, null, null, R, null);
            yield return Press(pads, A, A, A, A);
            string[] titles = { "SLOTS", "BLACKJACK", "ROULETTE", "VIDEO POKER" };
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(MenuLayout.Table, players[i].OpenedMenu?.Layout, $"player {i + 1} sits down at a table");
                StringAssert.EndsWith(titles[i], players[i].OpenedMenu.Title);
            }
            // Green puts a chip on red (two rows down)
            yield return Press(pads, null, null, D, null);
            yield return Press(pads, null, null, D, null);
            yield return Press(pads, null, null, A, null);
            // Then all four play at once: Red pulls the lever, Blue deals, Green spins, Gold deals
            int before = gm.SandDollars, net = gm.Casino.Net, hands = gm.Casino.HandsPlayed;
            yield return Press(pads, X, A, X, A);
            var slots = gm.Casino.Seat<Slots>(players[0], booth);
            var bj = gm.Casino.Seat<Blackjack>(players[1], booth);
            var rou = gm.Casino.Seat<Roulette>(players[2], booth);
            var vp = gm.Casino.Seat<VideoPoker>(players[3], booth);
            Assert.IsTrue(slots.Busy, "Red's reels are spinning");
            Assert.AreNotEqual(Blackjack.Phase.Betting, bj.State, "Blue's hand is dealt");
            Assert.IsTrue(rou.Busy, "Green's ball is rolling");
            Assert.AreEqual(VideoPoker.Phase.Draw, vp.State, "Gold's cards are dealt");
            Assert.AreEqual(hands + 3, gm.Casino.HandsPlayed, "three more bets (Green's chip went down earlier)");
            Assert.AreEqual(before + (gm.Casino.Net - net), gm.SandDollars, "all from the one purse (a blackjack may already have paid)");
            // Gold draws, Blue stands (if the hand's still going), and everything settles
            yield return Press(pads, null, bj.State == Blackjack.Phase.Playing ? GamepadButton.DpadDown : (GamepadButton?)null, null, X);
            if (bj.State == Blackjack.Phase.Playing) yield return Press(pads, null, A, null, null);
            for (float t = 0; t < 8 && (slots.Busy || rou.Busy); t += Time.unscaledDeltaTime) yield return null;
            Assert.IsFalse(slots.Busy); Assert.IsFalse(rou.Busy);
            Assert.AreEqual(Blackjack.Phase.Over, bj.State);
            Assert.AreEqual(VideoPoker.Phase.Deal, vp.State);
            Assert.AreEqual(5000 + gm.Casino.Net, gm.SandDollars, "every stake and payout went through the one purse, nothing lost or doubled");
            Assert.AreEqual(4, gm.Casino.HandsPlayed);

            // Now all four at the same machine: four seats, four independent spins
            for (int i = 0; i < 4; i++) players[i].OpenMenu(Casino.Table(players[i], gm.Casino.Seat<Slots>(players[i], booth), booth));
            yield return null;
            before = gm.SandDollars;
            yield return Press(pads, X, X, X, X);
            var seats = new Slots[4];
            for (int i = 0; i < 4; i++) { seats[i] = gm.Casino.Seat<Slots>(players[i], booth); Assert.IsTrue(seats[i].Busy, $"player {i + 1}'s reels spin"); }
            Assert.AreEqual(4, new System.Collections.Generic.HashSet<Slots>(seats).Count, "everyone has their own seat");
            Assert.AreEqual(before - 40, gm.SandDollars);
            for (float t = 0; t < 8 && System.Array.Exists(seats, s => s.Busy); t += Time.unscaledDeltaTime) yield return null;
            int won = 0; foreach (var s in seats) won += s.LastWin;
            Assert.AreEqual(before - 40 + won, gm.SandDollars, "each seat paid its own win");
            Assert.AreEqual(5000 + gm.Casino.Net, gm.SandDollars);
        }

        [UnityTest]
        public IEnumerator TheTableTravelsOnlineWithItsBoard()
        {
            var p = Seated();
            yield return null;
            var m = Casino.Table(p, gm.Casino.Seat<Fathoms>(p, Vector3.zero), Vector3.zero);
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) w.WriteMenu(m);
            ms.Position = 0;
            using var r = new BinaryReader(ms);
            var copy = r.ReadMenu();
            Assert.AreEqual(MenuLayout.Table, copy.Layout);
            Assert.AreEqual(m.Board(), copy.Board(), "the client draws the host's table");
            Assert.AreEqual(m.Items.Count, copy.Items.Count);
        }

        // ------------------------------------------------------------------ the debug menu
        static int OpenEverything(Menu m, int depth = 0)
        {
            int pages = 1;
            if (depth > 2) return pages;
            foreach (var it in new System.Collections.Generic.List<MenuItem>(m.Items))
                if (it.Open != null && it.Label != "Back" && it.Label != "Close")
                {
                    var sub = it.Open();
                    Assert.IsNotNull(sub, it.Label);
                    if (sub.Title.StartsWith(DebugMenu.Tag)) pages += OpenEverything(sub, depth + 1);
                }
            return pages;
        }

        [UnityTest]
        public IEnumerator EveryDebugPageOpensInPartOne()
        {
            var p = JoinPad();
            yield return null;
            Assert.IsTrue(DebugTools.Available(p));
            Assert.GreaterOrEqual(OpenEverything(DebugMenu.Main(p)), 8, "main, players, creatures, waves, wall, Part II, cards, casino, legacy");
        }

        [UnityTest]
        public IEnumerator DebugSwitchesWork()
        {
            var p = JoinPad();
            yield return null;
            // God mode
            DebugTools.God = true;
            float hp = p.Hp;
            p.TakeDamage(50, null, "test");
            Assert.AreEqual(hp, p.Hp, "god mode: no damage");
            DebugTools.God = false;
            p.TakeDamage(5, null, "test");
            Assert.Less(p.Hp, hp);
            // Time scale
            DebugTools.TimeScale = 2;
            yield return null;
            Assert.AreEqual(2, Time.timeScale);
            DebugTools.TimeScale = 1;
            yield return null;
            // Spawning from the menu, freezing, killing them all
            var crab = gm.DB.Enemy("crab");
            var e = gm.Waves.SpawnExtra(crab, new Vector3(0, 0, 12));
            DebugTools.Freeze = true;
            var at = e.transform.position;
            for (int i = 0; i < 20; i++) yield return null;
            Assert.AreEqual(at, e.transform.position, "frozen creatures stay put");
            DebugMenu.KillAll();
            Assert.AreEqual(0, GameManager.Enemies.Count);
            // Money and levels
            int sd = gm.SandDollars;
            var players = DebugMenu.Main(p).Items.Find(i => i.Label == "Players & money").Open();
            players.Items.Find(i => i.Label == "+1,000 sand dollars").OnPick();
            Assert.AreEqual(sd + 1000, gm.SandDollars);
            int lv = p.Level;
            players.Items.Find(i => i.Label == "Level up").OnPick();
            Assert.AreEqual(lv + 1, p.Level);
        }

        [UnityTest]
        public IEnumerator EveryDebugPageOpensInPartTwo()
        {
            var part = Campaign.Load().parts.Find(x => x.kind == PartKind.Assault);
            SceneManager.LoadScene(part.stages[4].scene);
            yield return null;
            yield return null;
            gm = GameManager.I;
            var p = JoinPad();
            yield return null;
            Assert.IsNotNull(Object.FindAnyObjectByType<CasinoBooth>(), "the crab's crate came down to the seabed");
            var main = DebugMenu.Main(p);
            OpenEverything(main);
            var ex = main.Items.Find(i => i.Label.StartsWith("The expedition")).Open();
            Assert.IsTrue(ex.Items.Exists(i => i.Label.StartsWith("Ending:")), "the Heart's endings can be played from here");
        }
    }
}
