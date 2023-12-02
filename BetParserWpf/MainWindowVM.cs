using BetsParserLib;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenQA.Selenium.DevTools;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace BetParserWpf
{
    internal partial class MainWindowVM : ObservableObject
    {
        public static string DirectoryNameHistory = "History";
        public static string DirectoryNameFiltered = "Filtered";

        public MainWindowVM()
        {
            Date = DateTime.Now;
            ProgressGamesText = $"Игр загружено:";
            ProgressForecastsText = $"коэффициентов загружено:";
        }

        [ObservableProperty]
        public DateTime date;

        [ObservableProperty]
        public bool isLoaded;

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
        public async Task StartLoad()
        {
            var progress = new Progress<LoadingProgress>(p =>
            {
                ProgressGame = (int)Math.Round((double)p.GamesLoaded / p.GamesCount * 100);
                ProgressForecast = (int)Math.Round((double)p.ForecastsLoaded / p.ForecastsCount * 100);
                ProgressGamesText = $"Игр загружено: {p.GamesLoaded}/{p.GamesCount}";
                ProgressForecastsText = $"коэффициентов загружено: {p.ForecastsLoaded} / {p.ForecastsCount}";
                LoadingObject = p.LoadingObjectName;
            });

            List<Game> games = new List<Game>();
            await Task.Run(() => games = Game.GetGames(Date, progress));

            try
            {
                if (!Directory.Exists(DirectoryNameHistory))
                    Directory.CreateDirectory(DirectoryNameHistory);

                if (!Directory.Exists(DirectoryNameFiltered))
                    Directory.CreateDirectory(DirectoryNameFiltered);
            }
            catch(Exception ex)
            {
                MessageBox.Show($"Не удалось создать директории для сохранения результата. {ex.Message}");
            }

            BetsHelper.WriteResultsCSV(BetsHelper.Translate(games), $"{DirectoryNameHistory}\\{GetFileName(Date)}");
            BetsHelper.WriteResultsCSV(BetsHelper.Translate(BetsHelper.Filter(games)), $"{DirectoryNameFiltered}\\filtered_{GetFileName(Date)}");
        }

        [RelayCommand]
        public void ShowLoaded()
        {
            Process.Start("explorer.exe", $"{AppDomain.CurrentDomain.BaseDirectory}{DirectoryNameHistory}");
        }

        public void CheckIsLoaded()
        {
            IsLoaded = File.Exists($"{AppDomain.CurrentDomain.BaseDirectory}{DirectoryNameHistory}\\{GetFileName(Date)}");
        }

        public string GetFileName(DateTime date)
        {
            return $"{date.ToString("yyyy_MM_dd")}.csv";
        }

    }
}
