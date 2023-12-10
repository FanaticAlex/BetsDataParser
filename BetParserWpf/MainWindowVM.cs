using BetsParserLib;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenQA.Selenium.DevTools;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace BetParserWpf
{
    public class StringWriterExt : StringWriter
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public delegate void FlushedEventHandler(object sender, EventArgs args);
        public event FlushedEventHandler Flushed;
        public virtual bool AutoFlush { get; set; }

        public StringWriterExt()
            : base() { }

        public StringWriterExt(bool autoFlush)
            : base() { this.AutoFlush = autoFlush; }

        protected void OnFlush()
        {
            var eh = Flushed;
            if (eh != null)
                eh(this, EventArgs.Empty);
        }

        public override void Flush()
        {
            base.Flush();
            OnFlush();
        }

        public override void Write(char value)
        {
            base.Write(value);
            if (AutoFlush) Flush();
        }

        public override void Write(string value)
        {
            base.Write(value);
            if (AutoFlush) Flush();
        }

        public override void Write(char[] buffer, int index, int count)
        {
            base.Write(buffer, index, count);
            if (AutoFlush) Flush();
        }
    }

    internal partial class MainWindowVM : ObservableObject
    {
        public static string DirectoryNameHistory = "History";
        public static string DirectoryNameFiltered = "Filtered";

        private CancellationTokenSource _cancellationTokenSource;
        private StringWriterExt _sw;

        public MainWindowVM()
        {
            _sw = new StringWriterExt();
            Console.SetOut(_sw);
            Console.SetError(_sw);
            _sw.Flushed += (s, a) => ConsoleOut = _sw.ToString();

            Date = DateTime.Now;
            ResetProgress();
        }

        [ObservableProperty]
        public string consoleOut;

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

            _cancellationTokenSource = new CancellationTokenSource();

            List<Game> games = new List<Game>();
            await Task.Run(() => games = Game.GetGames(Date, progress, _cancellationTokenSource.Token), _cancellationTokenSource.Token);

            if (CreateDirectories())
            {
                var str = BetsHelper.Serialize(games);
                BetsHelper.SaveToFile(str, $"{DirectoryNameHistory}\\{GetSaveFileName(Date)}");
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
        public void SotopLoad()
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

        public void CheckIsLoaded()
        {
            IsLoaded = File.Exists($"{AppDomain.CurrentDomain.BaseDirectory}{DirectoryNameHistory}\\{GetSaveFileName(Date)}");
        }

        public string GetSaveFileName(DateTime date)
        {
            return $"{date.ToString("yyyy_MM_dd")}.json";
        }

        public string GetFilteredFileName(DateTime date)
        {
            return $"{date.ToString("yyyy_MM_dd")}.csv";
        }
    }
}
