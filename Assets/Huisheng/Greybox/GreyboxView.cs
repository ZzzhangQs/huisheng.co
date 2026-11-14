using UnityEngine;

namespace Huisheng.Greybox
{
    sealed class GreyboxView
    {
        const float RiverY = -2.65f;
        const float GroundY = -2.45f;
        const float RunnerX = -3;
        const float DodgeY = -3;
        readonly Sprite tile;
        readonly GameObject root;
        readonly Transform[] layouts = new Transform[3];
        readonly SpriteRenderer player;
        readonly SpriteRenderer ground;
        readonly SpriteRenderer[] seedOuter;
        readonly SpriteRenderer[] seedInner;
        readonly SpriteRenderer[] obstacles;
        readonly SpriteRenderer[] bullets;
        static readonly Color Gray = Color.white * .52f;
        static readonly Color Dark = Color.white * .28f;

        public GreyboxView(Transform parent, LevelChart chart)
        {
            root = new GameObject("Greybox playfield");
            root.transform.SetParent(parent, false);
            parent = root.transform;
            seedOuter = new SpriteRenderer[chart.Music.Count];
            seedInner = new SpriteRenderer[chart.Music.Count];
            obstacles = new SpriteRenderer[chart.Runner.Count];
            bullets = new SpriteRenderer[chart.Dodge.Count];
            tile = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width,
                Texture2D.whiteTexture.height), Vector2.one / 2, Texture2D.whiteTexture.width);
            for (int i = 0; i < layouts.Length; i++)
            {
                layouts[i] = new GameObject(((Stage)i).ToString()).transform;
                layouts[i].SetParent(parent, false);
            }

            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * DemoRun.LaneWidth;
                Line(layouts[0], new Vector2(x, RiverY), new Vector2(x * .25f, 2.4f));
                Line(layouts[2], new Vector2(x, -3.6f), new Vector2(x, 3.25f));
            }
            Line(layouts[0], new Vector2(-3, RiverY), new Vector2(3, RiverY));
            Line(layouts[2], new Vector2(-3, DodgeY), new Vector2(3, DodgeY));
            ground = Block(layouts[1], "Ground", 0, Gray);
            for (int i = 0; i < seedOuter.Length; i++)
            {
                seedOuter[i] = Block(layouts[0], $"Seed {i + 1} outer", 1, Gray);
                seedInner[i] = Block(layouts[0], $"Seed {i + 1} inner", 2, Dark);
            }
            for (int i = 0; i < obstacles.Length; i++) obstacles[i] = Block(layouts[1], $"Obstacle {i + 1}", 1, Color.white);
            for (int i = 0; i < bullets.Length; i++) bullets[i] = Block(layouts[2], $"Bullet {i + 1}", 1, Color.white);
            player = Block(parent, "Water drop", 5, Color.white);
            player.transform.localRotation = Quaternion.Euler(0, 0, 45);
        }

        public void Draw(DemoRun run, float halfWidth)
        {
            for (int i = 0; i < layouts.Length; i++)
                layouts[i].gameObject.SetActive(!run.Transitioning && i == (int)run.Stage);

            var position = run.Stage switch
            {
                Stage.River => new Vector2(run.X, RiverY),
                Stage.Runner => new Vector2(RunnerX, GroundY + run.Height + DemoRun.PlayerRadius),
                _ => new Vector2(run.X, DodgeY)
            };
            if (run.Transitioning)
            {
                var target = run.Stage == Stage.River ? new Vector2(RunnerX, GroundY + DemoRun.PlayerRadius) : new Vector2(0, DodgeY);
                position = Vector2.Lerp(position, target, run.TransitionProgress);
            }

            player.color = run.Failed || run.HitFlash ? Gray : Color.white;
            Place(player, position, Vector2.one * (DemoRun.PlayerRadius * 1.4142f));
            if (run.Transitioning) return;

            switch (run.Stage)
            {
                case Stage.River:
                    for (int i = 0; i < run.Seeds.Length; i++) DrawSeed(run, i);
                    break;
                case Stage.Runner:
                    Place(ground, new Vector2(0, GroundY - .04f), new Vector2(2 * halfWidth, .08f));
                    for (int i = 0; i < run.Obstacles.Length; i++)
                    {
                        Challenge obstacle = run.Obstacles[i];
                        float x = RunnerX + (float)((obstacle.Event.Arrival - run.Clock) * obstacle.Event.Speed);
                        obstacles[i].enabled = run.Clock >= obstacle.Event.Spawn && x < halfWidth + DemoRun.RunnerWidth && x > -halfWidth - DemoRun.RunnerWidth;
                        Place(obstacles[i], new Vector2(x, GroundY + DemoRun.RunnerHeight / 2),
                            new Vector2(DemoRun.RunnerWidth, DemoRun.RunnerHeight));
                    }
                    break;
                case Stage.Dodge:
                    for (int i = 0; i < run.Bullets.Length; i++)
                    {
                        Challenge bullet = run.Bullets[i];
                        float y = DodgeY + (float)((bullet.Event.Arrival - run.Clock) * bullet.Event.Speed);
                        bullets[i].enabled = run.Clock >= bullet.Event.Spawn && y >= -3.9f && y <= 3.25f;
                        Place(bullets[i], new Vector2(DemoRun.LaneX(bullet.Lane), y), Vector2.one * bullet.Event.Size);
                    }
                    break;
            }
        }

        void DrawSeed(DemoRun run, int i)
        {
            Challenge seed = run.Seeds[i];
            float remaining = seed.Arrival - run.StageTime;
            bool pending = seed.Result == Outcome.Pending;
            bool active = seed.Result == Outcome.Perfect || seed.Result == Outcome.Good;
            seedOuter[i].enabled = pending && run.Clock >= seed.Event.Spawn;
            seedInner[i].enabled = seedOuter[i].enabled || active;
            float x = DemoRun.LaneX(seed.Lane);
            if (active)
            {
                Place(seedInner[i], new Vector2(x, RiverY + run.GrowthDepth(seed)),
                    Vector2.one * (2 * run.GrowthRadius(seed)));
                return;
            }

            float depth = Mathf.Clamp01(remaining / 2.75f);
            float scale = Mathf.Lerp(1, .25f, depth);
            var pos = new Vector2(x * scale, Mathf.Lerp(RiverY, 2.4f, depth));
            Place(seedOuter[i], pos, new Vector2(DemoRun.SeedOuter * 2, .43f) * scale);
            Place(seedInner[i], pos, Vector2.one * (DemoRun.SeedInner * 2 * scale));
        }

        void Line(Transform parent, Vector2 a, Vector2 b)
        {
            var line = Block(parent, "Track line", 0, Dark);
            Place(line, (a + b) / 2, new Vector2(Vector2.Distance(a, b), .035f));
            line.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
        }

        SpriteRenderer Block(Transform parent, string name, int order, Color shade)
        {
            var block = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            block.transform.SetParent(parent, false);
            block.sprite = tile;
            block.sortingOrder = order;
            shade.a = 1;
            block.color = shade;
            return block;
        }

        static void Place(SpriteRenderer block, Vector2 position, Vector2 size)
        {
            block.transform.localPosition = new Vector3(position.x, position.y, 10);
            block.transform.localScale = new Vector3(size.x, size.y, 1);
        }

        public void SetVisible(bool visible) => root.SetActive(visible);

        public void Dispose()
        {
            if (root != null)
            {
                root.SetActive(false);
                Object.Destroy(root);
            }
            if (tile != null) Object.Destroy(tile);
        }
    }
}
