namespace ExConnector.Models;

public class SageFolder
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public bool Active { get; set; } = false;
    
    // MAE (Comptabilité)
    public SageFileConfig? Mae { get; set; }
    
    // GCM (Gestion commerciale)
    public SageFileConfig? Gcm { get; set; }
    
    // IMO (Immobilisations)
    public SageFileConfig? Imo { get; set; }
    
    // MDP (Moyens de paiement)
    public SageFileConfig? Mdp { get; set; }
    
    // Infos SQL (lues depuis .mae)
    public string? CompanyServer { get; set; }
    public string? CompanyDatabase { get; set; }
}

public class SageFileConfig
{
    public string? Path { get; set; }
    public string? User { get; set; }
    public string? Password { get; set; }
}

public class SageFoldersConfig
{
    public List<SageFolder> Folders { get; set; } = new();
}
