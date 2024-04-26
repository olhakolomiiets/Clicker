using CBS.Models;
using System;

namespace CBS
{
    public interface IFriends
    {
        /// <summary>
        /// Notifies when a friend request has been approved.
        /// </summary>
        event Action<string> OnFriendAccepted;
        /// <summary>
        /// Notifies when a friend request has been rejected.
        /// </summary>
        event Action<string> OnFriendDeclined;
        /// <summary>
        /// Notifies when a new user has been added to your friends list.
        /// </summary>
        event Action<string> OnFriendAdded;

        /// <summary>
        /// Get friends list of current profile
        /// </summary>
        /// <param name="result"></param>
        void GetFriends(CBSProfileConstraints profileConstraints, Action<CBSGetFriendsResult> result);

        /// <summary>
        /// Get friends list of profile
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="profileConstraints"></param>
        /// <param name="result"></param>
        void GetProfileFriends(string profileID, CBSProfileConstraints profileConstraints, Action<CBSGetFriendsResult> result);

        /// <summary>
        /// Get a list of users who want to be friends with you.
        /// </summary>
        /// <param name="result"></param>
        void GetRequestedFriends(CBSProfileConstraints profileConstraints, Action<CBSGetFriendsResult> result);

        /// <summary>
        /// Checks if a user is on your friends list.
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="result"></param>
        void CheckFriendship(string profileID, Action<CBSCheckFriendshipResult> result);

        /// <summary>
        /// Send user a friend request.
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="result"></param>
        void SendFriendsRequest(string profileID, Action<CBSSendFriendsRequestResult> result);

        /// <summary>
        /// Remove user from friends.
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="result"></param>
        void RemoveFriend(string profileID, Action<CBSActionWithFriendResult> result);

        /// <summary>
        /// Reject a user's friend request.
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="result"></param>
        void DeclineFriendRequest(string profileID, Action<CBSActionWithFriendResult> result);

        /// <summary>
        /// Approve the user's friend request.
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="result"></param>
        void AcceptFriend(string profileID, Action<CBSActionWithFriendResult> result);

        /// <summary>
        /// Add user to friends without confirmation.
        /// </summary>
        /// <param name="profileID"></param>
        /// <param name="result"></param>
        void ForceAddFriend(string profileID, Action<CBSActionWithFriendResult> result);

        /// <summary>
        /// Get friends badge information. Return requested friends count.
        /// </summary>
        /// <param name="result"></param>
        void GetFriendsBadge(Action<CBSBadgeResult> result);

        /// <summary>
        /// Get shared friends list with profile
        /// </summary>
        /// <param name="withProfileID"></param>
        /// <param name="profileConstraints"></param>
        /// <param name="result"></param>
        void GetSharedFriendsWithProfile(string withProfileID, CBSProfileConstraints profileConstraints, Action<CBSGetFriendsResult> result);
    }
}
