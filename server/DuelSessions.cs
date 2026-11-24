namespace Spell.Server;

// Each arena is immutable and shared. Membership and invitations belong to one duel.
internal sealed class DuelSessions(ArenaPackage arena, TimeProvider time)
{
    private sealed class Room(DuelLobby lobby, long now)
    {
        public readonly DuelLobby Lobby = lobby;
        public long LastActive = now;
    }

    private readonly Dictionary<string, Room> rooms = new();
    private readonly object gate = new();

    public (Seat Seat, Invitation Invitation)? Create(string? attempt)
    {
        lock (gate)
        {
            if (attempt != null && !Guid.TryParseExact(attempt, "N", out _)) return null;
            var key = attempt ?? Guid.NewGuid().ToString("N");
            if (!rooms.TryGetValue(key, out var room))
            {
                if (rooms.Count >= 128) return null;
                rooms[key] = room = new Room(new DuelLobby(arena, time), time.GetTimestamp());
            }
            return room.Lobby.Create(key);
        }
    }

    public Seat? Join(string code, string? attempt)
    {
        lock (gate)
        {
            foreach (var room in rooms.Values)
            {
                if (room.Lobby.Join(code, attempt) is not { } seat) continue;
                room.LastActive = time.GetTimestamp();
                return seat;
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
