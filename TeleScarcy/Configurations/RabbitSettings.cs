namespace TeleScarcy.Configurations;

public class RabbitSettings
{
    public string? HostName { get; set; }
    public int Port { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? EventQueue { get; set; }
}
