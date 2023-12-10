using Serilog;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;

namespace BetsParserLib
{
    public class BetsHelper
    {
        private static List<string> _bookmakersNames = new List<string>()
        {
            "10x10bet",
            "1xBet",
            "888sport",
            "Alphabet",
            "BetVictor",
            "Betway",
            "Bwin",
            "Interwetten",
            "Pinnacle",
            "Unibet",
            "William Hill"
        };

        /// <summary>
        /// строка = одна игра со всеми коээф букмекеров
        /// </summary>
        /// <param name="games"></param>
        /// <returns></returns>
        public static List<string> GetFilteredRows(List<Game> games)
        {
            // Инициализация
            Log.Logger = new LoggerConfiguration()
                    .WriteTo.Debug()
                    .WriteTo.Console()
                    .WriteTo.File(path: $"logs\\Log.txt", rollingInterval: RollingInterval.Day)
                    .CreateLogger();

            Log.Logger.Information("Начало фильтрации.");

            var rows = new List<string>();
            foreach(var game in games)
            {
                var row = string.Empty;
                row += game.Date + ";";  // дата
                row += game.LeagueName + ";"; // лига
                row += game.Name + ";"; // название игры

                try
                {
                    var isThereProblem = false;
                    if (game.BookmakersRows.Count == 0)
                    {
                        Log.Logger.Warning($"У игры {game.Name} нет коэффициентов букммекеров, пропускаем подчет коэффициентов");
                        isThereProblem = true;
                    }

                    var centralTotal = game.GetCantralTotal();
                    if (centralTotal == null)
                    {
                        Log.Logger.Warning($"У игры {game.Name} нет подходящих тоталов, пропускаем подчет коэффициентов");
                        isThereProblem = true;
                    }

                    if (!isThereProblem)
                    {
                        foreach (var bookmakerName in _bookmakersNames)
                        {
                            var filteredRows = FilterBookmakersAndGroupByTotals(bookmakerName, game);
                            // нижний 
                            var lower = filteredRows.ElementAt(0);
                            var lowerRow = lower.Value.FirstOrDefault(); // тут может не быть значения
                            row += GetExodusRow(game, lowerRow, centralTotal.Value - 1); // исход
                            row += (game.GetKoeff(lowerRow).ToString() ?? string.Empty) + ";"; // коэфф
                            row += GetMove(game.GetKoeffStartOver(lowerRow), game.GetKoeffSecondOver(lowerRow)) + ";"; // исходный
                            row += GetMove(game.GetKoeffPenultimateOver(lowerRow), game.GetKoeffFinalOver(lowerRow)) + ";"; // перед матчем
                            row += GetMove(game.GetKoeffStartOver(lowerRow), game.GetKoeffFinalOver(lowerRow)) + ";"; // итого

                            // нижний 
                            var central = filteredRows.ElementAt(1);
                            var centralRow = central.Value.FirstOrDefault(); // тут может не быть значения
                            row += GetExodusRow(game, centralRow, centralTotal.Value); // исход
                            row += (game.GetKoeff(centralRow)?.ToString() ?? string.Empty) + ";"; // коэфф
                            row += GetMove(game.GetKoeffStartOver(centralRow), game.GetKoeffSecondOver(centralRow)) + ";"; // исходный
                            row += GetMove(game.GetKoeffPenultimateOver(centralRow), game.GetKoeffFinalOver(centralRow)) + ";"; // перед матчем
                            row += GetMove(game.GetKoeffStartOver(centralRow), game.GetKoeffFinalOver(centralRow)) + ";"; // итого

                            // нижний 
                            var upper = filteredRows.ElementAt(2);
                            var upperRow = upper.Value.FirstOrDefault(); // тут может не быть значения
                            row += GetExodusRow(game, upperRow, centralTotal.Value + 1); // исход
                            row += (game.GetKoeff(upperRow).ToString() ?? string.Empty) + ";"; // коэфф
                            row += GetMove(game.GetKoeffStartOver(upperRow), game.GetKoeffSecondOver(upperRow)) + ";"; // исходный
                            row += GetMove(game.GetKoeffPenultimateOver(upperRow), game.GetKoeffFinalOver(upperRow)) + ";"; // перед матчем
                            row += GetMove(game.GetKoeffStartOver(upperRow), game.GetKoeffFinalOver(upperRow)) + ";"; // итого
                        }
                    }
                }
                catch(Exception ex)
                {
                    Log.Logger.Error(ex, $"Не удалось отфильтровать коэффициенты игры: {game.Name}. ");
                    row = null;
                }

                if (row != null)
                    rows.Add(row);
            }

            Log.Logger.Information("Фильтрация завершена.");

            return rows;
        }

