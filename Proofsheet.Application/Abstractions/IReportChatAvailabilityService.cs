// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Application.Abstractions;

public interface IReportChatAvailabilityService
{
    bool IsConfigured { get; }

    string UnavailableReason { get; }
}
