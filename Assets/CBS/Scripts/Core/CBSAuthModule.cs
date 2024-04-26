using CBS.Core.Auth;
using CBS.Models;
using CBS.Other;
using CBS.Playfab;
using CBS.Scriptable;
using System;
using System.Linq;

namespace CBS
{
    public class CBSAuthModule : CBSModule, IAuth
    {
        /// <summary>
        /// An event that reports a successful user login
        /// </summary>
        public event Action<CBSLoginResult> OnLoginEvent;

        /// <summary>
        /// An event that reports when the user logged out
        /// </summary>
        public event Action<BaseAuthResult> OnLogoutEvent;

        /// <summary>
        /// Check if user is logged in
        /// </summary>
        public bool IsLoggedIn => FabAuth.IsLoggedIn;

        private IFabAuth FabAuth { get; set; }
        private AuthData AuthData { get; set; }

        protected override void Init()
        {
            FabAuth = FabExecuter.Get<FabAuth>();
            AuthData = CBSScriptable.Get<AuthData>();
        }

        /// <summary>
        /// Authorization using login and password. No automatic registration. Before login, you need to register
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        public void LoginWithMailAndPassword(CBSMailLoginRequest request, Action<CBSLoginResult> result)
        {
            FabAuth.LoginWithMailAndPassword(request, (onSuccess, postResult) =>
            {
                var successResult = new CBSLoginResult
                {
                    IsSuccess = true,
                    IsNew = false,
                    ProfileID = onSuccess.PlayFabId,
                    Result = onSuccess
                };

                // generate creds
                var autoLogin = AuthData.AutoLogin;
                if (autoLogin)
                {
                    var credential = new PasswordCredential
                    {
                        Mail = request.Mail,
                        Password = request.Password,
                        Type = CredentialType.PASSWORD
                    };
                    successResult.Credential = credential;
                }

                LoginPostProcess(result, successResult, postResult);
            }, onError =>
            {
                var failedResult = new CBSLoginResult
                {
                    IsSuccess = false,
                    Error = onError
                };
                result?.Invoke(failedResult);
            });
        }
        
        public void LoginWithUsernameAndPassword(CBSUserNameLoginRequest request, Action<CBSLoginResult> result)
        {
            FabAuth.LoginWithUserNameAndPassword(request, (onSuccess, postResult) =>
            {
                var successResult = new CBSLoginResult
                {
                    IsSuccess = true,
                    IsNew = false,
                    ProfileID = onSuccess.PlayFabId,
                    Result = onSuccess
                };

                // generate creds
                var autoLogin = AuthData.AutoLogin;
                if (autoLogin)
                {
                    var credential = new PasswordCredential
                    {
                        Username = request.UserName,
                        Password = request.Password,
                        Type = CredentialType.PASSWORD
                    };
                    successResult.Credential = credential;
                }

                LoginPostProcess(result, successResult, postResult);
            }, onError =>
            {
                var failedResult = new CBSLoginResult
                {
                    IsSuccess = false,
                    Error = onError
                };
                result?.Invoke(failedResult);
            });
        }

        /// <summary>
        /// Authorization using your own custom identifier. Auto-register user if there is no such user in the database.
        /// </summary>
        /// <param name="customID"></param>
        /// <param name="result"></param>
        public void LoginWithCustomID(string customID, Action<CBSLoginResult> result)
        {
            FabAuth.LoginWithCustomID(customID, (onSuccess, postResult) =>
            {
                var successResult = new CBSLoginResult
                {
                    IsSuccess = true,
                    IsNew = onSuccess.NewlyCreated,
                    ProfileID = onSuccess.PlayFabId,
                    Result = onSuccess
                };

                // generate creds
                var autoLogin = AuthData.AutoLogin;
                if (autoLogin)
                {
                    var credential = new CustomIDCredential
                    {
                        CustomID = customID,
                        Type = CredentialType.CUSTOM_ID
                    };
                    successResult.Credential = credential;
                }

                LoginPostProcess(result, successResult, postResult);
            }, onError =>
            {
                var failedResult = new CBSLoginResult
                {
                    IsSuccess = false,
                    Error = onError
                };
                result?.Invoke(failedResult);
            });
        }

