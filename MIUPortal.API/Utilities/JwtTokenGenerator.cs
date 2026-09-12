using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MIUPortal.API.Utilities
{
    public static class JwtTokenGenerator
    {
        // =========================================================
        // GENERATE TOKEN
        // =========================================================
        public static string GenerateToken(string email)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            var jwtSettings = config.GetSection("JwtSettings");

            string secretKey =
                jwtSettings["SecretKey"]
                ?? "your-super-secret-key-this-must-be-at-least-32-characters-long";

            int expiryMinutes =
                int.TryParse(jwtSettings["ExpiryMinutes"], out int minutes)
                    ? minutes
                    : 120;

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secretKey)
            );

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

            var claims = new[]
            {
                new Claim(ClaimTypes.Email, email),

                new Claim(
                    JwtRegisteredClaimNames.Jti,
                    Guid.NewGuid().ToString()
                )
            };

            var token = new JwtSecurityToken(
                issuer: "MIUPortal",
                audience: "MIUPortalUsers",
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }

        // =========================================================
        // GENERATE TOKEN WITH REG NUMBER
        // =========================================================
        public static string GenerateToken(
     string email,
     string? regNumber,
     int userId,
     string role)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            var jwtSettings = config.GetSection("JwtSettings");

            string secretKey =
                jwtSettings["SecretKey"]
                ?? "your-super-secret-key-this-must-be-at-least-32-characters-long";

            int expiryMinutes =
                int.TryParse(jwtSettings["ExpiryMinutes"], out int minutes)
                    ? minutes
                    : 120;

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secretKey)
            );

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

            var claims = new[]
            {
        new Claim(ClaimTypes.Email, email),

        new Claim("RegNumber", regNumber ?? ""),

        new Claim("UserId", userId.ToString()),

        new Claim(ClaimTypes.Role, role),

        new Claim(
            JwtRegisteredClaimNames.Jti,
            Guid.NewGuid().ToString()
        )
    };

            var token = new JwtSecurityToken(
                issuer: "MIUPortal",
                audience: "MIUPortalUsers",
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }

        // =========================================================
        // VALIDATE TOKEN
        // =========================================================
        public static bool ValidateToken(string token)
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json")
                    .Build();

                var jwtSettings = config.GetSection("JwtSettings");

                string secretKey =
                    jwtSettings["SecretKey"]
                    ?? "your-super-secret-key-this-must-be-at-least-32-characters-long";

                var key = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(secretKey)
                );

                var tokenHandler = new JwtSecurityTokenHandler();

                tokenHandler.ValidateToken(
                    token,
                    new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = key,

                        ValidateIssuer = true,
                        ValidIssuer = "MIUPortal",

                        ValidateAudience = true,
                        ValidAudience = "MIUPortalUsers",

                        ValidateLifetime = true,

                        ClockSkew = TimeSpan.Zero
                    },
                    out SecurityToken validatedToken
                );

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}