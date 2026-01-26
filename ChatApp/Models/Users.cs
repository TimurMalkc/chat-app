using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace ChatApp.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)] 
        public string Id { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public DateTime LastOnline { get; set; } = DateTime.UtcNow;
        public bool IsOnline { get; set; }
    }
}