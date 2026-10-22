using System.Net.Mail;
using HireFlow.Domain.Common;

namespace HireFlow.Domain.Candidates;

public sealed class Candidate
{
    private Candidate()
    {
    }

    public Candidate(
        string fullName,
        string email,
        string? headline = null,
        int yearsOfExperience = 0,
        IEnumerable<string>? skills = null)
    {
        FullName = Guard.NotBlank(fullName, nameof(fullName), 120);
        Email = NormalizeEmail(email);
        Headline = Guard.Optional(headline, nameof(headline), 160);

        if (yearsOfExperience is < 0 or > 60)
        {
            throw new DomainValidationException(nameof(yearsOfExperience), "Years of experience must be between 0 and 60.");
        }

        YearsOfExperience = yearsOfExperience;
        Skills = Guard.Tags(skills);
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public string FullName { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public string? Headline { get; private set; }

    public int YearsOfExperience { get; private set; }

    public List<string> Skills { get; private set; } = [];

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private static string NormalizeEmail(string email)
    {
        var value = Guard.NotBlank(email, nameof(email), 254);
        if (!MailAddress.TryCreate(value, out var parsed) || parsed.Address != value)
        {
            throw new DomainValidationException(nameof(email), $"'{value}' is not a valid email address.");
        }

        return value.ToLowerInvariant();
    }
}

public sealed class DuplicateCandidateException(string email)
    : DomainException($"A candidate with email '{email}' is already registered.")
{
    public string Email { get; } = email;
}