        /// <summary>
        /// Authorization that binds the account with the current ID of the device on which the application was launched. Auto-register user if there is no such user in the database.
        /// </summary>
        /// <param name="result"></param>
        public void LoginWithDevice(Action<CBSLoginResult> result)
        {
            string deviceID = Device.DEVICE_ID;

            FabAuth.LoginWithDevice((onSuccess, postResult) =>
            {
                var successResult = new CBSLoginResult
                {
                    IsSuccess = true,
                    IsNew = onSuccess.NewlyCreated,
                    ProfileID = onSuccess.PlayFabId,
                    Result = onSuccess
                };

                // generate creds
                var autoLogin = AuthData.AutoLogin;
                if (autoLogin)
                {
                    var credential = new DeviceIDCredential
                    {
                        DeviceID = deviceID,
                        Type = CredentialType.DEVICE_ID
                    };
                    successResult.Credential = credential;
                }

                LoginPostProcess(result, successResult, postResult);
            }, onError =>
            {
                var failedResult = new CBSLoginResult
                {
                    IsSuccess = false,
                    Error = onError
                };
                result?.Invoke(failedResult);
            });
        }

        /// <summary>
        /// Authorization with google account. Required server auth code.
        /// </summary>
        /// <param name="serverAuthCode"></param>
        /// <param name="result"></param>
        public void LoginWithGoolge(string serverAuthCode, Action<CBSLoginResult> result)
        {
            FabAuth.LoginWithGoogle(serverAuthCode, (onSuccess, postResult) =>
            {
                var successResult = new CBSLoginResult
                {
                    IsSuccess = true,
                    IsNew = onSuccess.NewlyCreated,
                    ProfileID = onSuccess.PlayFabId,
                    Result = onSuccess
                };

                // generate creds
                var autoLogin = AuthData.AutoLogin;
                if (autoLogin)
                {
                    var credential = new GoogleCredential
                    {
                        AuthCode = serverAuthCode,
                        Type = CredentialType.GOOGLE
                    };
                    successResult.Credential = credential;
                }

                LoginPostProcess(result, successResult, postResult);
            }, onError =>
            {
                var failedResult = new CBSLoginResult
                {
                    IsSuccess = false,
                    Error = onError
                };
                result?.Invoke(failedResult);
            });
        }

        /// <summary>
        /// Login with open id connection. Required connection id and id token
        /// </summary>
        /// <param name="connectionID"></param>
        /// <param name="IDToken"></param>
        /// <param name="result"></param>
        public void LoginWithOpenID(string connectionID, string IDToken, Action<CBSLoginResult> result)
        {
            FabAuth.LoginWithOpenID(connectionID, IDToken, (onSuccess, postResult) =>
            {
                var successResult = new CBSLoginResult
                {
                    IsSuccess = true,
                    IsNew = onSuccess.NewlyCreated,
                    ProfileID = onSuccess.PlayFabId,
                    Result = onSuccess
                };

                // generate creds
                var autoLogin = AuthData.AutoLogin;
                if (autoLogin)
                {
                    var credential = new OpenIDCredential
                    {
                        ConnectionID = connectionID,
                        IDToken = IDToken,
                        Type = CredentialType.OPEN_ID
                    };
                    successResult.Credential = credential;
                }

                LoginPostProcess(result, successResult, postResult);
            }, onError =>
            {
                var failedResult = new CBSLoginResult
                {
                    IsSuccess = false,
                    Error = onError
                };
                result?.Invoke(failedResult);
            });
        }

        /// <summary>
        /// Authorization with Steam account. Required steam ticket.
        /// </summary>
        /// <param name="steamTicket"></param>
        /// <param name="result"></param>
        public void LoginWithSteam(string steamTicket, Action<CBSLoginResult> result)
        {
            FabAuth.LoginWithSteam(steamTicket, (onSuccess, postResult) =>
            {
                var successResult = new CBSLoginResult
                {
                    IsSuccess = true,
                    IsNew = onSuccess.NewlyCreated,
                    ProfileID = onSuccess.PlayFabId,
                    Result = onSuccess
                };

                // generate creds
                var autoLogin = AuthData.AutoLogin;
                if (autoLogin)
                {
                    var credential = new SteamCredential
                    {
                        SteamTicket = steamTicket,
                        Type = CredentialType.STEAM
                    };
                    successResult.Credential = credential;
                }

                LoginPostProcess(result, successResult, postResult);
            }, onError =>
            {
                var failedResult = new CBSLoginResult
                {
                    IsSuccess = false,
                    Error = onError
                };
                result?.Invoke(failedResult);
            });
        }

