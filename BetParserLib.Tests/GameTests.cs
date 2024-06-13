using BetsParserLib;

namespace BetParserLib.Tests
{
    public class GameTests
    {
        private Game _game;

        [SetUp]
        public void Setup()
        {
            _game = new Game();

            var firstrow = new BookmakerGameKoeff();
            firstrow.Total = 6.5;
            firstrow.Under.Add(new HistoryRow() { Date = DateTime.Now, Value = 1.5 });
            firstrow.Over.Add(new HistoryRow() { Date = DateTime.Now, Value = 1.6 });
            _game.BookmakersRows.Add(firstrow);

            var secondrow = new BookmakerGameKoeff();
            secondrow.Total = 5.5;
            secondrow.Under.Add(new HistoryRow() { Date = DateTime.Now, Value = 1.7 });
            secondrow.Over.Add(new HistoryRow() { Date = DateTime.Now, Value = 1.8 });
            _game.BookmakersRows.Add(secondrow);
        }

        [Test]
        public void Test1()
        {
            Assert.That(_game.GetCantralTotal(), Is.EqualTo(5.5));
        }
    }
}