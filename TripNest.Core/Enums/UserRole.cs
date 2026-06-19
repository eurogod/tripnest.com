namespace TripNest.Core.Enums;

public enum UserRole
{
    Tenant,
    Landlord,
    Agent,
    Caretaker,
    Admin,
    // Appended last so existing persisted role integers are not renumbered.
    // A Guest browses/books without identity verification and is never gated.
    Guest
}
