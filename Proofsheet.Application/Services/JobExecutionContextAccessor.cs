// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Application.Abstractions;
using Proofsheet.Application.Models;

namespace Proofsheet.Application.Services;

public sealed class JobExecutionContextAccessor : IJobExecutionContextAccessor
{
    public JobExecutionContext? Current { get; set; }
}
