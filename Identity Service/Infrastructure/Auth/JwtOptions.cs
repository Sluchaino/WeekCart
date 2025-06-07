using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Auth
{
    /// <summary>Настройки, считываемые из конфигурации секции "Jwt"</summary>
    public sealed class JwtOptions
    {
        public const string SectionName = "Jwt";

        /// <summary>Кому выпущен токен (Issuer)</summary>
        public string Issuer { get; init; } = default!;

        /// <summary>Кто может принимать токен (Audience)</summary>
        public string Audience { get; init; } = default!;

        /// <summary>Секретная строка (не короче 32 символов)</summary>
        public string Key { get; init; } = default!;

        /// <summary>Время жизни access-токена, минут</summary>
        public int AccessMinutes { get; init; } = 15;

        /// <summary>Время жизни refresh-токена, дней</summary>
        public int RefreshDays { get; init; } = 7;
    }
}
