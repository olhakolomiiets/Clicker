
using System;
using System.Collections.Generic;

namespace CBS.Models
{
    public class BattlePassUserData : BattlePassInstanceAccess
    {
        public string BattlePassID;
        public string InstanceID;
        public int CurrentExp;
        public List<int> CollectedSimpleReward = new List<int>();
        public List<int> CollectedPremiumReward = new List<int>();
        public List<string> PurchasedTickets = new List<string>();
        public DateTime? LimitStartDate;

        public void AddExp(int value)
        {
            CurrentExp += value;
        }

        public void AddPurchasedTicket(string ticketID)
        {
            PurchasedTickets = PurchasedTickets ?? new List<string>();
            if (!PurchasedTickets.Contains(ticketID))
            {
                PurchasedTickets.Add(ticketID);
            }
        }
    }
}

