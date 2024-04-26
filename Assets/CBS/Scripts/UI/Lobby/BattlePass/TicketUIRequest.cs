using CBS.Models;
using System;

namespace CBS.UI
{
    public class TicketUIRequest
    {
        public BattlePassTicket Ticket;
        public bool Puchased;
        public Action<string, string, int> PurchaseRequest;
    }
}
