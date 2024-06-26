﻿using Newtonsoft.Json.Serialization;

namespace BetParser.TelegramBot
{
    public class UserInfo(long id, string name)
    {
        public long Id { get; set; } = id;
        public string Name { get; set; } = name;
    }
}