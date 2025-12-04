public class NotificationViewModel
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public string Icon { get; set; } = "";
    public string CssClass { get; set; } = "";
}