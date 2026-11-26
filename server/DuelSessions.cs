namespace Spell.Server;

internal sealed record Admission(Seat Seat, string PresetId, DuelRules Rules);

// Each arena is immutable and shared. Membership and invitations belong to one duel.
internal sealed class DuelSessions(ArenaPackage arena, TimeProvider time)
{
    private sealed class Room(DuelLobby lobby, long now, string presetId, DuelRules rules, byte[]? owner)
    {
        public readonly DuelLobby Lobby = lobby;
        public long LastActive = now;
        public readonly string PresetId = presetId;
        public readonly DuelRules Rules = rules;
        public readonly byte[]? Owner = owner;
    }

    private readonly Dictionary<string, Room> rooms = new();
    private readonly object gate = new();

    public (Admission Player, Invitation Invitation)? Create(string? attempt, SavedPreset? preset = null, byte[]? owner = null)
    {
        lock (gate)
        {
            if (attempt != null && !Guid.TryParseExact(attempt, "N", out _)) return null;
            var key = attempt ?? Guid.NewGuid().ToString("N");
            if (!rooms.TryGetValue(key, out var room))
            {
                if (rooms.Count >= 128) return null;
                var lobby = new DuelLobby(arena, time);
                var id = preset?.Id ?? DuelLobby.Preset;
                var rules = preset?.Rules ?? DuelRules.Default;
                lobby.ConfigureRules(id, rules);
                rooms[key] = room = new Room(lobby, time.GetTimestamp(), id, rules, owner);
            }
            if (room.PresetId != (preset?.Id ?? DuelLobby.Preset) ||
                !(room.Owner ?? Array.Empty<byte>()).SequenceEqual(owner ?? Array.Empty<byte>())) return null;
            return room.Lobby.Create(key) is { } created ? (new Admission(created.Seat, room.PresetId, room.Rules), created.Invitation) : null;
        }
    }

    public Admission? Join(string code, string? attempt)
    {
        lock (gate)
        {
            foreach (var room in rooms.Values)
            {
                if (room.Lobby.Join(code, attempt) is not { } seat) continue;
                room.LastActive = time.GetTimestamp();
                return new Admission(seat, room.PresetId, room.Rules);
            }
            return null;
        }
    }

    public DuelLobby? Find(string token)
    {
        lock (gate)
        {
            foreach (var room in rooms.Values)
            {
                if (!room.Lobby.HasSeat(token)) continue;
                room.LastActive = time.GetTimestamp();
                return room.Lobby;
            }
            return null;
        }
    }

    public bool End(string token)
    {
        lock (gate)
        {
            foreach (var (key, room) in rooms)
            {
                if (!room.Lobby.HasSeat(token)) continue;
                room.Lobby.Close();
                return rooms.Remove(key);
            }
            return false;
        }
    }

    public void Tick()
    {
        lock (gate)
        {
            foreach (var (key, room) in rooms)
            {
                room.Lobby.Tick();
                if (room.Lobby.HasConnection) room.LastActive = time.GetTimestamp();
                if (time.GetElapsedTime(room.LastActive) < TimeSpan.FromMinutes(15)) continue;
                room.Lobby.Close();
                rooms.Remove(key);
            }
        }
    }
}
