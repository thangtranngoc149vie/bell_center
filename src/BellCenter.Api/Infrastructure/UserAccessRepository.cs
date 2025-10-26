using System.Data;
using Dapper;

namespace BellCenter.Api.Infrastructure;

public sealed class UserAccessRepository(IDbConnection connection) : IUserAccessRepository
{
    private readonly IDbConnection _connection = connection;

    public Task<bool> UserHasNotificationAccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT EXISTS (SELECT 1 FROM users WHERE id = @uid)";
        var command = new CommandDefinition(sql, new { uid = userId }, cancellationToken: cancellationToken);
        return _connection.ExecuteScalarAsync<bool>(command);
    }
}
