namespace NickeltownFinance.Core.Enums;

public enum PaymentMethod
{
    Cash,
    EFT,
    Card,
    Cheque,
    Other,
    BankTransfer
}

public static class PaymentMethodExtensions
{
    public static string ToDisplayName(this PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Cash",
        PaymentMethod.EFT => "EFT",
        PaymentMethod.Card => "Card",
        PaymentMethod.Cheque => "Cheque",
        PaymentMethod.Other => "Other",
        PaymentMethod.BankTransfer => "Bank Transfer",
        _ => method.ToString()
    };
}
