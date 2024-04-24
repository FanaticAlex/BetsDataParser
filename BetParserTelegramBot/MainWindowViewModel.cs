using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetParserTelegramBot
{
    internal class MainWindowViewModel
    {
        public ObservableCollection<UserInfo> Users { get; set; } = [];
    }
}
