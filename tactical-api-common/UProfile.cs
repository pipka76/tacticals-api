namespace tacticals_api_server.Domain;
public class UProfile
{
    public UProfile()
    {
    }

    public UProfile(string name, string email, string password)
    {
        Id = Guid.Empty;
        Name = name;
        Email = email;
        PasswordHash = password;
        ArmySaves = new Dictionary<string, ArmySetupSave>();
    }
    
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { set; get; }
    public string PasswordHash { set; get; }
    public IDictionary<string, ArmySetupSave> ArmySaves { set; get; }
}