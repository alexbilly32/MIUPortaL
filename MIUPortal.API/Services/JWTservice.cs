using Microsoft.IdentityModel.Tokens;
using MIUPortal.API.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MIUPortal.API.Services
{
    public class JwtService
    {
        private readonly IConfiguration _configuration;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        
        public string GenerateJwt(string email, string role, int userId)
        {
            return GenerateJwt(email, role, userId, null);
        }

       
        public string GenerateJwt(string email, string role, int userId, Dictionary<string, string>? extraClaims)
        {
            
            var secretKey = GetRequiredConfig("JwtSettings:SecretKey");
            var issuer = GetRequiredConfig("JwtSettings:Issuer");
            var audience = GetRequiredConfig("JwtSettings:Audience");

            
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secretKey)
            );
            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

            
            var claimsList = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, email ?? ""),
                new Claim(ClaimTypes.Role, role ?? ""),
               
                new Claim("UserId", userId.ToString()),
                new Claim("Role", role ?? ""),
                new Claim("Email", email ?? "")
            };

           
            if (extraClaims != null)
            {
                foreach (var kvp in extraClaims)
                {
                    claimsList.Add(new Claim(kvp.Key, kvp.Value));
                }
            }

           
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claimsList,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateLecturerJwt(Lecturer lecturer)
        {
            var extraClaims = new Dictionary<string, string>
            {
                { "lecturerId", lecturer.LecturerId.ToString() },
                { "facultyId", lecturer.FacultyId?.ToString() ?? "0" },
                { "isDean", lecturer.IsDean.ToString().ToLower() },
                { ClaimTypes.Name, $"{lecturer.FirstName} {lecturer.LastName}" }
            };

            return GenerateJwt(lecturer.Email ?? "", "Lecturer", lecturer.LecturerId, extraClaims);
        }

       
        private string GetRequiredConfig(string key)
        {
            var value = _configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"Configuration error: '{key}' is missing or empty."
                );
            }
            return value;
        }
    }
}