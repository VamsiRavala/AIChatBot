using AIChatBot.Models;
using Microsoft.AspNetCore.SignalR;

namespace AIChatBot.SignalR
{
    public class ChatHub : Hub
    {
        public override Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            var sessionId = httpContext.Request.Query["sessionId"];
            var role = httpContext.Request.Query["role"];

            if (!string.IsNullOrEmpty(sessionId))
                ConnectionMapping.Add(sessionId, Context.ConnectionId);

            if (role == "admin")
                Groups.AddToGroupAsync(Context.ConnectionId, "Admins");

            return base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var httpContext = Context.GetHttpContext();
            var sessionId = httpContext.Request.Query["sessionId"];
            var role = httpContext.Request.Query["role"];

            if (!string.IsNullOrEmpty(role) && role == "admin")
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Admins");
            }
            else if (!string.IsNullOrEmpty(sessionId))
            {
                ConnectionMapping.Remove(sessionId, Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }


}
