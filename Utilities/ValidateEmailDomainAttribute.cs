using System.ComponentModel.DataAnnotations;

namespace OEEMonitoringSystem.Utilities
{
    public class ValidateEmailDomainAttribute : ValidationAttribute
    {
        private readonly string _allowedDomain;
        public ValidateEmailDomainAttribute(string allowedDomain)
        {
            _allowedDomain = allowedDomain;
        }
        protected override ValidationResult IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return ValidationResult.Success; // Allow empty values
            }
            var email = value.ToString();
            if (email.EndsWith($"@{_allowedDomain}", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.Success;
            }
            return new ValidationResult($"Email domain must be '{_allowedDomain}'.");
        }

    }
}
