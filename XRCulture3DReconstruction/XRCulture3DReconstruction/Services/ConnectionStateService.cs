using System.Collections.Concurrent;

namespace XRCulture3DReconstruction.Services
{
    public interface IConnectionStateService
    {
        Task AddConnectionToGroup(string connectionId, string groupName);
        Task RemoveConnectionFromGroup(string connectionId, string groupName);
        Task RemoveConnection(string connectionId);
        Task<IEnumerable<string>> GetConnectionsInGroup(string groupName);
        Task<string?> GetUserFromConnection(string connectionId);
    }

    public class ConnectionStateService : IConnectionStateService
    {
        // Thread-safe collections for concurrent access
        private readonly ConcurrentDictionary<string, HashSet<string>> _groupConnections = new();
        private readonly ConcurrentDictionary<string, string> _connectionGroups = new();
        private readonly ConcurrentDictionary<string, string> _connectionUsers = new();
        private readonly object _lock = new();

        public Task AddConnectionToGroup(string connectionId, string groupName)
        {
            lock (_lock)
            {
                _groupConnections.AddOrUpdate(groupName, 
                    new HashSet<string> { connectionId },
                    (key, existing) => { existing.Add(connectionId); return existing; });
                
                _connectionGroups[connectionId] = groupName;
            }
            return Task.CompletedTask;
        }

        public Task RemoveConnectionFromGroup(string connectionId, string groupName)
        {
            lock (_lock)
            {
                if (_groupConnections.TryGetValue(groupName, out var connections))
                {
                    connections.Remove(connectionId);
                    if (!connections.Any())
                    {
                        _groupConnections.TryRemove(groupName, out _);
                    }
                }
                _connectionGroups.TryRemove(connectionId, out _);
            }
            return Task.CompletedTask;
        }

        public Task RemoveConnection(string connectionId)
        {
            lock (_lock)
            {
                if (_connectionGroups.TryRemove(connectionId, out var groupName))
                {
                    if (_groupConnections.TryGetValue(groupName, out var connections))
                    {
                        connections.Remove(connectionId);
                        if (!connections.Any())
                        {
                            _groupConnections.TryRemove(groupName, out _);
                        }
                    }
                }
                _connectionUsers.TryRemove(connectionId, out _);
            }
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> GetConnectionsInGroup(string groupName)
        {
            _groupConnections.TryGetValue(groupName, out var connections);
            return Task.FromResult(connections?.AsEnumerable() ?? Enumerable.Empty<string>());
        }

        public Task<string?> GetUserFromConnection(string connectionId)
        {
            _connectionUsers.TryGetValue(connectionId, out var user);
            return Task.FromResult(user);
        }
    }
}