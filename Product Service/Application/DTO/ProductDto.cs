using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTO
{
    public record ProductDto(Guid Id, string Title, string? Description, decimal Price, string Currency, string? ImageUrl);
}