        /// <summary>
        /// Authorization with Facebook account. Required access token.
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="result"></param>
        public void LoginWithFacebook(string accessToken, Action<CBSLoginResult> result)
        {
            FabAuth.LoginWithFacebook(accessToken, (onSuccess, postResult) =>
            {
                var successResult = new CBSLoginResult
                {
                    IsSuccess = true,
                    IsNew = onSuccess.NewlyCreated,
                    ProfileID = onSuccess.PlayFabId,
                    Result = onSuccess
                };

                // generate creds
                var autoLogin = AuthData.AutoLogin;
                if (autoLogin)
                {
                    var credential = new FacebookCredential
                    {
                        AccessToken = accessToken,
                        Type = CredentialType.FACEBOOK
                    };
                    successResult.Credential = credential;
                }

                LoginPostProcess(result, successResult, postResult);
            }, onError =>
            {
                var failedResult = new CBSLoginResult
                {
                    IsSuccess = false,
                    Error = onError
                };
                result?.Invoke(failedResult);
            });
        }

        /// <summary>
        /// Authorization with Apple account. Required apple identity Token.
        /// </summary>
        /// <param name="identityToken"></param>
        /// <param name="result"></param>
        public void LoginWithApple(string identityToken, Action<CBSLoginResult> result)
        {
            FabAuth.LoginWithApple(identityToken, (onSuccess, postResult) =>
            {
                var successResult = new CBSLoginResult
                {
                    IsSuccess = true,
                    IsNew = onSuccess.NewlyCreated,
                    ProfileID = onSuccess.PlayFabId,
                    Result = onSuccess
                };

                // generate creds
                var autoLogin = AuthData.AutoLogin;
                if (autoLogin)
                {
                    var credential = new AppleCredential
                    {
                        IdentityToken = identityToken,
                        Type = CredentialType.APPLE
                    };
                    successResult.Credential = credential;
                }

                LoginPostProcess(result, successResult, postResult);
            }, onError =>
            {
                var failedResult = new CBSLoginResult
                {
                    IsSuccess = false,
                    Error = onError
                };
                result?.Invoke(failedResult);
            });
        }
        
        /// <summary>
        /// Login with playstation account. Required auth code
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        public void LoginWithPlaystation(CBSLoginWithPlaystationRequest request, Action<CBSLoginResult> result)
        {
            var authCode = request.AuthCode;
            var issuerId = request.IssuerId;
            var redirectUrl = request.RedirectUri;
            FabAuth.LoginWithPlaystation(authCode, issuerId, redirectUrl, (onSuccess, postResult) =>
            {
                var successResult = new CBSLoginResult
                {
                    IsSuccess = true,
                    IsNew = onSuccess.NewlyCreated,
                    ProfileID = onSuccess.PlayFabId,
                    Result = onSuccess
                };

                // generate creds
                var autoLogin = AuthData.AutoLogin;
                if (autoLogin)
                {
                    var credential = new PlaystationCredential()
                    {
                        AuthCode = authCode,
                        IssuerId = issuerId,
                        RedirectUri = redirectUrl,
                        Type = CredentialType.PLAYSTATION
                    };
                    successResult.Credential = credential;
                }

                LoginPostProcess(result, successResult, postResult);
            }, onError =>
            {
                var failedResult = new CBSLoginResult
                {
                    IsSuccess = false,
                    Error = onError
                };
                result?.Invoke(failedResult);
            });
        }

