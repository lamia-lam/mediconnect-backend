using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedConnect.Features.Users.Commands;
using MedConnect.Features.Users.Queries;
using MedConnect.Models;
using Microsoft.OpenApi.Extensions;

namespace MedConnect.Controllers;

[ApiController]
[Route("api/[controller]")]
//[Authorize]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IMediator mediator, ILogger<UsersController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    //[Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> GetAll()
    {
        _logger.LogInformation("Fetching all users");
        var users = await _mediator.Send(new GetAllUsersQuery());
        return Ok(users);
    }

    [HttpGet("{id}")]
    //[Authorize(Policy = "RequireDoctorOrPharma")]
    public async Task<IActionResult> GetById(int id)
    {
        _logger.LogInformation("Fetching user by ID: {Id}", id);
        var user = await _mediator.Send(new GetUserByIdQuery(id));
        if (user == null)
        {
            _logger.LogWarning("User not found with ID: {Id}", id);
            return NotFound();
        }
        return Ok(user);
    }

    [HttpPost]
    //[Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateUserCommand command)
    {
        _logger.LogInformation("Creating user: {Username}", command.Username);
        if (!Enum.IsDefined(typeof(Role), command.Role))
        {
            command = command with { Role = Role.Pharma };
        }
        var user = await _mediator.Send(command);
        _logger.LogInformation("User created successfully: {Username}", command.Username);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    [HttpPut("{id}")]
    //[Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserCommand command)
    {
        _logger.LogInformation("Updating user with ID: {Id}", id);
        if (id != command.Id)
        {
            _logger.LogWarning("User ID mismatch: route ID {RouteId}, command ID {CommandId}", id, command.Id);
            return BadRequest();
        }
        var user = await _mediator.Send(command);
        if (user == null)
        {
            _logger.LogWarning("User not found for update with ID: {Id}", id);
            return NotFound();
        }
        _logger.LogInformation("User updated successfully: {Id}", id);
        return Ok(user);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        _logger.LogInformation("Deleting user with ID: {Id}", id);
        var result = await _mediator.Send(new DeleteUserCommand(id));
        if (!result)
        {
            _logger.LogWarning("User not found for deletion with ID: {Id}", id);
            return NotFound();
        }
        _logger.LogInformation("User deleted successfully: {Id}", id);
        return NoContent();
    }
}