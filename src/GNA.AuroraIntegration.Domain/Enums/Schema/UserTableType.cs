namespace GNA.AuroraIntegration.Domain.Enums.Schema;

/// <summary>
/// Tipo de tabla de usuario en SAP B1 (equivalente a BoUTBTableType del DI API,
/// expuesto por Service Layer como string en UserTablesMD).
/// </summary>
public enum UserTableType
{
    NoObject = 0,
    MasterData = 1,
    MasterDataLines = 2,
    Document = 3,
    DocumentLines = 4
}