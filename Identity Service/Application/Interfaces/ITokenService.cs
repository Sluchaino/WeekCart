using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface ITokenService
    {
        Task<(string accessToken, string refreshToken)> IssueTokensAsync(ApplicationUser user, CancellationToken ct = default);
        Task<string> RotateRefreshAsync(string refreshToken, CancellationToken ct = default);
        Task RevokeUserRefreshTokensAsync(Guid userId, CancellationToken ct = default);
    }
}
