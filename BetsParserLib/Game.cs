using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using Serilog;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using Serilog.Core;

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
        /// <summary>
        /// Название лиги
        /// </summary>
        public string LeagueName { get; set; }
        /// <summary>
        /// Название игры
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Дата игры
        /// </summary>
        public string Date { get; set; }
        /// <summary>
        /// Результат игры
        /// </summary>
        public string Result { get; set; }
        /// <summary>
        /// Коэффициенты игры
        /// </summary>
        public List<BookmakerGameKoeff> BookmakersRows { get; set; } = new List<BookmakerGameKoeff>();
        /// <summary>
        /// Ссылка на страницу игры
        /// </summary>
        public string Reference { get; set; }

        /// <summary>
        /// Возвращает список игр в заданную дату.
        /// </summary>
        /// <param name="date"></param>
        /// <param name="progress"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static List<Game> GetGames(DateTime date, IProgress<LoadingProgress> progress, CancellationToken token)
        {
            // Инициализация
            Log.Logger = new LoggerConfiguration()
                    .WriteTo.Debug()
                    .WriteTo.Console()
                    .WriteTo.File(path: $"logs\\Log.txt", rollingInterval: RollingInterval.Day)
                    .CreateLogger();

            var chromeOptions = new ChromeOptions();
            chromeOptions.AddArguments("headless");
            var CurrentProgress = new LoadingProgress();

            Log.Logger.Information($"Начинаем парсить игры на дату: {date.ToString("d")}");

            using (var driver = new ChromeDriver(chromeOptions))
            {
                LoadGamesList(date, progress, CurrentProgress, driver);
                CloseCookiesWindow(driver);
                ReadOnlyCollection<IWebElement> leaguesElements = GetLeaguesRowsElements(driver);

                Log.Logger.Information($"Начинаем парсить список игр в каждой лиге");
                var games = new List<Game>();
                foreach (var leagueElement in leaguesElements)
                {
                    string leagueName = GetLeagueName(leagueElement);
                    if (leagueName == null)
                    {
                        Log.Logger.Information($"Пропускаем строку, тк лига не найдена");
                        continue;
                    }

                    Log.Logger.Information($"Начинаем парсить игру лигу {leagueName}");
                    var gamesElements = leagueElement.FindElements(By.XPath(".//tr")).ToList(); // список матчей лиги

                    for (int i = 1; i < gamesElements.Count; i++) // начинаем со второго элемента
                    {
                        if (token.IsCancellationRequested)
                        {
                            Log.Logger.Information($"Парсинг остановлен.");
                            return new List<Game>();
                        }

                        var gameElement = gamesElements[i];
                        Game game = ReadGame(date, leagueName, gameElement);
                        Log.Logger.Information($"Игра добавлена в список: {game.Name}");
                        if (game != null)
                            games.Add(game);
                    }
                }

                // начинаем парсить букмекеров
                Thread.Sleep(100);
                for (int i = 0; i < games.Count; i++)
                {
                    if (token.IsCancellationRequested)
                        break;

                    var game = games[i];
                    Log.Logger.Information($"Начинаем парсить игру: {game.Name}");

                    CurrentProgress.LoadingObjectName = game.Name;
                    CurrentProgress.GamesCount = games.Count;
                    progress.Report(CurrentProgress);

                    game.BookmakersRows = game.GetBookmakersRows(driver, game, progress, CurrentProgress, token);

                    CurrentProgress.GamesLoaded = (i + 1);
                    progress.Report(CurrentProgress);
                }

                return games;
            }
        }

        public List<BookmakerGameKoeff> GetBookmakersRows(
            ChromeDriver driver, Game game, IProgress<LoadingProgress> progress, LoadingProgress currentProgress, CancellationToken token)
        {
            driver.Url = game.Reference;

            List<IWebElement> bookmakersElements = GetBookmakersElements(driver);
            if (bookmakersElements == null)
                return new List<BookmakerGameKoeff>();

            List<BookmakerGameKoeff> bookmakersRows = new List<BookmakerGameKoeff>();
            for (int i = 0; i < bookmakersElements.Count; i++)
            {
                if (token.IsCancellationRequested)
                    break;

                var block = bookmakersElements[i];
                BookmakerGameKoeff bookmakerRow = ReadBoolmakerRow(driver, block);

                bookmakersRows.Add(bookmakerRow);

                currentProgress.ForecastsLoaded = (i + 1);
                currentProgress.ForecastsCount = bookmakersElements.Count;
                progress.Report(currentProgress);
            }

            return bookmakersRows;
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
                Log.Logger.Error(ex, $"Нет элеммента истории коэффициентов. {ex.Message}");
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
                            Log.Logger.Error(ex, $"Не удалось получить историю коээфициентов: {debugStr}. {ex.Message}");
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
                    Log.Logger.Error(ex, $"Не удалось получить коэффициент без истории. {ex.Message}");
                }
            }

            return history.OrderBy(r => r.Date).ToList();
        }

        public int? GetExodus(BookmakerGameKoeff? forecast)
        {
            if (forecast == null)
                return null;

            if (Result == null)
                return null;

            try
            {
                // формат результата:  "2:3" или "2:3 ET" или "2:3 PEN"
                // на странице матча - ET - "After Extra Time" и PEN - "After Penalties"
                // в случае ET и PEN вычитаем единицу из числа голов, тк он был забит по пенальти или в доп время.
                var additionalGole = 0;
                if (Result.Split(" ").Length > 1)
                {
                    var additional = Result.Split(" ")[1];
                    if (additional.Contains("ET") || additional.Contains("PEN."))
                        additionalGole = 1;
                }

                var score = Result.Split(" ")[0];
                var first = int.Parse(score.Split(":")[0], CultureInfo.InvariantCulture);
                var second = int.Parse(score.Split(":")[1], CultureInfo.InvariantCulture);
                var goleCount = first + second - additionalGole;
                var exodus = (goleCount > forecast.Total) ? 2 : 0;
                return exodus;
            }
            catch(Exception ex)
            {
                Log.Logger.Error(ex, $"Не удалось получить ИСХОД {forecast.BookmakerName}, {forecast.Over}. {ex.Message}");
                return null;
            }
        }

        public double? GetKoeffSecondOver(BookmakerGameKoeff? forecast) // второй
        {
            if (forecast == null)
                return null;

            if (forecast.Over.Count < 2)
                return null;

            return forecast.Over[1].Value;
        }

        public double? GetKoeffPenultimateOver(BookmakerGameKoeff? forecast) // предпоследний
        {
            if (forecast == null)
                return null;

            if (forecast.Over.Count <= 2)
                return null;

            var penultimateIndex = forecast.Over.Count - 2;
            return forecast.Over[penultimateIndex].Value;
        }

        public double? GetKoeffStartOver(BookmakerGameKoeff? forecast)
        {
            if (forecast == null)
                return null;

            if (!forecast.Over.Any())
                return null;

            return forecast?.Over.First().Value;
        }

        public double? GetKoeffStartUnder(BookmakerGameKoeff? forecast)
        {
            if (forecast == null)
                return null;

            if (!forecast.Under.Any())
                return null;

            return forecast?.Under.First().Value;
        }

        /// <summary>
        /// в поле Коэф ставим коэффициент перед матчем (то есть последний из истории),
        /// который сыграл: если Исход = 2, то Over, если Исход = 0, то Under
        /// </summary>
        public double? GetKoeffFinalOver(BookmakerGameKoeff? forecast)
        {
            if (forecast == null)
                return null;

            if (!forecast.Over.Any())
                return null;

            return forecast?.Over.Last().Value;
        }

        public double? GetKoeffFinalUnder(BookmakerGameKoeff? forecast)
        {
            if (forecast == null)
                return null;

            if (!forecast.Under.Any())
                return null;

            return forecast?.Under.Last().Value;
        }

        /// <summary>
        /// в поле Коэф ставим коэффициент перед матчем (то есть последний из истории),
        /// который сыграл: если Исход = 2, то Over, если Исход = 0, то Under
        /// </summary>
        public double? GetKoeff(BookmakerGameKoeff? forecast)
        {
            if (forecast == null) return null;
            return (GetExodus(forecast) == 2) ? GetKoeffFinalOver(forecast) : GetKoeffFinalUnder(forecast);
        }

        /// <summary>
        /// Возвращает тотал у которого меньший коэффициент (из Over и Under) ближе к 1.9, чем у других нецелых тоталов, кратный 0.5
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public double? GetCantralTotal()
        {
            var sortedForecasts = BookmakersRows
                .OrderBy(f =>
                {
                    var diffOver = Math.Abs((GetKoeffFinalOver(f) ?? 0) - 1.9);
                    var difUnder = Math.Abs((GetKoeffFinalUnder(f) ?? 0) - 1.9);
                    return Math.Min(diffOver, difUnder);
                });

            var centralTotal = sortedForecasts
                .Select(f => f.Total)
                .Where(total => total % 1 == 0.5) // нецелый кратный 0.5
                .FirstOrDefault(); // null если там например только целые тоталы

            return centralTotal;
        }

        private BookmakerGameKoeff ReadBoolmakerRow(ChromeDriver driver, IWebElement block)
        {
            var bookmakerRow = new BookmakerGameKoeff();
            var debugStr = string.Empty;
            try
            {
                bookmakerRow.BookmakerName = block.FindElement(By.XPath(".//td[1]/a")).Text;
                Log.Logger.Information($"Парсим коэффициенты: {bookmakerRow.BookmakerName}");
                debugStr += $"BookmakerName: {bookmakerRow.BookmakerName} ";

                bookmakerRow.Total = double.Parse(block.FindElement(By.ClassName("table-main__doubleparameter")).Text, CultureInfo.InvariantCulture);
                debugStr += $"Total: {bookmakerRow.Total} ";

                var overElement = block.FindElements(By.XPath(".//td[@data-odd]"))[0]; // содержит парамметр data-odd
                bookmakerRow.Over = GetHistory(driver, overElement);

                var underElement = block.FindElements(By.XPath(".//td[@data-odd]"))[1];
                bookmakerRow.Under = GetHistory(driver, underElement);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, $"Не удалось получить параметры {debugStr}. {ex.Message}");
            }

            return bookmakerRow;
        }

        private static List<IWebElement> GetBookmakersElements(ChromeDriver driver)
        {
            List<IWebElement> blocks = null;
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
            catch (Exception ex)
            {
                Log.Logger.Error(ex, $"Не удалось перейти на вкладку Over/Under. {ex.Message}");
                blocks = null;
            }

            return blocks;
        }

        private static IWebElement? GetElementIfExist(IWebElement webElement, string className)
        {
            if (webElement.FindElements(By.ClassName(className)).Count > 0)
                return webElement.FindElement(By.ClassName(className));

            return null;
        }

        private static string? GetLeagueName(IWebElement leagueElement)
        {
            try
            {
                return leagueElement.FindElement(By.XPath(".//tr[1]/th[1]/a")).Text;
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Не удалось прочитать название лиги. " + ex.Message);
            }

            return null;
        }

        private static Game ReadGame(DateTime date, string leagueName, IWebElement gameElement)
        {
            Game game = null;
            var debugStr = string.Empty;
            try
            {
                game = new Game();
                var mainElement = GetElementIfExist(gameElement, "table-main__tt");
                game.Name = mainElement?.FindElement(By.XPath(".//a")).Text;
                debugStr += $"Name {game.Name} ";
                game.Result = GetElementIfExist(gameElement, "table-main__result")?.FindElement(By.XPath(".//a")).Text;
                debugStr += $"Result {game.Result} ";
                game.Reference = mainElement?.FindElement(By.XPath(".//a")).GetAttribute("href");
                debugStr += $"Reference {game.Reference} ";

                game.Date = date.ToString("d");
                game.LeagueName = leagueName;
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, $"Не удалось прочитать чать параметров матча: {debugStr}. " + ex.Message);
                game = null;
            }

            return game;
        }

        private static void LoadGamesList(DateTime date, IProgress<LoadingProgress> progress, LoadingProgress CurrentProgress, ChromeDriver driver)
        {
            var gamePageUrl = $"https://www.betexplorer.com/hockey/results/?year=2023&month={date.Month}&day={date.Day}";
            driver.Url = gamePageUrl;
            CurrentProgress.LoadingObjectName = "Загрузка списка игр...";
            progress.Report(CurrentProgress);
            Thread.Sleep(1000); // ожидание окончания загрузки страницы!
        }

        private static ReadOnlyCollection<IWebElement> GetLeaguesRowsElements(ChromeDriver driver)
        {
            IWebElement rootTable = driver.FindElement(By.XPath("//*[@id=\"nr-all\"]/div[3]/div/table"));
            var children = rootTable.FindElements(By.XPath(".//tbody")); // получаем список игр
            return children;
        }

        private static void CloseCookiesWindow(ChromeDriver driver)
        {
            var acceptCookiesBtn = driver.FindElement(By.XPath("//*[@id='onetrust-accept-btn-handler']"));
            acceptCookiesBtn.Click(); // закрываем окно кукесов
        }
    }
}
