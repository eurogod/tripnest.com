using PhoneNumbers;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Offline phone-number format validation using libphonenumber. Numbers are parsed against a
/// default region (Ghana by default, configurable via Phone:DefaultRegion) so local formats
/// like 0244123456 and international +233244123456 both validate.
/// </summary>
public class PhoneNumberValidator : IPhoneNumberValidator
{
    private readonly PhoneNumberUtil _util = PhoneNumberUtil.GetInstance();
    private readonly string _defaultRegion;

    public PhoneNumberValidator(IConfiguration configuration)
    {
        _defaultRegion = configuration["Phone:DefaultRegion"] ?? "GH";
    }

    public bool IsValid(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return false;
        try
        {
            return _util.IsValidNumber(_util.Parse(phone, _defaultRegion));
        }
        catch (NumberParseException)
        {
            return false;
        }
    }

    public string? Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;
        try
        {
            var parsed = _util.Parse(phone, _defaultRegion);
            return _util.IsValidNumber(parsed) ? _util.Format(parsed, PhoneNumberFormat.E164) : null;
        }
        catch (NumberParseException)
        {
            return null;
        }
    }
}
