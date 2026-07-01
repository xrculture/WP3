using Microsoft.AspNetCore.SignalR;
using XRCulture3DReconstruction.Hubs;

namespace XRCulture3DReconstruction.Services
{
    public interface ISignalRLoggerService
    {
        Task SendLogMessage(string groupName, string message);
        Task SendLogMessage(string groupName, string message, object data);
        Task<int> GetActiveConnectionsCount(string groupName);
    }

    public class SignalRLoggerService : ISignalRLoggerService
    {
        private readonly IHubContext<LogHub> _hubContext;
        private readonly IConnectionStateService _connectionState;

        public SignalRLoggerService(IHubContext<LogHub> hubContext, IConnectionStateService connectionState)
        {
            _hubContext = hubContext;
            _connectionState = connectionState;
        }

        public async Task SendLogMessage(string groupName, string message)
        {
            var connections = await _connectionState.GetConnectionsInGroup(groupName);
            if (connections.Any())
            {
                await _hubContext.Clients.Group(groupName).SendAsync("ReceiveLogMessage", message);
            }
        }

        public async Task SendLogMessage(string groupName, string message, object data)
        {
            var connections = await _connectionState.GetConnectionsInGroup(groupName);
            if (connections.Any())
            {
                await _hubContext.Clients.Group(groupName).SendAsync("ReceiveLogMessage", message, data);
            }
        }

        public async Task<int> GetActiveConnectionsCount(string groupName)
        {
            var connections = await _connectionState.GetConnectionsInGroup(groupName);
            return connections.Count();
        }
    }
}