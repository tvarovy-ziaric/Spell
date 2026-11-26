using System.Text.Json;
using Spell.Server;
internal static class SurveyChecks
{
    public static void Run(ArenaPackage arena)
    {
        var path = Path.GetTempFileName();
        try
        {
            var survey = new FieldSurvey(1, arena.Digest, [1, 105, 2], [5, 106, 2]);
            void Save(FieldSurvey input) => File.WriteAllText(path, JsonSerializer.Serialize(input, JsonSerializerOptions.Web));
            Save(survey);
            var restored = FieldSurvey.Load(path, arena);
            if (restored?.BoardB[1] != 106 || FieldSurvey.Load(null, arena) != null) throw new Exception("Survey not restored");
            foreach (var invalid in new[] { survey with { Version = 2 }, survey with { ArenaSha256 = "other" },
                survey with { BoardB = [1, 105, 2] }, survey with { BoardA = [-50, 105, 2] }, survey with { BoardA = [] } })
            {
                Save(invalid);
                try { FieldSurvey.Load(path, arena); } catch (InvalidDataException) { continue; }
                throw new Exception("Invalid surveyed arena accepted");
            }
            Console.WriteLine("PASS surveyed marker geometry, differing heights, terrain digest and invalid manifests");
        }
        finally { File.Delete(path); }
    }
}
