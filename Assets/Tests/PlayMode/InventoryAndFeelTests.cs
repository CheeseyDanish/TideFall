// TideWall 3D — play-mode tests for the inventory (three guns carried, five attachment slots, parts that move between
// guns, skins that outlive runs), aiming down the sights, and how fights feel: wind-ups you can dodge, staggers,
// deaths that tumble, jointed creatures. Saves go to a temporary Storage.Root, never the player's files.
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
    public class InventoryAndFeelTests
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
            foreach (var d in devices) if (d.added) InputSystem.RemoveDevice(d);
            devices.Clear();
            DebugTools.ResetAll();
            StoryCard.Disabled = false;
            Time.timeScale = 1;
            SceneManager.LoadScene("TideWall");
            yield return null;
            yield return null;
            try { Directory.Delete(storage, true); } catch (System.Exception) { }
        }

        Player JoinPad(out Gamepad pad)
        {
            pad = InputSystem.AddDevice<Gamepad>();
            devices.Add(pad);
            return gm.Players.Join(new InputDevice[] { pad });
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        IEnumerator Tap(Gamepad pad, GamepadButton b)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(b));
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator YouCarryThreeGunsAndTheRestWaitInTheStash()
        {
            var p = JoinPad(out var pad);
            gm.Waves.RunWave = gm.DB.gunsUnlockWave;
            yield return Frames(2);
            Assert.AreEqual(3, gm.DB.gunSlots);
            Assert.AreEqual(new[] { "revolver" }, p.Loadout.ToArray());
            p.GiveGun("smg"); p.GiveGun("scattergun");
            Assert.AreEqual(new[] { "revolver", "smg", "scattergun" }, p.Loadout.ToArray());
            p.GiveGun("harpoon");
            Assert.AreEqual(new[] { "revolver", "smg", "harpoon" }, p.Loadout.ToArray(), "a fourth takes the place of the gun in your hands");
            Assert.IsTrue(p.OwnsGun("scattergun") && !p.Carries("scattergun"), "and that one's in the stash");
            yield return null;

            // Swap cycles only what you carry
            yield return Tap(pad, GamepadButton.North);
            Assert.AreEqual("revolver", p.Gun.id);
            yield return Tap(pad, GamepadButton.North);
            Assert.AreEqual("smg", p.Gun.id);
            yield return Tap(pad, GamepadButton.North);
            Assert.AreEqual("harpoon", p.Gun.id);
            p.SelectSlot(0);
            Assert.AreEqual("revolver", p.Gun.id);

            // The inventory's pages all build, and the stash can be brought back
            var main = InventoryMenus.Main(p);
            Assert.AreEqual(InventoryMenus.Title, main.Title);
            Assert.IsNotNull(main.Items.Find(i => i.Label == "Scattergun"), "the stash is listed");
            main.Items.Find(i => i.Label == "Scattergun").Open();
            var page = InventoryMenus.GunPage(p, gm.DB.Gun("scattergun"));
            page.Items.Find(i => i.Label == "Carry this").OnPick();
            Assert.AreEqual(new[] { "scattergun", "smg", "harpoon" }, p.Loadout.ToArray());
            p.Stash("harpoon");
            Assert.AreEqual(2, p.Loadout.Count);
            p.Stash("smg"); p.Stash("scattergun");
            Assert.AreEqual(1, p.Loadout.Count, "you always keep one");

            // The Inventory button opens it (and closes it)
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState { buttons = 1u << (int)GamepadButton.DpadUp });
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            Assert.AreEqual(InventoryMenus.Title, p.OpenedMenu?.Title);
        }

        [UnityTest]
        public IEnumerator EveryGunHasFiveSlotsAndPartsMoveBetweenGuns()
        {
            var p = JoinPad(out _);
            gm.Waves.RunWave = gm.DB.gunsUnlockWave;
            yield return Frames(2);
            gm.AddSandDollars(5000);
            int purse = gm.SandDollars;
            foreach (var id in new[] { "spyglass", "longbarrel", "harbourlaser", "driftstock", "incendiary" })
                Menus.Shop(p, Menus.ShopTab.Attachments, p.transform.position).Items.Find(i => i.Label == gm.DB.Attachment(id).displayName).OnPick();
            foreach (AttachmentSlot slot in System.Enum.GetValues(typeof(AttachmentSlot)))
                Assert.IsNotNull(p.AttachmentIn(p.Gun.id, slot), $"the {slot} slot");
            Assert.Less(gm.SandDollars, purse);
            yield return null;
            Assert.IsTrue(GameObject.Find("Laser") != null && GameObject.Find("Laser").GetComponent<LineRenderer>().enabled, "the laser shows its beam");

            // A part you bought fits your other guns for free, from the Inventory
            p.GiveGun("smg");
            purse = gm.SandDollars;
            var parts = InventoryMenus.PartPage(p, p.Gun, AttachmentSlot.Laser);
            Assert.IsNull(parts.Items.Find(i => i.Label == "Harbour Beam").Blocked());
            Assert.IsNotNull(parts.Items.Find(i => i.Label == "Angler Lure").Blocked(), "not one you haven't bought");
            parts.Items.Find(i => i.Label == "Harbour Beam").OnPick();
            Assert.AreEqual(purse, gm.SandDollars);
            Assert.Less(p.EffGun.spreadDegrees, p.Gun.spreadDegrees);
            float tight = p.EffGun.spreadDegrees;
            p.Unfit(p.Gun.id, AttachmentSlot.Laser);
            Assert.Greater(p.EffGun.spreadDegrees, tight);
            Assert.IsTrue(p.OwnsPart("harbourlaser"), "taking it off keeps it");
        }

        [UnityTest]
        public IEnumerator SkinsAreBoughtForGoodAndShowOnTheGun()
        {
            var p = JoinPad(out _);
            yield return Frames(2);
            var db = gm.DB;
            Assert.GreaterOrEqual(db.skins.Count, 10);
            foreach (var s in db.skins) Assert.IsNotEmpty(s.description, s.id);
            var tiger = db.Skin("tiger");
            Assert.IsFalse(Skins.Unlocked(tiger, Legacy.Load()));
            Assert.IsTrue(Skins.Unlocked(db.Skin("rust"), Legacy.Load()), "some are free");

            var page = InventoryMenus.SkinPage(p, p.Gun.id, p.Gun.model, p.Gun.displayName, null);
            var row = page.Items.Find(i => i.Label == tiger.displayName);
            Assert.IsNotNull(row.Blocked(), "no pearls yet");
            gm.AddPearls(tiger.pearls);
            Assert.IsNull(InventoryMenus.SkinPage(p, p.Gun.id, p.Gun.model, p.Gun.displayName, null).Items.Find(i => i.Label == tiger.displayName).Blocked());
            InventoryMenus.SkinPage(p, p.Gun.id, p.Gun.model, p.Gun.displayName, null).Items.Find(i => i.Label == tiger.displayName).OnPick();
            Assert.AreEqual(0, gm.Pearls);
            Assert.AreEqual(tiger, p.SkinOn(p.Gun.id));
            Assert.IsTrue(Skins.Unlocked(tiger, Legacy.Load()), "written to the record that outlives runs");
            yield return null;
            var gunModel = GunModelOf(p);
            Assert.IsNotNull(gunModel);
            Assert.AreEqual("TideWall/Skin", gunModel.GetComponentInChildren<MeshRenderer>().sharedMaterial.shader.name);

            // Enough all-time kills unlock it free
            var l = Legacy.Load(); l.kills = db.Skin("reef").kills; l.DebugWrite();
            Assert.IsTrue(Skins.Unlocked(db.Skin("reef"), Legacy.Load()));

            // Back to factory, and online it travels in the snapshot
            p.SetSkin(p.Gun.id, db.Skin("gilded"));
            var state = NetPlayerState.From(p);
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) state.Write(w);
            ms.Position = 0;
            using var r = new BinaryReader(ms);
            var copy = NetPlayerState.Read(r);
            Assert.AreEqual("gilded", copy.gunSkin);
            Assert.AreEqual("revolver", copy.loadout);
            p.SetSkin(p.Gun.id, null);
            Assert.IsNull(p.SkinOn(p.Gun.id));
        }

        static Transform GunModelOf(Player p)
        {
            foreach (var t in p.GetComponentsInChildren<Transform>(true)) if (t.name == p.Gun.model) return t;
            return null;
        }

        [UnityTest]
        public IEnumerator TheSightsComeUpAndZoomWithTheScope()
        {
            var p = JoinPad(out var pad);
            gm.Waves.RunWave = gm.DB.gunsUnlockWave;
            yield return Frames(4);
            float wide = p.Cam.Camera.fieldOfView;
            for (float t = 0; t < .35f; t += Time.unscaledDeltaTime) { InputSystem.QueueStateEvent(pad, new GamepadState { leftTrigger = 1 }); yield return null; }
            Assert.IsTrue(p.Aiming);
            Assert.Less(p.Cam.Camera.fieldOfView, wide * .95f, "iron sights close in a little");
            float iron = p.Cam.Camera.fieldOfView;
            p.FitAttachment(gm.DB.Attachment("spyglass"));
            for (float t = 0; t < .1f; t += Time.unscaledDeltaTime) { InputSystem.QueueStateEvent(pad, new GamepadState { leftTrigger = 1 }); yield return null; }
            Assert.Less(p.Cam.Camera.fieldOfView, iron * .7f, "a spyglass much further");
            InputSystem.QueueStateEvent(pad, new GamepadState());
            for (float t = 0; t < .35f; t += Time.unscaledDeltaTime) yield return null;
            Assert.IsFalse(p.Aiming);
            Assert.AreEqual(wide, p.Cam.Camera.fieldOfView, 1);
        }

        [UnityTest]
        public IEnumerator YouCanDashClearOfAWindUpAndStaggerStopsIt()
        {
            var p = JoinPad(out _);
            yield return Frames(2);
            p.Place(new Vector3(0, 0, 10));
            var e = gm.Waves.SpawnExtra(gm.DB.Enemy("drowner"), new Vector3(0, 0, 11.2f));
            float hp = p.Hp;
            float waited = 0;
            while (e.Windup <= 0 && waited < 3) { waited += Time.deltaTime; yield return null; }
            Assert.Greater(e.Windup, 0, "it rears back first");
            Assert.AreEqual(hp, p.Hp, "and nothing has landed yet");
            p.Place(new Vector3(0, 0, 3));   // out of reach before it lands
            while (e.Windup > 0) yield return null;
            yield return null;
            Assert.AreEqual(hp, p.Hp, "dodged");

            // Back in reach: this time it lands
            p.Place(e.transform.position + Vector3.back * .9f);
            waited = 0;
            while (p.Hp >= hp && waited < 4) { waited += Time.deltaTime; yield return null; }
            Assert.Less(p.Hp, hp, "stand there and it hits you");

            // A heavy enough hit mid-wind-up cancels it
            waited = 0;
            while (e.Windup <= 0 && waited < 4) { waited += Time.deltaTime; yield return null; }
            Assert.Greater(e.Windup, 0);
            e.TakeHit(new DamageInfo { amount = e.MaxHp * gm.DB.Enemy("drowner").poise + 1, source = p, dir = Vector3.forward, point = e.transform.position }, null);
            Assert.AreEqual(0, e.Windup, "staggered");
        }

        // ------------------------------------------------------------------ the wall (a crowd, for real, with nobody calling Damage by hand)
        [UnityTest]
        public IEnumerator ACrowdAtTheWallBattersTheSectionItsAt()
        {
            var p = JoinPad(out _);
            yield return Frames(2);
            p.Place(new Vector3(30, 0, -15));   // well out of their way
            var section = gm.Wall.Sections.Find(s => Mathf.Abs(s.x - 2) < 2.1f);
            float full = gm.Wall.Hp;
            float neighbourFar = gm.Wall.Sections.Find(s => Mathf.Abs(s.x - (section.x + 16)) < .1f).hp;
            for (int i = 0; i < 12; i++)
                gm.Waves.SpawnExtra(gm.DB.Enemy("crab"), new Vector3(section.x + (i % 4 - 1.5f) * .5f, 0, 3 + i % 3));
            float waited = 0;
            Time.timeScale = 4;
            while (section.hp > section.max * .6f && !section.breached && waited < 40) { waited += Time.unscaledDeltaTime; yield return null; }
            Time.timeScale = 1;
            Assert.Less(gm.Wall.Hp, full, "they hurt the wall");
            Assert.Less(section.hp, section.max * .75f, "that section, on the crowd, including those in the second rank");
            Assert.AreEqual(neighbourFar, gm.Wall.Sections.Find(s => Mathf.Abs(s.x - (section.x + 16)) < .1f).hp, "and not one far away");
            Assert.GreaterOrEqual(section.stage, 1, "and the damage shows");
            Assert.IsNotNull(section.scars[0]);
            StringAssert.Contains("%", gm.Level.StatusLine());
            gm.Wall.Repair(1);
            Assert.AreEqual(0, section.stage, "repairs clear the scars");
            Assert.IsFalse(section.scars[0].activeSelf);
        }

        // ------------------------------------------------------------------ surviving limb loss
        [UnityTest]
        public IEnumerator HeadlessCreaturesStumbleOnAndBleedOut()
        {
            var p = JoinPad(out _);
            yield return Frames(2);
            float chance = gm.DB.headlessChance;
            Vector2 seconds = gm.DB.headlessSeconds;
            try
            {
                gm.DB.headlessChance = 1;
                gm.DB.headlessSeconds = new Vector2(.6f, .6f);
                p.Place(new Vector3(30, 0, -15));
                var e = gm.Waves.SpawnExtra(gm.DB.Enemy("drowner"), new Vector3(-8, 0, 14));
                var head = e.Limbs.Find(l => l.Def.part == "Head");
                e.TakeHit(new DamageInfo { amount = head.Hp + 1, ignoreArmor = true, source = p, dir = Vector3.forward, point = e.transform.position }, head);
                Assert.IsFalse(e.Dead, "its head is off, and it doesn't know yet");
                Assert.IsTrue(e.Headless);
                int kills = p.Kills;
                yield return null;
                float t = 0;
                while (!e.Dead && t < 3) { t += Time.deltaTime; yield return null; }
                Assert.IsTrue(e.Dead, "it bleeds out");
                Assert.AreEqual(kills + 1, p.Kills, "and whoever took its head gets the kill");

                // An explosion that doesn't kill blows a limb off, and it carries on
                gm.DB.blastLimbChance = 1;
                var d = gm.Waves.SpawnExtra(gm.DB.Enemy("drowner"), new Vector3(-8, 0, 16));
                d.TakeHit(new DamageInfo { amount = 1, explosion = true, ignoreArmor = true, source = p }, null);
                Assert.IsFalse(d.Dead);
                Assert.IsTrue(d.Limbs.Exists(l => l.Severed));
            }
            finally { gm.DB.headlessChance = chance; gm.DB.headlessSeconds = seconds; gm.DB.blastLimbChance = .6f; }
        }

        // ------------------------------------------------------------------ what each kind is for
        [UnityTest]
        public IEnumerator EveryCreatureHasAJobAndItsOwnTrick()
        {
            var p = JoinPad(out _);
            yield return Frames(2);
            p.Place(new Vector3(30, 0, -15));
            foreach (var def in gm.DB.enemies)
            {
                Assert.IsNotEmpty(def.role, $"{def.id} has a job");
                Assert.IsNotEmpty(def.counter, $"{def.id} has an answer");
            }
            Assert.AreEqual(gm.DB.enemies.Count, Menus.FieldGuide(p).Items.Count, "the field guide lists them all");

            // Clawback: claws block from the front, not from behind, and not once one's gone
            var lob = gm.Waves.SpawnExtra(gm.DB.Enemy("lobster"), new Vector3(-10, 0, 14));
            lob.transform.rotation = Quaternion.identity;   // faces +Z
            float hp = lob.Hp;
            lob.TakeHit(new DamageInfo { amount = 50, ignoreArmor = true, dir = Vector3.back, point = lob.transform.position + Vector3.forward }, null);
            float front = hp - lob.Hp; hp = lob.Hp;
            lob.transform.rotation = Quaternion.identity;
            lob.TakeHit(new DamageInfo { amount = 50, ignoreArmor = true, dir = Vector3.forward, point = lob.transform.position - Vector3.forward }, null);
            float back = hp - lob.Hp;
            Assert.Less(front, back * .5f, "guarded from the front");

            // Shellback: softens hits on its neighbours
            var alone = gm.Waves.SpawnExtra(gm.DB.Enemy("drowner"), new Vector3(20, 0, 14));
            hp = alone.Hp; alone.TakeHit(new DamageInfo { amount = 10, ignoreArmor = true, source = p, point = alone.transform.position }, null);
            float bare = hp - alone.Hp;
            gm.Waves.SpawnExtra(gm.DB.Enemy("turtle"), new Vector3(-20, 0, 14));
            var covered = gm.Waves.SpawnExtra(gm.DB.Enemy("drowner"), new Vector3(-19, 0, 15));
            hp = covered.Hp; covered.TakeHit(new DamageInfo { amount = 10, ignoreArmor = true, source = p, point = covered.transform.position }, null);
            Assert.Less(hp - covered.Hp, bare, "a Shellback covers those close by");

            // Hermit Crab: hurt, it pulls into its shell and takes far less
            var hermit = gm.Waves.SpawnExtra(gm.DB.Enemy("hermit"), new Vector3(25, 0, 14));
            hermit.TakeHit(new DamageInfo { amount = hermit.MaxHp * .5f, ignoreArmor = true, point = hermit.transform.position }, null);
            Assert.IsTrue(hermit.Tucked);

            // Drowner: climbs over an unbroken wall. Force the choice with a high climb chance
            float chance = gm.DB.Enemy("drowner").climbChance;
            gm.DB.Enemy("drowner").climbChance = 1;
            var climber = gm.Waves.SpawnExtra(gm.DB.Enemy("drowner"), new Vector3(6, 0, 4));
            gm.DB.Enemy("drowner").climbChance = chance;
            Time.timeScale = 4;
            float t = 0;
            while (t < 25 && !Arena.Inside(climber.transform.position)) { t += Time.unscaledDeltaTime; yield return null; }
            Time.timeScale = 1;
            Assert.IsTrue(Arena.Inside(climber.transform.position), "it's over the wall without a breach");
            Assert.AreEqual(0, gm.Wall.BreachCount);
        }

        [UnityTest]
        public IEnumerator CreaturesAreJointedAndTumbleWhenTheyDie()
        {
            JoinPad(out _);
            yield return Frames(2);
            foreach (var def in gm.DB.enemies)
                foreach (var l in def.limbs)
                    Assert.IsNotNull(Content.Model(def.model).transform.Find("Limb_" + l.part), $"{def.model} still has Limb_{l.part}");
            int legs = 0;
            foreach (var t in Content.Model("Crab").GetComponentsInChildren<Transform>()) if (t.name.StartsWith("Part_Leg")) legs++;
            Assert.AreEqual(6, legs, "a crab has six jointed legs");

            var crab = gm.Waves.SpawnExtra(gm.DB.Enemy("crab"), new Vector3(3, 0, 12));
            yield return null;
            crab.Die(new DamageInfo { dir = Vector3.forward });
            Assert.IsNotNull(crab.GetComponent<DeathFlop>(), "it doesn't just snap to its corpse pose");
            float t0 = 0;
            while (crab != null && crab.GetComponent<DeathFlop>() != null && t0 < 2) { t0 += Time.deltaTime; yield return null; }
            Assert.Less(t0, 2, "and it lands");
            Assert.IsNotNull(crab.GetComponent<Corpse>());
            Assert.GreaterOrEqual(ModelFactory.Bounds(crab.Model.root).min.y, -.05f, "on the sand, not in it");
        }
    }
}
