using BetsParserLib;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenQA.Selenium.DevTools;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using BetParserWpf.TelegramBot;

namespace BetParserWpf
{
    internal partial class MainWindowVM : ObservableObject
    {
        public static string DirectoryNameHistory = "History";
        public static string DirectoryNameFiltered = "Filtered";

        private CancellationTokenSource _cancellationTokenSource;
        private StringWriterExt _sw;
        public static BetHelperBot bot = new();

        public MainWindowVM()
        {
            _sw = new StringWriterExt();
            Console.SetOut(_sw);
            Console.SetError(_sw);
            _sw.Flushed += (s, a) =>
            {
                var temp = _sw.ToString();
                ConsoleOut = temp.Substring(Math.Max(0, temp.Length - 2000), Math.Min(temp.Length, 2000));
            };

            Date = DateTime.Now;
            ResetProgress();
        }

        public ObservableCollection<UserInfo> Users { get; set; } = [];

        [ObservableProperty]
        public string consoleOut;

        [ObservableProperty]
        public DateTime date;

        [ObservableProperty]
        public int progressGame;

        [ObservableProperty]
        public string progressGamesText;

        [ObservableProperty]
        public int progressForecast;

        [ObservableProperty]
        public string progressForecastsText;

        [ObservableProperty]
        public string loadingObject;

        [RelayCommand]
        public async Task SendNews()
        {
            var games = BetsHelper.Deserialize($"{DirectoryNameHistory}\\{GetSaveFileName(Date)}");
            if (games == null)
                MessageBox.Show("Нет загруженных игр за этот день");

            bot.SendNews(games);
        }

        [RelayCommand]
        public async Task StartLoad()
        {
            try
            {
                var progress = new Progress<LoadingProgress>(p =>
                {
                    ProgressGame = (int)Math.Round((double)p.GamesLoaded / p.GamesCount * 100);
                    ProgressForecast = (int)Math.Round((double)p.ForecastsLoaded / p.ForecastsCount * 100);
                    ProgressGamesText = $"Игр загружено: {p.GamesLoaded}/{p.GamesCount}";
                    ProgressForecastsText = $"коэффициентов загружено: {p.ForecastsLoaded} / {p.ForecastsCount}";
                    LoadingObject = p.LoadingObjectName;
                });

                _cancellationTokenSource = new CancellationTokenSource();

                List<Game> games = new List<Game>();
                await Task.Run(() => games = Game.GetGames(Date, progress, _cancellationTokenSource.Token), _cancellationTokenSource.Token);

                if (CreateDirectories())
                {
                    var str = BetsHelper.Serialize(games);
                    BetsHelper.SaveToFile(str, $"{DirectoryNameHistory}\\{GetSaveFileName(Date)}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Необработанное исключение. {ex.Message}", "Ошибка");
            }
        }

        private static bool CreateDirectories()
        {
            try
            {
                if (!Directory.Exists(DirectoryNameHistory))
                    Directory.CreateDirectory(DirectoryNameHistory);

                if (!Directory.Exists(DirectoryNameFiltered))
                    Directory.CreateDirectory(DirectoryNameFiltered);

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось создать директории для сохранения результата. {ex.Message}", "Ошибка");
                return false;
            }
        }

        [RelayCommand]
        public void StopLoad()
        {
            _cancellationTokenSource.Cancel();
            MessageBox.Show($"Загрузка отстановлена.");

            ResetProgress();
        }

        private void ResetProgress()
        {
            ProgressGame = 0;
            ProgressForecast = 0;
            ProgressGamesText = $"Игр загружено:";
            ProgressForecastsText = $"коэффициентов загружено:";
            LoadingObject = string.Empty;
        }

        [RelayCommand]
        public void ShowLoaded()
        {
            Process.Start("explorer.exe", $"{AppDomain.CurrentDomain.BaseDirectory}{DirectoryNameHistory}");
        }

        [RelayCommand]
        public void Filter()
        {
            var games = BetsHelper.Deserialize($"{DirectoryNameHistory}\\{GetSaveFileName(Date)}");
            if (games == null)
                return;

            var rows1 = BetsHelper.GetFilteredRows(games);

            BetsHelper.WriteResultsCSV(rows1, $"{DirectoryNameFiltered}\\filtered_{GetFilteredFileName(Date)}");
            Process.Start("explorer.exe", $"{AppDomain.CurrentDomain.BaseDirectory}{DirectoryNameFiltered}");
        }

        public Boolean CheckIsLoaded(DateTime newDate)
        {
            return File.Exists($"{AppDomain.CurrentDomain.BaseDirectory}{DirectoryNameHistory}\\{GetSaveFileName(newDate)}");
        }

        public string GetSaveFileName(DateTime date)
        {
            return $"{date.ToString("yyyy_MM_dd")}.json";
        }

        public string GetFilteredFileName(DateTime date)
        {
            return $"{date.ToString("yyyy_MM_dd")}.csv";
        }

        public List<CalendarDateRange> GetLoadedDates()
        {
            var loadedDates = new List<CalendarDateRange>();
            foreach(var d in GetDates(Date.Year, Date.Month))
            {
                if (d >= Date.Date)
                    continue;

                if (CheckIsLoaded(d))
                  loadedDates.Add(new CalendarDateRange(d));
            }

            return loadedDates;
        }

        public static List<DateTime> GetDates(int year, int month)
        {
            var dates = new List<DateTime>();

            // Loop from the first day of the month until we hit the next month, moving forward a day at a time
            for (var date = new DateTime(year, month, 1); date.Month == month; date = date.AddDays(1))
            {
                dates.Add(date);
            }

            return dates;
        }
    }
}
