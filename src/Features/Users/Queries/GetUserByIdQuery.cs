using MediatR;
using MedConnect.Models;
using MedConnect.Repositories;

namespace MedConnect.Features.Users.Queries;

public record GetUserByIdQuery(int Id) : IRequest<User?>;

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, User?>
{
    private readonly IUserRepository _userRepository;

    public GetUserByIdQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<User?> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        return await _userRepository.GetByIdAsync(request.Id);
    }
}