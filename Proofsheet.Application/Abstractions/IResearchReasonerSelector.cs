// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Application.Models;
using Proofsheet.Domain.Enums;

namespace Proofsheet.Application.Abstractions;

public interface IResearchReasonerSelector
{
    ReasonerSelection Select(ResearchExecutionMode executionMode);
}
