using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using XRCulture3DReconstruction.Services;

namespace XRCulture3DReconstruction.Hubs
{
    [AllowAnonymous]
    public class LogHub : Hub
    {
        private readonly IConnectionStateService _connectionState;

        public LogHub(IConnectionStateService connectionState)
        {
            _connectionState = connectionState;
        }

        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            await _connectionState.AddConnectionToGroup(Context.ConnectionId, groupName);
        }

        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            await _connectionState.RemoveConnectionFromGroup(Context.ConnectionId, groupName);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await _connectionState.RemoveConnection(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}