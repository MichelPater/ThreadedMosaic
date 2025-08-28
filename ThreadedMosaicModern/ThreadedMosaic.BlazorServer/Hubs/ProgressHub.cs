using Microsoft.AspNetCore.SignalR;

namespace ThreadedMosaic.BlazorServer.Hubs
{
    /// <summary>
    /// SignalR hub for real-time progress updates during mosaic processing
    /// </summary>
    public class ProgressHub : Hub
    {
        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}