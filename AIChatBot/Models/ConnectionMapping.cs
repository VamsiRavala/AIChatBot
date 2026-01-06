using System.Collections.Concurrent;

namespace AIChatBot.Models
{
    public static class ConnectionMapping
    {
        private static readonly ConcurrentDictionary<string, string> _connections = new();

        public static void Add(string sessionId, string connectionId)
        {
            _connections[sessionId] = connectionId;
        }

        public static void Remove(string sessionId, string connectionId)
        {
            _connections.TryRemove(sessionId, out _);
        }

        public static string GetConnectionId(string sessionId)
        {
            return _connections.TryGetValue(sessionId, out var connectionId) ? connectionId : null;
        }
    }
}
