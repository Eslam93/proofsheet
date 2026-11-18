// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Domain.Enums;

namespace Proofsheet.Application.Abstractions;

public interface IResearchExecutionModeResolver
{
    ResearchExecutionMode Resolve();
}
