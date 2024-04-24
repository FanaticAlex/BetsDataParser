using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using System.Xml;

namespace BetParserTelegramBot
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly UserManager userManager = new UserManager();
        private static readonly string BotName = "BetHelperBot";
        private const string botKey = "6706170571:AAGHZSzTOXMTGqU8LLGzV3LCBVrXKH8UCX0";
        private static readonly TelegramBotClient bot = new(botKey);

        public MainWindow()
        {
            InitializeComponent();
            NameLabel.Content = NameLabel.Content + " " + BotName;

            var admin = bot.GetMeAsync().Result;
            Console.WriteLine($"Hello, World! I am user {admin.Id} and my name is {admin.FirstName}.");
            bot.StartReceiving(new DefaultUpdateHandler(HandleUpdateAsync, HandleErrorAsync));

            UpdateUsers();

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
                UpdateUsers();
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

        private void UpdateUsers()
        {
            ((MainWindowViewModel)DataContext).Users.Clear();
            foreach (var user in userManager.Users)
            {
                ((MainWindowViewModel)DataContext).Users.Add(user);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            foreach(var user in userManager.Users)
            {
                bot.SendTextMessageAsync(user.Id, "test message");
            }

            MessageBox.Show("Сообщение отправлено");
        }
    }
}