using System.ComponentModel.DataAnnotations;
using Jiten.Core.Data;

namespace Jiten.Api.Dtos.Requests;

public class CreateMediaRequestRequest
{
    [Required, MaxLength(300)]
    public required string Title { get; set; }

    [Required]
    public MediaType MediaType { get; set; }

    [MaxLength(500)]
    public string? ExternalUrl { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }
}

public class UpdateRequestStatusRequest
{
    [Required]
    public MediaRequestStatus Status { get; set; }

    [MaxLength(500)]
    public string? AdminNote { get; set; }

    public int? FulfilledDeckId { get; set; }
}

public class AddMediaRequestCommentRequest
{
    [MaxLength(500)]
    public string? Text { get; set; }
}

public class AdminReviewUploadRequest
{
    [Required]
    public bool AdminReviewed { get; set; }

    [MaxLength(500)]
    public string? AdminNote { get; set; }
}

public class AdminEditMediaRequestRequest
{
    [Required, MaxLength(300)]
    public required string Title { get; set; }

    [Required]
    public MediaType MediaType { get; set; }

    [MaxLength(500)]
    public string? ExternalUrl { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }
}
