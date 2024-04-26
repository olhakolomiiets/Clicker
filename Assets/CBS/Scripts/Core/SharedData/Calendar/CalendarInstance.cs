
using CBS.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CBS.Models
{
    [Serializable]
    public class CalendarInstance : ICustomData<CBSCalendarCustomData>
    {
        public string ID;
        public string InstanceID;
        public string DisplayName;
        public string Description;
        public bool Enabled;
        public bool Looped;
        public bool NoPenalty;
        public bool DontIncrementIndexWhenSkipping;
        public CBSPrice Price;
        public CalendarTemplate Template;
        public ActivationType Activation;
        public List<CalendarPosition> Positions;
        public CalendarWeekTemplate WeekTemplate;
        public CalendarMonthTemplate MonthTemplate;

        // states
        public int CurrentIndex;
        public bool IsAvailable;
        public int BadgeCount;
        public DateTime EndDate;

        public string CustomDataClassName { get; set; }
        public string CustomRawData { get; set; }
        public bool CompressCustomData => false;

        public virtual T GetCustomData<T>() where T : CBSCalendarCustomData
        {
            if (CompressCustomData)
                return JsonPlugin.FromJsonDecompress<T>(CustomRawData);
            else
                return JsonPlugin.FromJson<T>(CustomRawData);
        }

        public void AddPostion(CalendarPosition position)
        {
            Positions = Positions ?? new List<CalendarPosition>();
            var lastIndex = Positions.Count - 1;
            var newIndex = lastIndex + 1;
            position.Position = newIndex;
            Positions.Add(position);
        }

        public void RemoveLastPosition()
        {
            if (Positions == null || Positions.Count == 0)
                return;
            var lastIndex = Positions.Count - 1;
            Positions.RemoveAt(lastIndex);
            Positions.TrimExcess();
        }

        public List<CalendarPosition> GetCustomPositions()
        {
            Positions = Positions ?? new List<CalendarPosition>();
            return Positions;
        }

        public List<CalendarPosition> GetWeeklyPositions()
        {
            WeekTemplate = WeekTemplate ?? new CalendarWeekTemplate();
            return WeekTemplate.GetPositions();
        }

        public List<CalendarPosition> GetMonthlyPositions(int monthIndex)
        {
            MonthTemplate = MonthTemplate ?? new CalendarMonthTemplate();
            return MonthTemplate.GetPositions(monthIndex);
        }

        public void CleanUp()
        {
            if (Template == CalendarTemplate.CUSTOM)
            {
                WeekTemplate = null;
                MonthTemplate = null;
            }
            else if (Template == CalendarTemplate.MONTHLY_TEMPLATE)
            {
                WeekTemplate = null;
                Positions = null;
            }
            else if (Template == CalendarTemplate.WEEKLY_TEMPLATE)
            {
                MonthTemplate = null;
                Positions = null;
            }
            if (Activation == ActivationType.ALWAYS_AVAILABLE)
            {
                Price = null;
            }
        }

        public string GetTemplateInstanceID()
        {
            var baseValue = ID + (int)Template;
            byte[] bytes = Encoding.ASCII.GetBytes(baseValue);
            var uid = Regex.Replace(Convert.ToBase64String(bytes), "[/+=]", "");
            return uid;
        }

        public void UpdatePosition(CalendarPosition position, int index)
        {
            if (Positions == null)
                return;
            var oldPosition = Positions.FirstOrDefault(x => x.Position == index);
            var arrayIndex = Positions.IndexOf(oldPosition);
            Positions[arrayIndex] = position;
        }
    }
}
