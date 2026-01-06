namespace AIChatBot.Models
{
    public static class AdminSessionTracker
    {
        private static readonly HashSet<string> AdminJoinedSessions = new();

        public static void MarkAdminJoined(string sessionId)
        {
            lock (AdminJoinedSessions)
            {
                AdminJoinedSessions.Add(sessionId);
            }
        }

        public static bool IsAdminJoined(string sessionId)
        {
            lock (AdminJoinedSessions)
            {
                return AdminJoinedSessions.Contains(sessionId);
            }
        }
    }
}
