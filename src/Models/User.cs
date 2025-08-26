using System.ComponentModel.DataAnnotations;

namespace MedConnect.Models
{
    public class User
    {
        public int Id { get; set; }
        [Required]
        public string Username { get; set; } = string.Empty;
        [Required]
        public string Email { get; set; } = string.Empty;
        [Required]
        public string PasswordHash { get; set; } = string.Empty;
        [Required]
        public Role Role { get; set; } = Role.Pharma; // Default role
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public ICollection<string> RevokedJtis { get; set; } = new List<string>(); // For JWT revocation
    }
}