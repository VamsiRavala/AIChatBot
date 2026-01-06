using System.Text.Json;

namespace AIChatBot.Models
{
    public static class SessionExtensions
    {
        public static void SetObject<T>(this ISession session, string key, T value) =>
            session.SetString(key, JsonSerializer.Serialize(value));

        public static T GetObject<T>(this ISession session, string key)
        {
            var val = session.GetString(key);
            return val == null ? default : JsonSerializer.Deserialize<T>(val);
        }
    }
}
