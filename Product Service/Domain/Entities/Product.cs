using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class Product
    {
        private Product() { } // EF

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid SellerId { get; private set; }

        public string Title { get; private set; } = default!;
        public string? Description { get; private set; }
        public decimal Price { get; private set; }
        public string Currency { get; private set; } = "RUB";
        public string? ImageUrl { get; private set; }

        public bool IsArchived { get; private set; }
        public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; private set; }

        public static Product Create(
            Guid sellerId,
            string title,
            string? description,
            decimal price,
            string currency = "RUB",
            string? imageUrl = null)
            => new()
            {
                SellerId = sellerId,
                Title = title,
                Description = description,
                Price = price,
                Currency = currency,
                ImageUrl = imageUrl
            };

        public void Update(string title, string? description, decimal price, string currency, string? imageUrl)
        {
            if (IsArchived) throw new InvalidOperationException("Product is archived");
            Title = title;
            Description = description;
            Price = price;
            Currency = currency;
            ImageUrl = imageUrl;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        public void Archive()
        {
            if (IsArchived) return;
            IsArchived = true;
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}
