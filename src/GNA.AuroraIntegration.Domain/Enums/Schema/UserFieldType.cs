namespace GNA.AuroraIntegration.Domain.Enums.Schema;

/// <summary>
/// Tipo de dato de un campo de usuario (equivalente a BoFieldTypes del DI API).
/// </summary>
public enum UserFieldType
{
    Alpha = 0,
    Numeric = 1,
    Date = 2,
    Float = 3,
    Memo = 4,
    Time = 5
}