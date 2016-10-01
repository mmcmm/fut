using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using UltimateTeam.Toolkit.Constants;
using UltimateTeam.Toolkit.Exceptions;
using UltimateTeam.Toolkit.Extensions;
using UltimateTeam.Toolkit.Models;
using UltimateTeam.Toolkit.Services;

namespace UltimateTeam.Toolkit.Requests
{
    internal class LoginRequestMobile : FutRequestBase, IFutRequest<LoginResponse>
    {
        private readonly LoginDetails _loginDetails;
        private readonly ITwoFactorCodeProvider _twoFactorCodeProvider;
        private IHasher _hasher;

        private static readonly Random Random = new Random();
        private readonly string _machineKey = GetRandomHexNumber(16);
        private string _sessionId = string.Empty;
        private string _powSessionId = string.Empty;
        private string _nucUserId = string.Empty;
        private string _nucPersonaId = string.Empty;
        private string _code = string.Empty;

        public IHasher Hasher
        {
            get { return _hasher ?? (_hasher = new Hasher()); }
            set { _hasher = value; }
        }

        public LoginRequestMobile(LoginDetails loginDetails, ITwoFactorCodeProvider twoFactorCodeProvider)
        {
            loginDetails.ThrowIfNullArgument();
            _loginDetails = loginDetails;
            _twoFactorCodeProvider = twoFactorCodeProvider;
        }

        public void SetCookieContainer(CookieContainer cookieContainer)
        {
            HttpClient.MessageHandler.CookieContainer = cookieContainer;
        }

        public async Task<LoginResponse> PerformRequestAsync()
        {
            try
            {
                var mainPageResponseMessage = await GetMainPageAsync().ConfigureAwait(false);
                await LoginAsync(_loginDetails, mainPageResponseMessage);
                var authToken = await GetAuthTokenAsync(_code);
                var pid = await GetMobilePidAsync(authToken.Access_Token);
                _nucUserId = pid.Pid.ExternalRefValue;

                var sessionCode = await GetMobileAuthCodeAsync(authToken.Access_Token);              
                try
                {
                    var powSessionId = await AuthPOWAsync(sessionCode.Code);
                    _powSessionId = powSessionId.Sid;
                }
                catch (Exception)
                {
                    // ignored 
                }
                var authCode = await GetMobileAuthCodeAsync(authToken.Access_Token);

                var shards = await GetMobileShardsAsync();
                var userAccounts = await GetMobileUserAccountsAsync(_loginDetails.Platform);
                _nucPersonaId = userAccounts.UserAccountInfo.Personas.LastOrDefault().PersonaId.ToString();            
                var sessionId = await AuthAsync(authCode.Code, _nucPersonaId, GetGameSku(_loginDetails.Platform));
                _sessionId = sessionId.Sid;

                var phishingToken = await ValidateAsync(_loginDetails);

                return new LoginResponse(_nucUserId, shards, userAccounts, _sessionId, phishingToken, _nucPersonaId);
            }
            catch (Exception e)
            {
                throw new FutException($"Unable to login to {AppVersion}", e);
            }
        }

        private async Task<AuthToken> GetAuthTokenAsync(string code)
        {
            AddMobileLoginHeaders();
            AddContentHeader("application/x-www-form-urlencoded");
            var authTokenResponseMessage = await HttpClient.PostAsync(string.Format(Resources.Token, code), new FormUrlEncodedContent(new KeyValuePair<string, string>[0]));
            return await DeserializeAsync<AuthToken>(authTokenResponseMessage);
        }

        private async Task<PidData> GetMobilePidAsync(string authCode)
        {
            AddMobileLoginHeaders();
            AddAuthorizationHeader(authCode);
            var pidDataResponseMessage = await HttpClient.GetAsync(string.Format(Resources.Pid));
            return await DeserializeAsync<PidData>(pidDataResponseMessage);
        }

