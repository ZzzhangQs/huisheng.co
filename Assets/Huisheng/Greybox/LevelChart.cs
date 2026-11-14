using System;
using System.Collections.Generic;
using UnityEngine;

namespace Huisheng.Greybox
{
    public enum PlantType { Basic, Reed, Lily }
    public enum ChallengeType { Note, LowBarrier, Pulse, ReedPulse, LilyPulse }

    [Serializable]
    public sealed class SeedPlacement
    {
        public string SeedID;
        public float BeatPosition;
        public int LaneIndex;
        public PlantType PlantType;

        public SeedPlacement(string id, float beat, int lane, PlantType plant)
        {
            SeedID = id;
            BeatPosition = beat;
            LaneIndex = lane;
            PlantType = plant;
        }
    }

    [Serializable]
    public sealed class SeedLayout
    {
        public string LevelID = "greybox";
        public float BPM = 120;
        public int Subdivisions = 2;
        public float TotalBeats = 30;
        public float OffsetSeconds;
        public float PerfectSeconds = .07f;
        public float GoodSeconds = .15f;
        public List<SeedPlacement> Seeds = new()
        {
            new("seed-1", 4, 1, PlantType.Basic),
            new("seed-2", 8, 0, PlantType.Reed),
            new("seed-3", 12, 2, PlantType.Lily),
            new("seed-4", 16, 1, PlantType.Reed),
            new("seed-5", 20, 0, PlantType.Lily),
            new("seed-6", 23, 2, PlantType.Basic)
        };
    }

    public readonly struct ChartEvent
    {
        public readonly string SeedID;
        public readonly PlantType PlantType;
        public readonly double Beat;
        public readonly int Lane;
        public readonly ChallengeType Type;
        public readonly double Spawn;
        public readonly double Arrival;
        public readonly float Speed;
        public readonly float Size;

        internal ChartEvent(SeedPlacement seed, double beat, int lane, ChallengeType type, double arrival,
            double spawn, float speed = 0, float size = 0)
        {
            SeedID = seed.SeedID;
            PlantType = seed.PlantType;
            Beat = beat;
            Lane = lane;
            Type = type;
            Arrival = arrival;
            Spawn = spawn;
            Speed = speed;
            Size = size;
        }
    }

    public sealed class LevelChart
    {
        public IReadOnlyList<ChartEvent> Music { get; }
        public IReadOnlyList<ChartEvent> Runner { get; }
        public IReadOnlyList<ChartEvent> Dodge { get; }
        public IReadOnlyList<string> Adjustments { get; }
        public double StageSpan { get; }
        public double TransitionSpan { get; }
        public float JumpLead { get; }

        internal LevelChart(List<ChartEvent> music, List<ChartEvent> runner, List<ChartEvent> dodge,
            List<string> adjustments, double stageSpan, double transitionSpan, float jumpLead)
        {
            Music = music.AsReadOnly();
            Runner = runner.AsReadOnly();
            Dodge = dodge.AsReadOnly();
            Adjustments = adjustments.AsReadOnly();
            StageSpan = stageSpan;
            TransitionSpan = transitionSpan;
            JumpLead = jumpLead;
        }

        public double Start(Stage stage) => (int)stage * StageSpan;
        public double End(Stage stage) => Start(stage) + StageSpan - TransitionSpan;
    }

    public static class LevelChartGenerator
    {
        const float SeedLead = 2.75f;
        const float RunnerLeadDistance = 6;
        const float DodgeLeadDistance = 6.25f;
        const double Safety = DemoRun.Tick * 3;

