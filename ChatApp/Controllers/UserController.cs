using ChatApp.Hubs;
using ChatApp.Models;
using ChatApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ChatApp.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class UserController : Controller
    {
        private readonly IMongoCollection<User> _users;
        private readonly IConfiguration _config;
        private readonly ChatService _userService;
        private readonly GroupService _groupService;
        private readonly IHubContext<ChatHub> _hubContext;

        public UserController(IConfiguration config, ChatService userService, GroupService groupService,
            IHubContext<ChatHub> hubContext)
        {
            _config = config;
            _userService = userService;
            _groupService = groupService;
            _hubContext = hubContext;

            var client = new MongoClient(config.GetConnectionString("MongoDb"));
            var database = client.GetDatabase("ChatAppDB");
            _users = database.GetCollection<User>("Users");
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserRegisterDto dto)
        {
            if (await _users.Find(u => u.Username == dto.Username).AnyAsync())
                return BadRequest("Username already exists");

            var user = new User
            {
                Username = dto.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
            };

            await _hubContext.Clients.All.SendAsync("UserRegistered", dto.Username);
            await _users.InsertOneAsync(user);
            return Ok("User registered");
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto dto)
        {
            var user = await _users.Find(u => u.Username == dto.Username).FirstOrDefaultAsync();
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return Unauthorized("Invalid username or password");

            var token = GenerateJwtToken(user.Username);
            return Ok(new { token });
        }


        private string GenerateJwtToken(string username)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
            new Claim(ClaimTypes.Name, username)
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(3),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }



        [HttpGet("info/{username}")]
        [Authorize]
        public async Task<IActionResult> GetUserInfo(string username)
        {
            var currentUser = User.FindFirst(ClaimTypes.Name)?.Value;

            var user = await _users.Find(u => u.Username == username).FirstOrDefaultAsync();
            if (user == null)
                return NotFound("User not found");

         
            var targetGroups = await _groupService.GetGroupsForUser(username);

            
            var currentUserGroups = await _groupService.GetGroupsForUser(currentUser);

         
            var mutualGroups = targetGroups
                .Where(g => currentUserGroups.Any(cg => cg.Name == g.Name))
                .Select(g => g.Name)
                .ToList();

            return Ok(new
            {
                username = user.Username,
                lastOnline = user.LastOnline,
                groups = mutualGroups
            });

        }


        [HttpPost("disconnect")]
        public async Task<IActionResult> Disconnect([FromBody] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest("Missing token");

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            var username = jwt.Claims.First(c => c.Type.Contains("name")).Value;

            await _userService.UpdateLastOnline(username, DateTime.UtcNow);
            return Ok();
        }


        [Authorize] // Ensure only logged-in users can access this
        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteProfile()
        {
            // Get the username from the current JWT token
            var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(username))
                return Unauthorized();

            // Delete the user from MongoDB
            var result = await _users.DeleteOneAsync(u => u.Username == username);

            if (result.DeletedCount > 0)
            {
                // Real-time update: Notify all clients to refresh their user list
                await _hubContext.Clients.All.SendAsync("UserDeleted", username);
                return Ok("Profile deleted successfully.");
            }

            return BadRequest("Could not delete user.");
        }





    }





    public class UserRegisterDto
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class UserLoginDto
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

}
