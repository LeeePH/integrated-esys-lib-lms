using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace E_SysV0._01.Hubs
{
    public class AdminNotificationsHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            if (Context.User?.IsInRole("Admin") == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
            }
            await base.OnConnectedAsync();
        }
    }
}