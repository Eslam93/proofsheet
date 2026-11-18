// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Application.Abstractions;
using Proofsheet.Application.Models;

namespace Proofsheet.Application.Services;

public sealed class ResearchJobQueryService : IResearchJobQueryService
{
    private readonly IResearchJobStore _jobStore;

    public ResearchJobQueryService(IResearchJobStore jobStore)
    {
        _jobStore = jobStore;
    }

    public ResearchJobRecord? Get(Guid jobId) => _jobStore.Get(jobId);
}
