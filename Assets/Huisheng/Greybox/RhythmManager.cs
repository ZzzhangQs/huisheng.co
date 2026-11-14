using System;
using System.Collections.Generic;

namespace Huisheng.Greybox
{
    public sealed class RhythmManager
    {
        readonly IReadOnlyList<ChartEvent> music;
        int nextEvent;
        long nextSubdivision;

        public double BPM { get; }
        public int Subdivisions { get; }
        public double OffsetSeconds { get; }
        public double PerfectSeconds { get; }
        public double GoodSeconds { get; }
        public double PlaybackTime { get; private set; }
        public double Beat => (PlaybackTime - OffsetSeconds) * BPM / 60;
        public double SecondsPerBeat => 60 / BPM;
        public bool Silent => AudioReceiver == null;
        public Action<ChartEvent, double> AudioReceiver { get; set; }
        public event Action<ChartEvent> MusicEvent;
        public event Action<long, double> Subdivision;

        internal RhythmManager(SeedLayout layout, IReadOnlyList<ChartEvent> music)
        {
            BPM = layout.BPM;
            Subdivisions = layout.Subdivisions;
            OffsetSeconds = layout.OffsetSeconds;
            PerfectSeconds = layout.PerfectSeconds;
            GoodSeconds = layout.GoodSeconds;
            this.music = music;
        }

        public Outcome Judge(double errorSeconds)
        {
            double error = Math.Abs(errorSeconds);
            return error <= PerfectSeconds ? Outcome.Perfect : error <= GoodSeconds ? Outcome.Good : Outcome.Miss;
        }

        internal void Advance(double dt)
        {
            PlaybackTime += dt;
            while (nextEvent < music.Count && music[nextEvent].Arrival <= PlaybackTime)
            {
                ChartEvent note = music[nextEvent++];
                MusicEvent?.Invoke(note);
                AudioReceiver?.Invoke(note, note.Arrival);
            }

            double time = OffsetSeconds + nextSubdivision * SecondsPerBeat / Subdivisions;
            while (time <= PlaybackTime)
            {
                if (time >= 0) Subdivision?.Invoke(nextSubdivision, time);
                time = OffsetSeconds + ++nextSubdivision * SecondsPerBeat / Subdivisions;
            }
        }
    }
}