        /// <summary>
        /// Authorization based on the last successful login. Enable this setting in CBS Configurator-> Configurator-> Auth-> Enable AutoLogin
        /// </summary>
        /// <param name="result"></param>
        public void AutoLogin(Action<CBSLoginResult> result)
        {
            if (Credential.Exist())
            {
                var baseCredential = Credential.Get<BaseCredential>();
                var type = baseCredential.Type;
                if (type == CredentialType.CUSTOM_ID)
                {
                    var credential = Credential.Get<CustomIDCredential>();
                    var customID = credential.CustomID;
                    LoginWithCustomID(customID, result);
                }
                else if (type == CredentialType.DEVICE_ID)
                {
                    LoginWithDevice(result);
                }
                else if (type == CredentialType.FACEBOOK)
                {
                    var credential = Credential.Get<FacebookCredential>();
                    var accessToken = credential.AccessToken;
                    LoginWithFacebook(accessToken, result);
                }
                else if (type == CredentialType.GOOGLE)
                {
                    var credential = Credential.Get<GoogleCredential>();
                    var authCode = credential.AuthCode;
                    LoginWithGoolge(authCode, result);
                }
                else if (type == CredentialType.STEAM)
                {
                    var credential = Credential.Get<SteamCredential>();
                    var steamTicket = credential.SteamTicket;
                    LoginWithSteam(steamTicket, result);
                }
                else if (type == CredentialType.PASSWORD)
                {
                    var credential = Credential.Get<PasswordCredential>();
                    var mail = credential.Mail;
                    var userName = credential.Username;
                    var password = credential.Password;
                    if (!string.IsNullOrEmpty(userName))
                    {
                        LoginWithUsernameAndPassword(new CBSUserNameLoginRequest
                        {
                            UserName = userName,
                            Password = password
                        }, result);
                    }
                    else
                    {
                        LoginWithMailAndPassword(new CBSMailLoginRequest
                        {
                            Mail = mail,
                            Password = password
                        }, result);
                    }
                }
                else if (type == CredentialType.APPLE)
                {
                    var credential = Credential.Get<AppleCredential>();
                    var identityToken = credential.IdentityToken;
                    LoginWithApple(identityToken, result);
                }
                else if (type == CredentialType.OPEN_ID)
                {
                    var credential = Credential.Get<OpenIDCredential>();
                    var connectionID = credential.ConnectionID;
                    var idToken = credential.IDToken;
                    LoginWithOpenID(connectionID, idToken, result);
                }
                else if (type == CredentialType.PLAYSTATION)
                {
                    var credential = Credential.Get<PlaystationCredential>();
                    var authCode = credential.AuthCode;
                    var issuerId = credential.IssuerId;
                    var redirectUri = credential.RedirectUri;
                    var loginRequest = new CBSLoginWithPlaystationRequest
                    {
                        AuthCode = authCode,
                        IssuerId = issuerId,
                        RedirectUri = redirectUri
                    };
                    LoginWithPlaystation(loginRequest, result);
                }
                else
                {
                    result?.Invoke(new CBSLoginResult
                    {
                        IsSuccess = false,
                        Error = CBSError.CredentialNotFound()
                    });
                }
            }
            else
            {
                result?.Invoke(new CBSLoginResult
                {
                    IsSuccess = false,
                    Error = CBSError.CredentialNotFound()
                });
            }
        }

        /// <summary>
        /// User registration using mail and password. Auto generation of the name is not applied. The name must be specified in the request.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="result"></param>
        public void RegisterWithMailAndPassword(CBSMailRegistrationRequest request, Action<BaseAuthResult> result)
        {
            FabAuth.RegisterWithMailAndPassword(request, onSuccess =>
            {
                var successResult = new BaseAuthResult
                {
                    IsSuccess = true,
                    ProfileID = onSuccess.PlayFabId
                };
                result?.Invoke(successResult);
            }, onError =>
            {
                var failedResult = new BaseAuthResult
                {
                    IsSuccess = false,
                    Error = onError
                };
                result?.Invoke(failedResult);
            });
        }

        /// <summary>
        /// Recovering a player's password using mail. Works only for users who have registered using mail and password.
        /// </summary>
        /// <param name="password"></param>
        /// <param name="result"></param>
        public void SendPasswordRecovery(string mail, Action<CBSBaseResult> result)
        {
            FabAuth.SendPasswordRecovery(mail, onSuccess =>
            {
                var successResult = new CBSBaseResult
                {
                    IsSuccess = true,
                };
                result?.Invoke(successResult);
            }, onError =>
            {
                var failedResult = new CBSBaseResult
                {
                    IsSuccess = false,
                    Error = CBSError.FromTemplate(onError)
                };
                result?.Invoke(failedResult);
            });
        }

