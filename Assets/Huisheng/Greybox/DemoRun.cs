using System;
using System.Collections.Generic;
using UnityEngine;

namespace Huisheng.Greybox
{
    public enum Stage { River, Runner, Dodge }
    public enum Outcome { Pending, Perfect, Good, Hit, Miss, Avoided }
    public enum RunnerState { Grounded, Airborne, Hit, Dead }

    [Serializable]
    public sealed class DemoSettings
    {
        [Min(1)] public int life = 5;
        [Min(.1f)] public float growthSeconds = 3;
        [Min(.1f)] public float riverSpeed = 4;
        [Min(.1f)] public float jumpHeight = 2;
        [Min(.1f)] public float gravity = 18;
        [Min(.1f)] public float runnerSpeed = 5;
        [Min(.1f)] public float bulletSpeed = 5;
        [Min(.05f)] public float laneChangeSeconds = .2f;
        [Min(.1f)] public float transitionSeconds = .6f;
    }

    public sealed class Challenge
    {
        public readonly ChartEvent Event;
        public readonly float Arrival;
        public readonly int Lane;
        public Outcome Result { get; internal set; }
        public Outcome JumpRating { get; internal set; }
        public bool JumpRated { get; internal set; }
        public double ActivatedAt { get; internal set; }
        public bool GrowthHit { get; internal set; }

        public Challenge(ChartEvent chartEvent, double stageStart)
        {
            Event = chartEvent;
            Arrival = (float)(chartEvent.Arrival - stageStart);
            Lane = chartEvent.Lane;
        }
    }

    public readonly struct SeedActivation
    {
        public readonly string SeedID;
        public readonly PlantType PlantType;
        public readonly double BeatPosition;
        public readonly int LaneIndex;
        public readonly Outcome TriggerQuality;

        public SeedActivation(Challenge seed)
        {
            SeedID = seed.Event.SeedID;
            PlantType = seed.Event.PlantType;
            BeatPosition = seed.Event.Beat;
            LaneIndex = seed.Lane;
            TriggerQuality = seed.Result;
        }
    }

    public sealed class DemoRun
    {
        public const float LaneWidth = 2;
        public const float PlayerRadius = .19f;
        public const float RunnerWidth = .56f;
        public const float RunnerHeight = .6f;
        public const float BulletSize = .54f;
        public const float SeedInner = .19f;
        public const float SeedOuter = .68f;
        public const float SeedPerfect = .47f;
        public const float GrowthSpeed = .85f;
        public const float Tick = 1f / 120;

        readonly DemoSettings settings;
        readonly List<SeedActivation> performance = new();
        readonly LevelChart chart;
        public RhythmManager Rhythm { get; }
        double transitionEnds => chart.Start(Stage) + chart.StageSpan;
        double lastHit = -10;
        float verticalVelocity;
        float laneFrom;
        float laneProgress = 1;

        public readonly Challenge[] Seeds;
        public readonly Challenge[] Obstacles;
        public readonly Challenge[] Bullets;

        public IReadOnlyList<SeedActivation> Performance => performance;
        public event Action<SeedActivation> SeedActivated;
        public Stage Stage { get; private set; }
        public double Clock => Rhythm.PlaybackTime;
        public float StageTime => (float)(Clock - chart.Start(Stage));
        public bool Transitioning { get; private set; }
        public float TransitionProgress => Mathf.Clamp01(1 - (float)(transitionEnds - Clock) / settings.transitionSeconds);
        public bool Finished { get; private set; }
        public bool Failed => Life == 0;
        public int Life { get; private set; }
        public int Score { get; private set; }
        public int Lane { get; private set; } = 1;
        public float X { get; private set; }
        public float Height { get; private set; }
        public bool RecentHit => Clock - lastHit < 1.2;
        public bool HitFlash => Clock - lastHit < .28;
        public Outcome LastResult { get; private set; }
        public RunnerState JumpState => Failed ? RunnerState.Dead : HitFlash ? RunnerState.Hit
            : Height > 0 || verticalVelocity > 0 ? RunnerState.Airborne : RunnerState.Grounded;

