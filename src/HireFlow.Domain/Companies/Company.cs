using HireFlow.Domain.Common;

namespace HireFlow.Domain.Companies;

public sealed class Company
{
    private Company()
    {
    }

    public Company(string name, string? website = null, string? industry = null, string? location = null)
    {
        Name = Guard.NotBlank(name, nameof(name), 120);
        Website = Guard.Optional(website, nameof(website));
        Industry = Guard.Optional(industry, nameof(industry), 80);
        Location = Guard.Optional(location, nameof(location), 120);
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public string Name { get; private set; } = null!;

    public string? Website { get; private set; }

    public string? Industry { get; private set; }

    public string? Location { get; private set; }

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
}
