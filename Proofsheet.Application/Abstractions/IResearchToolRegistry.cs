// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Application.Models;

namespace Proofsheet.Application.Abstractions;

public interface IResearchToolRegistry
{
    Task<string> ExecuteAsync(
        AutonomousJobSession session,
        string toolName,
        string arguments,
        Func<AutonomousProgressEvent, Task> reportProgressAsync,
        CancellationToken cancellationToken);
}