        public static bool TryGenerate(SeedLayout layout, DemoSettings settings, out LevelChart chart, out string error)
        {
            chart = null;
            error = Validate(layout, settings);
            if (error != null) return false;

            double secondsPerBeat = 60d / layout.BPM;
            double grid = secondsPerBeat / layout.Subdivisions;
            double end = layout.TotalBeats * secondsPerBeat + layout.OffsetSeconds;
            double stageSpan = Math.Ceiling((end + settings.transitionSeconds + Safety) / grid) * grid;
            double playEnd = stageSpan - settings.transitionSeconds;
            double beatShift = stageSpan / secondsPerBeat;

            var seeds = new List<SeedPlacement>(layout.Seeds);
            seeds.Sort((a, b) => a.BeatPosition != b.BeatPosition ? a.BeatPosition.CompareTo(b.BeatPosition) : a.LaneIndex.CompareTo(b.LaneIndex));
            var music = new List<ChartEvent>();
            var runner = new List<ChartEvent>();
            var dodge = new List<ChartEvent>();
            var adjustments = new List<string>();

            double apex = Math.Sqrt(2d * settings.jumpHeight / settings.gravity);
            double clearHalf = settings.jumpHeight > DemoRun.RunnerHeight
                ? Math.Sqrt(2d * (settings.jumpHeight - DemoRun.RunnerHeight) / settings.gravity) : 0;
            double runnerHalf = (DemoRun.RunnerWidth / 2 + DemoRun.PlayerRadius) / settings.runnerSpeed;
            double previousJump = double.NegativeInfinity;
            double previousDodgeEnd = 0;
            int reachable = 1 << 1;

            foreach (SeedPlacement seed in seeds)
            {
                double time = seed.BeatPosition * secondsPerBeat + layout.OffsetSeconds;
                music.Add(new ChartEvent(seed, seed.BeatPosition, seed.LaneIndex, ChallengeType.Note, time, Math.Max(0, time - SeedLead)));

                double jump = time - apex;
                double runnerSpawn = Math.Max(0, time - RunnerLeadDistance / settings.runnerSpeed);
                if (clearHalf >= runnerHalf + Safety && jump >= runnerSpawn + Safety
                    && time + runnerHalf + Safety <= playEnd && jump - previousJump >= 2 * apex + Safety)
                {
                    runner.Add(new ChartEvent(seed, seed.BeatPosition + beatShift, 1, ChallengeType.LowBarrier,
                        stageSpan + time, stageSpan + runnerSpawn, settings.runnerSpeed, DemoRun.RunnerWidth));
                    previousJump = jump;
                }
                else adjustments.Add($"跑酷跳过种子 {seed.SeedID}：起跳提示、越障高度或落地间隔不足。");

                int lane = seed.PlantType switch
                {
                    PlantType.Reed => (seed.LaneIndex + 1) % 3,
                    PlantType.Lily => 2 - seed.LaneIndex,
                    _ => seed.LaneIndex
                };
                float size = seed.PlantType switch { PlantType.Reed => .68f, PlantType.Lily => .42f, _ => DemoRun.BulletSize };
                ChallengeType type = seed.PlantType switch
                {
                    PlantType.Reed => ChallengeType.ReedPulse,
                    PlantType.Lily => ChallengeType.LilyPulse,
                    _ => ChallengeType.Pulse
                };
                double half = (size / 2 + DemoRun.PlayerRadius) / settings.bulletSpeed;
                double gap = time - half - previousDodgeEnd;
                int next = 0;
                if (gap >= Safety && time + half + Safety <= playEnd)
                {
                    // Hold a safe lane for the full collision interval. Switch only between intervals.
                    for (int from = 0; from < 3; from++)
                    for (int to = 0; to < 3; to++)
                        if ((reachable & (1 << from)) != 0 && to != lane
                            && Math.Abs(from - to) * (settings.laneChangeSeconds + Safety) <= gap)
                            next |= 1 << to;
                }

                if (next == 0)
                {
                    adjustments.Add($"闪避跳过种子 {seed.SeedID}：弹幕时间重叠或来不及换到安全轨道。");
                    continue;
                }

                dodge.Add(new ChartEvent(seed, seed.BeatPosition + 2 * beatShift, lane, type, 2 * stageSpan + time,
                    2 * stageSpan + Math.Max(0, time - DodgeLeadDistance / settings.bulletSpeed), settings.bulletSpeed, size));
                reachable = next;
                previousDodgeEnd = time + half;
            }

            chart = new LevelChart(music, runner, dodge, adjustments, stageSpan, settings.transitionSeconds, (float)apex);
            return true;
        }

        static string Validate(SeedLayout layout, DemoSettings settings)
        {
            if (layout == null || string.IsNullOrWhiteSpace(layout.LevelID)) return "请填写关卡 ID。";
            if (!InRange(layout.BPM, 20, 400)) return "每分钟拍数（BPM）必须在 20 至 400 之间。";
            if (layout.Subdivisions < 1 || layout.Subdivisions > 16) return "每拍细分必须在 1 至 16 之间。";
            if (!InRange(layout.TotalBeats, 1, 512)) return "总拍数必须在 1 至 512 之间。";
            if (!InRange(layout.OffsetSeconds, -2, 2)) return "时间偏移必须在 −2 至 2 秒之间。";
            if (!InRange(layout.PerfectSeconds, .001f, .5f) || !InRange(layout.GoodSeconds, layout.PerfectSeconds, .5f))
                return "节奏判定窗口必须满足：0 < 完美 ≤ 普通 ≤ 0.5 秒。";
            if (settings == null || settings.life < 1 || !InRange(settings.riverSpeed, .1f, 30)
                || !InRange(settings.growthSeconds, .1f, 30) || !InRange(settings.jumpHeight, .1f, 10)
                || !InRange(settings.gravity, .1f, 100) || !InRange(settings.runnerSpeed, .1f, 30)
                || !InRange(settings.bulletSpeed, .1f, 30) || !InRange(settings.laneChangeSeconds, .05f, 5)
                || !InRange(settings.transitionSeconds, .1f, 5)) return "玩法参数超出范围。";
            if (layout.Seeds == null) return "缺少种子列表。";
            if (layout.TotalBeats * 60d / layout.BPM + layout.OffsetSeconds <= 0) return "关卡结束时间早于开始时间。";

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var cells = new HashSet<(int, int)>();
            foreach (SeedPlacement seed in layout.Seeds)
            {
                if (seed == null || string.IsNullOrWhiteSpace(seed.SeedID)) return "每颗种子都需要一个 ID。";
                if (!ids.Add(seed.SeedID)) return $"种子 ID 重复：{seed.SeedID}。";
                if (seed.LaneIndex < 0 || seed.LaneIndex > 2) return $"种子 {seed.SeedID} 的轨道无效：{seed.LaneIndex}。";
                if (!Enum.IsDefined(typeof(PlantType), seed.PlantType)) return $"种子 {seed.SeedID} 的类型无效。";
                if (!InRange(seed.BeatPosition, 0, layout.TotalBeats) || seed.BeatPosition >= layout.TotalBeats
                    || seed.BeatPosition * 60d / layout.BPM + layout.OffsetSeconds < 0)
                    return $"种子 {seed.SeedID} 的节拍超出可玩范围。";
                double cell = seed.BeatPosition * (double)layout.Subdivisions;
                if (Math.Abs(cell - Math.Round(cell)) > .0001) return $"种子 {seed.SeedID} 未对齐节拍网格。";
                if (!cells.Add(((int)Math.Round(cell), seed.LaneIndex))) return $"种子 {seed.SeedID} 与另一颗种子占用同一格。";
            }
            return null;
        }

        static bool InRange(float value, float min, float max) => value >= min && value <= max;
    }
}
