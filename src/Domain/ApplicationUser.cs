using Microsoft.AspNetCore.Identity;

namespace Domain;

public class ApplicationUser : IdentityUser<long>
{
    private long? _aiSettingId;

    private AISetting? _aiSetting;

    public Guid ExternalId { get; set; }
    
    public string FirstName { get; set; } = "";

    public string LastName { get; set; } = "";

    /// <summary>
    /// 1-1 Relationship
    /// </summary>
    public long? AISettingId
    {
        get => _aiSettingId;
        protected set => _aiSettingId = value;
    }

    public AISetting? AISetting
    {
        get => _aiSetting;
        set
        {
            _aiSetting = value;
            _aiSettingId = value?.Id;
        }
    }
}