        public DemoRun(DemoSettings settings, SeedLayout layout, LevelChart chart)
        {
            this.settings = settings;
            this.chart = chart;
            Rhythm = new RhythmManager(layout, chart.Music);
            Seeds = CreateChallenges(chart.Music, Stage.River);
            Obstacles = CreateChallenges(chart.Runner, Stage.Runner);
            Bullets = CreateChallenges(chart.Dodge, Stage.Dodge);
            Life = settings.life;
        }

        Challenge[] CreateChallenges(IReadOnlyList<ChartEvent> events, Stage stage)
        {
            var challenges = new Challenge[events.Count];
            for (int i = 0; i < challenges.Length; i++)
                challenges[i] = new Challenge(events[i], chart.Start(stage));
            return challenges;
        }

        public static float LaneX(int lane) => (lane - 1) * LaneWidth;
        public float Growth(Challenge seed) => Mathf.Clamp01((float)(Clock - seed.ActivatedAt) / settings.growthSeconds);
        public float GrowthRadius(Challenge seed) => Mathf.Lerp(SeedInner, .7f, Growth(seed));
        public float GrowthDepth(Challenge seed) => -(float)(Clock - seed.ActivatedAt) * GrowthSpeed;

        public void Step(float dt, float horizontal, bool jump, int laneStep)
        {
            if (Failed || Finished) return;

            float previous = StageTime;
            Rhythm.Advance(dt);
            if (Transitioning)
            {
                if (Clock >= transitionEnds)
                {
                    Stage++;
                    Transitioning = false;
                    X = Height = verticalVelocity = 0;
                    Lane = 1;
                    laneProgress = 1;
                    LastResult = Outcome.Pending;
                }
                return;
            }

            switch (Stage)
            {
                case Stage.River: StepRiver(dt, previous, horizontal); break;
                case Stage.Runner: StepRunner(dt, previous, jump); break;
                case Stage.Dodge: StepDodge(dt, previous, laneStep); break;
            }

            if (Failed) return;
            if (Clock < chart.End(Stage)) return;
            if (Stage == Stage.Dodge)
            {
                Finished = true;
                return;
            }

            Transitioning = true;
        }

        void StepRiver(float dt, float previous, float horizontal)
        {
            float oldX = X;
            X = Mathf.Clamp(X + horizontal * settings.riverSpeed * dt, -3, 3);
            for (int i = 0; i < Seeds.Length && !Failed; i++)
            {
                Challenge seed = Seeds[i];
                float center = LaneX(seed.Lane);
                if (seed.Result == Outcome.Pending && StageTime >= seed.Arrival)
                {
                    float contactX = Mathf.Lerp(oldX, X, Mathf.InverseLerp(previous, StageTime, seed.Arrival));
                    float distance = Mathf.Abs(contactX - center);
                    seed.Result = distance <= SeedInner + PlayerRadius ? Outcome.Hit
                        : distance < SeedPerfect + PlayerRadius ? Outcome.Good
                        : distance <= SeedOuter + PlayerRadius ? Outcome.Perfect : Outcome.Miss;
                    LastResult = seed.Result;
                    if (seed.Result == Outcome.Hit) Damage();
                    if (seed.Result == Outcome.Perfect || seed.Result == Outcome.Good)
                    {
                        seed.ActivatedAt = seed.Event.Arrival;
                        Score += seed.Result == Outcome.Perfect ? 100 : 50;
                        var activation = new SeedActivation(seed);
                        performance.Add(activation);
                        SeedActivated?.Invoke(activation);
                    }
                }

                if (!seed.GrowthHit && (seed.Result == Outcome.Perfect || seed.Result == Outcome.Good))
                {
                    float radius = GrowthRadius(seed) + PlayerRadius;
                    if (new Vector2(X - center, GrowthDepth(seed)).sqrMagnitude <= radius * radius)
                    {
                        seed.GrowthHit = true;
                        Damage();
                    }
                }
            }
        }

