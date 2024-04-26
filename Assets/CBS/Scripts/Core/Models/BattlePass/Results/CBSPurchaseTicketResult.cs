

namespace CBS.Models
{
    public class CBSPurchaseTicketResult : CBSBaseResult
    {
        public string BattlePassID;
        public string BattlePassInstanceID;
        public string TicketID;
        public string TicketCatalogID;
        public BattlePassTicket Ticket;
        public string PriceCode;
        public int PriceValue;
    }
}