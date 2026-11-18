using Microsoft.AspNetCore.Mvc.RazorPages;
using Proofsheet.Application.Abstractions;
using Proofsheet.Domain.Enums;
using Proofsheet.Infrastructure.Providers;

namespace Proofsheet.Api.Pages;

public sealed class IndexModel : PageModel
{
    private readonly IResearchExecutionModeResolver _executionModeResolver;
    private readonly MockResearchSeedCatalog _sampleCatalog;

    public IndexModel(IResearchExecutionModeResolver executionModeResolver, MockResearchSeedCatalog sampleCatalog)
    {
        _executionModeResolver = executionModeResolver;
        _sampleCatalog = sampleCatalog;
    }

    /// <summary>True when this server has no live data configured, so every report uses fictional sample data.</summary>
    public bool UsesSampleData { get; private set; }

    public IReadOnlyCollection<string> SampleCompanies => _sampleCatalog.Identifiers;

    public string DefaultIdentifier => UsesSampleData ? MockResearchSeedCatalog.FeaturedIdentifier : string.Empty;

    public void OnGet()
    {
        UsesSampleData = _executionModeResolver.Resolve() == ResearchExecutionMode.Mock;
    }
}