        private async Task<AuthCode> GetMobileAuthCodeAsync(string accessToken)
        {
            AddMobileLoginHeaders();
            var authTokenResponseMessage = await HttpClient.PostAsync(string.Format(Resources.AuthCode, accessToken, _machineKey), new FormUrlEncodedContent(new KeyValuePair<string, string>[0]));
            return await DeserializeAsync<AuthCode>(authTokenResponseMessage);
        }


        private async Task<Auth> AuthPOWAsync(string authCode)
        {
            AddMobileLoginHeaders();
            HttpClient.AddRequestHeader(NonStandardHttpHeaders.PowSessionId, string.Empty);
            HttpClient.AddRequestHeader(NonStandardHttpHeaders.SessionId, string.Empty);
            var content = $@"{{ ""isReadOnly"":true,""sku"":""FUT17AND"",""clientVersion"":21,""locale"":""en-GB"",""method"":""authcode"",""priorityLevel"":4,""identification"":{{""authCode"":""{authCode}"",""redirectUrl"":""nucleus:rest""}} }}";
            var authMessage = await HttpClient.PostAsync(string.Format(Resources.POWAuth, DateTime.Now.ToUnixTime()), new StringContent(content));
            var authResponse = await DeserializeAsync<Auth>(authMessage);

            _powSessionId = authResponse.Sid;

            return authResponse;
        }

        private async Task<User> GetMobileNucleusIdAsync()
        {
            AddMobileLoginHeaders();
            HttpClient.AddRequestHeader(NonStandardHttpHeaders.NucleusId, _nucUserId);
            HttpClient.AddRequestHeader(NonStandardHttpHeaders.PowSessionId, _powSessionId);
            var nucleusResponseMessage = await HttpClient.GetAsync(string.Format(Resources.NucleusId, _nucUserId, DateTime.Now.ToUnixTime()));
            return await DeserializeAsync<User>(nucleusResponseMessage);
        }

        private async Task<Shards> GetMobileShardsAsync()
        {
            AddMobileLoginHeaders();
            HttpClient.AddRequestHeader(NonStandardHttpHeaders.NucleusId, _nucUserId);
            HttpClient.AddRequestHeader(NonStandardHttpHeaders.SessionId, string.Empty);
            var shardsResponseMessage = await HttpClient.GetAsync(string.Format(Resources.Shards, DateTime.Now.ToUnixTime()));
            return await DeserializeAsync<Shards>(shardsResponseMessage);
        }

        private async Task<UserAccounts> GetMobileUserAccountsAsync(Platform platform)
        {
            AddMobileLoginHeaders();
            HttpClient.AddRequestHeader(NonStandardHttpHeaders.NucleusId, _nucUserId);
            HttpClient.AddRequestHeader(NonStandardHttpHeaders.SessionId, string.Empty);
            var accountInfoResponseMessage = await HttpClient.GetAsync(string.Format(Resources.AccountInfo, DateTime.Now.ToUnixTime()));
            return await DeserializeAsync<UserAccounts>(accountInfoResponseMessage);
        }

        private async Task<Auth> AuthAsync(string authCode, string personaId, string sku)
        {
            AddMobileLoginHeaders();
            HttpClient.AddRequestHeader(NonStandardHttpHeaders.SessionId, string.Empty);
            HttpClient.AddRequestHeader(NonStandardHttpHeaders.PowSessionId, string.Empty);
            var content = $@"{{ ""isReadOnly"":false,""sku"":""FUT17AND"",""clientVersion"":21,""locale"":""en-GB"",""method"":""authcode"",""priorityLevel"":4,""identification"":{{""authCode"":""{authCode}"",""redirectUrl"":""nucleus:rest""}},""nucleusPersonaId"":""{personaId}"",""gameSku"":""{sku}"" }}";
            var authMessage = await HttpClient.PostAsync(string.Format(Resources.Auth, DateTime.Now.ToUnixTime()), new StringContent(content));
            var authResponse = await DeserializeAsync<Auth>(authMessage);

            return authResponse;
        }

