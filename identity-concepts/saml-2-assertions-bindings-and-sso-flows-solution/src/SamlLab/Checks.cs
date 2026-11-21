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
        var conditions = SamlXml.Child(assertion, SamlNs.Assertion, "Conditions")
            ?? throw new SamlValidationException("The assertion has no Conditions");
        var now = time.GetUtcNow();
        var notOnOrAfter = conditions.GetAttribute("NotOnOrAfter");
        if (notOnOrAfter.Length == 0) throw new SamlValidationException("Conditions has no NotOnOrAfter");
        if (now - skew >= SamlXml.ParseInstant(notOnOrAfter))
            throw new SamlValidationException("The assertion has expired (clocks skewed too much?)");
        var notBefore = conditions.GetAttribute("NotBefore");
        if (notBefore.Length > 0 && now + skew < SamlXml.ParseInstant(notBefore))
            throw new SamlValidationException("The assertion is not valid yet");
    }

    // TODO-04: Check the audience. The assertion must have at least one AudienceRestriction, and EVERY
    // AudienceRestriction must list this SP's entityID among its Audience values (compare exactly).
    public static void CheckAudience(XmlElement assertion, string spEntityId)
    {
        var conditions = SamlXml.Child(assertion, SamlNs.Assertion, "Conditions")
            ?? throw new SamlValidationException("The assertion has no Conditions");
        var restrictions = SamlXml.Children(conditions, SamlNs.Assertion, "AudienceRestriction").ToList();
        if (restrictions.Count == 0) throw new SamlValidationException("The assertion has no AudienceRestriction");
        foreach (var restriction in restrictions)
        {
            var audiences = SamlXml.Children(restriction, SamlNs.Assertion, "Audience").Select(a => a.InnerText.Trim());
            if (!audiences.Contains(spEntityId, StringComparer.Ordinal))
                throw new SamlValidationException($"Audience mismatch: this SP is {spEntityId}");
        }
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
        if (response.GetAttribute("Destination") != options.AcsUrl)
            throw new SamlValidationException($"Destination '{response.GetAttribute("Destination")}' is not this SP's ACS");

        var subject = SamlXml.Child(assertion, SamlNs.Assertion, "Subject")
            ?? throw new SamlValidationException("The assertion has no Subject");
        var data = SamlXml.Children(subject, SamlNs.Assertion, "SubjectConfirmation")
            .Where(c => c.GetAttribute("Method") == SamlNs.BearerMethod)
            .Select(c => SamlXml.Child(c, SamlNs.Assertion, "SubjectConfirmationData"))
            .FirstOrDefault(d => d is not null)
            ?? throw new SamlValidationException("No bearer SubjectConfirmationData");

        if (data.GetAttribute("Recipient") != options.AcsUrl)
            throw new SamlValidationException($"Recipient '{data.GetAttribute("Recipient")}' is not this SP's ACS");
        var notOnOrAfter = data.GetAttribute("NotOnOrAfter");
        if (notOnOrAfter.Length == 0 || time.GetUtcNow() - options.ClockSkew >= SamlXml.ParseInstant(notOnOrAfter))
            throw new SamlValidationException("The subject confirmation has expired");

        var inResponseTo = data.GetAttribute("InResponseTo");
        if (inResponseTo.Length == 0)
        {
            if (!options.AllowIdpInitiated) throw new SamlValidationException("InResponseTo is missing from the response");
            return;
        }
        var outer = response.GetAttribute("InResponseTo");
        if (outer.Length > 0 && outer != inResponseTo) throw new SamlValidationException("InResponseTo values disagree");
        if (!pending.TryConsume(inResponseTo)) throw new SamlValidationException("InResponseTo is not valid");
    }
}
