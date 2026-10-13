using FluentValidation;
using GorevTakip.Dtos;

namespace GorevTakip.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        // CascadeMode.Stop: bir alanda ilk kural tutmazsa gerisine bakma. Yoksa boş e-posta için
        // hem "boş olamaz" hem "geçerli adres girin" gibi birbirini tekrar eden iki mesaj çıkardı.
        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("E-posta boş olamaz.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi girin.")
            .MaximumLength(254).WithMessage("E-posta en fazla 254 karakter olabilir.");

        // Üst sınır: çok uzun bir şifre hash'lenirken sunucuyu gereksiz yormasın
        RuleFor(x => x.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Şifre boş olamaz.")
            .MinimumLength(8).WithMessage("Şifre en az 8 karakter olmalı.")
            .MaximumLength(128).WithMessage("Şifre en fazla 128 karakter olabilir.");
    }
}

// Girişte şifre kurallarını (uzunluk vb.) tekrar etmiyoruz: yanlış şifre ile "kural dışı şifre"
// aynı cevabı versin, saldırgana ipucu verilmesin. Sadece boş olmasın.
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta boş olamaz.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Şifre boş olamaz.");
    }
}
