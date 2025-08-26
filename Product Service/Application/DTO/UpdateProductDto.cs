using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTO
{
    public record UpdateProductDto(
        string Title,
        string? Description,
        decimal Price,
        string Currency = "RUB",
        string? ImageUrl = null);
}
