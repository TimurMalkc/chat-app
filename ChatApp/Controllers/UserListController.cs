using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using ChatApp.Models;

namespace ChatApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserListController : ControllerBase
    {
        private readonly IMongoCollection<User> _users;

        public UserListController(IConfiguration config)
        {
            var client = new MongoClient(config.GetConnectionString("MongoDb"));
            var database = client.GetDatabase("ChatAppDB");
            _users = database.GetCollection<User>("Users");
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _users.Find(_ => true).ToListAsync();

            var result = users.Select(u => new
            {
                username = u.Username
            });

            return Ok(result);
        }
    }
}
