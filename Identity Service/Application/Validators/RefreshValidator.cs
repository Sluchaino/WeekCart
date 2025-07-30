using Application.DTO;
using FluentValidation;

namespace Application.Validators
{
    public sealed class RefreshValidator : AbstractValidator<RefreshDTO>
    {
        public RefreshValidator()
        {
            RuleFor(x => x.RefreshToken)
                .NotEmpty().WithMessage("Refresh-токен обязателен.");
        }
    }
}
