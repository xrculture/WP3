namespace XRCultureHub.Models
{
    public class AuthorizationRequest
    {
        public string ProviderId { get; set; } = string.Empty;
        public string SessionToken { get; set; } = string.Empty;
        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
    }
}
