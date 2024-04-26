using CBS.Models;
using System;

namespace CBS
{
    public interface IAuth
    {
        /// <summary>
        /// An event that reports a successful user login
        /// </summary>
        event Action<CBSLoginResult> OnLoginEvent;

        /// <summary>
        /// An event that reports when the user logged out
        /// </summary>
        event Action<BaseAuthResult> OnLogoutEvent;

        /// <summary>
        /// Authorization using login and password. No automatic registration. Before login, you need to register
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        void LoginWithMailAndPassword(CBSMailLoginRequest request, Action<CBSLoginResult> result);

        void LoginWithUsernameAndPassword(CBSUserNameLoginRequest request, Action<CBSLoginResult> result);

        /// <summary>
        /// Authorization that binds the account with the current ID of the device on which the application was launched. Auto-register user if there is no such user in the database.
        /// </summary>
        /// <param name="result"></param>
        void LoginWithDevice(Action<CBSLoginResult> result);

        /// <summary>
        /// Authorization using your own custom identifier. Auto-register user if there is no such user in the database.
        /// </summary>
        /// <param name="customID"></param>
        /// <param name="result"></param>
        void LoginWithCustomID(string customID, Action<CBSLoginResult> result);

        /// <summary>
        /// Authorization with google account. Required server auth code.
        /// </summary>
        /// <param name="serverAuthCode"></param>
        /// <param name="result"></param>
        void LoginWithGoolge(string serverAuthCode, Action<CBSLoginResult> result);

        /// <summary>
        /// Authorization with Steam account. Required steam ticket.
        /// </summary>
        /// <param name="steamTicket"></param>
        /// <param name="result"></param>
        void LoginWithSteam(string steamTicket, Action<CBSLoginResult> result);

        /// <summary>
        /// Authorization with Facebook account. Required access token.
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="result"></param>
        void LoginWithFacebook(string accessToken, Action<CBSLoginResult> result);

        /// <summary>
        /// Login with open id connection. Required connection id and id token
        /// </summary>
        /// <param name="connectionID"></param>
        /// <param name="IDToken"></param>
        /// <param name="result"></param>
        void LoginWithOpenID(string connectionID, string IDToken, Action<CBSLoginResult> result);
        
        /// <summary>
        /// Login with playstation account. Required auth code
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        void LoginWithPlaystation(CBSLoginWithPlaystationRequest request, Action<CBSLoginResult> result);

        /// <summary>
        /// Authorization based on the last successful login. Enable this setting in CBS Configurator-> Configurator-> Auth-> Enable AutoLogin
        /// </summary>
        /// <param name="result"></param>
        void AutoLogin(Action<CBSLoginResult> result);

        /// <summary>
        /// User registration using mail and password. Auto generation of the name is not applied. The name must be specified in the request.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        void RegisterWithMailAndPassword(CBSMailRegistrationRequest request, Action<BaseAuthResult> result);

        /// <summary>
        /// Recovering a player's password using mail. Works only for users who have registered using mail and password.
        /// </summary>
        /// <param name="mail"></param>
        /// <param name="result"></param>
        void SendPasswordRecovery(string mail, Action<CBSBaseResult> result);

        /// <summary>
        /// Authorization with Apple account. Required apple identity Token.
        /// </summary>
        /// <param name="identityToken"></param>
        /// <param name="result"></param>
        void LoginWithApple(string identityToken, Action<CBSLoginResult> result);

        /// <summary>
        /// Link existing account with Facebook
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="result"></param>
        void LinkFacebookAccount(string accessToken, Action<CBSBaseResult> result);

        /// <summary>
        /// Unlink existing account from Facebook
        /// </summary>
        /// <param name="result"></param>
        void UnlinkFacebookAccount(Action<CBSBaseResult> result);

        /// <summary>
        /// Link existing account with Google
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="result"></param>
        void LinkGoogleAccount(string serverAuthCode, Action<CBSBaseResult> result);

        /// <summary>
        /// Unlink existing account from Google
        /// </summary>
        /// <param name="result"></param>
        void UnlinkGoogleAccount(Action<CBSBaseResult> result);

        /// <summary>
        /// Link existing account with Apple
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="result"></param>
        void LinkAppleAccount(string identityToken, Action<CBSBaseResult> result);

        /// <summary>
        /// Unlink existing account from Apple
        /// </summary>
        /// <param name="result"></param>
        void UnlinkAppleAccount(Action<CBSBaseResult> result);

        /// <summary>
        /// Link existing account with Steam
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="result"></param>
        void LinkSteamAccount(string steamTicket, Action<CBSBaseResult> result);

        /// <summary>
        /// Unlink existing account from Steam
        /// </summary>
        /// <param name="result"></param>
        void UnlinkSteamAccount(Action<CBSBaseResult> result);

        /// <summary>
        /// Sign Out. Stops running and executing CBS scripts. Clears all cached information.
        /// </summary>
        /// <param name="result"></param>
        void Logout(Action<BaseAuthResult> result = null);

        /// <summary>
        /// Check if user is logged in
        /// </summary>
        bool IsLoggedIn { get; }
    }
}
