using ChatApp.Models;
using MongoDB.Driver;

namespace ChatApp.Services
{
    public class GroupService
    {
        private readonly IMongoCollection<ChatGroup> _groups;

        public GroupService(IConfiguration config)
        {
            var client = new MongoClient(config.GetConnectionString("MongoDb"));
            var database = client.GetDatabase("ChatAppDB");
            _groups = database.GetCollection<ChatGroup>("Groups");
        }

        public async Task<List<ChatGroup>> GetGroupsForUser(string username)
        {
            return await _groups
                .Find(g => g.CreatedBy == username || g.Members.Contains(username))
                .ToListAsync();
        }
        public async Task CreateGroup(ChatGroup group)
        {
            await _groups.InsertOneAsync(group);
        }

        public async Task<ChatGroup> GetGroupByName(string name)
        {
            return await _groups.Find(g => g.Name == name).FirstOrDefaultAsync();
        }

        public async Task UpdateGroup(ChatGroup group)
        {
            await _groups.ReplaceOneAsync(g => g.Id == group.Id, group);
        }

        public async Task<bool> DeleteGroupByName(string groupName)
        {
            var result = await _groups.DeleteOneAsync(g => g.Name == groupName);
            return result.DeletedCount > 0;
        }
    }
}
