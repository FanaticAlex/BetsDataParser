using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

using BetParserWpf.TelegramBot;

namespace BetParserWpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            UpdateDates();
            NameLabel.Content = NameLabel.Content + " " + BetHelperBot.BotName;
            UpdateUsers();

            Assembly assembly = Assembly.GetExecutingAssembly();
            System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            string version = ": " + fvi.FileVersion;
            Title += version;

            MainWindowVM.bot.OnUpdateUsers += MainWindow_OnUpdateUsers;
        }

        private void MainWindow_OnUpdateUsers(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(() => UpdateUsers());
        }

        private void UpdateUsers()
        {
            ((MainWindowVM)DataContext).Users.Clear();
            foreach (var user in BetHelperBot.userManager.Users)
            {
                ((MainWindowVM)DataContext).Users.Add(user);
            }
        }

        private void Claendar1_DisplayModeChanged(object sender, CalendarModeChangedEventArgs e)
        {
            UpdateDates();
        }

        private void UpdateDates()
        {
            Claendar1.SelectedDates.Clear();
            foreach (var range in ((MainWindowVM)DataContext).GetLoadedDates())
                Claendar1.SelectedDates.Add(range.Start);
        }
    }
}
