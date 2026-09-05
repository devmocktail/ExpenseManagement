using Microsoft.AspNetCore.Identity;

namespace ExpenseManagement.Domain.Entities;

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }

    public ApplicationRole(string roleName) : base(roleName) { }

    public string? Description { get; set; }
}

/// <summary>Role names used in <c>[Authorize(Roles = ...)]</c> and policy definitions.</summary>
public static class RoleNames
{
    public const string User = "User";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [User, Admin];
}
