
using System.Collections.Generic;

namespace CBS.Models
{
    public class CalendarWeekTemplate
    {
        private readonly int DaysInWeek = 7;

        public List<CalendarPosition> Position;

        public List<CalendarPosition> GetPositions()
        {
            if (Position == null)
            {
                Position = new List<CalendarPosition>();
                for (int i = 0; i < DaysInWeek; i++)
                {
                    Position.Add(new CalendarPosition { Position = i });
                }
            }
            return Position;
        }
    }
}