        private async Task<string> ValidateAsync(LoginDetails loginDetails)
        {
            AddMobileLoginHeaders();
            HttpClient.AddRequestHeader(NonStandardHttpHeaders.NucleusId, _nucUserId);
            HttpClient.AddRequestHeader(NonStandardHttpHeaders.SessionId, _sessionId);
            var validateResponseMessage = await HttpClient.PostAsync(Resources.Validate, new FormUrlEncodedContent(
                new[]
                {
                    new KeyValuePair<string, string>("answer", Hasher.Hash(loginDetails.SecretAnswer))
                }));
            var validateResponse = await DeserializeAsync<ValidateResponse>(validateResponseMessage);

            return validateResponse.Token;
        }

        private static string GetGameSku(Platform platform)
        {
            switch (platform)
            {
                case Platform.Ps3:
                    return "FFA17PS3";
                case Platform.Ps4:
                    return "FFA17PS4";
                case Platform.Xbox360:
                    return "FFA17XBX";
                case Platform.XboxOne:
                    return "FFA17XBO";
                case Platform.Pc:
                    return "FFA17PCC";
                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, null);
            }
        }

        private async Task LoginAsync(LoginDetails loginDetails, HttpResponseMessage mainPageResponseMessage)
        {
            var loginResponseMessage = await HttpClient.PostAsync(mainPageResponseMessage.RequestMessage.RequestUri, new FormUrlEncodedContent(
                                                                                                                         new[]
                                                                                                                         {
                                                                                                                             new KeyValuePair<string, string>("email", loginDetails.Username),
                                                                                                                             new KeyValuePair<string, string>("password", loginDetails.Password),
                                                                                                                             new KeyValuePair<string, string>("country", "DE"),
                                                                                                                             new KeyValuePair<string, string>("phoneNumber", ""),
                                                                                                                             new KeyValuePair<string, string>("passwordForPhone", ""),
                                                                                                                             new KeyValuePair<string, string>("_rememberMe", "on"),
                                                                                                                             new KeyValuePair<string, string>("rememberMe", "on"),
                                                                                                                             new KeyValuePair<string, string>("_eventId", "submit"),
                                                                                                                             new KeyValuePair<string, string>("gCaptchaResponse", ""),
                                                                                                                             new KeyValuePair<string, string>("isPhoneNumberLogin", "false"),
                                                                                                                             new KeyValuePair<string, string>("isIncompletePhone", "")
                                                                                                                         }));
            loginResponseMessage.EnsureSuccessStatusCode();
            HttpResponseMessage loginResponseMessage2 = null;
            //check if twofactorcode is required
            var contentData = await loginResponseMessage.Content.ReadAsStringAsync();
            if (contentData.Contains("var redirectUri = 'https://signin.ea.com:443/p/web2/login?execution="))
            {
                var redirectUrl = "https://signin.ea.com:443/p/web2/login?execution=" +
                                     contentData.Substring(
                                         contentData.IndexOf("https://signin.ea.com:443/p/web2/login?execution=") +
                                         "https://signin.ea.com:443/p/web2/login?execution=".Length);
                redirectUrl = redirectUrl.Substring(0, redirectUrl.IndexOf("'")) + "&_eventId=end";
                 

                loginResponseMessage2 = await HttpClient.GetAsync(redirectUrl);
                loginResponseMessage2.EnsureSuccessStatusCode();

                
                contentData = await loginResponseMessage2.Content.ReadAsStringAsync();

            }

            //check if twofactorcode is required
            if (contentData.Contains("We sent a security code to your") || contentData.Contains("Your security code was sent to") || contentData.Contains("Enter the 6-digit verification code generated by your App Authenticator") || contentData.Contains("Enter the 6-digit verification code generated by your App Authenticator"))
                await SetTwoFactorCodeAsync(loginResponseMessage2);
     
            else if (loginResponseMessage2 != null && loginResponseMessage2.RequestMessage.RequestUri.ToString().Contains("code"))
            {
                var code = loginResponseMessage2.RequestMessage.RequestUri.ToString();
                code = code.Substring(code.IndexOf("code=", StringComparison.Ordinal) + "code=".Length);
                
                //TO DO: How to verify, that content is a companion code
                if (code.Length <= 64)
                {
                    _code = code;
                }
            }
        }

