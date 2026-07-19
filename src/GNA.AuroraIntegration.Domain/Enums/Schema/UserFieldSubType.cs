namespace GNA.AuroraIntegration.Domain.Enums;

/// <summary>
/// Subtipo de un campo de usuario (equivalente a BoFldSubTypes del DI API).
/// </summary>
public enum UserFieldSubType
{
    None = 0,
    Phone = 1,
    Percentage = 2,
    Password = 3,
    Sum = 4,
    Rate = 5,
    Link = 6,
    Time = 7,
    Address = 8
}