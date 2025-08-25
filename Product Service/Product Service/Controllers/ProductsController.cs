using Application.DTO;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using DomainProduct = Domain.Entities.Product;

namespace Product_Service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class ProductsController : ControllerBase
    {
        private readonly ProductDbContext _db;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(ProductDbContext db, ILogger<ProductsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // GET: /api/products?page=1&pageSize=20&q=mouse&mine=true
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> List(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? q = null,
            [FromQuery] bool mine = false,
            CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var qry = _db.Products.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
                qry = qry.Where(p => EF.Functions.ILike(p.Title, $"%{q}%"));

            if (mine && User.Identity?.IsAuthenticated == true)
            {
                var me = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (Guid.TryParse(me, out var myId))
                    qry = qry.Where(p => p.SellerId == myId);
            }

            var total = await qry.LongCountAsync(ct);

            var items = await qry
                .OrderByDescending(p => p.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductDto(
                    p.Id, p.Title, p.Description!, p.Price, p.Currency, p.ImageUrl))
                .ToListAsync(ct);

            return Ok(new { total, page, pageSize, items });
        }

        // GET: /api/products/{id}
        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
        {
            var p = await _db.Products.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            if (p is null) return NotFound();

            var dto = new ProductDto(p.Id, p.Title, p.Description!, p.Price, p.Currency, p.ImageUrl);
            return Ok(dto);
        }

        // POST: /api/products
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create([FromBody] CreateProductDto dto, CancellationToken ct = default)
        {
            var sellerIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(sellerIdStr, out var sellerId)) return Unauthorized();

            var entity = DomainProduct.Create(sellerId, dto.Title, dto.Description, dto.Price, dto.Currency, dto.ImageUrl);

            _db.Products.Add(entity);
            await _db.SaveChangesAsync(ct);

            var outDto = new ProductDto(entity.Id, entity.Title, entity.Description!, entity.Price, entity.Currency, entity.ImageUrl);
            return CreatedAtAction(nameof(Get), new { id = entity.Id }, outDto);
        }

        // PUT: /api/products/{id}
        [HttpPut("{id:guid}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductDto dto, CancellationToken ct = default)
        {
            var me = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(me, out var myId)) return Unauthorized();

            var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (p is null) return NotFound();

            if (p.SellerId != myId && !User.IsInRole("ADMIN"))
                return Forbid();

            p.Update(dto.Title, dto.Description, dto.Price, dto.Currency, dto.ImageUrl);
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }

        // DELETE: /api/products/{id}  (soft delete)
        [HttpDelete("{id:guid}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
        {
            var me = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(me, out var myId)) return Unauthorized();

            var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (p is null) return NotFound();

            if (p.SellerId != myId && !User.IsInRole("ADMIN"))
                return Forbid();

            p.Archive();
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }
    }
}
