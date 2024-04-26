using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using System;
using System.Linq;
using UnityEngine;

namespace CBS.UI
{
    public class BattlePassTicketsDrawer : MonoBehaviour
    {
        [SerializeField]
        private TicketScroller TicketScroller;

        public Action ReloadRequest { get; set; }

        private string BattlePassID { get; set; }
        private IBattlePass BattlePass { get; set; }
        private BattlePassPrefabs PassPrefabs { get; set; }

        private void Awake()
        {
            BattlePass = CBSModule.Get<CBSBattlePassModule>();
            PassPrefabs = CBSScriptable.Get<BattlePassPrefabs>();
        }

        public void Draw(BattlePassInstance instance, BattlePassUserInfo state)
        {
            BattlePassID = instance.ID;
            var tickets = instance.GetPaidTickets();
            var purchasedTicketIDs = state.PurchasedTickets ?? new string[] { };
            var requestList = tickets.Select(x => new TicketUIRequest
            {
                Ticket = x,
                PurchaseRequest = PurchaseTicketRequest,
                Puchased = purchasedTicketIDs.Contains(x.GetCatalogID())
            }).ToList();

            var slotPrefab = PassPrefabs.TicketSlot;
            TicketScroller.Spawn(slotPrefab, requestList);
        }

        // events
        private void PurchaseTicketRequest(string ticketID, string code, int value)
        {
            if (code == PlayfabUtils.REAL_MONEY_CODE)
            {
                BattlePass.PurchaseTicketWithRealMoney(BattlePassID, ticketID, OnPurchaseTicketWithRM);
            }
            else
            {
                BattlePass.PurchaseTicket(BattlePassID, ticketID, OnPurchaseTicket);
            }
        }

        private void OnPurchaseTicket(CBSPurchaseTicketResult result)
        {
            if (result.IsSuccess)
            {
                ReloadRequest?.Invoke();
                new PopupViewer().ShowSimplePopup(new PopupRequest
                {
                    Title = BattlePassTXTHandler.PurchaseTitle,
                    Body = BattlePassTXTHandler.PurchaseBody
                });
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }

        private void OnPurchaseTicketWithRM(CBSPurchaseTicketWithRMResult result)
        {
            if (result.IsSuccess)
            {
                ReloadRequest?.Invoke();
                new PopupViewer().ShowSimplePopup(new PopupRequest
                {
                    Title = BattlePassTXTHandler.PurchaseTitle,
                    Body = BattlePassTXTHandler.PurchaseBody
                });
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }
    }
}
