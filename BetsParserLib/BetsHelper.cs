using OpenQA.Selenium.DevTools.V117.Debugger;
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
        public static List<string> Translate1(List<Game> games)
        {
            Log.Logger.Information("Попытка преобразовать игры в строки");

            var rows = new List<string>();
            foreach(var game in games)
            {
                if (game.BookmakersRows.Count == 0)
                    continue;

                var row = string.Empty;
                row += game.Date + ";";  // дата
                row += game.LeagueName + ";"; // лига
                row += game.Name + ";"; // название игры

                try
                {
                    foreach (var bookmakerName in _bookmakersNames)
                    {
                        var filteredRows = FilterBookmakersAndGroupByTotals(bookmakerName, game);
                        // нижний 
                        var lower = filteredRows.ElementAt(0);
                        var lowerRow = lower.Value.FirstOrDefault(); // тут может не быть значения
                        row += game.GetExodus(lowerRow) != null ? game.GetExodus(lowerRow) + ";" : string.Empty + ";"; // исход
                        row += game.GetKoeffFinal(lowerRow) != null ? game.GetKoeffFinal(lowerRow).Value + ";" : string.Empty + ";"; // коэфф
                        row += GetMove(game.GetKoeffStart(lowerRow), game.GetKoeffSecond(lowerRow)) + ";"; // исходный
                        row += GetMove(game.GetKoeffPenultimate(lowerRow), game.GetKoeffFinal(lowerRow)) + ";"; // перед матчем
                        row += GetMove(game.GetKoeffStart(lowerRow), game.GetKoeffFinal(lowerRow)) + ";"; // итого

                        // нижний 
                        var central = filteredRows.ElementAt(1);
                        var centralRow = central.Value.FirstOrDefault(); // тут может не быть значения
                        row += game.GetExodus(centralRow) != null ? game.GetExodus(centralRow) + ";" : string.Empty + ";"; // исход
                        row += game.GetKoeffFinal(centralRow) != null ? game.GetKoeffFinal(centralRow).Value + ";" : string.Empty + ";"; // коэфф
                        row += GetMove(game.GetKoeffStart(centralRow), game.GetKoeffSecond(centralRow)) + ";"; // исходный
                        row += GetMove(game.GetKoeffPenultimate(centralRow), game.GetKoeffFinal(centralRow)) + ";"; // перед матчем
                        row += GetMove(game.GetKoeffStart(centralRow), game.GetKoeffFinal(centralRow)) + ";"; // итого

                        // нижний 
                        var upper = filteredRows.ElementAt(2);
                        var upperRow = upper.Value.FirstOrDefault(); // тут может не быть значения
                        row += game.GetExodus(upperRow) != null ? game.GetExodus(upperRow) + ";" : string.Empty + ";"; // исход
                        row += game.GetKoeffFinal(upperRow) != null ? game.GetKoeffFinal(upperRow).Value + ";" : string.Empty + ";"; // коэфф
                        row += GetMove(game.GetKoeffStart(upperRow), game.GetKoeffSecond(upperRow)) + ";"; // исходный
                        row += GetMove(game.GetKoeffPenultimate(upperRow), game.GetKoeffFinal(upperRow)) + ";"; // перед матчем
                        row += GetMove(game.GetKoeffStart(upperRow), game.GetKoeffFinal(upperRow)) + ";"; // итого
                    }
                }
                catch(Exception ex)
                {
                    Log.Logger.Error($"Не удалось преобразовать обьект в строку: {game.Name}. " + ex.Message);
                    row = null;
                }

                if (row != null)
                    rows.Add(row);
            }

            return rows;
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
                Log.Logger.Error($"Не удалось сериализовать обьект истории игр. " + ex.Message);
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
                Log.Logger.Error($"Не удалось десериализовать обьект истории игр. " + ex.Message);
                return null;
            }
        }


        // фильтруем по тоталам
        // находим НЕЦЕЛЫЙ ТОТАЛ, у которого меньший коэффициент (из Over и Under) ближе к 1.9, чем у других нецелых тоталов.
        // берем его И ДВА СОСЕДНИХ +1 и -1
        public static Dictionary<double, List<BookmakerGameKoeff>> FilterBookmakersAndGroupByTotals(string bookmakerName, Game game)
        {
            var selectedBookmakerRows = game.BookmakersRows.Where(row => row.BookmakerName == bookmakerName);
            var result = new Dictionary<double, List<BookmakerGameKoeff>>();
            var debugStr = string.Empty;
            try
            {
                debugStr += $"Name: {game.Name} ";
                var cantralTotal = GetCantralTotal(game);
                debugStr += $"Вычисленный тотал по игре: {cantralTotal} ";
                var leftTotal = cantralTotal - 1;
                var rightTotal = cantralTotal + 1;

                var leftTotalRows = selectedBookmakerRows.Where(f => f.Total == leftTotal).ToList();
                result.Add(leftTotal, leftTotalRows);

                var centralTotalRows = selectedBookmakerRows.Where(f => f.Total == cantralTotal).ToList();
                result.Add(cantralTotal, centralTotalRows);

                var rightTotalRows = selectedBookmakerRows.Where(f => f.Total == rightTotal).ToList();
                result.Add(rightTotal, rightTotalRows);
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"Не удалось отфильтровать результат игры: {debugStr}. " + ex.Message);
            }

            return result;
        }

        /// <summary>
        /// Возвращает три НЕЦЕЛЫХ тотала центральный +1 и -1
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        private static double GetCantralTotal(Game game)
        {
            var sortedForecasts = game.BookmakersRows
                .OrderBy(f => Math.Min(f.Over.Last().Value, Math.Abs(f.Under.Last().Value - 1.9)));

            var centralTotal = sortedForecasts
                .Select(f => f.Total)
                .Where(total => total % 1 != 0) // нецелый
                .First();

            return centralTotal;
        }

        public static void SaveToFile(string str, string filename)
        {
            Log.Logger.Information($"Попытка сохранить в файл {filename}");
            var debugStr = string.Empty;
            try
            {
                var appPath = AppDomain.CurrentDomain.BaseDirectory;
                string filePath = $"{appPath}{filename}";
                Log.Logger.Information($"Попытка сохранить строки в файл {filePath}");
                debugStr += $"filePath: {filePath} ";
                if (File.Exists(filePath))
                    File.Delete(filePath);

                File.WriteAllText(filePath, str);
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"Не удалось сохранить строки в файл: {debugStr}. " + ex.Message);
            }
        }

        public static void WriteResultsCSV(List<string> rows, string filename)
        {
            Log.Logger.Information($"Попытка сохранить строки в файл {filename}");
            var debugStr = string.Empty;
            try
            {
                var appPath = AppDomain.CurrentDomain.BaseDirectory;
                string filePath = $"{appPath}{filename}";
                Log.Logger.Information($"Попытка сохранить строки в файл {filePath}");
                debugStr += $"filePath: {filePath} ";
                if (File.Exists(filePath))
                    File.Delete(filePath);

                File.WriteAllLines(filePath, rows);
            }
            catch(Exception ex)
            {
                Log.Logger.Error($"Не удалось сохранить строки в файл: {debugStr}. " + ex.Message);
            }
        }
    }
}
