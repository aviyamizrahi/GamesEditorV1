using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace UsersManager.Server
{
    //לא לגעת - ניהול טוקנים
    public class TokenService
    {
        private readonly IConfiguration _configuration;
        private readonly string _securityKey;
        private readonly string _validIssuer;
        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
            _securityKey = _configuration["JWTSettings:securityKey"];
            _validIssuer = _configuration["JWTSettings:validIssuer"];
        }

        public string GenerateToken(List<Claim> claims)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_securityKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _validIssuer,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(2),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }


        public ClaimsPrincipal ValidateToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            
            // Basic JWT format validation
            if (!tokenHandler.CanReadToken(token))
            {
                return null;
            }

            var key = Encoding.UTF8.GetBytes(_securityKey);
            try
            {
                var claimsPrincipal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = _validIssuer,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                return claimsPrincipal;
            }
            catch
            {
                return null;
            }
        }

        public bool IsTokenExpired(string token)
        {
            var jwtToken = new JwtSecurityTokenHandler().ReadToken(token) as JwtSecurityToken;
            return jwtToken.ValidTo < DateTime.UtcNow;
        }

        public string RefreshToken(string token)
        {
            var principal = ValidateToken(token);
            if (principal == null)
            {
                return null;
            }

            var jwtToken = new JwtSecurityTokenHandler().ReadToken(token) as JwtSecurityToken;
            var expirationTime = jwtToken.ValidTo;
            var currentTime = DateTime.UtcNow;

            // Refresh the token if it's about to expire in the next 5 minutes
            if (expirationTime > currentTime && expirationTime <= currentTime.AddMinutes(5))
            {
                var claims = principal.Claims.ToList();
                return GenerateToken(claims);
            }

            return token; 
        }

        public List<Claim> GetClaims(string jwt)
        {
            if (string.IsNullOrWhiteSpace(jwt))
            {
                return new List<Claim>();
            }

            try
            {
                var handler = new JwtSecurityTokenHandler();
                
                // Basic JWT format validation
                if (!handler.CanReadToken(jwt) || jwt.Split('.').Length != 3)
                {
                    return new List<Claim>();
                }

                var token = handler.ReadJwtToken(jwt);
                return token.Claims.ToList();
            }
            catch
            {
                return new List<Claim>();
            }
        }

    }
}