        /// <summary>
        /// Link existing account with Facebook
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="result"></param>
        public void LinkFacebookAccount(string accessToken, Action<CBSBaseResult> result)
        {
            FabAuth.LinkFacebookAccount(accessToken,
                onLink =>
                {
                    result?.Invoke(new CBSBaseResult { IsSuccess = true });
                    Get<CBSProfileModule>().NotifyAboutChangingLinkedAccount(CredentialType.FACEBOOK, true);
                },
                onFailed => result.Invoke(new CBSBaseResult { Error = CBSError.FromTemplate(onFailed) }));
        }

        /// <summary>
        /// Unlink existing account from Facebook
        /// </summary>
        /// <param name="result"></param>
        public void UnlinkFacebookAccount(Action<CBSBaseResult> result)
        {
            FabAuth.UnlinkFacebookAccount(onUnlink =>
            {
                result?.Invoke(new CBSBaseResult { IsSuccess = true });
                Get<CBSProfileModule>().NotifyAboutChangingLinkedAccount(CredentialType.FACEBOOK, false);
            },
            onFailed => result.Invoke(new CBSBaseResult { Error = CBSError.FromTemplate(onFailed) }));
        }

        /// <summary>
        /// Link existing account with Google
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="result"></param>
        public void LinkGoogleAccount(string serverAuthCode, Action<CBSBaseResult> result)
        {
            FabAuth.LinkGoogleAccount(serverAuthCode,
            onLink =>
            {
                result?.Invoke(new CBSBaseResult { IsSuccess = true });
                Get<CBSProfileModule>().NotifyAboutChangingLinkedAccount(CredentialType.GOOGLE, true);
            },
            onFailed => result.Invoke(new CBSBaseResult { Error = CBSError.FromTemplate(onFailed) }));
        }

        /// <summary>
        /// Unlink existing account from Google
        /// </summary>
        /// <param name="result"></param>
        public void UnlinkGoogleAccount(Action<CBSBaseResult> result)
        {
            FabAuth.UnlinkGoogleAccount(onUnlink =>
            {
                result?.Invoke(new CBSBaseResult { IsSuccess = true });
                Get<CBSProfileModule>().NotifyAboutChangingLinkedAccount(CredentialType.GOOGLE, false);
            },
            onFailed => result.Invoke(new CBSBaseResult { Error = CBSError.FromTemplate(onFailed) }));
        }

        /// <summary>
        /// Link existing account with Apple
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="result"></param>
        public void LinkAppleAccount(string identityToken, Action<CBSBaseResult> result)
        {
            FabAuth.LinkAppleAccount(identityToken,
            onLink =>
            {
                result?.Invoke(new CBSBaseResult { IsSuccess = true });
                Get<CBSProfileModule>().NotifyAboutChangingLinkedAccount(CredentialType.APPLE, true);
            },
            onFailed => result.Invoke(new CBSBaseResult { Error = CBSError.FromTemplate(onFailed) }));
        }

        /// <summary>
        /// Unlink existing account from Apple
        /// </summary>
        /// <param name="result"></param>
        public void UnlinkAppleAccount(Action<CBSBaseResult> result)
        {
            FabAuth.UnlinkAppleAccount(onUnlink =>
            {
                result?.Invoke(new CBSBaseResult { IsSuccess = true });
                Get<CBSProfileModule>().NotifyAboutChangingLinkedAccount(CredentialType.APPLE, false);
            },
            onFailed => result.Invoke(new CBSBaseResult { Error = CBSError.FromTemplate(onFailed) }));
        }

        /// <summary>
        /// Link existing account with Steam
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="result"></param>
        public void LinkSteamAccount(string steamTicket, Action<CBSBaseResult> result)
        {
            FabAuth.LinkSteamAccount(steamTicket,
            onLink =>
            {
                result?.Invoke(new CBSBaseResult { IsSuccess = true });
                Get<CBSProfileModule>().NotifyAboutChangingLinkedAccount(CredentialType.STEAM, true);
            },
            onFailed => result.Invoke(new CBSBaseResult { Error = CBSError.FromTemplate(onFailed) }));
        }

