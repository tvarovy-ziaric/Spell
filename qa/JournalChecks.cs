using Spell.Client.Diagnostics;

internal static class JournalChecks
{
    public static void Run()
    {
        var directory = Directory.CreateTempSubdirectory("spell-journal-");
        try
        {
            var journal = new PilotJournal(directory.FullName);
            journal.Begin("test-permit");
            journal.Record(Measure.GnssFix,2); journal.Record(Measure.VisualFrame,15);
            journal.Record(Measure.Estimate,10); journal.Record(Measure.SentPose,7);
            journal.Record(Measure.TerrainBytes,312); journal.Record(Measure.Thermal,3);
            journal.Flush();
            if (journal.Pending(DateTime.UtcNow).Length != 0) throw new Exception("Upload during play");
            var reopened = new PilotJournal(directory.FullName);
            var file = reopened.Pending(DateTime.UtcNow).Single();
            var report = PilotJournal.Read(file, out var permit);
            if (report.gnssFixes != 2 || report.visualFrames != 15 || report.estimates != 10 ||
                report.sentPoses != 7 || report.terrainBytes != 312 || report.peakThermalStatus != 3 ||
                permit != "test-permit") throw new Exception("Recovery counters differ");
            journal.End();
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddHours(-49));
            if (reopened.Pending(DateTime.UtcNow).Length != 0) throw new Exception("Expired journal");
            journal.Begin("");
            if (journal.Recording) throw new Exception("Uncredentialed recording");
            Console.WriteLine("PASS pilot journal, typed counters, recovery, finalization and retention");
        }
        finally { directory.Delete(true); }
    }
}
