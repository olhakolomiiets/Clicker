using CBS.Models;
using PlayFab;
using PlayFab.ClientModels;
using System;

namespace CBS.Playfab
{
    public interface IFabAuth
    {
        bool IsLoggedIn { get; }

        void RegisterWithMailAndPassword(CBSMailRegistrationRequest request, Action<RegisterPlayFabUserResult> onSuccess, Action<CBSError> onFailed);

        void LoginWithMailAndPassword(CBSMailLoginRequest request, Action<LoginResult, FunctionPostLoginResult> onSuccess, Action<CBSError> onFailed);

        void LoginWithUserNameAndPassword(CBSUserNameLoginRequest request, Action<LoginResult, FunctionPostLoginResult> onSuccess, Action<CBSError> onFailed);

        void LoginWithCustomID(string id, Action<LoginResult, FunctionPostLoginResult> onSuccess, Action<CBSError> onFailed);

        void LoginWithDevice(Action<LoginResult, FunctionPostLoginResult> onSuccess, Action<CBSError> onFailed);

        void LoginWithGoogle(string serverAuthCode, Action<LoginResult, FunctionPostLoginResult> onSuccess, Action<CBSError> onFailed);

        void LoginWithSteam(string steamTicket, Action<LoginResult, FunctionPostLoginResult> onSuccess, Action<CBSError> onFailed);

        void LoginWithOpenID(string connectionID, string IDToken, Action<LoginResult, FunctionPostLoginResult> onSuccess, Action<CBSError> onFailed);

        void LoginWithFacebook(string accessToken, Action<LoginResult, FunctionPostLoginResult> onSuccess, Action<CBSError> onFailed);

        void LoginWithApple(string identityToken, Action<LoginResult, FunctionPostLoginResult> onSuccess, Action<CBSError> onFailed);

        void LoginWithPlaystation(string authCode, int? issuerId, string redirectUri, Action<LoginResult, FunctionPostLoginResult> onSuccess, Action<CBSError> onFailed);
        
        void SendPasswordRecovery(string mail, Action<SendAccountRecoveryEmailResult> onSuccess, Action<PlayFabError> onFailed);

        void LinkFacebookAccount(string accessToken, Action<LinkFacebookAccountResult> onSuccess, Action<PlayFabError> onFailed);

        void LinkAppleAccount(string identityToken, Action<EmptyResult> onSuccess, Action<PlayFabError> onFailed);

        void LinkGoogleAccount(string serverAuthCode, Action<LinkGoogleAccountResult> onSuccess, Action<PlayFabError> onFailed);

        void LinkSteamAccount(string steamTicket, Action<LinkSteamAccountResult> onSuccess, Action<PlayFabError> onFailed);

        void UnlinkFacebookAccount(Action<UnlinkFacebookAccountResult> onSuccess, Action<PlayFabError> onFailed);

        void UnlinkAppleAccount(Action<EmptyResponse> onSuccess, Action<PlayFabError> onFailed);

        void UnlinkGoogleAccount(Action<UnlinkGoogleAccountResult> onSuccess, Action<PlayFabError> onFailed);

        void UnlinkSteamAccount(Action<UnlinkSteamAccountResult> onSuccess, Action<PlayFabError> onFailed);

        void Logout();
    }
}
