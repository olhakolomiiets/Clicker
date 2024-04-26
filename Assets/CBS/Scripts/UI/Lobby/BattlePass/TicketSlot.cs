using CBS.Core;
using CBS.Models;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class TicketSlot : MonoBehaviour, IScrollableItem<TicketUIRequest>
    {
        [SerializeField]
        private Text DisplayName;
        [SerializeField]
        private Text Description;
        [SerializeField]
        private PurchaseButton Button;

        private BattlePassTicket Ticket { get; set; }
        private Action<string, string, int> PurchaseAction { get; set; }

        public void Display(TicketUIRequest data)
        {
            Ticket = data.Ticket;
            PurchaseAction = data.PurchaseRequest;

            DisplayName.text = Ticket.DisplayName;
            Description.text = Ticket.Description;
            Button.Init();
            Button.SetActivity(!data.Puchased);
            Button.Display(Ticket.Price, OnPurchasePress);
        }

        // events
        private void OnPurchasePress(string code, int value)
        {
            PurchaseAction?.Invoke(Ticket.ID, code, value);
        }
    }
}
