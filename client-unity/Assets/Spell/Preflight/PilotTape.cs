using System;
using System.Collections.Generic;
using System.IO;

namespace Spell.Client.Diagnostics
{
    public enum PilotEvent { Calibration = 1, Tracking, Modules, Connection, Health, Cast, Impact, Phase, Pose }
    public interface IPilotEventSink { void Event(PilotEvent kind, int value, int detail = 0); }

    // Times are elapsed milliseconds. Pose (only after explicit authorization) uses arena-local centimetres.
    [Serializable] public sealed class TapeItem { public int ms, kind, value, detail, eastCm, upCm, northCm; }
    [Serializable] public sealed class TapePage { public int version = 1, pageNumber, pages, dropped; public TapeItem[] events; }

    public sealed class PilotTape
    {
        public const int Capacity = 256, PageSize = 12;
        const int MaxTime = 7 * 86400000;
        readonly Queue<TapeItem> items = new Queue<TapeItem>();
        readonly bool capturePose;
        readonly Func<long> milliseconds;
        readonly long began;
        int dropped, lastTime;
        public int Count => items.Count;
        public PilotTape(bool capturePose, Func<long> milliseconds)
        {
            this.capturePose = capturePose;
            this.milliseconds = milliseconds;
            began = milliseconds();
        }
        public void Event(PilotEvent kind, int value, int detail = 0)
        {
            if (kind < PilotEvent.Calibration || kind >= PilotEvent.Pose) return;
            Add(new TapeItem { kind = (int)kind, value = value, detail = detail });
        }
        public void Pose(double east, double up, double north, double sigma)
        {
            if (!capturePose || !Bounded(east) || !Bounded(up) || !Bounded(north) || !(sigma >= 0 && sigma <= 1000)) return;
            Add(new TapeItem { kind = (int)PilotEvent.Pose, value = (int)Math.Round(sigma*100),
                eastCm = (int)Math.Round(east*100), upCm = (int)Math.Round(up*100), northCm = (int)Math.Round(north*100) });
        }
        static bool Bounded(double coordinate) => coordinate >= -10000 && coordinate <= 10000;
        void Add(TapeItem item)
        {
            lastTime = Math.Max(lastTime, (int)Math.Max(0, Math.Min(MaxTime, milliseconds()-began)));
            item.ms = lastTime;
            if (items.Count == Capacity) { items.Dequeue(); if (dropped < int.MaxValue) dropped++; }
            items.Enqueue(item);
        }
        public void Write(BinaryWriter writer)
        {
            writer.Write(dropped);
            writer.Write(Count);
            foreach (var e in items)
            {
                writer.Write(e.ms); writer.Write(e.kind); writer.Write(e.value); writer.Write(e.detail);
                writer.Write(e.eastCm); writer.Write(e.upCm); writer.Write(e.northCm);
            }
        }
        public static TapePage[] Read(BinaryReader reader)
        {
            int loss = reader.ReadInt32(), count = reader.ReadInt32();
            if (count < 0 || count > Capacity || loss < 0) throw new InvalidDataException("Invalid event tape.");
            int pages = (count + PageSize - 1) / PageSize, previous = -1;
            var result = new TapePage[pages];
            for (int i = 0; i < pages; i++)
            {
                var page = new TapePage { pageNumber=i, pages=pages, dropped=loss, events=new TapeItem[Math.Min(PageSize,count-i*PageSize)] };
                result[i] = page;
                for (int j = 0; j < page.events.Length; j++)
                {
                    var item = new TapeItem { ms=reader.ReadInt32(), kind=reader.ReadInt32(), value=reader.ReadInt32(),
                        detail=reader.ReadInt32(), eastCm=reader.ReadInt32(), upCm=reader.ReadInt32(), northCm=reader.ReadInt32() };
                    if (item.ms < previous || item.ms > MaxTime || item.kind < 1 || item.kind > 9)
                        throw new InvalidDataException("Invalid event time or kind.");
                    page.events[j] = item;
                    previous = item.ms;
                }
            }
            return result;
        }
    }
}
