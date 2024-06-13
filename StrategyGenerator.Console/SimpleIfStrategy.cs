using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StrategyGenerator.Console
{
    internal class SimpleIfStrategy
    {
        private List<string> _conditions;

        public int OverallFit {  get; set; }
        public int OverallSuccess {  get; set; }

        public bool IsFit(GameItem item)
        {
            for (int i = 0; i < item.Attributes.Count(); i++)
            {
                if (_conditions[i] == string.Empty) // любое значение
                    continue;

                if (_conditions[i] != item.Attributes[i]) // не подходит
                    return false;
            }

            OverallFit++;
            return true; // подходит
        }

        public bool IsSuccess(GameItem item)
        {
            if (item.Target == "2")
            {
                OverallSuccess++;
                return true;
            }

            return false;
        }

        public double GetSuccesPercent()
        {
            return Math.Round(((double)OverallSuccess / OverallFit) * 100);
        }

        public SimpleIfStrategy(List<string> conditions)
        {
            _conditions = conditions;
        }

        public override string ToString()
        {
            return $"{string.Join(",", _conditions)}  Всего подошло:{OverallFit}  Всего угадано:{OverallSuccess}  Процент:{GetSuccesPercent()}%";
        }
    }
}
