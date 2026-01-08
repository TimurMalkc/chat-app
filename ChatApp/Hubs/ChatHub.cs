using ChatApp.Models;
using ChatApp.Services;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace ChatApp.Hubs
{
    public class ChatHub : Hub
    {
        public static ConcurrentDictionary<string, string> UserConnections
        = new ConcurrentDictionary<string, string>();
        private readonly ChatService _chatService;
        private readonly GroupService _groupService;

        public ChatHub(ChatService chatService, GroupService groupService)
        {
            _chatService = chatService;
            _groupService = groupService;
        }

        private string GetPrivateRoomName(string user1, string user2)
        {
            return string.CompareOrdinal(user1, user2) < 0
                ? $"{user1}_{user2}"
                : $"{user2}_{user1}";
        }
        public async Task JoinPrivateChat(string otherUser)
        {
            var username = Context.User.FindFirst(ClaimTypes.Name)?.Value;
            var roomName = GetPrivateRoomName(username, otherUser);

            await Groups.AddToGroupAsync(Context.ConnectionId, roomName);

            var messages = await _chatService.GetMessagesByRoomAsync(roomName);
            await Clients.Caller.SendAsync("LoadChatHistory", messages);
        }

        public async Task LeaveRoom(string roomName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
        }

        public async Task SendPrivateMessage(string otherUser, string message)
        {
            var username = Context.User.FindFirst(ClaimTypes.Name)?.Value;
            var roomName = GetPrivateRoomName(username, otherUser);

            var msg = new ChatMessage
            {
                Room = roomName,
                User = username,
                Message = message,
                Timestamp = DateTime.UtcNow
            };

            await _chatService.SaveMessageAsync(msg);

            await Clients.Group(roomName).SendAsync("ReceiveMessage", username, message, msg.Timestamp);
        }


        public async Task SendGroupMessage(string groupName, string message)
        {
            var username = Context.User.FindFirst(ClaimTypes.Name)?.Value;
            var group = await _groupService.GetGroupByName(groupName);

            if (group == null || (!group.Members.Contains(username) && group.Admin != username))
                throw new HubException("You are not a member of this group");

            var msg = new ChatMessage
            {
                Room = groupName,
                User = username,
                Message = message,
                Timestamp = DateTime.UtcNow
            };

            await _chatService.SaveMessageAsync(msg);

            await Clients.Group(groupName)
                .SendAsync("ReceiveMessage", username, message, msg.Timestamp);
        }

        public async Task JoinRoom(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            var messages = await _chatService.GetMessagesByRoomAsync(groupName);
            await Clients.Caller.SendAsync("LoadChatHistory", messages);
        }


        public override async Task OnConnectedAsync()
        {
            var username = Context.User.FindFirst(ClaimTypes.Name)?.Value;

            if (username != null)
            {
                UserConnections[username] = Context.ConnectionId;
                await _chatService.UpdateLastOnline(username, DateTime.UtcNow);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var username = Context.User.FindFirst(ClaimTypes.Name)?.Value;

            if (username != null)
            {
                UserConnections.TryRemove(username, out _);
                await _chatService.UpdateLastOnline(username, DateTime.UtcNow);

            }

            await base.OnDisconnectedAsync(exception);
        }

    }
}