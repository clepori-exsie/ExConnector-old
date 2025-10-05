namespace ExConnector.Models;

public class AdminConfig
{
    public string? User { get; set; }
    public string? Password { get; set; }
    public bool LocalOnly { get; set; } = true;
}
