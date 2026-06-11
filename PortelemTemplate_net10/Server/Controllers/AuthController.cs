using UsersManager.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using UsersManager.Server;
using Microsoft.Extensions.Logging;

namespace AuthTemplate.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AuthRepository _authRepository;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly TokenService _tokenService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AuthRepository authRepository, IHttpClientFactory httpClientFactory, IConfiguration configuration, TokenService tokenService, ILogger<AuthController> logger)
        {
            _authRepository = authRepository;
            _httpClient = httpClientFactory.CreateClient();
            _configuration = configuration;
            _tokenService = tokenService;
            _logger = logger;
        }

        //קבלת פרטי משתמש
        [HttpGet("user")]
        public async Task<ActionResult> GetUser()
        {
            User user = await _authRepository.GetUser();
            return Ok(user);
        }
        

        //התחברות
        [HttpPost("portelemLogin")]
        public async Task<IActionResult> PortelemLogin([FromBody] string ssoToken)
        {
            if (string.IsNullOrWhiteSpace(ssoToken))
            {
                _logger.LogWarning("PortelemLogin: Empty or null SSO token provided");
                return BadRequest(new { error = "Invalid token", message = "SSO token cannot be empty" });
            }

            // Basic JWT format validation
            if (ssoToken.Split('.').Length != 3)
            {
                _logger.LogWarning("PortelemLogin: Invalid JWT format");
                return BadRequest(new { error = "Invalid token", message = "Invalid JWT token format" });
            }

            try
            {
                object obj = new
                {
                    token = ssoToken,
                    ServiceId = Convert.ToInt16(_configuration["Portelem:ServiceId"]),
                    ServiceSecret = _configuration["Portelem:SecretKey"]
                };
                string url = _configuration["Portelem:PortelemUrl"];
                var ssoResponse = await _httpClient.PostAsJsonAsync(url, obj);
                
                if (ssoResponse.IsSuccessStatusCode)
                {
                    var responseContent = await ssoResponse.Content.ReadAsStringAsync();
                    if (responseContent.Equals("false", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("PortelemLogin: User not authorized in Portelem");
                        return Unauthorized(new { error = "Unauthorized", message = "User not authorized in Portelem" });
                    }

                    var claims = _tokenService.GetClaims(ssoToken);
                    var emailClaim = claims.FirstOrDefault(s => s.Type == AuthConstants.ClaimTypeEmail || s.Type == ClaimTypes.Email);
                    
                    if (emailClaim == null || string.IsNullOrWhiteSpace(emailClaim.Value))
                    {
                        _logger.LogWarning("PortelemLogin: Email claim not found in SSO token");
                        return BadRequest(new { error = "Invalid token", message = "Email claim not found in token" });
                    }

                    //מנסה להתחבר
                    string loginToken = await _authRepository.Login(emailClaim.Value);
                    
                    if (loginToken == AuthConstants.NoUser)
                    {
                        _logger.LogInformation("PortelemLogin: User not found, attempting signup");
                        string signupToken = await SignUp(ssoToken);
                        if (signupToken == AuthConstants.ErrorSignup)
                        {
                            _logger.LogError("PortelemLogin: Signup failed");
                            return BadRequest(new { error = "Signup error", message = "Failed to create user account" });
                        }
                        return Ok(new { token = signupToken });
                    }
                    
                    return Ok(new { token = loginToken });
                }

                var errorContent = await ssoResponse.Content.ReadAsStringAsync();
                _logger.LogError("PortelemLogin: Portelem API error - {StatusCode}: {Error}", ssoResponse.StatusCode, errorContent);
                return BadRequest(new { error = "Portelem API error", message = errorContent });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PortelemLogin: Unexpected error during authentication");
                return StatusCode(500, new { error = "Internal server error", message = "An error occurred during authentication" });
            }
        }

        async Task<string> SignUp(string jwt)
        {
            try
            {
                var claims = _tokenService.GetClaims(jwt);
                
                var subClaim = claims.FirstOrDefault(s => s.Type == AuthConstants.ClaimTypeSub || s.Type == ClaimTypes.NameIdentifier);
                var emailClaim = claims.FirstOrDefault(s => s.Type == AuthConstants.ClaimTypeEmail || s.Type == ClaimTypes.Email);
                var givenNameClaim = claims.FirstOrDefault(s => s.Type == AuthConstants.ClaimTypeGivenName || s.Type == ClaimTypes.GivenName);
                var familyNameClaim = claims.FirstOrDefault(s => s.Type == AuthConstants.ClaimTypeFamilyName || s.Type == ClaimTypes.Surname);

                if (subClaim == null || string.IsNullOrWhiteSpace(subClaim.Value))
                {
                    _logger.LogError("SignUp: Sub claim not found in token");
                    return AuthConstants.ErrorSignup;
                }

                if (emailClaim == null || string.IsNullOrWhiteSpace(emailClaim.Value))
                {
                    _logger.LogError("SignUp: Email claim not found in token");
                    return AuthConstants.ErrorSignup;
                }

                if (!int.TryParse(subClaim.Value, out int portelemId))
                {
                    _logger.LogError("SignUp: Invalid PortelemId format in token");
                    return AuthConstants.ErrorSignup;
                }

                PortelemUser newUser = new PortelemUser()
                {
                    PortelemId = portelemId,
                    Email = emailClaim.Value,
                    FirstName = givenNameClaim?.Value ?? string.Empty,
                    LastName = familyNameClaim?.Value ?? string.Empty
                };

                string user = await _authRepository.Signup(newUser);

                switch (user)
                {
                    case AuthConstants.UserExists:
                        _logger.LogWarning("SignUp: User already exists with email {Email}", newUser.Email);
                        return AuthConstants.ErrorSignup;
                    case AuthConstants.ErrorInsertToDb:
                        _logger.LogError("SignUp: Failed to insert user to database");
                        return AuthConstants.ErrorSignup;
                    default:
                        return user;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignUp: Unexpected error during signup");
                return AuthConstants.ErrorSignup;
            }
        }

        //מרענן את הטוקן שעומד לפוג
        [HttpGet("refresh")]
        public async Task<IActionResult> refreshToken()
        {
            try
            {
                string token = await _authRepository.RefreshToken();
                if (token == AuthConstants.TokenInvalid)
                {
                    _logger.LogWarning("RefreshToken: Invalid token provided");
                    return Unauthorized(new { error = "Invalid token", message = "Token is invalid or expired" });
                }

                if (string.IsNullOrWhiteSpace(token))
                {
                    _logger.LogWarning("RefreshToken: Token refresh returned null or empty");
                    return Unauthorized(new { error = "Token refresh failed", message = "Unable to refresh token" });
                }

                return Ok(new { token = token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RefreshToken: Unexpected error during token refresh");
                return StatusCode(500, new { error = "Internal server error", message = "An error occurred during token refresh" });
            }
        }

        //התנתקות
        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await _authRepository.Logout();
                _logger.LogInformation("Logout: User logged out successfully");
                return Ok(new { message = "Logged out successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout: Unexpected error during logout");
                return StatusCode(500, new { error = "Internal server error", message = "An error occurred during logout" });
            }
        }
    }
}