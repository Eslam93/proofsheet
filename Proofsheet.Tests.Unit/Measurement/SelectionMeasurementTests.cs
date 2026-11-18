// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Tests.Unit.Measurement;

public sealed class SelectionMeasurementTests
{
    private const string UpdateVariable = "PROOFSHEET_UPDATE_MEASUREMENT";

    // Runs both selection rules over the committed list and captured facts and compares the output with the committed
    // results file, so neither a change to a rule nor to the data can move the published numbers unnoticed. To
    // regenerate after a deliberate change, set PROOFSHEET_UPDATE_MEASUREMENT=1, rerun this test and review the diff.
    [Fact]
    public async Task Committed_Measurement_Matches_A_Fresh_Run()
    {
        var root = RepositoryRoot();
        var resultsPath = Path.Combine(root, "data", "measurement", "results.md");

        var fresh = await SelectionMeasurement.RunAsync(root, Path.Combine("data", "measurement", "companies.json"));
        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            await File.WriteAllTextAsync(resultsPath, fresh);
        }

        var committed = File.Exists(resultsPath) ? (await File.ReadAllTextAsync(resultsPath)).Replace("\r\n", "\n", StringComparison.Ordinal) : string.Empty;
        Assert.True(
            committed == fresh,
            $"data/measurement/results.md differs from a fresh run. Set {UpdateVariable}=1, rerun this test and review the diff.");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Proofsheet.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root (Proofsheet.slnx) was not found.");
    }
}
