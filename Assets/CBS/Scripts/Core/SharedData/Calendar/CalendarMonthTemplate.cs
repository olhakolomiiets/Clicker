
using System;
using System.Collections.Generic;

namespace CBS.Models
{
    public class CalendarMonthTemplate
    {
        private readonly int MonthInYear = 12;

        public List<List<CalendarPosition>> Positions;

        public List<CalendarPosition> GetPositions(int monthIndex)
        {
            if (Positions == null)
            {
                Positions = new List<List<CalendarPosition>>();
                for (int i = 0; i < MonthInYear; i++)
                {
                    var monthList = new List<CalendarPosition>();
                    var year = DateTime.UtcNow.Year;
                    var daysInMonth = DateTime.DaysInMonth(year, i + 1);
                    for (int j = 0; j < daysInMonth; j++)
                    {
                        monthList.Add(new CalendarPosition
                        {
                            Position = j
                        });
                    }
                    Positions.Add(monthList);
                }
            }
            return Positions[monthIndex];
        }
    }
}

