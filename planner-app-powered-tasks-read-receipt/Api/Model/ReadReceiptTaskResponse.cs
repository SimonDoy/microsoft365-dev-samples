using System;

namespace i365.ReadReceipt.Tasks.Model
{
    public class ReadReceiptTaskResponse
    {
        public string? Id { get; set; }
        public string? Description { get; set; }
        public string? ExternalId { get; set; }
        public string? UserPrincipalName { get; set; }
        public string? UnderstandingLevel { get; set; }
        public bool? HasReadContent { get; set; }
        public int? PercentComplete { get; set; }
        public DateTimeOffset? ConfirmationDate { get; set; }

        public string? ContentTitle { get; set; }
        public string? ContentUrl { get; set; }
    }
}
