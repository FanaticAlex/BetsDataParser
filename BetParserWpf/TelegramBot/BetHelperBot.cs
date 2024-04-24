using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using System.Windows;
using BetsParserLib;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BetParserWpf.TelegramBot
{
    internal class BetHelperBot
    {
        public static readonly UserManager userManager = new UserManager();
        public static readonly string BotName = "BetHelperBot";
        private const string botKey = "6706170571:AAGHZSzTOXMTGqU8LLGzV3LCBVrXKH8UCX0";
        private static readonly TelegramBotClient bot = new(botKey);

        public event EventHandler OnUpdateUsers;

        public BetHelperBot()
        {
            var admin = bot.GetMeAsync().Result;
            Console.WriteLine($"TelegramBot: user {admin.Id} and name is {admin.FirstName}.");
            bot.StartReceiving(new DefaultUpdateHandler(HandleUpdateAsync, HandleErrorAsync));

            //bot.OnMakingApiRequest += Bot_OnMakingApiRequest;
            //bot.OnApiResponseReceived += Bot_OnApiResponseReceived;
            //bot.OnMessage += BotOnMessageReceived;
            //bot.OnCallbackQuery += Bot_OnCallbackQuery;
            //var chatId = "BetHelperBot";
            //var mess = bot.SendTextMessageAsync(chatId, "test message").Result;
            //Console.WriteLine(mess);
        }

        async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type != UpdateType.Message)
                return;

            if (update?.Message?.Type != MessageType.Text)
                return;

            var chatId = update.Message.Chat.Id;
            Console.WriteLine($"Received a '{update.Message.Text}' message in chat {chatId}.");

            if (!userManager.Users.Select(u => u.Id).Contains(chatId))
            {
                userManager.AddUser(update.Message.From.FirstName, update.Message.From.Id);
                OnUpdateUsers?.Invoke(null, null);
                await botClient.SendTextMessageAsync(chatId: chatId, text: "Вы добавлены в расылку чата");
            }
        }

        static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }

        public void SendNews(List<BetsParserLib.Game> games)
        {
            foreach (var user in userManager.Users)
                foreach(var game in games)
                 bot.SendTextMessageAsync(user.Id, game.Reference);

            MessageBox.Show("Сообщение отправлено");
        }
    }
}
