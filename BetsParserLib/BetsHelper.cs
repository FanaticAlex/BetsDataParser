using Serilog;
using System.Text.Json;

namespace BetsParserLib
{
    public class BetsHelper
    {
        public static List<string> Translate(List<Game> games)
        {
            Log.Logger.Information("Попытка преобразовать игры в строки");

            var result = new List<string>();
            var header = "Название игры;" +
                         "Дата;" +
                         "Счет;" +
                         "Букмекер;" +
                         "Total;" +
                         "Исход;" +
                         "Коэф. начало;" +
                         "Коэф. второй;" +
                         "Коэф. предпоследний;" +
                         "Коэф. итоговый";
            result.Add(header);
            foreach (Game game in games)
            {
                foreach (Forecast forecast in game.Forecasts)
                {
                    var debugStr = JsonSerializer.Serialize(forecast);
                    try
                    {
                        var row = $"{game.Name};" +  // название игры
                                  $"{game.Date};" +  // дата
                                  $"{game.Result};" +  // счет
                                  $"{forecast.CompanyName};" + // букмекер
                                  $"{forecast.Total};" +  // total
                                  $"{game.GetExodus(forecast)};" + // исход
                                  $"{game.GetKoeffStart(forecast)};" + // коэффициент начало
                                  $"{game.GetKoeffSecond(forecast) ?? string.Empty};" +  // коэффициент второй
                                  $"{game.GetKoeffPenultimate(forecast) ?? string.Empty};" +  // коэффициент предпоследний
                                  $"{game.GetKoeffFinal(forecast)}"; // коэф итоговый
                        result.Add(row);
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Error($"Не удалось преобразовать обьект в строку: {debugStr}. " + ex.Message);
                    }
                }
            }

            return result;
        }


        // фильтруем по тоталам
        // находим НЕЦЕЛЫЙ ТОТАЛ, у которого меньший коэффициент (из Over и Under) ближе к 1.9, чем у других нецелых тоталов.
        // берем его И ДВА СОСЕДНИХ +1 и -1
        public static List<Game> Filter(List<Game> games)
        {
            Log.Logger.Information("Попытка фильтрации игр");
            foreach (var game in games)
            {
                var debugStr = string.Empty;
                try
                {
                    debugStr += $"Name: {game.Name} ";

                    if (game.Forecasts.Count == 0)
                        continue;

                    var sortedForecasts = game.Forecasts
                        .OrderBy(f => Math.Min(f.Over.Last().Value, Math.Abs(f.Under.Last().Value - 1.9)));

                    var centralTotal = sortedForecasts
                        .Select(f => f.Total)
                        .Where(total => total % 1 != 0) // нецелый
                        .First();

                    debugStr += $"Вычисленный тотал по игре: {centralTotal} ";

                    var acceptedTotals = new List<double> { centralTotal - 1, centralTotal, centralTotal + 1 };

                    var filteredForecasts = game.Forecasts
                        .Where(f => acceptedTotals.Contains(f.Total));

                    var filtered = new List<Forecast>();
                    foreach (var forecast in game.Forecasts)
                    {
                        if (filteredForecasts.Contains(forecast))
                            filtered.Add(forecast);
                    }

                    game.Forecasts = filtered;
                }
                catch(Exception ex)
                {
                    Log.Logger.Error($"Не удалось отфильтровать результат игры: {debugStr}. " + ex.Message);
                }
            }

            return games.Where(g => g.Forecasts.Count > 0).ToList();
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
