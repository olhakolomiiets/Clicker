using CBS.Models;
using CBS.Other;
using CBS.Scriptable;
using CBS.Utils;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public class FabAuth : FabExecuter, IFabAuth
    {
        private AuthData AuthData { get; set; }

        public bool IsLoggedIn => PlayFabClientAPI.IsClientLoggedIn();

        protected override void Init()
        {
            AuthData = CBSScriptable.Get<AuthData>();
        }

        public void RegisterWithMailAndPassword(CBSMailRegistrationRequest request, Action<RegisterPlayFabUserResult> onSuccess, Action<CBSError> onFailed)
        {
            var regRequest = new RegisterPlayFabUserRequest
            {
                Email = request.Mail,
                Password = request.Password,
                DisplayName = request.DisplayName,
                Username = request.UserName,
                RequireBothUsernameAndEmail = false
            };

            PlayFabClientAPI.RegisterPlayFabUser(regRequest, success =>
            {
                PostMailRegistrationProccess(success, onSuccess, onFailed);
            }, onFail =>
            {
                onFailed?.Invoke(CBSError.FromTemplate(onFail));
            });
        }

        public void LoginWithMailAndPassword(CBSMailLoginRequest request, Action<LoginResult, FunctionPostLoginResult> onSuccess, Action<CBSError> onFailed)
        {
            var loginRequest = new LoginWithEmailAddressRequest
            {
                Email = request.Mail,
                Password = request.Password,
                InfoRequestParameters = GetLoginRequestParams()
            };

            PlayFabClientAPI.LoginWithEmailAddress(loginRequest, result =>
            {
                PostAuthProccess(result, onSuccess, onFailed);
            }, onFail =>
            {
                onFailed?.Invoke(CBSError.FromTemplate(onFail));
            });
        }
        
        public void LoginWithUserNameAndPassword(CBSUserNameLoginRequest request, Action<LoginResult, FunctionPostLoginResult> onSuccess, Action<CBSError> onFailed)
        {
            var loginRequest = new LoginWithPlayFabRequest
            {
                Username = request.UserName,
                Password = request.Password,
                InfoRequestParameters = GetLoginRequestParams()
            };

            PlayFabClientAPI.LoginWithPlayFab(loginRequest, result =>
            {
                PostAuthProccess(result, onSuccess, onFailed);
            }, onFail =>
            {
                onFailed?.Invoke(CBSError.FromTemplate(onFail));
            });
        }

        public void LoginWithDevice(Action<LoginResult, FunctionPostLoginResult> onSuccess, Action<CBSError> onFailed)
        {
            string deviceID = Device.DEVICE_ID;

#if UNITY_ANDROID && !UNITY_EDITOR
            var androidRequest = new LoginWithAndroidDeviceIDRequest
            {
                AndroidDeviceId = deviceID,
                CreateAccount = true,
                InfoRequestParameters = GetLoginRequestParams()
            };
            PlayFabClientAPI.LoginWithAndroidDeviceID(androidRequest, result =>
            {
                PostAuthProccess(result, onSuccess, onFailed);
            }, onFail =>
            {
                onFailed?.Invoke(CBSError.FromTemplate(onFail));
            });
#elif UNITY_IOS && !UNITY_EDITOR
            var iosRequest = new LoginWithIOSDeviceIDRequest
            {
                DeviceId = deviceID,
                CreateAccount = true,
                InfoRequestParameters = GetLoginRequestParams()
            };
            PlayFabClientAPI.LoginWithIOSDeviceID(iosRequest, result =>
            {
                PostAuthProccess(result, onSuccess, onFailed);
            }, onFail =>
            {
                onFailed?.Invoke(CBSError.FromTemplate(onFail));
            });
#else
            LoginWithCustomID(deviceID, onSuccess, onFailed);
#endif
        }

        public void LoginWithCustomID(string id, Action<LoginResult, FunctionPostLoginResult> onSuccess, Action<CBSError> onFailed)
        {
            var request = new LoginWithCustomIDRequest
            {
                CustomId = id,
                CreateAccount = true,
                InfoRequestParameters = GetLoginRequestParams()
            };

            PlayFabClientAPI.LoginWithCustomID(request, result =>
            {
                PostAuthProccess(result, onSuccess, onFailed);
            }, onFail =>
            {
                onFailed?.Invoke(CBSError.FromTemplate(onFail));
            });
        }

        public void LoginWithGoogle(string serverAuthCode, Action<LoginResult, FunctionPostLoginResult> onSuccess, Action<CBSError> onFailed)
        {
            var request = new LoginWithGoogleAccountRequest
            {
                CreateAccount = true,
                InfoRequestParameters = GetLoginRequestParams(),
                ServerAuthCode = serverAuthCode
            };
            PlayFabClientAPI.LoginWithGoogleAccount(request, result =>
            {
                PostAuthProccess(result, onSuccess, onFailed);
            }, onFail =>
            {
                onFailed?.Invoke(CBSError.FromTemplate(onFail));
            });
        }

        public void LoginWithOpenID(string connectionID, string IDToken, Action<LoginResult, FunctionPostLoginResult> onSuccess, Action<CBSError> onFailed)
        {
            var request = new LoginWithOpenIdConnectRequest
            {
                CreateAccount = true,
                InfoRequestParameters = GetLoginRequestParams(),
                ConnectionId = connectionID,
                IdToken = IDToken
            };
            PlayFabClientAPI.LoginWithOpenIdConnect(request, result =>
            {
                PostAuthProccess(result, onSuccess, onFailed);
            }, onFail =>
            {
                onFailed?.Invoke(CBSError.FromTemplate(onFail));
            });
        }

        public void LoginWithSteam(string steamTicket, Action<LoginResult, FunctionPostLoginResult> onSuccess, Action<CBSError> onFailed)
        {
            var request = new LoginWithSteamRequest
            {
                CreateAccount = true,
                InfoRequestParameters = GetLoginRequestParams(),
                SteamTicket = steamTicket
            };
            PlayFabClientAPI.LoginWithSteam(request, result =>
            {
                PostAuthProccess(result, onSuccess, onFailed);
            }, onFail =>
            {
                onFailed?.Invoke(CBSError.FromTemplate(onFail));
            });
        }

        public void LoginWithFacebook(string accessToken, Action<LoginResult, FunctionPostLoginResult> onSuccess, Action<CBSError> onFailed)
        {
            var request = new LoginWithFacebookRequest
            {
                CreateAccount = true,
                InfoRequestParameters = GetLoginRequestParams(),
                AccessToken = accessToken
            };
            PlayFabClientAPI.LoginWithFacebook(request, result =>
            {
                PostAuthProccess(result, onSuccess, onFailed);
            }, onFail =>
            {
                onFailed?.Invoke(CBSError.FromTemplate(onFail));
            });
        }

        public void LoginWithApple(string identityToken, Action<LoginResult, FunctionPostLoginResult> onSuccess, Action<CBSError> onFailed)
        {
            var request = new LoginWithAppleRequest
            {
                CreateAccount = true,
                InfoRequestParameters = GetLoginRequestParams(),
                IdentityToken = identityToken
            };
            PlayFabClientAPI.LoginWithApple(request, result =>
            {
                PostAuthProccess(result, onSuccess, onFailed);
            }, onFail =>
            {
                onFailed?.Invoke(CBSError.FromTemplate(onFail));
            });
        }
        
        public void LoginWithPlaystation(string authCode, int? issuerId, string redirectUri, Action<LoginResult, FunctionPostLoginResult> onSuccess, Action<CBSError> onFailed)
        {
            var request = new LoginWithPSNRequest()
            {
                CreateAccount = true,
                InfoRequestParameters = GetLoginRequestParams(),
                AuthCode = authCode,
                IssuerId = issuerId,
                RedirectUri = redirectUri
            };
            PlayFabClientAPI.LoginWithPSN(request, result =>
            {
                PostAuthProccess(result, onSuccess, onFailed);
            }, onFail =>
            {
                onFailed?.Invoke(CBSError.FromTemplate(onFail));
            });
        }

        public void SendPasswordRecovery(string mail, Action<SendAccountRecoveryEmailResult> onSuccess, Action<PlayFabError> onFailed)
        {
            var request = new SendAccountRecoveryEmailRequest
            {
                Email = mail,
                TitleId = PlayFabSettings.TitleId
            };
            PlayFabClientAPI.SendAccountRecoveryEmail(request, onSuccess, onFailed);
        }

        public void LinkFacebookAccount(string accessToken, Action<LinkFacebookAccountResult> onSuccess, Action<PlayFabError> onFailed)
        {
            var request = new LinkFacebookAccountRequest
            {
                AccessToken = accessToken
            };
            PlayFabClientAPI.LinkFacebookAccount(request, onSuccess, onFailed);
        }

        public void LinkAppleAccount(string identityToken, Action<PlayFab.ClientModels.EmptyResult> onSuccess, Action<PlayFabError> onFailed)
        {
            var request = new LinkAppleRequest
            {
                IdentityToken = identityToken
            };
            PlayFabClientAPI.LinkApple(request, onSuccess, onFailed);
        }

        public void LinkGoogleAccount(string serverAuthCode, Action<LinkGoogleAccountResult> onSuccess, Action<PlayFabError> onFailed)
        {
            var request = new LinkGoogleAccountRequest
            {
                ServerAuthCode = serverAuthCode
            };
            PlayFabClientAPI.LinkGoogleAccount(request, onSuccess, onFailed);
        }

        public void LinkSteamAccount(string steamTicket, Action<LinkSteamAccountResult> onSuccess, Action<PlayFabError> onFailed)
        {
            var request = new LinkSteamAccountRequest
            {
                SteamTicket = steamTicket
            };
            PlayFabClientAPI.LinkSteamAccount(request, onSuccess, onFailed);
        }

        public void UnlinkFacebookAccount(Action<UnlinkFacebookAccountResult> onSuccess, Action<PlayFabError> onFailed)
        {
            var request = new UnlinkFacebookAccountRequest();
            PlayFabClientAPI.UnlinkFacebookAccount(request, onSuccess, onFailed);
        }

        public void UnlinkAppleAccount(Action<EmptyResponse> onSuccess, Action<PlayFabError> onFailed)
        {
            var request = new UnlinkAppleRequest();
            PlayFabClientAPI.UnlinkApple(request, onSuccess, onFailed);
        }

        public void UnlinkGoogleAccount(Action<UnlinkGoogleAccountResult> onSuccess, Action<PlayFabError> onFailed)
        {
            var request = new UnlinkGoogleAccountRequest();
            PlayFabClientAPI.UnlinkGoogleAccount(request, onSuccess, onFailed);
        }

        public void UnlinkSteamAccount(Action<UnlinkSteamAccountResult> onSuccess, Action<PlayFabError> onFailed)
        {
            var request = new UnlinkSteamAccountRequest();
            PlayFabClientAPI.UnlinkSteamAccount(request, onSuccess, onFailed);
        }

        public void Logout()
        {
            PlayFabClientAPI.ForgetAllCredentials();
        }

        private void PostAuthProccess(LoginResult loginResult, Action<LoginResult, FunctionPostLoginResult> onLogin, Action<CBSError> onFailed)
        {
            var profileID = loginResult.PlayFabId;
            var isNew = loginResult.NewlyCreated;
            var autoRenerateName = AuthData.AutoGenerateRandomNickname;
            var loadInventory = AuthData.PreloadInventory && isNew;
            var loadLevel = AuthData.PreloadLevelData;
            var loadAccountdata = AuthData.PreloadAccountInfo;
            var loadCurrencies = AuthData.PreloadCurrency;
            var loadClan = AuthData.PreloadClan;
            var namePrefix = AuthData.RandomNamePrefix;
            var newPlayerChecker = AuthData.NewPlayerCheckSolution;
            var loadCatalogItems = AuthData.LoadItemsType;

            var functionRequest = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.PostAuthMethod,
                FunctionParameter = new FunctionPostLoginRequest
                {
                    ProfileID = profileID,
                    AuthGenerateName = autoRenerateName,
                    NewlyCreated = isNew,
                    RandomNamePrefix = namePrefix,
                    PreloadPlayerLevel = loadLevel,
                    PreloadAccountData = loadAccountdata,
                    PreloadClan = loadClan,
                    NewPlayerChecker = newPlayerChecker,
                    LoadItems = loadCatalogItems
                }
            };

            PlayFabCloudScriptAPI.ExecuteFunction(functionRequest, onExecute =>
            {
                var error = onExecute.GetCBSError();
                if (error != null)
                {
                    onFailed?.Invoke(error);
                }
                else
                {
                    var postLoginResult = onExecute.GetResult<FunctionPostLoginResult>();
                    isNew = postLoginResult.OverridedNewPlayerValue;
                    loginResult.NewlyCreated = isNew;
                    if (isNew && autoRenerateName && AuthData.PreloadAccountInfo)
                    {
                        var profile = loginResult.InfoResultPayload.PlayerProfile ?? new PlayFab.ClientModels.PlayerProfileModel();
                        profile.DisplayName = postLoginResult.DisplayName;
                        loginResult.InfoResultPayload.PlayerProfile = profile;
                    }

                    if (loadCatalogItems == SharedData.LoadCatalogItems.SINGLE_CALL)
                    {
                        onLogin?.Invoke(loginResult, postLoginResult);
                    }
                    else if (loadCatalogItems == SharedData.LoadCatalogItems.SEPARATE_CALL)
                    {
                        LoadCatalogItems(onLogin, loginResult, postLoginResult, onFailed);
                    }
                    
                }
            }, onError =>
            {
                onFailed?.Invoke(CBSError.FromTemplate(onError));
            });
        }

        private void LoadCatalogItems(Action<LoginResult, FunctionPostLoginResult> onLogin, LoginResult loginResult, FunctionPostLoginResult postLoginResult, Action<CBSError> onFailed)
        {
            var itemsRequest = new GetCatalogItemsRequest
            {
                CatalogVersion = CatalogKeys.ItemsCatalogID
            };
            PlayFabClientAPI.GetCatalogItems(itemsRequest, onGet =>
            {
                postLoginResult.ItemsResult = onGet;
                onLogin?.Invoke(loginResult, postLoginResult);
            }, onLoadFailed =>
            {
                onFailed?.Invoke(CBSError.FromTemplate(onLoadFailed));
            });
        }

        private void PostMailRegistrationProccess(RegisterPlayFabUserResult registerResult, Action<RegisterPlayFabUserResult> onRegister, Action<CBSError> onFailed)
        {
            var profileID = registerResult.PlayFabId;
            var isNew = true;
            var autoRenerateName = AuthData.AutoGenerateRandomNickname;
            var namePrefix = AuthData.RandomNamePrefix;
            var newPlayerChecker = AuthData.NewPlayerCheckSolution;

            var functionRequest = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.PostAuthMethod,
                FunctionParameter = new FunctionPostLoginRequest
                {
                    ProfileID = profileID,
                    AuthGenerateName = autoRenerateName,
                    NewlyCreated = isNew,
                    RandomNamePrefix = namePrefix,
                    NewPlayerChecker = newPlayerChecker
                }
            };

            PlayFabCloudScriptAPI.ExecuteFunction(functionRequest, onExecute =>
            {
                var error = onExecute.GetCBSError();
                if (error != null)
                {
                    onFailed?.Invoke(error);
                }
                else
                {
                    onRegister?.Invoke(registerResult);
                }
            }, onError =>
            {
                onFailed?.Invoke(CBSError.FromTemplate(onError));
            });
        }

        private GetPlayerCombinedInfoRequestParams GetLoginRequestParams()
        {
            return new GetPlayerCombinedInfoRequestParams
            {
                GetUserAccountInfo = AuthData.PreloadAccountInfo,
                GetPlayerProfile = AuthData.PreloadAccountInfo,
                GetPlayerStatistics = AuthData.PreloadStatistics,
                GetUserInventory = AuthData.PreloadInventory,
                GetUserData = AuthData.PreloadUserData,
                GetTitleData = AuthData.PreloadTitleData,
                GetUserVirtualCurrency = AuthData.PreloadCurrency
            };
        }
    }
}
