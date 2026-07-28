using GNA.AuroraIntegration.Domain.Enums.Schema;

namespace GNA.AuroraIntegration.Domain.Entities.Schema;

/// <summary>
/// Definición de un objeto de usuario (UDO) a provisionar en SAP B1.
/// </summary>
public sealed class UserObjectDefinition
{
    public string Code { get; }
    public string Name { get; }
    public string TableName { get; }
    public bool CanClose { get; }
    public bool CanFind { get; }
    public bool MenuItem { get; }
    public string MenuCaption { get; }
    public int FatherMenuID { get; }
    public int Position { get; }
    public string MenuUID { get; }
    public bool CanCreateDefaultForm { get; }

    public UserObjectType ObjectType { get; }

    public UserObjectDefinition(
        string code,
        string name,
        string tableName,
        bool canClose,
        bool canFind,
        bool menuItem,
        string menuCaption,
        int fatherMenuID,
        int position,
        string menuUID,
        bool canCreateDefaultForm,
        UserObjectType objectType
        )
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("TableName no puede ser vacío.", nameof(tableName));

        Code = code;
        Name = name;
        TableName = tableName;
        CanClose = canClose;
        CanFind = canFind;
        MenuItem = menuItem;
        MenuCaption = menuCaption;
        FatherMenuID = fatherMenuID;
        Position = position;
        MenuUID = menuUID;
        CanCreateDefaultForm = canCreateDefaultForm;
        ObjectType = objectType;
    }
    
}