using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StrategyGenerator.Console
{
    internal class GameItem
    {
        public List<string> Attributes { get; set; } = [];
        public string Target { get; set; }
    }
}
