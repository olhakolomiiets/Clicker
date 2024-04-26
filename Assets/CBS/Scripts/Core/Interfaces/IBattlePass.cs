using CBS.Models;
using System;

namespace CBS
{
    public interface IBattlePass
    {
        /// <summary>
        /// Notify when the reward was received.
        /// </summary>
        event Action<GrantRewardResult> OnRewardRecived;
        /// <summary>
        /// Notify when experience has been gained for a specific Battle Pass.
        /// </summary>
        event Action<string, int> OnExpirienceAdded;
        /// <summary>
        /// Notify when profile bought a ticket.
        /// </summary>
        event Action<BattlePassTicket> OnTicketPurchased;
        /// <summary>
        /// Get information about the state of the player's instances of Battle passes. Does not contain complete information about levels and rewards. More suitable for implementing badges.
        /// </summary>
        /// <param name="result"></param>
        void GetBattlePassStates(Action<CBSGetPlayerBattlePassStatesResult> result);
        /// <summary>
        /// Get information about the state of Battle Pass instance. Does not contain complete information about levels and rewards. More suitable for implementing badges.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        void GetBattlePassState(string battlePassID, Action<CBSGetPlayerBattlePassStatesResult> result);
        /// <summary>
        /// Get complete information about the state of the player's instances of Battle passes and instance levels. Contains complete information about levels and rewards.
        /// </summary>
        /// <param name="battlePassID"></param>
        /// <param name="result"></param>
        void GetBattlePassFullInformation(string battlePassID, Action<CBSGetBattlePassFullInformationResult> result);
        /// <summary>
        /// Add player experience for a specific instance of Battle Pass.
        /// </summary>
        /// <param name="instanceID"></param>
        /// <param name="exp"></param>
        /// <param name="result"></param>
        void AddExpirienceToInstance(string instanceID, int exp, Action<CBSAddExpirienceToInstanceResult> result);
        /// <summary>
        /// Add player experience for all active instances of Battle Passes.
        /// </summary>
        /// <param name="exp"></param>
        /// <param name="result"></param>
        void AddExpirienceToAllActiveInstances(int exp, Action<CBSAddExpirienceToAllInstancesResult> result);
        /// <summary>
        /// Grant the player a reward from a specific instance of Battle Pass.
        /// </summary>
        /// <param name="battlePassID"></param>
        /// <param name="level"></param>
        /// <param name="isPremium"></param>
        /// <param name="result"></param>
        void GrantAwardToProfile(string battlePassID, int level, bool isPremium, Action<CBSGrantAwardToPlayerResult> result);
        /// <summary>
        /// Reset player data for a specific Battle Pass.
        /// </summary>
        /// <param name="battlePassID"></param>
        /// <param name="result"></param>
        void ResetBattlePassStateForProfile(string battlePassID, Action<CBSResetBattlePassStateResult> result);
        /// <summary>
        /// Grant ticket to profile for active battle pass instance
        /// </summary>
        /// <param name="battlePassID"></param>
        /// <param name="ticketID"></param>
        /// <param name="result"></param>
        void GrantTicket(string battlePassID, string ticketID, Action<CBSGrantTicketResult> result);
        /// <summary>
        /// Purchase ticket for active battle pass instance
        /// </summary>
        /// <param name="battlePassID"></param>
        /// <param name="ticketID"></param>
        /// <param name="result"></param>
        void PurchaseTicket(string battlePassID, string ticketID, Action<CBSPurchaseTicketResult> result);
        /// <summary>
        /// Purchase ticket with real money for active battle pass instance
        /// </summary>
        /// <param name="battlePassID"></param>
        /// <param name="ticketID"></param>
        /// <param name="result"></param>
        void PurchaseTicketWithRealMoney(string battlePassID, string ticketID, Action<CBSPurchaseTicketWithRMResult> result);
        /// <summary>
        /// Adds the points you specified to the task if task exist for battle pass
        /// </summary>
        /// <param name="battlePassID"></param>
        /// <param name="tasksPoolID"></param>
        /// <param name="taskID"></param>
        /// <param name="points"></param>
        /// <param name="result"></param>
        void AddBattlePassTaskPoints(string battlePassID, string taskID, int points, Action<CBSModifyProfileTaskPointsResult> result);
        /// <summary>
        /// Get tasks available for profile from pool of battle pass
        /// </summary>
        /// <param name="result"></param>
        void GetBattlePassTasksForProfile(string battlePassID, Action<CBSGetTasksForProfileResult> result);
        /// <summary>
        /// Pick up a reward from a completed task if it hasn't been picked up before.
        /// </summary>
        /// <param name="taskID"></param>
        /// <param name="result"></param>
        void PickupBattlePassTaskReward(string battlepassID, string taskID, Action<CBSModifyProfileTaskPointsResult> result);
    }
}
