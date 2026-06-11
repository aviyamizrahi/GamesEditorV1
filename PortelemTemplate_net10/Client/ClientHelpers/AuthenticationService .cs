using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Net.Http.Json;
using System.Net.Http;
using System.Security.Claims;
using System.Web;
using Microsoft.AspNetCore.Components;
using UsersManager.Shared;


namespace UsersManager.Client
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _options;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly AuthenticationState _anonymous;
        private readonly NavigationManager _navigationManager;
        private readonly IConfiguration _configuration;

        public event Action OnAuthenticationStateChanged;

        public AuthenticationService(HttpClient client, AuthenticationStateProvider authStateProvider,
            NavigationManager navigationManager, IConfiguration configuration)
        {
            _httpClient = client;
            _options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _authStateProvider = authStateProvider;
            _navigationManager = navigationManager;
            _configuration = configuration;
        }

        //התנתקות 
        public async Task Logout()
        {
            var get = await _httpClient.GetAsync("api/auth/logout");
            await ((AuthStateProvider)_authStateProvider).NotifyUserLogout();
            OnAuthenticationStateChanged?.Invoke();
            _navigationManager.NavigateTo($"{_configuration["portelem:mainUrl"]}");
        }


        public async Task<bool> LoginWithPortelem()
        {
            if (string.IsNullOrEmpty(GetQueryParm("token")))
            {
                // תוספת לתמיכה במערכות בפיתוח
                if (_configuration["portelem:type"] == "inDevelop")
                {
                    var baseUri = _navigationManager.Uri;
                    _navigationManager.NavigateTo(
                        $"{_configuration["portelem:loginUrl"]}?id={_configuration["portelem:serviceId"]}&link={baseUri}");
                    return false;
                }
                // מערכות ספציפיות לא בפיתוח
                _navigationManager.NavigateTo(
                    $"{_configuration["portelem:loginUrl"]}{_configuration["portelem:serviceId"]}");
                return false;
            }

            var response = await _httpClient.PostAsJsonAsync($"api/auth/portelemLogin/", GetQueryParm("token"));
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(await response.Content.ReadAsStringAsync());
                return false;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
            if (tokenResponse?.Token == null)
            {
                Console.WriteLine("Failed to get token from response");
                return false;
            }
            
            await ((AuthStateProvider)_authStateProvider).NotifyAuthenticationStateChanged(tokenResponse.Token);
            OnAuthenticationStateChanged?.Invoke();
            // Extract the base path (without the query string)
            var uriWithoutQuery = _navigationManager.Uri.Split('?')[0];
            // Navigate to the same path without the query string
            _navigationManager.NavigateTo(uriWithoutQuery, forceLoad: false);
            return true;
        }
        
        // Add this class to hold the response
        public class TokenResponse
        {
            public string Token { get; set; }
        }

        string GetQueryParm(string parmName)
        {
            var uriBuilder = new UriBuilder(_navigationManager.Uri);
            var q = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
            return q[parmName] ?? "";
        }

        //קבלת פרטי המשתמש
        public async Task<User> GetUserFromClaimAsync()
        {
            var authenticationState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authenticationState.User;

            //אם המשתמש מחובר
            if (user.Identity.IsAuthenticated)
            {
                var userDto = new User
                {
                    FirstName = user.FindFirst(c => c.Type == "given_name")?.Value,
                    LastName = user.FindFirst(c => c.Type == "family_name")?.Value,
                    Email = user.FindFirst(c => c.Type == "email")?.Value,
                    Id = Convert.ToInt16(user.FindFirst(c => c.Type == "nameid")?.Value),
                };

                return userDto;
            }
            //אם לא 
            else
            {
                return null;
            }
        }
    }
}