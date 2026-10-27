// TideWall 3D — the Heart (stage 5): the Heart of the Tide in its shell cradle, an empty hollow beside it, and five
// petal-seats around them. Come close and the Tidemother rises. When she falls, choose: strike the Heart, or (with all
// five Spiral Shards) lay down the Crest. What the symbols here mean is in docs/STORY.md; nothing in game says it.
using System.IO;
using UnityEngine;

namespace TideWall
{
    public class HeartAltar : Objective
    {
        public enum Phase : byte { Waiting, Fight, Choosing, Chosen }

        [SerializeField] Transform heart, bossSpawn;
        [SerializeField] Light heartLight;
        [SerializeField] Renderer[] seats = new Renderer[5];
        [SerializeField] HeartChoice strike, lay;
        public Phase State { get; private set; }
        Enemy boss;
        float pulse;
        int hostShards;

        /// <summary>A shard picked up during this stage counts at once.</summary>
        public void ShardFound(int stage) { hostShards = Legacy.Load().ShardCount; if (stage < seats.Length && seats[stage]) seats[stage].enabled = true; }

        public static HeartAltar Create(Transform parent, Vector3 pos, Vector3 bossPos)
        {
            var go = new GameObject("The Heart");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var a = go.AddComponent<HeartAltar>();
            var cradle = ModelFactory.Spawn("HeartCradle", go.transform, new Vector3(-2.2f, 0, 0), 1, colliders: true, triggers: false);
            var hollow = ModelFactory.Spawn("HeartCradle", go.transform, new Vector3(2.2f, 0, 0), 1, colliders: true, triggers: false);
            hollow.name = "Hollow";
            a.heart = ModelFactory.Spawn("Heart", go.transform, new Vector3(-2.2f, 1.6f, 0)).root.transform;
            Glyphs.Decal(a.heart, "spiral", new Vector3(0, 0, -.82f), Quaternion.identity, 1.2f, new Color(.7f, 1f, .8f, .9f), glow: true);
            // The hollow is the shape of Saltmarrow's core; the petals say who took what was in it
            Glyphs.Decal(hollow.root.transform, "petals", new Vector3(0, .05f, 0), Quaternion.Euler(90, 0, 0), 2.6f, new Color(.95f, .9f, .75f, .5f));
            a.heartLight = new GameObject("Heart light").AddComponent<Light>();
            a.heartLight.transform.SetParent(a.heart, false);
            a.heartLight.type = LightType.Point; a.heartLight.range = 22; a.heartLight.color = new Color(.45f, 1f, .6f); a.heartLight.shadows = LightShadows.None;
            for (int i = 0; i < 5; i++)
            {
                float ang = Mathf.PI / 2 + i * Mathf.PI * 2 / 5;
                var seatPos = new Vector3(Mathf.Cos(ang) * 7.5f, 0, Mathf.Sin(ang) * 7.5f);
                var stone = ModelFactory.Spawn("PetalSeat", go.transform, seatPos, 1, colliders: true, triggers: false);
                stone.root.transform.localRotation = Quaternion.LookRotation(-seatPos);
                a.seats[i] = Glyphs.Decal(stone.root.transform, "crest", new Vector3(0, 1.21f, 0), Quaternion.Euler(90, 0, 72 * i), .9f, new Color(1f, .85f, .45f, .9f), glow: true).GetComponent<Renderer>();
            }
            a.bossSpawn = new GameObject("Boss rises here").transform;
            a.bossSpawn.SetParent(go.transform, false);
            a.bossSpawn.position = bossPos;
            a.strike = HeartChoice.Create(go.transform, new Vector3(-2.2f, 0, -2.8f), a, "strike", "tally");
            a.lay = HeartChoice.Create(go.transform, new Vector3(2.2f, 0, -2.8f), a, "spiral", "crest");
            return a;
        }

        public override void Activate(Expedition e)
        {
            base.Activate(e);
            // The seats light for the shards this machine has found
            var legacy = Legacy.Load();
            hostShards = legacy.ShardCount;
            for (int i = 0; i < seats.Length; i++) if (seats[i]) seats[i].enabled = legacy.HasShard(i);
        }

        public override void Tick(float dt)
        {
            switch (State)
            {
                case Phase.Waiting:
                    if (NearestPlayer(transform.position, 34) != null)
                    {
                        State = Phase.Fight;
                        var gm = GameManager.I;
                        var def = gm.DB.Enemy("tidemother");
                        if (def != null) boss = gm.Waves.SpawnExtra(def, bossSpawn.position);
                        gm.Banner("THE TIDEMOTHER", "");
                        Sfx.Play("horn");
                        foreach (var p in gm.Players.All) p.Shake(.6f);
                    }
                    break;
                case Phase.Fight:
                    if (boss == null || boss.Dead)
                    {
                        State = Phase.Choosing;
                        GameManager.I.Banner("THE HEART OF THE TIDE", "");
                        Sfx.Play("bell");
                    }
                    break;
            }
        }

        public Enemy Boss => boss;

        /// <summary>Which choices can be made now (the second needs all five shards, on the host's record).</summary>
        public bool Offers(string ending)
        {
            if (State != Phase.Choosing) return false;
            var def = GameManager.I.Campaign.Ending(ending);
            return def != null && (!def.needsShards || hostShards >= GameManager.I.Campaign.shardCount);
        }

        public void Choose(string ending)
        {
            if (Net.IsClient || !Offers(ending)) return;
            State = Phase.Chosen;
            Finish();
            expedition.Finish(ending);
        }

        public override string Progress => State switch
        {
            Phase.Waiting => "find the Heart",
            Phase.Fight => boss != null ? $"the Tidemother {Mathf.CeilToInt(boss.Hp / Mathf.Max(1, boss.MaxHp) * 100)}%" : "the Tidemother",
            Phase.Choosing => "choose",
            _ => null,
        };

        void Update()
        {
            pulse += Time.deltaTime;
            float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(pulse * 1.3f)), 8);   // a slow heartbeat
            if (heartLight) heartLight.intensity = State == Phase.Chosen ? Mathf.MoveTowards(heartLight.intensity, 0, Time.deltaTime * 4) : 4 + beat * 10;
            if (heart) heart.localScale = Vector3.one * (1 + beat * .06f);
        }

        public override void WriteNet(BinaryWriter w) { base.WriteNet(w); w.Write((byte)State); }
        public override void ReadNet(BinaryReader r) { base.ReadNet(r); State = (Phase)r.ReadByte(); }
    }
}
