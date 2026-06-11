using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace UsersManager.Server
{
    public class AuthCheck : IAsyncActionFilter
    {
        private const string AuthUserIdKey = "authUserId";
        private readonly ITokenBlacklistService _tokenBlacklistService;
        private readonly TokenService _tokenService;
        private readonly ILogger<AuthCheck> _logger;

        public AuthCheck(
            ITokenBlacklistService tokenBlacklistService,
            TokenService tokenService,
            ILogger<AuthCheck> logger)
        {
            _tokenBlacklistService =
                tokenBlacklistService ?? throw new ArgumentNullException(nameof(tokenBlacklistService));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var token = ExtractToken(context.HttpContext.Request);

            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("Authentication failed: Missing token");
                context.Result = new UnauthorizedResult();
                return;
            }

            // Validate token first (cheaper than DB lookup)
            var principal = _tokenService.ValidateToken(token);
            if (principal == null)
            {
                _logger.LogWarning("Authentication failed: Invalid token");
                context.Result = new UnauthorizedResult();
                return;
            }

            // Extract userId early for logging
            var userId = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int userIdInt))
            {
                _logger.LogWarning("Authentication failed: Invalid user ID in token");
                context.Result = new UnauthorizedResult();
                return;
            }

            // Check blacklist after validation
            if (_tokenBlacklistService.IsBlacklisted(token))
            {
                _logger.LogWarning("Authentication failed: Token blacklisted for user {UserId}", userIdInt);
                context.Result = new UnauthorizedResult();
                return;
            }

            context.ActionArguments[AuthUserIdKey] = userIdInt;
            await next();
        }

        private static string ExtractToken(HttpRequest request)
        {
            var authHeader = request.Headers["Authorization"].ToString();
            return authHeader.StartsWith(AuthConstants.BearerPrefix, StringComparison.OrdinalIgnoreCase)
                ? authHeader.Substring(AuthConstants.BearerPrefix.Length)
                : authHeader;
        }
    }
}