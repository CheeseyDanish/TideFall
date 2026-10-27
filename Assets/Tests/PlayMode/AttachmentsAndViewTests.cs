// TideWall 3D — play-mode tests for gun attachments (bought in the shop, one per slot, folded into the gun's numbers,
// fitted to its model, sent online) and the third-person view (camera over the shoulder, aiming at the crosshair,
// walking where you look). Saves go to a temporary Storage.Root, never the player's files.
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
    public class AttachmentsAndViewTests
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

        // ------------------------------------------------------------------ attachments
        [UnityTest]
        public IEnumerator TheShopSellsAttachmentsThatChangeTheGun()
        {
            var p = JoinPad(out _);
            yield return Frames(2);
            gm.AddSandDollars(2000);
            int purse = gm.SandDollars;
            float range = p.EffGun.range, spread = p.EffGun.spreadDegrees;

            var shop = Menus.Shop(p, Menus.ShopTab.Attachments, p.transform.position);
            Assert.AreEqual(gm.DB.attachments.Count, shop.Items.Count, "every attachment is for sale for the gun in your hands");
            var spyglass = gm.DB.Attachment("spyglass");
            shop.Items.Find(i => i.Label == spyglass.displayName).OnPick();
            Assert.AreEqual(purse - spyglass.price, gm.SandDollars, "paid from the team purse");
            Assert.IsTrue(p.HasAttachment(p.Gun.id, "spyglass"));
            Assert.AreEqual(range * spyglass.range, p.EffGun.range, .01f, "the scope adds reach");
            Assert.Less(p.EffGun.spreadDegrees, spread, "and tightens the shots");
            Assert.IsNotNull(FindDeep(p.transform, "AttScope"), "it's fitted to the gun in your hands");

            // One attachment per gun: the Sharkskin Grip replaces the scope
            Menus.Shop(p, Menus.ShopTab.Attachments, p.transform.position).Items.Find(i => i.Label == "Sharkskin Grip").OnPick();
            Assert.IsFalse(p.HasAttachment(p.Gun.id, "spyglass"), "one attachment per gun");
            Assert.AreEqual(gm.DB.Attachment("sharkgrip"), p.AttachmentIn(p.Gun.id, AttachmentSlot.Attachment));
            Assert.AreEqual(range, p.EffGun.range, .01f, "the scope's reach went with it");
            Assert.IsNotNull(FindDeep(p.transform, "AttGrip"));

            // ... and one ammo modifier alongside it: incendiary rounds with a sharkskin grip
            var fire = gm.DB.Attachment("incendiary");
            Menus.Shop(p, Menus.ShopTab.Attachments, p.transform.position).Items.Find(i => i.Label == fire.displayName).OnPick();
            Assert.AreEqual(fire, p.AttachmentIn(p.Gun.id, AttachmentSlot.Ammo));
            Assert.IsTrue(p.HasAttachment(p.Gun.id, "sharkgrip"), "the grip stays: ammo has its own slot");
            Assert.AreEqual(Element.Fire, p.EffGun.element, "the rounds burn");
            Assert.Greater(p.EffGun.fireRate, p.Gun.fireRate, "and the grip still speeds up the trigger");
            var again = Menus.Shop(p, Menus.ShopTab.Attachments, p.transform.position).Items.Find(i => i.Label == fire.displayName);
            Assert.IsNotNull(again.Blocked(), "can't buy it twice for the same gun");
            // Other ammo replaces it
            Menus.Shop(p, Menus.ShopTab.Attachments, p.transform.position).Items.Find(i => i.Label == "Harpoon-Tip Rounds").OnPick();
            Assert.AreEqual(Element.None, p.EffGun.element, "the fire's gone");
            Assert.AreEqual(p.Gun.pierce + 1, p.EffGun.pierce, "harpoon tips go through one more");
            Assert.IsTrue(p.HasAttachment(p.Gun.id, "sharkgrip"));

            // Online: the host's snapshot lists them, for the proxies' models
            var state = NetPlayerState.From(p);
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) state.Write(w);
            ms.Position = 0;
            using var r = new BinaryReader(ms);
            var copy = NetPlayerState.Read(r);
            StringAssert.Contains("sharkgrip", copy.attach);
            StringAssert.Contains("harpoontip", copy.attach);

            // A drum on another gun comes loaded; that gun starts bare
            p.GiveGun("scattergun");
            yield return null;   // (the old gun model goes at the end of the frame)
            Assert.IsNull(p.AttachmentIn("scattergun", AttachmentSlot.Attachment));
            Assert.IsNull(FindDeep(p.transform, "AttGrip"), "the revolver's grip stays on the revolver");
            int mag = p.MagSize;
            Menus.Shop(p, Menus.ShopTab.Attachments, p.transform.position).Items.Find(i => i.Label == "Drum Magazine").OnPick();
            Assert.Greater(p.MagSize, mag);
            Assert.AreEqual(p.MagSize, p.Ammo, "the new drum comes loaded");
        }

        static Transform FindDeep(Transform t, string name)
        {
            foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c.name == name) return c;
            return null;
        }

        [Test]
        public void EveryAttachmentHasAModelAndASlot()
        {
            var db = GameDatabase.Load();
            Assert.GreaterOrEqual(db.attachments.FindAll(a => a.slot == AttachmentSlot.Attachment).Count, 8);
            Assert.GreaterOrEqual(db.attachments.FindAll(a => a.slot == AttachmentSlot.Ammo).Count, 5);
            foreach (AttachmentSlot slot in System.Enum.GetValues(typeof(AttachmentSlot)))
                Assert.IsTrue(db.attachments.Exists(a => a.slot == slot), $"something for the {slot}");
            foreach (var a in db.attachments) Assert.IsNotNull(Content.Model(a.model), $"{a.id}'s model {a.model}");
        }

        // ------------------------------------------------------------------ third person
        [UnityTest]
        public IEnumerator ThirdPersonAimsAtTheCrosshairAndWalksWhereYouLook()
        {
            var p = JoinPad(out var pad);
            yield return Frames(2);
            p.SwitchView();
            Assert.IsFalse(p.FirstPerson);
            p.Place(new Vector3(0, 0, 10));
            DebugTools.Freeze = true;
            var e = gm.Waves.SpawnExtra(gm.DB.Enemy("turtle"), new Vector3(0, 0, 20));
            p.SetLook(0, 4);
            yield return Frames(4);
            Assert.That(Vector3.Distance(p.Cam.transform.position, p.EyePos), Is.InRange(1.5f, 5f), "over the shoulder");
            // Put the crosshair on it (as a player would), then the aim should land on it
            var bounds = ModelFactory.Bounds(e.gameObject);
            var to = Quaternion.LookRotation(bounds.center - p.Cam.transform.position).eulerAngles;
            p.SetLook(to.y, to.x > 180 ? to.x - 360 : to.x);
            yield return Frames(4);
            bounds.Expand(.3f);
            Assert.IsTrue(bounds.Contains(p.AimPoint), $"the crosshair is on the creature ahead (aim {p.AimPoint}, creature {bounds.center})");

            // The stick walks relative to where you look: facing east, forward goes east
            DebugTools.Freeze = false;
            DebugMenu.KillAll();
            p.SetLook(90, 0);
            var start = p.transform.position;
            for (float t = 0; t < .5f; t += Time.unscaledDeltaTime) { InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0, 1) }); yield return null; }
            InputSystem.QueueStateEvent(pad, new GamepadState());
            var moved = p.transform.position - start;
            Assert.Greater(moved.x, .5f, "forward is the way you face");
            Assert.Less(Mathf.Abs(moved.z), moved.x * .5f);
        }
    }
}
