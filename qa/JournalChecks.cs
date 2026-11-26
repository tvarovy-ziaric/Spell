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
            CheckTape();
            Console.WriteLine("PASS pilot journal, chronological tape, consent gate, loss counters, legacy import and retention");
        }
        finally { directory.Delete(true); }
    }
    static void CheckTape()
    {
        long time = 0;
        var tape = new PilotTape(false, () => time);
        tape.Pose(10,30,5,.5);
        if (tape.Count != 0) throw new Exception("Pose recorded without consent");
        for (int i=0; i<260; i++) { time++; tape.Event(PilotEvent.Cast,i+1); }
        TapePage[] Pages(PilotTape source)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer);
            source.Write(writer);
            buffer.Position = 0;
            return PilotTape.Read(new BinaryReader(buffer));
        }
        var pages = Pages(tape);
        if (pages.Length != 22 || pages[0].dropped != 4 || pages[0].events[0].value != 5 ||
            pages[^1].events[^1].value != 260 || pages.SelectMany(p=>p.events).Any(e=>e.kind == 9))
            throw new Exception("Bounded tape order");
        var optIn = new PilotTape(true, () => time);
        optIn.Pose(double.NaN,1,0,.1); optIn.Pose(1.25,2.5,-3,.25);
        var one = Pages(optIn).Single().events.Single();
        if (one.eastCm != 125 || one.upCm != 250 || one.northCm != -300 || one.value != 25) throw new Exception("Synthetic pose");
        var file = Path.GetTempFileName();
        try
        {
            using (var writer = new BinaryWriter(File.OpenWrite(file))) {
                writer.Write(1); writer.Write("legacy-permit"); for (int i=1;i<=7;i++) writer.Write(i);
            }
            if (PilotJournal.Read(file,out var permit,out var oldTape).gnssFixes != 1 || permit!="legacy-permit" || oldTape.Length!=0)
                throw new Exception("Legacy journal compatibility");
        }
        finally { File.Delete(file); }
    }
}
