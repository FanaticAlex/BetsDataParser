using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using Serilog;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace BetsParserLib
{
    public class LoadingProgress
    {
        public int GamesLoaded { get; set; }
        public int GamesCount { get; set; }
        public int ForecastsLoaded { get; set; }
        public int ForecastsCount { get; set; }
        public string LoadingObjectName { get; set; }
    }

    public class Game
    {
        public string LeagueName { get; set; }
        public string Name { get; set; }
        public string Date { get; set; }
        public string Result { get; set; }
        public List<Forecast> Forecasts { get; set; } = new List<Forecast>();
        public string Reference { get; set; }

        private static IWebElement? GetElementIfExist(IWebElement webElement, string className)
        {
            if (webElement.FindElements(By.ClassName(className)).Count > 0)
                return webElement.FindElement(By.ClassName(className));

            return null;
        }

        public static List<Game> GetGames(DateTime date, IProgress<LoadingProgress> progress)
        {
            Log.Logger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .WriteTo.File(path: $"logs\\Log.txt", rollingInterval: RollingInterval.Day)
                    .CreateLogger();

            var chromeOptions = new ChromeOptions();
            chromeOptions.AddArguments("headless");

            using (var driver = new ChromeDriver(chromeOptions))
            {
                var gamePageUrl = $"https://www.betexplorer.com/hockey/results/?year=2023&month={date.Month}&day={date.Day}";
                driver.Url = gamePageUrl;

                var CurrentProgress = new LoadingProgress() { LoadingObjectName = "Загрузка списка игр..." };
                progress.Report(CurrentProgress);

                Thread.Sleep(1000); // ожидание окончания загрузки страницы!

                var acceptCookiesBtn = driver.FindElement(By.XPath("//*[@id='onetrust-accept-btn-handler']"));
                acceptCookiesBtn.Click(); // закрываем окно кукесов

                IWebElement rootTable = driver.FindElement(By.XPath("//*[@id=\"nr-all\"]/div[3]/div/table"));
                var children = rootTable.FindElements(By.XPath(".//tbody")); // получаем список игр

                var games = new List<Game>();
                foreach (var child in children)
                {
                    var leagueName = string.Empty;
                    try
                    {
                        leagueName = child.FindElement(By.XPath(".//tr[1]/th[1]/a")).Text;
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Error("Не удалось прочитать название лиги. " + ex.Message);
                    }

                    var matchesElements = child.FindElements(By.XPath(".//tr")).ToList(); // список матчей лиги
                    for (int i = 1; i < matchesElements.Count; i++)
                    {
                        var debugStr = string.Empty;
                        try
                        {
                            var game = new Game();
                            var mainElement = GetElementIfExist(matchesElements[i], "table-main__tt");
                            game.Name = mainElement?.FindElement(By.XPath(".//a")).Text;
                            debugStr += $"Name {game.Name} ";
                            game.Result = GetElementIfExist(matchesElements[i], "table-main__result")?.FindElement(By.XPath(".//a")).Text;
                            debugStr += $"Result {game.Result} ";
                            game.Date = date.ToString("d");
                            debugStr += $"Date {game.Date} ";
                            game.Reference = mainElement?.FindElement(By.XPath(".//a")).GetAttribute("href");
                            debugStr += $"Reference {game.Reference} ";
                            game.LeagueName = leagueName;
                            games.Add(game);
                        }
                        catch(Exception ex)
                        {
                            Log.Logger.Error($"Не удалось прочитать чать параметров матча: {debugStr}. " + ex.Message);
                        }
                    }
                }

                Thread.Sleep(100);
                for(int i = 0; i < games.Count; i++)
                {
                    var game = games[i];

                    Log.Logger.Information($"Парсим игру: {game.Name}");

                    CurrentProgress.LoadingObjectName = game.Name;
                    CurrentProgress.GamesCount = games.Count;
                    progress.Report(CurrentProgress);

                    game.Forecasts = game.GetForecasts(driver, game, progress, CurrentProgress);

                    CurrentProgress.GamesLoaded = (i + 1);
                    progress.Report(CurrentProgress);

                }

                return games;
            }
        }

        public List<Forecast> GetForecasts(
            ChromeDriver driver, Game game, IProgress<LoadingProgress> progress, LoadingProgress currentProgress)
        {
            List<Forecast> forecasts = new List<Forecast>();
            driver.Url = game.Reference;

            List<IWebElement> blocks;
            try
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));
                wait.Until(drv => drv.FindElements(By.XPath("//*[@id=\"odds-all\"]/div[1]/div/ul/li[3]/a")).ToList().Count() > 0);
                var over_under = driver.FindElement(By.XPath("//*[@id=\"odds-all\"]/div[1]/div/ul/li[3]/a")); // вкладка Over/Under
                over_under.Click();
                Thread.Sleep(1000);
                var wait1 = new WebDriverWait(driver, TimeSpan.FromSeconds(5));
                wait1.Until(drv => drv.FindElements(By.Id("odds-preloader")).ToList().Count() > 0);
                var wait2 = new WebDriverWait(driver, TimeSpan.FromSeconds(5));
                wait2.Until(drv => drv.FindElement(By.Id("odds-preloader")).GetAttribute("style") == "display: none;");
                blocks = driver.FindElements(By.XPath("//tr[@data-bid]")).ToList();
            }
            catch(Exception ex)
            {
                Log.Logger.Error($"Не удалось перейти на вкладку Over/Under. {ex.Message}");
                return new List<Forecast>();
            }

            for (int i = 0; i < blocks.Count; i++)
            {
                var debugStr = string.Empty;
                try
                {
                    var block = blocks[i];
                    var forecast = new Forecast();
                    forecast.CompanyName = block.FindElement(By.XPath(".//td[1]/a")).Text;
                    Log.Logger.Information($"Парсимм коэффициенты: {forecast.CompanyName}");
                    debugStr += $"CompanyName: {forecast.CompanyName} ";
                    forecast.Total = double.Parse(block.FindElement(By.ClassName("table-main__doubleparameter")).Text, CultureInfo.InvariantCulture);
                    debugStr += $"Total: {forecast.Total} ";
                    
                    var overElement = block.FindElements(By.XPath(".//td[@data-odd]"))[0];
                    forecast.Over = GetHistory(driver, overElement);

                    var underElement = block.FindElements(By.XPath(".//td[@data-odd]"))[1];
                    forecast.Under = GetHistory(driver, underElement);

                    forecasts.Add(forecast);

                    currentProgress.ForecastsLoaded = (i + 1);
                    currentProgress.ForecastsCount = blocks.Count;
                    progress.Report(currentProgress);
                }
                catch(Exception ex)
                {
                    Log.Logger.Error($"Не удалось получить параметры {debugStr}. {ex.Message}");
                }
            }

            return forecasts;
        }

        public List<HistoryRow> GetHistory(ChromeDriver driver, IWebElement overElement)
        {
            var displayedHistory = new List<IWebElement>();
            try
            {
                Actions action = new Actions(driver);
                action.MoveToElement(overElement).Perform(); // наводимся на элемент для показа истории
                Thread.Sleep(100);
                var aodds = driver.FindElement(By.XPath("//*[@id='aodds-desktop']"));
                displayedHistory = aodds.FindElements(By.XPath(".//tbody[contains(@id, 'aodds')]")).Where(e => e.Displayed).ToList();
            }
            catch(Exception ex )
            {
                Log.Logger.Error($"Не удалось получить навести курсор на историю коэффициентов. {ex.Message}");
                return new List<HistoryRow>(); ;
            }

            var history = new List<HistoryRow>();
            if (displayedHistory.Count() == 1) // история есть
            {
                var historyBlock = displayedHistory.Single();
                var historyRows = historyBlock.FindElements(By.XPath(".//tr"));
                foreach (var historyRow in historyRows)
                {
                    if (historyRow.FindElements(By.XPath(".//td")).Count > 0)
                    {
                        var debugStr = string.Empty;
                        try
                        {
                            var row = new HistoryRow();
                            var dateStr = historyRow.FindElement(By.XPath(".//td[1]")).Text;
                            debugStr += $"Date: {dateStr} ";
                            row.Date = dateStr == string.Empty ? null : DateTime.ParseExact(dateStr, "dd.MM. HH:mm", CultureInfo.InvariantCulture);
                            var valueStr = historyRow.FindElement(By.XPath(".//td[2]")).Text;
                            debugStr += $"value: {valueStr} ";
                            row.Value = valueStr == string.Empty ? 0 : double.Parse(valueStr, CultureInfo.InvariantCulture);
                            var differenceStr = historyRow.FindElement(By.XPath(".//td[3]")).Text;
                            debugStr += $"difference: {differenceStr} ";
                            row.Difference = differenceStr == string.Empty ? 0 : double.Parse(differenceStr, CultureInfo.InvariantCulture);
                            history.Add(row);
                        }
                        catch(Exception ex)
                        {
                            Log.Logger.Error($"Не удалось получить историю коээфициентов: {debugStr}. {ex.Message}");
                        }
                    }
                }
            }
            else // истории нет
            {
                try
                {
                    var singleHistoryRow = new HistoryRow();
                    singleHistoryRow.Date = null;
                    singleHistoryRow.Value = double.Parse(overElement.FindElement(By.XPath(".//span[text()]")).Text, CultureInfo.InvariantCulture);
                    history.Add(singleHistoryRow);
                }
                catch(Exception ex)
                {
                    Log.Logger.Error($"Не удалось получить коэффициент без истории. {ex.Message}");
                }
            }

            return history.OrderBy(r => r.Date).ToList();
        }

        public int GetExodus(Forecast forecast)
        {
            if (Result == null)
                return -1;

            try
            {
                var first = int.Parse(Result.Split(" ")[0].Split(":")[0], CultureInfo.InvariantCulture);
                var second = int.Parse(Result.Split(" ")[0].Split(":")[1], CultureInfo.InvariantCulture);
                var goleCount = first + second;
                var exodus = (goleCount > forecast.Total) ? 2 : 0;
                return exodus;
            }
            catch(Exception ex)
            {
                Log.Logger.Error($"Не удалось получить ИСХОД {forecast.CompanyName}, {forecast.Over}. {ex.Message}");
                return -1;
            }
        }

        public double GetKoeffStart(Forecast forecast)
        {
            return (GetExodus(forecast) == 2) ? forecast.Over.First().Value : forecast.Under.First().Value;
        }

        public string? GetKoeffSecond(Forecast forecast) // второй
        {
            if (GetExodus(forecast) == 2)
            {
                if (forecast.Over.Count <= 2)
                    return null;

                return forecast.Over[1].Value.ToString();
            }
            else
            {
                if (forecast.Under.Count <= 2)
                    return null;

                return forecast.Under[1].Value.ToString();
            }
        }

        public string? GetKoeffPenultimate(Forecast forecast) // предпоследний
        {
            if (GetExodus(forecast) == 2)
            {
                if (forecast.Over.Count <= 2)
                    return null;

                var penultimateIndex = forecast.Over.Count - 2;
                return forecast.Over[penultimateIndex].Value.ToString();
            }
            else
            {
                if (forecast.Under.Count <= 2)
                    return null;

                var penultimateIndex = forecast.Under.Count - 2;
                return forecast.Under[penultimateIndex].Value.ToString();
            }
        }

        public double GetKoeffFinal(Forecast forecast)
        {
            return (GetExodus(forecast) == 2) ? forecast.Over.Last().Value : forecast.Under.Last().Value;
        }
    }
}
