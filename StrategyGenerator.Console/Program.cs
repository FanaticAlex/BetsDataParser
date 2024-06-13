// See https://aka.ms/new-console-template for more information
using ExcelDataReader;
using StrategyGenerator.Console;
using System.Data;
using System.Text.RegularExpressions;

// генерировать стратегии
var strategy1Str = new List<string>() { "", "U", "", "", "", "", "", "", "", "", "", "" };
var strategy2Str = new List<string>() { "", "V", "", "", "", "", "", "", "", "", "", "" };
var strategy3Str = new List<string>() { "", "", "U", "", "", "", "", "", "", "", "", "" };
var strategy1 = new SimpleIfStrategy(strategy1Str);
var strategy2 = new SimpleIfStrategy(strategy2Str);
var strategy3 = new SimpleIfStrategy(strategy3Str);
var strategies = new List<SimpleIfStrategy>() { strategy1, strategy2, strategy3 };

var gameItems = GetGameItems("totalmy.xlsx");
foreach (var strategy in strategies)
{
    foreach (var gameItem in gameItems)
    {
        if (!strategy.IsFit(gameItem))
            continue;

        strategy.IsSuccess(gameItem);
    }

    Console.WriteLine(strategy.ToString());
}

Console.ReadLine();


static List<GameItem> GetGameItems(string filename)
{
    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

    var originalFileName = filename;
    using (var stream = File.Open(originalFileName, FileMode.Open, FileAccess.Read))
    {
        IExcelDataReader reader;

        // Create Reader - old until 3.4+
        ////var file = new FileInfo(originalFileName);
        ////if (file.Extension.Equals(".xls"))
        ////    reader = ExcelDataReader.ExcelReaderFactory.CreateBinaryReader(stream);
        ////else if (file.Extension.Equals(".xlsx"))
        ////    reader = ExcelDataReader.ExcelReaderFactory.CreateOpenXmlReader(stream);
        ////else
        ////    throw new Exception("Invalid FileName");
        // Or in 3.4+ you can only call this:
        reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream);

        //// reader.IsFirstRowAsColumnNames
        var conf = new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = true
            }
        };

        var dataSet = reader.AsDataSet(conf);
        var dataTable = dataSet.Tables[0];

        var gameItems = new List<GameItem>();
        for (var i = 1; i < dataTable.Rows.Count; i++) // первая строка заголовок
        {
            var gameItem = new GameItem();
            for (var j = 0; j < dataTable.Columns.Count; j++)
            {
                var data = dataTable.Rows[i][j];
                if (j == 1)
                {
                    gameItem.Target = data.ToString();
                    continue;
                }

                gameItem.Attributes.Add(data.ToString());
            }

            gameItems.Add(gameItem);
        }

        return gameItems;
    }


}