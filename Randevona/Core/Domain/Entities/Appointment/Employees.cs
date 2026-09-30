using Domain.Common.Attributes;
using Domain.Entities.BaseEntities;

namespace Domain.Entities.Appointment
{
    public class Employees : OrganizationBaseEntity
    {
        [EncryptedField]
        public string FirstName { get; set; } = string.Empty;
        [EncryptedField]
        public string LastName { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public bool AcceptsAppointments { get; set; } = true;
    }
}

