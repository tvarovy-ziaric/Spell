using Spell.Geometry;

namespace Spell.Server;

public sealed class ArenaPackage
{
    public byte[] Bytes { get; }
    public ArenaSurface Surface { get; }
    public string Digest => Surface.Sha256;

    public ArenaPackage(byte[] bytes, string digest)
    {
        Surface = ArenaSurface.Load(bytes, digest);
        if (!Surface.PlayableAreaValid)
            throw new InvalidDataException("The playable area contains missing heights.");
        Bytes = bytes;
    }
}