        private async Task SetTwoFactorCodeAsync(HttpResponseMessage loginResponse)
        {
            var tfCode = await _twoFactorCodeProvider.GetTwoFactorCodeAsync();

            var responseContent = await loginResponse.Content.ReadAsStringAsync();

            AddReferrerHeader(loginResponse.RequestMessage.RequestUri.ToString());

            var codeResponseMessage = await HttpClient.PostAsync(loginResponse.RequestMessage.RequestUri, new FormUrlEncodedContent(
                        new[]
                            {
                            new KeyValuePair<string, string>(responseContent.Contains("twofactorCode") ? "twofactorCode" : "twoFactorCode", tfCode),
                            new KeyValuePair<string, string>("_eventId", "submit"),
                            new KeyValuePair<string, string>("_trustThisDevice", "on"),
                            new KeyValuePair<string, string>("trustThisDevice", "on")
                            }));

            codeResponseMessage.EnsureSuccessStatusCode();

            var contentData = await codeResponseMessage.Content.ReadAsStringAsync();

            if (contentData.Contains("Incorrect code entered"))
                throw new FutException("Incorrect TwoFactorCode entered.");

            if (contentData.Contains("Tired of waiting for your code?"))
                await SkipAuthenticatorAdvertiseAsync(codeResponseMessage);
          
            else if (codeResponseMessage.RequestMessage.RequestUri.ToString().Contains("code"))
            {
                var code = codeResponseMessage.RequestMessage.RequestUri.ToString();
                code = code.Substring(code.IndexOf("code=", StringComparison.Ordinal) + "code=".Length);

                //TO DO: How to verify, that content is a companion code
                if (code.Length <= 64)
                {
                    _code = code;
                }
            }
        }

        private async Task SkipAuthenticatorAdvertiseAsync(HttpResponseMessage codeResponse)
        {
            var responseContent = await codeResponse.Content.ReadAsStringAsync();

            AddReferrerHeader(codeResponse.RequestMessage.RequestUri.ToString());

            var authenticatorResponseMessage = await HttpClient.PostAsync(codeResponse.RequestMessage.RequestUri, new FormUrlEncodedContent(
                        new[]
                            {
                            new KeyValuePair<string, string>("_eventId", "cancel"),
                            new KeyValuePair<string, string>("appDevice", "ANDROID"),
                            }));

            authenticatorResponseMessage.EnsureSuccessStatusCode();

            var contentData = await authenticatorResponseMessage.Content.ReadAsStringAsync();
            
           if (authenticatorResponseMessage.RequestMessage.RequestUri.ToString().Contains("code"))
            {
                var code = authenticatorResponseMessage.RequestMessage.RequestUri.ToString();
                code = code.Substring(code.IndexOf("code=", StringComparison.Ordinal) + "code=".Length);

                //TO DO: How to verify, that content is a companion code
                if (code.Length <= 64)
                {
                    _code = code;
                }
            }
        }

        private async Task<HttpResponseMessage> GetMainPageAsync()
        {
            AddUserAgent();
            AddAcceptEncodingHeader();
            var mainPageResponseMessage = await HttpClient.GetAsync(Resources.Home);
            mainPageResponseMessage.EnsureSuccessStatusCode();

            //check if twofactorcode is required
            var contentData = await mainPageResponseMessage.Content.ReadAsStringAsync();
            if (contentData.Contains("We sent a security code to your") ||
                contentData.Contains("Your security code was sent to") ||
                contentData.Contains("Enter the 6-digit verification code generated by your App Authenticator"))
            {
                await SetTwoFactorCodeAsync(mainPageResponseMessage);
            }

            return mainPageResponseMessage;
        }

        public static string GetRandomHexNumber(int digits)
        {
            byte[] buffer = new byte[digits / 2];
            Random.NextBytes(buffer);
            string result = String.Concat(buffer.Select(x => x.ToString("X2")).ToArray());
            if (digits % 2 == 0)
                return result;
            return result + Random.Next(17).ToString("X");
        }
    }
}