        /// <summary>
        /// Unlink existing account from Steam
        /// </summary>
        /// <param name="result"></param>
        public void UnlinkSteamAccount(Action<CBSBaseResult> result)
        {
            FabAuth.UnlinkSteamAccount(onUnlink =>
            {
                result?.Invoke(new CBSBaseResult { IsSuccess = true });
                Get<CBSProfileModule>().NotifyAboutChangingLinkedAccount(CredentialType.STEAM, false);
            },
            onFailed => result.Invoke(new CBSBaseResult { Error = CBSError.FromTemplate(onFailed) }));
        }

        /// <summary>
        /// Sign Out. Stops running and executing CBS scripts. Clears all cached information.
        /// </summary>
        /// <param name="result"></param>
        public void Logout(Action<BaseAuthResult> result = null)
        {
            FabAuth.Logout();
            LogoutProcces();
            var logoutResult = new BaseAuthResult
            {
                IsSuccess = true
            };
            Credential.Clear();
            OnLogoutEvent?.Invoke(logoutResult);
            result?.Invoke(logoutResult);
        }

        // other tools
        private void LoginPostProcess(Action<CBSLoginResult> successAction, CBSLoginResult loginResult, FunctionPostLoginResult postLoginResult)
        {
            // bind modules
            Get<CBSProfileModule>().Bind();
            Get<CBSCurrencyModule>().Bind();
            Get<CBSItemsModule>().Bind();
            Get<CBSInventoryModule>().Bind();
            Get<CBSTitleDataModule>().Bind();

            loginResult.ItemsResult = postLoginResult.ItemsResult;
            loginResult.ItemsCategoryData = postLoginResult.CategoriesResult;
            Get<CBSItemsModule>().ParseMetaData(postLoginResult.Recipes, postLoginResult.Upgrades, postLoginResult.LootboxTable);

            // init iap module
            var fabItems = postLoginResult.ItemsResult.Catalog;
            var curenciesPacksIDS = postLoginResult.CurrencyPacksIDs ?? new System.Collections.Generic.List<string>();
            var calendarIDs = postLoginResult.CalendarCatalogIDs ?? new System.Collections.Generic.List<string>();
            var tickectsIDs = postLoginResult.TicketsCatalogIDs ?? new System.Collections.Generic.List<string>();
            var itemsIDs = fabItems.Select(x => x.ItemId).ToList();
            Get<CBSInAppPurchaseModule>().SetupStore(itemsIDs, curenciesPacksIDS, calendarIDs, tickectsIDs);

            bool preloadLevelData = AuthData.PreloadLevelData;
            if (preloadLevelData)
            {
                var levelData = postLoginResult.PlayerLevelInfo;
                Get<CBSProfileModule>().ParseLevelData(levelData);
            }
            var preloadAccountInfo = AuthData.PreloadAccountInfo;
            if (preloadAccountInfo)
            {
                var avatarID = postLoginResult.AvatarID;
                Get<CBSProfileModule>().ParseAvatarID(avatarID);
            }
            var preloadTitleData = AuthData.PreloadTitleData;
            if (preloadTitleData)
            {
                var loginDetail = loginResult.Result;
                var titleData = loginDetail.InfoResultPayload.TitleData;
                Get<CBSTitleDataModule>().ParseData(titleData);
            }

            var clanID = postLoginResult.ClanID;
            var clanRoleID = postLoginResult.ClanRoleID;
            var clanEntity = postLoginResult.ClanEntity;
            Get<CBSProfileModule>().ParseClanInfo(clanID, clanEntity);
            Get<CBSClanModule>().ParseClanInfo(clanID, clanRoleID, clanEntity);

            var profileChatData = postLoginResult.ProfileChatData;
            Get<CBSChatModule>().ParseChatData(profileChatData);

            // save credential
            var autoLogin = AuthData.AutoLogin;
            if (autoLogin)
            {
                var credential = loginResult.Credential;
                Credential.Save(credential);
            }

            OnLoginEvent?.Invoke(loginResult);
            successAction?.Invoke(loginResult);
        }
    }
}
