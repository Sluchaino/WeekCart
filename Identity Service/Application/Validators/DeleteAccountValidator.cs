using Application.DTO;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Validators
{
    public sealed class DeleteAccountValidator : AbstractValidator<DeleteAccountDto>
    {
        public DeleteAccountValidator()
        {
            RuleFor(x => x.PasswordConfirmation)
                .NotEmpty().WithMessage("Для удаления укажите пароль.");
        }
    }
}
