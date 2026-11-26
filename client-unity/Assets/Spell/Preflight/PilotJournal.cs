using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Spell.Client.Diagnostics
{
    public enum Measure { GnssFix, VisualFrame, Estimate, SentPose, TerrainBytes, TerrainRequest, Thermal }
    public interface IMeasurementSink { void Record(Measure kind, int value = 1); }

    [Serializable]
    public sealed class PilotCounters
    {
        public int version = 1, gnssFixes, visualFrames, estimates, sentPoses, terrainBytes, terrainRequests;
        public int peakThermalStatus = -1;
    }

    // One atomic local snapshot per seat. Journal format 2 retains the original version 1 counter API.
    public sealed class PilotJournal : IMeasurementSink, IPilotEventSink
    {
        readonly string directory;
        string active, permit;
        PilotCounters counts;
        PilotTape tape;
        readonly bool approvedPoseCapture;
        public bool Recording => active != null;
        public PilotJournal(string directory, bool approvedPoseCapture = false)
        {
            this.directory = directory;
            this.approvedPoseCapture = approvedPoseCapture;
            Directory.CreateDirectory(directory);
        }
        public void Begin(string batchPermit)
        {
            End();
            if (string.IsNullOrEmpty(batchPermit) || batchPermit.Length > 2048) return;
            if (Directory.GetFiles(directory, "*.batch").Length >= 64) return;
            active = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".batch");
            permit = batchPermit;
            counts = new PilotCounters();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            tape = new PilotTape(approvedPoseCapture, () => clock.ElapsedMilliseconds);
            Flush();
        }
        public void Event(PilotEvent kind, int value, int detail = 0) => tape?.Event(kind, value, detail);
        public void Pose(double east, double up, double north, double sigma) => tape?.Pose(east, up, north, sigma);
        public void Record(Measure kind, int value = 1)
        {
            if (!Recording || value < 0) return;
            switch (kind)
            {
                case Measure.GnssFix: counts.gnssFixes = Add(counts.gnssFixes, value); break;
                case Measure.VisualFrame: counts.visualFrames = Add(counts.visualFrames, value); break;
                case Measure.Estimate: counts.estimates = Add(counts.estimates, value); break;
                case Measure.SentPose: counts.sentPoses = Math.Min(counts.estimates, Add(counts.sentPoses, value)); break;
                case Measure.TerrainBytes: counts.terrainBytes = Add(counts.terrainBytes, value); break;
                case Measure.TerrainRequest: counts.terrainRequests = Add(counts.terrainRequests, value); break;
                case Measure.Thermal: if (value <= 6) counts.peakThermalStatus = Math.Max(counts.peakThermalStatus, value); break;
            }
        }
        static int Add(int old, int value) => (int)Math.Min(50_000_000L, (long)old + value);

        public void Flush()
        {
            if (!Recording) return;
            string temp = active + ".tmp";
            using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(file, Encoding.UTF8))
            {
                writer.Write(2); writer.Write(permit);
                writer.Write(counts.gnssFixes); writer.Write(counts.visualFrames); writer.Write(counts.estimates);
                writer.Write(counts.sentPoses); writer.Write(counts.terrainBytes); writer.Write(counts.terrainRequests);
                writer.Write(counts.peakThermalStatus); tape.Write(writer); writer.Flush(); file.Flush(true);
            }
            if (File.Exists(active)) File.Replace(temp, active, null); else File.Move(temp, active);
        }
        public void End()
        {
            Flush();
            active = permit = null;
            counts = null;
            tape = null;
        }
        public string[] Pending(DateTime nowUtc)
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                if (file == active) continue;
                if (file.EndsWith(".tmp", StringComparison.Ordinal) || File.GetLastWriteTimeUtc(file) < nowUtc.AddHours(-48))
                    File.Delete(file);
            }
            return Directory.GetFiles(directory, "*.batch").Where(file => file != active).OrderBy(File.GetLastWriteTimeUtc).ToArray();
        }
        public static PilotCounters Read(string file, out string uploadPermit) => Read(file, out uploadPermit, out _);
        public static PilotCounters Read(string file, out string uploadPermit, out TapePage[] pages)
        {
            if (new FileInfo(file).Length > 16384) throw new InvalidDataException("Oversized journal.");
            using var reader = new BinaryReader(File.OpenRead(file));
            int format = reader.ReadInt32();
            if (format != 1 && format != 2) throw new InvalidDataException("Unknown journal version.");
            uploadPermit = reader.ReadString();
            var data = new PilotCounters { gnssFixes = reader.ReadInt32(), visualFrames = reader.ReadInt32(),
                estimates = reader.ReadInt32(), sentPoses = reader.ReadInt32(), terrainBytes = reader.ReadInt32(),
                terrainRequests = reader.ReadInt32(), peakThermalStatus = reader.ReadInt32() };
            pages = format == 2 ? PilotTape.Read(reader) : Array.Empty<TapePage>();
            if (reader.BaseStream.Position != reader.BaseStream.Length || uploadPermit.Length == 0)
                throw new InvalidDataException("Invalid journal.");
            return data;
        }
    }
}
