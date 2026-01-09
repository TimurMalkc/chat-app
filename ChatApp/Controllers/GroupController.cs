using ChatApp.Hubs;
using ChatApp.Models;
using ChatApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChatApp.Controllers
{
    [ApiController]
    [Route("api/group")]
    public class GroupController : ControllerBase
    {
        private readonly GroupService _groupService;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IMongoCollection<ChatGroup> _groups;

        public GroupController(GroupService groupService, IHubContext<ChatHub> hubContext)
        {
            _groupService = groupService;
            _hubContext = hubContext;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetMyGroups()
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            var groups = await _groupService.GetGroupsForUser(username);
            return Ok(groups);
        }

        public class GroupCreateDto
        {
            public string Name { get; set; }
        }

        [HttpPost("create")]
        [Authorize]
        public async Task<IActionResult> CreateGroup([FromBody] GroupCreateDto dto)
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;

            var group = new ChatGroup
            {
                Name = dto.Name,
                Admin = username,
                CreatedBy = username,
                Members = new List<string>()
            };

            await _groupService.CreateGroup(group);
            return Ok("Group created");
        }

        [HttpPost("addUser")]
        [Authorize]
        public async Task<IActionResult> AddUserToGroup([FromBody] AddUserDto dto)
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;

            var group = await _groupService.GetGroupByName(dto.GroupName);
            if (group == null)
                return NotFound("Group not found");

            if (group.Admin != username)
                return Unauthorized("Only admin can add users");

            if (!group.Members.Contains(dto.Username))
                group.Members.Add(dto.Username);

            await _groupService.UpdateGroup(group);

            if (ChatHub.UserConnections.TryGetValue(dto.Username, out var connectionId))
            {
                await _hubContext.Clients.Client(connectionId)
                    .SendAsync("GroupAdded", dto.GroupName);
            }

            return Ok("User added");
        }

        public class AddUserDto
        {
            public string GroupName { get; set; }
            public string Username { get; set; }
        }

        [HttpDelete("{groupName}/users/{username}")]
        [Authorize]
        public async Task<IActionResult> RemoveUserFromGroup(
    string groupName,
    string username)
        {
            var currentUser = User.FindFirst(ClaimTypes.Name)?.Value;

            var group = await _groupService.GetGroupByName(groupName);
            if (group == null)
                return NotFound("Group not found");

            if (group.Admin != currentUser)
                return Unauthorized("Only admin can remove users");

            if (!group.Members.Contains(username))
                return BadRequest("User is not in the group");

            // Prevent admin self-removal
            if (username == group.Admin)
                return BadRequest("Admin cannot remove himself");

            group.Members.Remove(username);
            await _groupService.UpdateGroup(group);

            if (ChatHub.UserConnections.TryGetValue(username, out var connectionId))
            {
                await _hubContext.Groups.RemoveFromGroupAsync(connectionId, groupName);

                await _hubContext.Clients.Client(connectionId)
                    .SendAsync("GroupRemoved", groupName);
            }

            return NoContent(); // ✅ Correct DELETE response
            }


        


        [HttpDelete("delete/{groupName}")]
        [Authorize]
        public async Task<IActionResult> DeleteGroup(string groupName)
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;

            // 1. Get the group using your existing service
            var group = await _groupService.GetGroupByName(groupName);

            if (group == null)
                return NotFound("Group not found");

            // 2. Check if the user is the Admin
            if (group.Admin != username)
                return Unauthorized("Only the admin can delete this group");

            // 3. Delete using the service method you just added
            var success = await _groupService.DeleteGroupByName(groupName);

            if (success)
            {
                // 4. Notify everyone
                await _hubContext.Clients.All.SendAsync("GroupDeleted", groupName);
                return Ok("Group deleted");
            }

            return BadRequest("Failed to delete group");
        }



        public class RemoveUserDto
        {
            public string GroupName { get; set; }
            public string Username { get; set; }
        }


        [HttpGet("{groupName}")]
        [Authorize]
        public async Task<IActionResult> GetGroupByName(string groupName)
        {
            var group = await _groupService.GetGroupByName(groupName);
            if (group == null)
                return NotFound("Group not found");

            return Ok(group);
        }



        public class LeaveGroupDto
        {
            public string GroupName { get; set; }
        }

        [HttpPost("leave")]
        [Authorize]
        public async Task<IActionResult> LeaveGroup([FromBody] LeaveGroupDto dto)
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            var group = await _groupService.GetGroupByName(dto.GroupName);

            if (group == null) return NotFound("Group not found");

            // 1. Validation: Admins cannot "leave" (they must delete the group)
            if (group.Admin == username)
            {
                return BadRequest("As the admin, you cannot leave the group. You must delete it.");
            }

            // 2. Remove user from MongoDB Members list
            if (group.Members.Contains(username))
            {
                group.Members.Remove(username);
                await _groupService.UpdateGroup(group); // Assumes you have this method in service
            }
            else
            {
                return BadRequest("You are not a member of this group.");
            }

            // 3. SignalR: Remove their connection from the group so they stop getting messages
            if (ChatHub.UserConnections.TryGetValue(username, out var connectionId))
            {
                await _hubContext.Groups.RemoveFromGroupAsync(connectionId, dto.GroupName);

                // Notify the user so their UI updates (removes the group from list)
                await _hubContext.Clients.Client(connectionId).SendAsync("GroupRemoved", dto.GroupName);
            }

            return Ok("You left the group.");
        }



    }
}
