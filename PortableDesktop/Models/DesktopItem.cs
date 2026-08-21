namespace PortableDesktop.Models;

public class DesktopItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public int IconIndex { get; set; }
}
