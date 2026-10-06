using Domain.Models.Identity.Management;
using System.ComponentModel.DataAnnotations;
namespace Randevona.Models.Management;
public sealed class ReviewApplicationModel
{
    [EnumDataType(typeof(ApplicationDecision))]
    public ApplicationDecision Decision { get; set; }
    [StringLength(1000, ErrorMessage = "The rejection reason must not exceed 1,000 characters.")]
    public string? Reason { get; set; }
}