        private static string GetExodusRow(Game game, BookmakerGameKoeff? row, double total)
        {
            if (game.GetExodus(row) != null)
                return $"{game.GetExodus(row)}({total});";
            else
                return ";";
        }

        private static string GetMove(double? first, double? second)
        {
            if (first == null || second == null)
                return string.Empty;

            if (first < second)
                return "вырос";

            if (first > second)
                return "упал";

            return string.Empty;
        }

        public static string Serialize(List<Game> games)
        {
            try
            {
                Log.Logger.Information("Serialize");
                return JsonSerializer.Serialize(games);
            }
            catch(Exception ex)
            {
                Log.Logger.Error(ex, $"Не удалось сериализовать обьект истории игр. " + ex.Message);
                return string.Empty;
            }
        }

        public static List<Game> Deserialize(string filePath)
        {
            try
            {
                Log.Logger.Information("Deserialize");
                var str = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<List<Game>>(str);
            }
            catch(Exception ex)
            {
                Log.Logger.Error(ex, $"Не удалось десериализовать обьект истории игр. " + ex.Message);
                return null;
            }
        }


        // фильтруем по тоталам
        // находим ЦЕНТРАЛЬНЫЙ ТОТАЛ И ДВА СОСЕДНИХ +1 и -1
        public static Dictionary<double, List<BookmakerGameKoeff>> FilterBookmakersAndGroupByTotals(string bookmakerName, Game game)
        {
            var selectedBookmakerRows = game.BookmakersRows.Where(row => row.BookmakerName == bookmakerName);
            var result = new Dictionary<double, List<BookmakerGameKoeff>>();
            var debugStr = string.Empty;
            try
            {
                debugStr += $"Name: {game.Name} ";

                var centralTotal = game.GetCantralTotal();
                if (centralTotal == null)
                {
                    Log.Logger.Warning($"У букмекеров игры {game.Name} нет подходящих тоталов, пропускаем");
                    return result; 
                }

                debugStr += $"Вычисленный тотал по игре: {centralTotal} ";
                var leftTotal = centralTotal.Value - 1;
                var rightTotal = centralTotal.Value + 1;

                var leftTotalRows = selectedBookmakerRows.Where(f => f.Total == leftTotal).ToList();
                result.Add(leftTotal, leftTotalRows);

                var centralTotalRows = selectedBookmakerRows.Where(f => f.Total == centralTotal).ToList();
                result.Add(centralTotal.Value, centralTotalRows);

                var rightTotalRows = selectedBookmakerRows.Where(f => f.Total == rightTotal).ToList();
                result.Add(rightTotal, rightTotalRows);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, $"Не удалось отфильтровать букмекеров по тоталу. {debugStr}. " + ex.Message);
            }

            return result;
        }

        public static void SaveToFile(string str, string filename)
        {
            var debugStr = string.Empty;
            try
            {
                var appPath = AppDomain.CurrentDomain.BaseDirectory;
                string filePath = $"{appPath}{filename}";
                Log.Logger.Information($"Попытка сохранить строки в файл json {filePath}");
                debugStr += $"filePath: {filePath} ";
                if (File.Exists(filePath))
                    File.Delete(filePath);

                File.WriteAllText(filePath, str);
                Log.Logger.Information($"Сохранено успешно в файл json {filePath}");
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, $"Не удалось сохранить строки в файл json: {debugStr}. " + ex.Message);
            }
        }

        public static void WriteResultsCSV(List<string> rows, string filename)
        {
            var debugStr = string.Empty;
            try
            {
                var appPath = AppDomain.CurrentDomain.BaseDirectory;
                string filePath = $"{appPath}{filename}";
                Log.Logger.Information($"Попытка сохранить строки в файл csv {filePath}");
                debugStr += $"filePath: {filePath} ";
                if (File.Exists(filePath))
                    File.Delete(filePath);

                File.WriteAllLines(filePath, rows);
                Log.Logger.Information($"Сохранено успешно в файл csv {filePath}");
            }
            catch(Exception ex)
            {
                Log.Logger.Error(ex, $"Не удалось сохранить строки в файл csv: {debugStr}. " + ex.Message);
            }
        }
    }
}
