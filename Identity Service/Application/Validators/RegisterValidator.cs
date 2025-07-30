using Application.DTO;
using FluentValidation;

namespace Application.Validators
{
    public sealed class RegisterValidator : AbstractValidator<RegisterDTO>
    {
        public RegisterValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("E-mail обязателен.")
                .EmailAddress().WithMessage("Некорректный e-mail.");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Пароль обязателен.")
                .MinimumLength(6).WithMessage("Мин. 6 символов.")
                .Matches("[A-Z]").WithMessage("Нужна хотя бы одна заглавная буква.")
                .Matches("[a-z]").WithMessage("Нужна хотя бы одна строчная буква.")
                .Matches("[0-9]").WithMessage("Нужна хотя бы одна цифра.");

            RuleFor(x => x.DisplayName)
                .NotEmpty().WithMessage("Имя профиля обязательно.")
                .MaximumLength(30).WithMessage("Не более 30 символов.");
        }
    }
}
