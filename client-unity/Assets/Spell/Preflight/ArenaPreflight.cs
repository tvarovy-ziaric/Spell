using System.IO;
using Spell.Geometry;

namespace Spell.Client.Preflight
{
    public static class ArenaPreflight
    {
        public static ArenaSurface Load(byte[] bytes, string expectedSha256)
        {
            ArenaSurface arena = ArenaSurface.Load(bytes, expectedSha256);
            if (!arena.PlayableAreaValid)
                throw new InvalidDataException("V hracej ploche chýbajú platné výškové dáta.");
            return arena;
        }
    }
}