        void StepRunner(float dt, float previous, bool jump)
        {
            if (jump && Height == 0 && verticalVelocity == 0)
            {
                verticalVelocity = Mathf.Sqrt(2 * settings.gravity * settings.jumpHeight);
                RateJump(previous);
            }

            float oldHeight = Height;
            Height = Mathf.Max(0, Height + verticalVelocity * dt - .5f * settings.gravity * dt * dt);
            verticalVelocity = Height > 0 ? verticalVelocity - settings.gravity * dt : 0;

            float halfTime = (RunnerWidth / 2 + PlayerRadius) / settings.runnerSpeed;
            foreach (Challenge obstacle in Obstacles)
            {
                if (!obstacle.JumpRated && StageTime > obstacle.Arrival - chart.JumpLead + Rhythm.GoodSeconds)
                {
                    obstacle.JumpRated = true;
                    obstacle.JumpRating = Outcome.Miss;
                    LastResult = Outcome.Miss;
                }

                if (obstacle.Result != Outcome.Pending || Failed) continue;
                float overlapStart = Mathf.Max(previous, obstacle.Arrival - halfTime);
                float overlapEnd = Mathf.Min(StageTime, obstacle.Arrival + halfTime);
                if (overlapStart <= overlapEnd)
                {
                    float firstHeight = Mathf.Lerp(oldHeight, Height, Mathf.InverseLerp(previous, StageTime, overlapStart));
                    float lastHeight = Mathf.Lerp(oldHeight, Height, Mathf.InverseLerp(previous, StageTime, overlapEnd));
                    if (Mathf.Min(firstHeight, lastHeight) <= RunnerHeight)
                    {
                        obstacle.Result = Outcome.Hit;
                        Damage();
                        continue;
                    }
                }
                if (StageTime >= obstacle.Arrival + halfTime) obstacle.Result = Outcome.Avoided;
            }
        }

        void RateJump(float time)
        {
            Challenge best = null;
            double error = Rhythm.GoodSeconds;
            foreach (Challenge obstacle in Obstacles)
            {
                double difference = Math.Abs(time - (obstacle.Arrival - chart.JumpLead));
                if (obstacle.JumpRated || difference > error) continue;
                best = obstacle;
                error = difference;
            }

            LastResult = best == null ? Outcome.Miss : Rhythm.Judge(error);
            if (best == null) return;
            best.JumpRated = true;
            best.JumpRating = LastResult;
            Score += LastResult == Outcome.Perfect ? 100 : 50;
        }

        void StepDodge(float dt, float previous, int laneStep)
        {
            float oldX = X;
            if (laneStep != 0 && laneProgress >= 1)
            {
                int target = Mathf.Clamp(Lane + laneStep, 0, 2);
                if (target != Lane)
                {
                    laneFrom = X;
                    Lane = target;
                    laneProgress = 0;
                }
            }

            laneProgress = Mathf.Min(1, laneProgress + dt / settings.laneChangeSeconds);
            X = Mathf.Lerp(laneFrom, LaneX(Lane), laneProgress);
            foreach (Challenge bullet in Bullets)
            {
                if (bullet.Result != Outcome.Pending || Failed) continue;
                float hitRadius = bullet.Event.Size / 2 + PlayerRadius;
                float halfTime = hitRadius / bullet.Event.Speed;
                float overlapStart = Mathf.Max(previous, bullet.Arrival - halfTime);
                float overlapEnd = Mathf.Min(StageTime, bullet.Arrival + halfTime);
                if (overlapStart <= overlapEnd)
                {
                    float firstX = Mathf.Lerp(oldX, X, Mathf.InverseLerp(previous, StageTime, overlapStart));
                    float lastX = Mathf.Lerp(oldX, X, Mathf.InverseLerp(previous, StageTime, overlapEnd));
                    float center = LaneX(bullet.Lane);
                    float distance = Mathf.Abs(Mathf.Clamp(center, Mathf.Min(firstX, lastX), Mathf.Max(firstX, lastX)) - center);
                    if (distance <= hitRadius)
                    {
                        bullet.Result = Outcome.Hit;
                        Damage();
                        continue;
                    }
                }

                if (StageTime >= bullet.Arrival + halfTime)
                {
                    bullet.Result = Outcome.Avoided;
                    LastResult = Outcome.Avoided;
                    Score += 25;
                }
            }
        }

        void Damage()
        {
            Life--;
            lastHit = Clock;
            LastResult = Outcome.Hit;
        }
    }
}
