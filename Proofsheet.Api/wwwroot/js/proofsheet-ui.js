// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

(function () {
    "use strict";

    const defaultSuggestions = [
        "Why is this signal bullish or neutral?",
        "What are the main risks in this report?",
        "Which sources matter most here?"
    ];
    const appBaseHref = getAppBaseHref();

    document.addEventListener("DOMContentLoaded", () => {
        initHomePage();
        initReportPage();
    });

    function initHomePage() {
        const form = document.getElementById("research-form");
        if (!form) {
            return;
        }

        const identifierInput = document.getElementById("identifier");
        const researchModeInput = document.getElementById("research-mode");
        const resultsContainer = document.getElementById("ticker-results");
        const submitButton = document.getElementById("generate-report");
        const messageElement = document.getElementById("form-message");
        let searchHandle = 0;

        identifierInput.addEventListener("input", () => {
            window.clearTimeout(searchHandle);
            const query = (identifierInput.value || "").trim();
            if (query.length < 2) {
                hideTickerResults(resultsContainer);
                return;
            }

            searchHandle = window.setTimeout(async () => {
                try {
                    const response = await fetch(resolveAppUrl(`api/v1/tickers/search?q=${encodeURIComponent(query)}&limit=8`), {
                        headers: { "Accept": "application/json" }
                    });

                    if (!response.ok) {
                        hideTickerResults(resultsContainer);
                        return;
                    }

                    const payload = await response.json();
                    renderTickerResults(resultsContainer, payload.items || [], (selected) => {
                        identifierInput.value = selected.symbol;
                        hideTickerResults(resultsContainer);
                    });
                } catch {
                    hideTickerResults(resultsContainer);
                }
            }, 180);
        });

        document.addEventListener("click", (event) => {
            if (!resultsContainer.contains(event.target) && event.target !== identifierInput) {
                hideTickerResults(resultsContainer);
            }
        });

        form.addEventListener("submit", async (event) => {
            event.preventDefault();

            const identifier = (identifierInput.value || "").trim();
            if (!identifier) {
                messageElement.textContent = "Choose a stock first.";
                return;
            }

            submitButton.disabled = true;
            messageElement.textContent = "Starting report...";

            try {
                const response = await fetch(resolveAppUrl("api/v1/research-jobs"), {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ identifier, researchMode: researchModeInput ? researchModeInput.value : "Standard" })
                });

                if (!response.ok) {
                    const problem = await tryReadJson(response);
                    messageElement.textContent = problem?.detail || "Failed to start the report.";
                    return;
                }

                const payload = await response.json();
                window.location.assign(resolveAppUrl(`reports/${payload.jobId}`));
            } catch (error) {
                messageElement.textContent = error instanceof Error ? error.message : "Failed to start the report.";
            } finally {
                submitButton.disabled = false;
            }
        });
    }

    function initReportPage() {
        const page = document.getElementById("report-page");
        if (!page) {
            return;
        }

        const jobId = page.dataset.jobId;
        if (!jobId) {
            return;
        }

        const elements = {
            company: document.getElementById("report-company"),
            statusBadge: document.getElementById("status-badge"),
            progressToggle: document.getElementById("progress-toggle"),
            progressPanel: document.getElementById("progress-panel"),
            progressClose: document.getElementById("progress-close"),
            progressBackdrop: document.getElementById("progress-backdrop"),
            warningsList: document.getElementById("warnings-list"),
            traceList: document.getElementById("progress-trace"),
            failurePanel: document.getElementById("failure-panel"),
            failureMessage: document.getElementById("failure-message"),
            reportContent: document.getElementById("report-content"),
            reportPlaceholder: document.getElementById("report-placeholder"),
            progressCaption: document.getElementById("progress-caption"),
            progressStepLabel: document.getElementById("progress-step-label"),
            progressPercent: document.getElementById("progress-percent"),
            progressFill: document.getElementById("progress-fill"),
            signal: document.getElementById("report-signal"),
            confidence: document.getElementById("report-confidence"),
            signalRationale: document.getElementById("report-signal-rationale"),
            confidenceRationale: document.getElementById("report-confidence-rationale"),
            confidenceBounds: document.getElementById("report-confidence-bounds"),
            synthesis: document.getElementById("report-synthesis"),
            signalCard: document.getElementById("signal-card"),
            confidenceCard: document.getElementById("confidence-card"),
            thesis: document.getElementById("report-thesis"),
            insights: document.getElementById("report-insights"),
            sources: document.getElementById("report-sources"),
            providerUsage: document.getElementById("report-provider-usage"),
            dataLabel: document.getElementById("report-data-label"),
            figuresSection: document.getElementById("report-figures-section"),
            figuresNote: document.getElementById("report-figures-note"),
            figures: document.getElementById("report-figures"),
            chatTranscript: document.getElementById("chat-transcript"),
            chatEmptyState: document.getElementById("chat-empty-state"),
            chatWelcome: document.getElementById("chat-welcome"),
            chatSuggestions: document.getElementById("chat-suggestions"),
            chatForm: document.getElementById("chat-form"),
            chatInput: document.getElementById("chat-message"),
            chatSend: document.getElementById("chat-send"),
            chatStatus: document.getElementById("chat-status")
        };

        const state = {
            jobId,
            pollHandle: null,
            completedReport: null,
            chatEnabled: page.dataset.chatEnabled === "true",
            chatUnavailableReason: page.dataset.chatUnavailableReason || "Report chat is not configured.",
            chatReady: false,
            chatReadyPromise: null,
            threadId: "",
            lastSequence: 0,
            connection: null,
            assistantBubble: null,
            progressOpen: false,
            welcomeTimer: null,
            hasUserMessage: false
        };

        elements.progressToggle.addEventListener("click", () => {
            state.progressOpen = !state.progressOpen;
            syncProgressPanel(state, elements);
        });

        elements.progressClose.addEventListener("click", () => {
            state.progressOpen = false;
            syncProgressPanel(state, elements);
        });

        elements.progressBackdrop.addEventListener("click", () => {
            state.progressOpen = false;
            syncProgressPanel(state, elements);
        });

        document.addEventListener("keydown", (event) => {
            if (event.key === "Escape" && state.progressOpen) {
                state.progressOpen = false;
                syncProgressPanel(state, elements);
            }
        });

        elements.chatForm.addEventListener("submit", async (event) => {
            event.preventDefault();
            await sendChatMessage(state, elements, (elements.chatInput.value || "").trim());
        });

        renderSuggestionButtons(state, elements);
        setChatComposerEnabled(elements, false);
        elements.chatStatus.textContent = state.chatEnabled
            ? "Finish the report to unlock chat."
            : state.chatUnavailableReason;
        startPolling(state, elements);
    }

    function startPolling(state, elements) {
        const tick = async () => {
            const keepGoing = await pollStatus(state, elements);
            if (!keepGoing && state.pollHandle) {
                window.clearInterval(state.pollHandle);
                state.pollHandle = null;
            }
        };

        tick();
        state.pollHandle = window.setInterval(tick, 2000);
    }

    async function pollStatus(state, elements) {
        try {
            const response = await fetch(resolveAppUrl(`api/v1/research-jobs/${state.jobId}`), {
                headers: { "Accept": "application/json" }
            });

            if (!response.ok) {
                const problem = await tryReadJson(response);
                elements.failurePanel.hidden = false;
                elements.failureMessage.textContent = problem?.detail || "Failed to load job status.";
                return false;
            }

            const status = await response.json();
            renderStatus(status, elements);

            if (status.status === "Completed") {
                await loadCompletedReport(state, elements);
                return false;
            }

            if (status.status === "Failed") {
                elements.reportPlaceholder.hidden = true;
                elements.reportContent.hidden = true;
                elements.failurePanel.hidden = false;
                elements.failureMessage.textContent = status.error?.message || "The research job failed.";
                state.progressOpen = true;
                syncProgressPanel(state, elements);
                return false;
            }

            return true;
        } catch (error) {
            elements.failurePanel.hidden = false;
            elements.failureMessage.textContent = error instanceof Error ? error.message : "Failed to load job status.";
            return false;
        }
    }

    async function loadCompletedReport(state, elements) {
        if (state.completedReport) {
            return;
        }

        const response = await fetch(resolveAppUrl(`api/v1/research-jobs/${state.jobId}/result`), {
            headers: { "Accept": "application/json" }
        });

        if (!response.ok) {
            const problem = await tryReadJson(response);
            throw new Error(problem?.detail || "Failed to load the completed report.");
        }

        const result = await response.json();
        state.completedReport = result;
        renderCompletedReport(result, elements);
        renderSuggestionButtons(state, elements);

        if (!state.chatEnabled) {
            elements.chatStatus.textContent = state.chatUnavailableReason;
            setChatComposerEnabled(elements, false);
            return;
        }

        setChatComposerEnabled(elements, false);
        elements.chatStatus.textContent = "Preparing chat...";
        void prepareChatThread(state, elements);

        state.welcomeTimer = window.setTimeout(() => {
            if (!state.hasUserMessage) {
                elements.chatWelcome.textContent = `Hi, ask me why ${result.company} is ${humanizeToken(result.signal).toLowerCase()} or what the main risks are.`;
            }
        }, 1400);
    }

    function renderStatus(status, elements) {
        elements.company.textContent = humanizeIdentifier(status.identifier || "Preparing report");
        elements.statusBadge.textContent = humanizeToken(status.status);
        elements.statusBadge.className = `status-badge status-badge--${String(status.status || "").toLowerCase()}`;
        renderInlineProgress(status, elements);

        renderWarnings(status.warnings || [], elements.warningsList);
        renderTrace(status.trace || [], elements.traceList);

        if (status.status !== "Failed") {
            elements.failurePanel.hidden = true;
            elements.failureMessage.textContent = "";
        }
    }

    function renderCompletedReport(result, elements) {
        elements.company.textContent = result.company;
        elements.statusBadge.textContent = "Completed";
        elements.statusBadge.className = "status-badge status-badge--completed";
        renderInlineProgress({ status: "Completed", currentStep: "Completion", trace: result.trace || [] }, elements);
        elements.signal.textContent = humanizeToken(result.signal);
        elements.confidence.textContent = String(result.confidence);
        elements.signalCard.className = `metric-card metric-card--signal metric-card--${String(result.signal || "").toLowerCase()}`;
        elements.confidenceCard.className = `metric-card metric-card--confidence metric-card--${confidenceBand(result.confidence)}`;
        elements.thesis.textContent = result.thesis;
        renderDataLabel(result, elements.dataLabel);
        renderFigures(result, elements);
        elements.signalRationale.textContent = result.signalDetails?.rationale || "Signal rationale was not provided.";
        elements.confidenceRationale.textContent = result.confidenceDetails?.rationale || "Confidence rationale was not provided.";
        elements.confidenceBounds.textContent = result.confidenceDetails
            ? `Model requested ${result.confidenceDetails.boundedFrom}; final bounded confidence is ${result.confidenceDetails.boundedTo}.`
            : "";
        elements.synthesis.textContent = result.synthesisDetails
            ? formatSynthesisSummary(result.synthesisDetails, result.orchestrationDetails || null)
            : "Synthesis details were not provided.";
        elements.reportPlaceholder.hidden = true;
        elements.failurePanel.hidden = true;
        elements.reportContent.hidden = false;

        renderWarnings(result.warnings || [], elements.warningsList);
        renderTrace(result.trace || [], elements.traceList);

        elements.insights.innerHTML = "";
        (result.insights || []).forEach((insight) => {
            const item = document.createElement("li");
            item.textContent = insight;
            elements.insights.appendChild(item);
        });

        elements.sources.innerHTML = "";
        (result.sourceDetails || []).forEach((source) => {
            const item = document.createElement("li");
            item.innerHTML = [
                `<strong>${escapeHtml(source.title || source.sourceId)}</strong>`,
                `<span>${escapeHtml(source.sourceId)}</span>`,
                `<small>${escapeHtml(humanizeToken(source.type))} / ${escapeHtml(sourceProviderLabel(source.providerName || "Unknown provider"))} / ${escapeHtml(humanizeToken(source.providerOrigin || "Unknown"))}</small>`
            ].join("");
            elements.sources.appendChild(item);
        });

        elements.providerUsage.innerHTML = "";
        (result.providerUsage || []).forEach((usage) => {
            const item = document.createElement("li");
            const available = usage.status === "Success";
            const fallbackNote = !available
                ? "Not available for this report."
                : usage.fallbackOccurred && usage.fallbackReason
                    ? `Fallback: ${usage.fallbackReason}`
                    : usage.fallbackOccurred ? "Fallback occurred." : "Primary path succeeded.";
            const source = available
                ? `${humanizeToken(usage.origin)} via ${sourceProviderLabel(usage.providerName || "Unknown provider")}`
                : `Not available from ${sourceProviderLabel(usage.providerName || "Unknown provider")}`;

            item.innerHTML = [
                `<strong>${escapeHtml(humanizeToken(usage.domain))}</strong>`,
                `<span>${escapeHtml(source)}</span>`,
                `<small>${escapeHtml(fallbackNote)}</small>`
            ].join("");
            elements.providerUsage.appendChild(item);
        });
    }

    // Says what the report's data is, from the origin each domain actually came from, so fictional sample data can
    // never pass for a real company's figures.
    function renderDataLabel(result, label) {
        const usages = (result.providerUsage || []).filter((usage) => usage.status === "Success");
        const sample = usages.filter((usage) => usage.origin === "Mock");
        const recorded = usages.filter((usage) => usage.origin === "Recorded");
        const recordedOn = document.getElementById("report-page")?.dataset.recordedOn;
        let text;
        let kind;
        if (usages.length > 0 && recorded.length === usages.length) {
            kind = "recorded";
            text = `Recorded SEC data: every figure is what the company had filed with the SEC by ${result.asOf || recordedOn || "the recording date"}, replayed from a recording${recordedOn ? ` made on ${recordedOn}` : ""}, and a template wrote the text. Market data, news and chat are off in this demo.`;
        } else if (usages.length > 0 && sample.length === usages.length) {
            kind = "sample";
            text = "Fictional sample data. This company and every figure, headline and filing excerpt in this report are invented to show how Proofsheet works.";
        } else if (sample.length > 0) {
            kind = "sample";
            text = `Partly fictional sample data: ${sample.map((usage) => humanizeToken(usage.domain).toLowerCase()).join(", ")} came from sample data; the rest is live data retrieved ${formatDate(result.generatedAtUtc)}.`;
        } else {
            kind = "live";
            const providers = [...new Set(usages.map((usage) => sourceProviderLabel(usage.providerName)))];
            text = `Live data from ${providers.join(", ")}, retrieved ${formatDate(result.generatedAtUtc)}.`;
        }

        label.textContent = `${text} Not investment advice.`;
        label.className = `data-label data-label--${kind}`;
    }

    // Every figure links to the EDGAR filing it was reported in, so a reader can check it against the filing itself.
    function renderFigures(result, elements) {
        const figures = result.figures || [];
        elements.figuresSection.hidden = figures.length === 0;
        if (figures.length === 0) {
            return;
        }

        const asOf = result.asOf ? ` on or before ${result.asOf}` : "";
        elements.figuresNote.textContent = `Each figure is a fact the company filed with the SEC${asOf}, in US dollars as the filing reports it. The link opens that filing on EDGAR.`;
        const body = elements.figures.querySelector("tbody");
        body.innerHTML = "";
        figures.forEach((figure) => {
            const row = document.createElement("tr");
            const period = figure.periodStart
                ? `${isoDate(figure.periodStart)} to ${isoDate(figure.periodEnd)}`
                : `at ${isoDate(figure.periodEnd)}`;
            const filingLabel = `${figure.form || "Filing"} filed ${isoDate(figure.filedDate)}`;
            const filing = isEdgarUrl(figure.filingUrl)
                ? `<a href="${escapeHtml(figure.filingUrl)}" target="_blank" rel="noopener noreferrer">${escapeHtml(filingLabel)}</a>`
                : escapeHtml(filingLabel);
            row.innerHTML = [
                `<td>${escapeHtml(figure.label)}</td>`,
                `<td class="figure-table__value">${escapeHtml(formatUsdMillions(figure.value))}</td>`,
                `<td>${escapeHtml(period)}</td>`,
                `<td><code>${escapeHtml(figure.concept)}</code></td>`,
                `<td>${filing}<small>${escapeHtml(figure.accessionNumber || "")}</small></td>`
            ].join("");
            body.appendChild(row);
        });
    }

    function isEdgarUrl(value) {
        return typeof value === "string" && value.startsWith("https://www.sec.gov/Archives/edgar/data/");
    }

    function formatUsdMillions(value) {
        const millions = Number(value) / 1e6;
        return `$${millions.toLocaleString("en-US", { maximumFractionDigits: 1 })}M`;
    }

    function isoDate(value) {
        return String(value || "").slice(0, 10);
    }

    function renderWarnings(warnings, warningsList) {
        warningsList.innerHTML = "";
        if (!warnings.length) {
            return;
        }

        warnings.forEach((warning) => {
            const item = document.createElement("li");
            item.textContent = warning;
            warningsList.appendChild(item);
        });
    }

    function renderTrace(trace, traceList) {
        traceList.innerHTML = "";

        trace.forEach((entry) => {
            const item = document.createElement("li");
            item.innerHTML = [
                `<div class="trace-list__meta"><strong>${escapeHtml(humanizeToken(entry.step))}</strong><span>${escapeHtml(humanizeToken(entry.status))}</span></div>`,
                `<p>${escapeHtml(entry.summary)}</p>`,
                `<small>${formatDate(entry.timestampUtc)}</small>`
            ].join("");
            traceList.appendChild(item);
        });
    }

    function renderInlineProgress(status, elements) {
        if (!elements.progressFill || !elements.progressPercent || !elements.progressStepLabel) {
            return;
        }

        const progress = calculateProgress(status);
        elements.progressFill.style.width = `${progress.percent}%`;
        elements.progressPercent.textContent = `${progress.percent}%`;
        elements.progressStepLabel.textContent = progress.label;

        if (elements.progressCaption) {
            elements.progressCaption.textContent = progress.caption;
        }
    }

    function calculateProgress(status) {
        const currentStep = String(status.currentStep || status.status || "Queued");
        const trace = Array.isArray(status.trace) ? status.trace : [];
        const activeAgent = humanizeToken(status.activeAgent || "");
        const agentPhase = humanizeToken(status.agentPhase || "");

        if (status.status === "Completed") {
            return {
                percent: 100,
                label: "Completed",
                caption: "The report is ready."
            };
        }

        if (status.status === "Failed") {
            const failedStep = currentStep === "Failed" ? "Failed" : humanizeToken(currentStep);
            return {
                percent: Math.max(12, deriveProgressPercent(currentStep, trace)),
                label: failedStep,
                caption: "The report stopped before completion."
            };
        }

        return {
            percent: deriveProgressPercent(currentStep, trace),
            label: humanizeToken(currentStep),
            caption: currentStep === "Queued"
                ? "Your job is waiting for a worker."
                : buildProgressCaption(currentStep, activeAgent, agentPhase)
        };
    }

    function deriveProgressPercent(currentStep, trace) {
        const normalizedStep = String(currentStep || "Queued");
        const baseProgressByStep = {
            Queued: 8,
            IdentifierResolution: 16,
            ResearchPlanCreation: 26,
            ParallelSourceCollection: 34,
            QualityAssessment: 72,
            AdaptiveBranching: 82,
            Synthesis: 90,
            FinalReflection: 96,
            CoordinatorPlanning: 24,
            CoordinatorToolExecution: 44,
            EvidenceGapReview: 62,
            ContradictionReview: 70,
            SpecialistHandoff: 78,
            SpecialistDraft: 86,
            SpecialistCritique: 92,
            OutputValidation: 97,
            Completion: 100,
            Failed: 100
        };

        if (normalizedStep === "ParallelSourceCollection") {
            const collectedDomains = trace.filter((entry) =>
                entry.step === "ParallelSourceCollection" &&
                typeof entry.summary === "string" &&
                /was collected from/i.test(entry.summary)).length;
            const boundedDomains = Math.max(0, Math.min(collectedDomains, 5));
            return 34 + Math.round((boundedDomains / 5) * 32);
        }

        return baseProgressByStep[normalizedStep] ?? 12;
    }

    function buildProgressCaption(currentStep, activeAgent, agentPhase) {
        const step = humanizeToken(currentStep).toLowerCase();
        const parts = [`Currently on ${step}.`];

        if (activeAgent) {
            const agentText = agentPhase
                ? `${activeAgent.toLowerCase()} / ${agentPhase.toLowerCase()}`
                : activeAgent.toLowerCase();
            parts.push(`Active agent: ${agentText}.`);
        }

        return parts.join(" ");
    }

    function renderTickerResults(container, items, onSelect) {
        container.innerHTML = "";
        if (!items.length) {
            hideTickerResults(container);
            return;
        }

        items.forEach((item) => {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "ticker-result";
            button.innerHTML = `<strong>${escapeHtml(item.symbol)}</strong><span>${escapeHtml(item.companyName)}</span><small>${escapeHtml(item.exchange || "US")} / ${escapeHtml(item.cik || "")}</small>`;
            button.addEventListener("click", () => onSelect(item));
            container.appendChild(button);
        });

        container.hidden = false;
    }

    function hideTickerResults(container) {
        container.innerHTML = "";
        container.hidden = true;
    }

    function renderSuggestionButtons(state, elements) {
        elements.chatSuggestions.innerHTML = "";

        defaultSuggestions.forEach((text) => {
            const button = document.createElement("button");
            button.className = "chat-suggestion";
            button.type = "button";
            button.textContent = text;
            button.addEventListener("click", async () => {
                await sendChatMessage(state, elements, text);
            });

            elements.chatSuggestions.appendChild(button);
        });

        syncSuggestionButtons(elements, state.chatReady);
    }

    async function sendChatMessage(state, elements, message) {
        if (!message) {
            elements.chatStatus.textContent = "Enter a question about the report.";
            return;
        }

        if (!state.completedReport) {
            elements.chatStatus.textContent = "Wait for the report to finish first.";
            return;
        }

        if (!state.chatEnabled) {
            elements.chatStatus.textContent = state.chatUnavailableReason;
            return;
        }

        if (!state.chatReady) {
            elements.chatStatus.textContent = "Chat is still preparing.";
            return;
        }

        try {
            state.hasUserMessage = true;
            if (state.welcomeTimer) {
                window.clearTimeout(state.welcomeTimer);
                state.welcomeTimer = null;
            }

            hideChatEmptyState(elements);
            appendChatBubble(elements.chatTranscript, "user", message);
            elements.chatInput.value = "";
            elements.chatStatus.textContent = "Streaming answer...";
            elements.chatSend.disabled = true;
            state.assistantBubble = appendChatBubble(elements.chatTranscript, "assistant", "");
            await state.connection.invoke("SendMessage", state.threadId, message);
        } catch (error) {
            elements.chatStatus.textContent = extractHubError(error);
            elements.chatSend.disabled = false;
            state.assistantBubble = null;
        }
    }

    async function prepareChatThread(state, elements) {
        try {
            await ensureChatReady(state, elements);
            state.chatReady = true;
            setChatComposerEnabled(elements, true);
            elements.chatStatus.textContent = "Chat is ready.";
        } catch (error) {
            state.chatReady = false;
            setChatComposerEnabled(elements, false);
            elements.chatStatus.textContent = extractHubError(error);
        }
    }

    async function ensureChatReady(state, elements) {
        if (state.connection && state.threadId) {
            return;
        }

        if (state.chatReadyPromise) {
            await state.chatReadyPromise;
            return;
        }

        state.chatReadyPromise = (async () => {
            if (!window.signalR) {
                throw new Error("SignalR client was not loaded.");
            }

            state.connection = new window.signalR.HubConnectionBuilder()
                .withUrl(resolveAppUrl("hubs/report-chat"))
                .withAutomaticReconnect()
                .build();

            state.connection.on("ReceiveStreamEvent", (event) => {
                state.lastSequence = Math.max(state.lastSequence, event.sequence || 0);
                handleStreamEvent(state, elements, event);
            });

            state.connection.on("ReceiveChatMessage", (message) => {
                renderChatMessage(state, elements, message);
                state.assistantBubble = null;
                elements.chatSend.disabled = false;
                elements.chatStatus.textContent = "Answer complete.";
            });

            state.connection.onreconnected(async () => {
                if (!state.threadId) {
                    return;
                }

                const resumed = await state.connection.invoke("ResumeStream", state.threadId, state.lastSequence);
                (resumed.events || []).forEach((event) => {
                    state.lastSequence = Math.max(state.lastSequence, event.sequence || 0);
                    handleStreamEvent(state, elements, event);
                });
            });

            await state.connection.start();
            const created = await state.connection.invoke("CreateThread", state.jobId);
            state.threadId = created.threadId;
        })();

        try {
            await state.chatReadyPromise;
        } catch (error) {
            state.threadId = "";
            state.connection = null;
            throw error;
        } finally {
            state.chatReadyPromise = null;
        }
    }

    function handleStreamEvent(state, elements, event) {
        switch (event.kind) {
            case "start":
                elements.chatStatus.textContent = "Preparing answer...";
                break;
            case "delta":
                if (!state.assistantBubble) {
                    state.assistantBubble = appendChatBubble(elements.chatTranscript, "assistant", "");
                }

                state.assistantBubble.querySelector(".chat-bubble__body").textContent += event.delta || "";
                scrollTranscript(elements.chatTranscript);
                break;
            case "tool":
                elements.chatStatus.textContent = "Checking report evidence...";
                break;
            case "done":
                elements.chatStatus.textContent = "Finishing answer...";
                break;
            case "canceled":
                elements.chatStatus.textContent = event.error || "Stream stopped.";
                elements.chatSend.disabled = false;
                state.assistantBubble = null;
                break;
            case "error":
                elements.chatStatus.textContent = event.error || "Chat failed.";
                elements.chatSend.disabled = false;
                state.assistantBubble = null;
                break;
            default:
                break;
        }
    }

    function renderChatMessage(state, elements, message) {
        let bubble = state.assistantBubble;

        if (!bubble && message.responseId) {
            bubble = elements.chatTranscript.querySelector(`[data-response-id="${CSS.escape(message.responseId)}"]`);
        }

        if (!bubble) {
            bubble = appendChatBubble(elements.chatTranscript, "assistant", "");
        }

        if (message.responseId) {
            bubble.dataset.responseId = message.responseId;
        }

        bubble.querySelector(".chat-bubble__body").innerHTML = renderMarkdownToHtml(message.answer || "");
        bubble.querySelector(".chat-bubble__meta").innerHTML = renderCitations(message.citedSourceIds || [], message.reportContext || null);
        scrollTranscript(elements.chatTranscript);
    }

    function appendChatBubble(container, role, text) {
        const wrapper = document.createElement("article");
        wrapper.className = `chat-bubble chat-bubble--${role}`;

        const body = document.createElement("div");
        body.className = "chat-bubble__body";
        body.textContent = text || "";

        const meta = document.createElement("div");
        meta.className = "chat-bubble__meta";

        wrapper.appendChild(body);
        wrapper.appendChild(meta);
        container.appendChild(wrapper);
        scrollTranscript(container);
        return wrapper;
    }

    function hideChatEmptyState(elements) {
        elements.chatEmptyState.classList.add("is-hidden");
    }

    function renderCitations(sourceIds, context) {
        const parts = [];

        if (sourceIds.length) {
            parts.push(`Sources: ${sourceIds.map(escapeHtml).join(", ")}`);
        }

        if (context && context.company) {
            parts.push(`Context: ${escapeHtml(context.company)} / ${escapeHtml(humanizeToken(context.signal))} / confidence ${escapeHtml(String(context.confidence))}`);
        }

        return parts.length ? `<small>${parts.join(" | ")}</small>` : "";
    }

    function syncProgressPanel(state, elements) {
        const isOpen = !!state.progressOpen;
        elements.progressPanel.classList.toggle("is-open", isOpen);
        elements.progressPanel.setAttribute("aria-hidden", String(!isOpen));
        elements.progressToggle.setAttribute("aria-expanded", String(isOpen));
        elements.progressToggle.textContent = isOpen ? "Hide Progress" : "Show Progress";
        elements.progressBackdrop.hidden = !isOpen;
    }

    function setChatComposerEnabled(elements, enabled) {
        elements.chatInput.disabled = !enabled;
        elements.chatSend.disabled = !enabled;
        syncSuggestionButtons(elements, enabled);
    }

    function syncSuggestionButtons(elements, enabled) {
        elements.chatSuggestions
            .querySelectorAll(".chat-suggestion")
            .forEach((button) => {
                button.disabled = !enabled;
            });
    }

    function renderMarkdownToHtml(text) {
        const normalized = String(text || "").replace(/\r\n/g, "\n").trim();
        if (!normalized) {
            return "";
        }

        const lines = normalized.split("\n");
        const html = [];
        const paragraphLines = [];
        let listType = "";
        let listItems = [];

        const flushParagraph = () => {
            if (!paragraphLines.length) {
                return;
            }

            html.push(`<p>${paragraphLines.map(renderInlineMarkdown).join("<br>")}</p>`);
            paragraphLines.length = 0;
        };

        const flushList = () => {
            if (!listItems.length || !listType) {
                return;
            }

            html.push(`<${listType}>${listItems.map((item) => `<li>${renderInlineMarkdown(item)}</li>`).join("")}</${listType}>`);
            listItems = [];
            listType = "";
        };

        lines.forEach((rawLine) => {
            const line = rawLine.trim();
            if (!line) {
                flushParagraph();
                flushList();
                return;
            }

            const headingMatch = line.match(/^(#{1,6})\s+(.*)$/);
            if (headingMatch) {
                flushParagraph();
                flushList();
                const level = Math.min(headingMatch[1].length, 4);
                html.push(`<h${level}>${renderInlineMarkdown(headingMatch[2])}</h${level}>`);
                return;
            }

            const orderedMatch = line.match(/^\d+\.\s+(.*)$/);
            if (orderedMatch) {
                flushParagraph();
                if (listType && listType !== "ol") {
                    flushList();
                }

                listType = "ol";
                listItems.push(orderedMatch[1]);
                return;
            }

            const unorderedMatch = line.match(/^[-*]\s+(.*)$/);
            if (unorderedMatch) {
                flushParagraph();
                if (listType && listType !== "ul") {
                    flushList();
                }

                listType = "ul";
                listItems.push(unorderedMatch[1]);
                return;
            }

            flushList();
            paragraphLines.push(line);
        });

        flushParagraph();
        flushList();

        return html.join("");
    }

    function renderInlineMarkdown(text) {
        let output = escapeHtml(text);
        output = output.replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>");
        output = output.replace(/`(.+?)`/g, "<code>$1</code>");
        return output;
    }

    function tryReadJson(response) {
        return response.json().catch(() => null);
    }

    function formatDate(value) {
        if (!value) {
            return "";
        }

        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return value;
        }

        return date.toLocaleString();
    }

    function confidenceBand(value) {
        if (value >= 80) {
            return "high";
        }

        if (value >= 60) {
            return "medium";
        }

        return "low";
    }

    function formatExecutionModeLabel(value) {
        const normalized = String(value || "");
        if (normalized === "LiveRequired") {
            return "Live";
        }

        return humanizeToken(normalized);
    }

    function formatSynthesisSummary(details, orchestration) {
        const parts = [`${formatExecutionModeLabel(details.mode)} mode.`];

        if (orchestration && orchestration.researchMode === "Autonomous") {
            parts.push("Autonomous coordinator and specialist flow.");

            if (orchestration.handoffOccurred) {
                parts.push(`Handoff completed after ${orchestration.toolCallCount} tool calls.`);
            }

            if ((orchestration.reflectionRounds || 0) > 0) {
                parts.push(`Reflection rounds: ${orchestration.reflectionRounds}.`);
            }
        }

        if ((details.validationRetries || 0) > 0) {
            parts.push(`Validation retries: ${details.validationRetries}.`);
        }

        if (details.fallbackOccurred) {
            parts.push("Fallback occurred.");
        }

        return parts.join(" ");
    }

    function humanizeToken(value) {
        if (!value) {
            return "";
        }

        return String(value)
            .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
            .replace(/[_-]+/g, " ")
            .replace(/\s+/g, " ")
            .trim();
    }

    function humanizeIdentifier(value) {
        if (!value) {
            return "";
        }

        return /^[A-Z0-9.\-]+$/.test(value) ? value : humanizeToken(value);
    }

    function sourceProviderLabel(value) {
        return value === "SeededMock" ? "Fictional sample data" : value;
    }

    function scrollTranscript(container) {
        container.scrollTop = container.scrollHeight;
    }

    function extractHubError(error) {
        if (!error) {
            return "The chat request failed.";
        }

        if (typeof error === "string") {
            return error;
        }

        if (error.message) {
            // SignalR wraps the server's text as "An unexpected error occurred invoking 'X' on the server. HubException: ...".
            const marker = "HubException: ";
            const index = error.message.indexOf(marker);
            return index >= 0 ? error.message.slice(index + marker.length) : error.message;
        }

        return "The chat request failed.";
    }

    function getAppBaseHref() {
        const baseElement = document.querySelector("base");
        return baseElement?.href || `${window.location.origin}/`;
    }

    function resolveAppUrl(path) {
        return new URL(String(path || "").replace(/^\/+/, ""), appBaseHref).toString();
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll("\"", "&quot;")
            .replaceAll("'", "&#39;");
    }
})();
