using MediatR;
using MedConnect.Models;
using MedConnect.Repositories;

namespace MedConnect.Features.Users.Commands;

using MedConnect.Models;

public record UpdateUserCommand(int Id, string Username, string Email, Role Role) : IRequest<User?>;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, User?>
{
    private readonly IUserRepository _userRepository;

    public UpdateUserCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<User?> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.Id);
        if (user == null) return null;

        user.Username = request.Username;
        user.Email = request.Email;
        user.Role = request.Role;

        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        return user;
    }
}