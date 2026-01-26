using ChatApp.Models;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChatApp.Services
{
    public class ChatService
    {
        private readonly IMongoCollection<ChatMessage> _messages;
        private readonly IMongoCollection<User> _users;

        public ChatService(IConfiguration config)
        {
            var client = new MongoClient(config.GetConnectionString("MongoDb"));
            var database = client.GetDatabase("ChatAppDB");
            _messages = database.GetCollection<ChatMessage>("Messages");
            _users = database.GetCollection<User>("Users");
        }

        public async Task UpdateLastOnline(string username, DateTime time)
        {
            var update = Builders<User>.Update.Set(u => u.LastOnline, time);
            await _users.UpdateOneAsync(u => u.Username == username, update);
        }
        public async Task SaveMessageAsync(ChatMessage msg)
        {
            await _messages.InsertOneAsync(msg);
        }
        public async Task<List<ChatMessage>> GetMessagesByRoomAsync(string room)
        {
            return await _messages
                .Find(m => m.Room == room)
                .SortBy(m => m.Timestamp)
                .ToListAsync();
        }
    }
}
