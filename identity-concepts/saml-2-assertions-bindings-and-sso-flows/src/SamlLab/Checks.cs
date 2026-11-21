using System.Xml;

namespace SamlLab;

public static class Conditions
{
    // TODO-03: Check the validity window of the assertion's Conditions element with the injected
    // TimeProvider. Require a Conditions element with a NotOnOrAfter. NotBefore is inclusive, and
    // NotOnOrAfter is EXCLUSIVE (at that instant the assertion is already invalid). The clock skew
    // widens the window on both sides. Throw SamlValidationException outside it.
    public static void CheckTimeWindow(XmlElement assertion, TimeProvider time, TimeSpan skew)
    {
        throw new NotImplementedException();
    }

    // TODO-04: Check the audience. The assertion must have at least one AudienceRestriction, and EVERY
    // AudienceRestriction must list this SP's entityID among its Audience values (compare exactly).
    public static void CheckAudience(XmlElement assertion, string spEntityId)
    {
        throw new NotImplementedException();
    }
}

public static class SubjectChecks
{
    // TODO-05: Bind the assertion to THIS delivery. All of these throw SamlValidationException:
    //   - the Response's Destination must be present and equal to options.AcsUrl exactly (a library
    //     that only warns here lets a response be relayed to another SP);
    //   - the assertion needs a bearer SubjectConfirmation (SamlNs.BearerMethod) whose
    //     SubjectConfirmationData has Recipient equal to options.AcsUrl exactly, and a NotOnOrAfter that
    //     has not passed (use the skew);
    //   - InResponseTo: when the SubjectConfirmationData has one, it must equal the Response's own
    //     InResponseTo (if any) and pending.TryConsume must accept it (the request is ours and is used
    //     only once). When it has none, the response is IdP-initiated and is allowed only if
    //     options.AllowIdpInitiated is set.
    public static void CheckBearerConfirmation(
        XmlElement response, XmlElement assertion, SpOptions options, PendingRequests pending, TimeProvider time)
    {
        throw new NotImplementedException();
    }
}
