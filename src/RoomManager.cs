using System.Collections.Concurrent;

namespace OSSMWebServer
{
    public class RoomManager
    {
        private readonly ConcurrentDictionary<string, Room> _rooms = new();
        private readonly ConcurrentDictionary<Guid, Client> _clients = new();
        private readonly ConcurrentDictionary<Client, Room> _pendingJoinRequests = new();

        public void RegisterClient(Client client)
        {
            if (!_clients.TryAdd(client.Id, client))
                throw new InvalidOperationException("Client already registered.");
        }

        public async Task UnregisterClient(Client client)
        {
            if (!_clients.TryRemove(client.Id, out _))
                return;

            _pendingJoinRequests.TryRemove(client, out _);

            await LeaveRoom(client);
        }

        public Room CreateRoom(Connection host)
        {
            if (host.ActiveRoom is not null)
                throw new InvalidOperationException("Already in a room.");

            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            string id;
            do
            {
                id = new string(Enumerable.Range(0, 6)
                    .Select(_ => chars[Random.Shared.Next(chars.Length)])
                    .ToArray());
            } while (_rooms.ContainsKey(id));

            Room room = new()
            {
                Id = id,
                Host = host
            };

            _rooms[room.Id] = room;

            host.ActiveRoom = room;

            return room;
        }

        public bool TryGetRoom(string roomId, out Room room)
            => _rooms.TryGetValue(roomId, out room!);

        public bool TryGetClient(Guid clientId, out Client client)
            => _clients.TryGetValue(clientId, out client!);

        public async Task CreateJoinRequest(Room room, Client guest)
        {
            if (room.Host.Id == guest.Id)
                throw new InvalidOperationException("Host cannot join their own room");

            if (guest.ActiveRoom is not null)
                throw new InvalidOperationException("Already in a room");

            _pendingJoinRequests[guest] = room;

            await room.Host.NotifyAsync(new { command = ECommand.JoinRequest, clientId = guest.Id });
        }

        public async Task ResolveJoinRequest(Client author, Room room, Client guest, bool accepted)
        {
            if (room.Host.Id != author.Id)
                throw new InvalidOperationException("Only the host can resolve join requests");

            if (!_pendingJoinRequests.TryGetValue(guest, out Room? pendingRoom))
                throw new Exception("No pending join request for this guest");

            if (pendingRoom.Id != room.Id)
                throw new InvalidOperationException("Pending join request does not match the specified room");

            _pendingJoinRequests.TryRemove(guest, out _);

            if (accepted)
            {
                guest.ActiveRoom = room;
                room.Guests.Add(guest);
            }
            
            await guest.NotifyAsync(new
            {
                command = ECommand.JoinRequest,
                roomId = room.Id,
                status = accepted ? EStatus.Accepted : EStatus.Rejected
            });
        }

        public async Task LeaveRoom(Client client)
        {
            if (client.ActiveRoom is not null && client.ActiveRoom.Host.Id == client.Id)
            {
                // Case: Client is the host

                // Remove the room from the manager
                _rooms.TryRemove(client.ActiveRoom.Id, out _);

                // Notify all clients in the room to leave
                foreach (Client guest in client.ActiveRoom.Guests)
                    await guest.NotifyAsync(new { command = ECommand.LeaveRoom, clientId = client.Id, message = "Host has left the room" });
            }
            else if (client.ActiveRoom is not null)
            {
                // Case: Client is the guest

                // Remove the client from the room's guest list
                client.ActiveRoom.Guests.Remove(client);

                // Notify the host that the clienthas left
                await client.ActiveRoom.Host.NotifyAsync(new { command = ECommand.LeaveRoom, clientId = client.Id, message = "Guest has left the room" });
            }
        }

        public async Task StateUpdate(Client author, ROssmState state)
        {
            // Do some basic validation of the state data (avoid sending invalid/malicious data to other clients)
            state.Validate();

            if (author.ActiveRoom is not null && author.ActiveRoom.Host.Id == author.Id)
            {
                // Case: Author is the host, send state to all guests
                foreach (Client guest in author.ActiveRoom.Guests)
                    await guest.NotifyAsync(new { command = ECommand.StateUpdate, clientId = author.Id, state });
            }
            else if (author.ActiveRoom is not null)
            {
                // Case: Author is a guest, send state to the host (host will validate and report back the state to all guests)
                await author.ActiveRoom.Host.NotifyAsync(new { command = ECommand.StateUpdate, clientId = author.Id, state });
            }
            else
            {
                // Case: Author is not in a room, ignore the state update
                throw new InvalidOperationException("Not in a room");
            }
        }
    }
}
