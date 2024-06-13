using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace BetParser.TelegramBot
{
    public class UserManager
    {
        private readonly string filename = "users.json";

        public List<UserInfo> Users { get; set; } = [];

        public UserManager()
        {
            Load();
        }

        public void AddUser(string name, long id)
        {
            var user = new UserInfo(id, name);
            Users.Add(user);
            Save();
        }

        public void Save()
        {
            var jsonStr = JsonConvert.SerializeObject(Users);
            File.WriteAllText(filename, jsonStr);
        }

        public void Load()
        {
            if (!File.Exists(filename))
                return;

            var jsonStr = File.ReadAllText(filename);
            Users = JsonConvert.DeserializeObject<List<UserInfo>>(jsonStr);
        }
    }
}