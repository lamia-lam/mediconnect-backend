using MedConnect.Models;

namespace MedConnect.Services
{
    public interface IJwtService
    {
        string GenerateToken(User user);
    }
}