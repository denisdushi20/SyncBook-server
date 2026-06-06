using Microsoft.AspNetCore.Authorization;
using SyncBook.Server.Models;
using SyncBook.Server.Services;

namespace SyncBook.Server.Authorization;

public class BusinessOwnerRequirement : IAuthorizationRequirement;

public class BusinessOwnerAuthorizationHandler : AuthorizationHandler<BusinessOwnerRequirement>
{
    private readonly CurrentUserService _currentUser;

    public BusinessOwnerAuthorizationHandler(CurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        BusinessOwnerRequirement requirement)
    {
        if (_currentUser.Role == UserRole.BusinessOwner && !string.IsNullOrEmpty(_currentUser.BusinessId))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
