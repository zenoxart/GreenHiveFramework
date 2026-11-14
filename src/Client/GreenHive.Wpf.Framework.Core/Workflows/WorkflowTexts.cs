using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.Framework.Workflows;

/// <summary>Texts of the "workflow" and "ai" scopes (English and German), including the server's workflow messages.</summary>
public static class WorkflowTexts
{
    public static void Register(LanguageManager languages)
    {
        languages.Register("workflow", "en", new Dictionary<string, string>
        {
            ["step.missing"] = "The workflow refers to a step that does not exist.",
            ["view.inbox"] = "Inbox", ["view.inbox.label"] = "Inbox", ["view.inbox.tooltip"] = "Your workflow tasks and AI proposals to approve",
            ["view.workflows"] = "Workflows", ["view.workflows.label"] = "Workflows", ["view.workflows.tooltip"] = "Workflow definitions, instances and history",
            ["view.impact"] = "Workflow impact", ["view.impact.label"] = "Workflow impact", ["view.impact.tooltip"] = "Which workflows module updates affect",
            ["view.ai"] = "AI agent", ["view.ai.label"] = "AI agent", ["view.ai.tooltip"] = "Status, proposals and audit of the AI agent",
            ["inbox.empty"] = "Nothing to do - your inbox is empty.", ["inbox.select"] = "Select a task on the left.",
            ["inbox.refresh"] = "Refresh", ["inbox.start"] = "Start a workflow", ["inbox.submit"] = "Complete step", ["inbox.approve"] = "Approve",
            ["inbox.reject"] = "Reject", ["inbox.confirm"] = "Confirm", ["inbox.comment"] = "Comment", ["inbox.handToAi"] = "Hand over to AI",
            ["inbox.runCommand"] = "Run", ["inbox.cancelInstance"] = "Cancel workflow", ["inbox.history"] = "History",
            ["inbox.proposal"] = "Proposal of the AI agent", ["inbox.confidence"] = "Confidence: {value}", ["inbox.runAs"] = "On behalf of {name}",
            ["inbox.since"] = "Waiting since {time}", ["inbox.badge.ai"] = "AI proposal", ["inbox.badge.approval"] = "Approval",
            ["inbox.badge.problem"] = "Needs attention", ["inbox.completed"] = "Step completed.", ["inbox.variables"] = "Data of the workflow",
            ["list.start"] = "Start", ["list.startAi"] = "Start with AI", ["list.publish"] = "Publish", ["list.enable"] = "Activate",
            ["list.disable"] = "Deactivate", ["list.edit"] = "Edit (JSON)", ["list.new"] = "New", ["list.record"] = "Record",
            ["list.stopRecording"] = "Stop recording ({count} steps)", ["list.steps"] = "Steps", ["list.triggers"] = "Triggers",
            ["list.dependencies"] = "Dependencies", ["list.instances"] = "Instances", ["list.policy"] = "AI policy: {autonomy}",
            ["list.saved"] = "Saved as version {version} ({status}).", ["list.started"] = "Workflow started.", ["list.invalid"] = "The definition has errors:",
            ["list.recordName"] = "Name of the recorded workflow", ["list.empty"] = "No workflows.",
            ["impact.empty"] = "No module update affected workflows yet.", ["impact.diff"] = "Changes of the module",
            ["impact.workflows"] = "Affected workflows", ["impact.reason"] = "Reason", ["impact.migrated"] = "Migrated to version {version}",
            ["impact.suspended"] = "AI triggers suspended", ["impact.running"] = "{count} running instance(s): {action}", ["impact.open"] = "Open workflow",
            ["ai.provider"] = "Provider: {provider} ({model})", ["ai.today"] = "Today: {runs} runs, {tokens} tokens", ["ai.enabled"] = "AI agent enabled",
            ["ai.proposals"] = "Proposals waiting for approval", ["ai.audit"] = "Recent decisions",
            // server messages
            ["start.notActive"] = "The workflow is not active.", ["step.notCurrent"] = "This step is no longer current - refresh the inbox.",
            ["step.notWaiting"] = "The workflow is not waiting for this step.", ["step.notYourTask"] = "This task belongs to someone else.",
            ["approval.decisionRequired"] = "Approve or reject.", ["ai.disabledForWorkflow"] = "The AI is switched off for this workflow.",
            ["ai.stepNotAllowed"] = "The AI may not execute this step.", ["instance.notFound"] = "The workflow instance does not exist.",
            ["instance.finished"] = "The workflow has already finished.", ["instance.notPaused"] = "The workflow is not paused.",
            ["definition.notFound"] = "The workflow does not exist.", ["impact.notFound"] = "The report does not exist.",
            ["command.notExecuted"] = "The command could not be executed.", ["step.noClientHandler"] = "This step cannot be executed in the app.",
            ["stepType.unknown"] = "Unknown step type.", ["expression.empty"] = "The expression is empty.",
            ["validation.nameRequired"] = "The workflow needs a name.", ["validation.noSteps"] = "The workflow needs at least one step.",
            // notifications
            ["notification.taskWaiting"] = "A workflow task is waiting for you", ["notification.approvalRequested"] = "Approval requested",
            ["notification.aiProposal"] = "The AI agent proposes values - please approve", ["notification.criticalStep"] = "The AI wants to run a critical step - please approve",
            ["notification.approvalDecided"] = "Approval decided", ["notification.proposalApproved"] = "AI proposal approved",
            ["notification.proposalRejected"] = "AI proposal rejected - please complete the step", ["notification.handedOver"] = "A workflow was handed over to you",
            ["notification.stepNeedsAttention"] = "A workflow step needs your attention", ["notification.completed"] = "Workflow completed",
            ["notification.failed"] = "Workflow failed", ["notification.impactDetected"] = "A module update affects workflows",
            ["notification.triggerSuspended"] = "AI trigger suspended after a module update",
        });
        languages.Register("workflow", "de", new Dictionary<string, string>
        {
            ["step.missing"] = "Der Workflow verweist auf einen Schritt, den es nicht gibt.",
            ["view.inbox"] = "Posteingang", ["view.inbox.label"] = "Posteingang", ["view.inbox.tooltip"] = "Ihre Workflow-Aufgaben und KI-Vorschläge zur Freigabe",
            ["view.workflows"] = "Workflows", ["view.workflows.label"] = "Workflows", ["view.workflows.tooltip"] = "Workflow-Definitionen, Instanzen und Verlauf",
            ["view.impact"] = "Workflow-Auswirkungen", ["view.impact.label"] = "Workflow-Auswirkungen", ["view.impact.tooltip"] = "Welche Workflows Modul-Updates betreffen",
            ["view.ai"] = "KI-Agent", ["view.ai.label"] = "KI-Agent", ["view.ai.tooltip"] = "Status, Vorschläge und Protokoll des KI-Agenten",
            ["inbox.empty"] = "Nichts zu tun - Ihr Posteingang ist leer.", ["inbox.select"] = "Wählen Sie links eine Aufgabe.",
            ["inbox.refresh"] = "Aktualisieren", ["inbox.start"] = "Workflow starten", ["inbox.submit"] = "Schritt abschließen", ["inbox.approve"] = "Freigeben",
            ["inbox.reject"] = "Ablehnen", ["inbox.confirm"] = "Bestätigen", ["inbox.comment"] = "Kommentar", ["inbox.handToAi"] = "An KI übergeben",
            ["inbox.runCommand"] = "Ausführen", ["inbox.cancelInstance"] = "Workflow abbrechen", ["inbox.history"] = "Verlauf",
            ["inbox.proposal"] = "Vorschlag des KI-Agenten", ["inbox.confidence"] = "Sicherheit: {value}", ["inbox.runAs"] = "Im Auftrag von {name}",
            ["inbox.since"] = "Wartet seit {time}", ["inbox.badge.ai"] = "KI-Vorschlag", ["inbox.badge.approval"] = "Freigabe",
            ["inbox.badge.problem"] = "Benötigt Aufmerksamkeit", ["inbox.completed"] = "Schritt abgeschlossen.", ["inbox.variables"] = "Daten des Workflows",
            ["list.start"] = "Starten", ["list.startAi"] = "Mit KI starten", ["list.publish"] = "Veröffentlichen", ["list.enable"] = "Aktivieren",
            ["list.disable"] = "Deaktivieren", ["list.edit"] = "Bearbeiten (JSON)", ["list.new"] = "Neu", ["list.record"] = "Aufzeichnen",
            ["list.stopRecording"] = "Aufzeichnung beenden ({count} Schritte)", ["list.steps"] = "Schritte", ["list.triggers"] = "Auslöser",
            ["list.dependencies"] = "Abhängigkeiten", ["list.instances"] = "Instanzen", ["list.policy"] = "KI-Richtlinie: {autonomy}",
            ["list.saved"] = "Als Version {version} gespeichert ({status}).", ["list.started"] = "Workflow gestartet.", ["list.invalid"] = "Die Definition enthält Fehler:",
            ["list.recordName"] = "Name des aufgezeichneten Workflows", ["list.empty"] = "Keine Workflows.",
            ["impact.empty"] = "Noch hat kein Modul-Update Workflows betroffen.", ["impact.diff"] = "Änderungen des Moduls",
            ["impact.workflows"] = "Betroffene Workflows", ["impact.reason"] = "Grund", ["impact.migrated"] = "Auf Version {version} migriert",
            ["impact.suspended"] = "KI-Auslöser ausgesetzt", ["impact.running"] = "{count} laufende Instanz(en): {action}", ["impact.open"] = "Workflow öffnen",
            ["ai.provider"] = "Anbieter: {provider} ({model})", ["ai.today"] = "Heute: {runs} Läufe, {tokens} Tokens", ["ai.enabled"] = "KI-Agent aktiviert",
            ["ai.proposals"] = "Vorschläge, die auf Freigabe warten", ["ai.audit"] = "Letzte Entscheidungen",
            ["start.notActive"] = "Der Workflow ist nicht aktiv.", ["step.notCurrent"] = "Dieser Schritt ist nicht mehr aktuell - aktualisieren Sie den Posteingang.",
            ["step.notWaiting"] = "Der Workflow wartet nicht auf diesen Schritt.", ["step.notYourTask"] = "Diese Aufgabe gehört jemand anderem.",
            ["approval.decisionRequired"] = "Freigeben oder ablehnen.", ["ai.disabledForWorkflow"] = "Die KI ist für diesen Workflow ausgeschaltet.",
            ["ai.stepNotAllowed"] = "Die KI darf diesen Schritt nicht ausführen.", ["instance.notFound"] = "Die Workflow-Instanz existiert nicht.",
            ["instance.finished"] = "Der Workflow ist bereits beendet.", ["instance.notPaused"] = "Der Workflow ist nicht pausiert.",
            ["definition.notFound"] = "Der Workflow existiert nicht.", ["impact.notFound"] = "Der Bericht existiert nicht.",
            ["command.notExecuted"] = "Der Befehl konnte nicht ausgeführt werden.", ["step.noClientHandler"] = "Dieser Schritt kann in der App nicht ausgeführt werden.",
            ["stepType.unknown"] = "Unbekannter Schritt-Typ.", ["expression.empty"] = "Der Ausdruck ist leer.",
            ["validation.nameRequired"] = "Der Workflow braucht einen Namen.", ["validation.noSteps"] = "Der Workflow braucht mindestens einen Schritt.",
            ["notification.taskWaiting"] = "Eine Workflow-Aufgabe wartet auf Sie", ["notification.approvalRequested"] = "Freigabe angefordert",
            ["notification.aiProposal"] = "Der KI-Agent schlägt Werte vor - bitte freigeben", ["notification.criticalStep"] = "Die KI möchte einen kritischen Schritt ausführen - bitte freigeben",
            ["notification.approvalDecided"] = "Freigabe entschieden", ["notification.proposalApproved"] = "KI-Vorschlag freigegeben",
            ["notification.proposalRejected"] = "KI-Vorschlag abgelehnt - bitte schließen Sie den Schritt ab", ["notification.handedOver"] = "Ein Workflow wurde an Sie übergeben",
            ["notification.stepNeedsAttention"] = "Ein Workflow-Schritt benötigt Ihre Aufmerksamkeit", ["notification.completed"] = "Workflow abgeschlossen",
            ["notification.failed"] = "Workflow fehlgeschlagen", ["notification.impactDetected"] = "Ein Modul-Update betrifft Workflows",
            ["notification.triggerSuspended"] = "KI-Auslöser nach einem Modul-Update ausgesetzt",
        });
        languages.Register("ai", "en", new Dictionary<string, string>
        {
            ["disabled"] = "The AI agent is switched off.", ["policyOff"] = "The workflow does not allow AI execution.", ["stepNotAllowed"] = "The AI may not execute this step.",
            ["limitRunsPerDay"] = "Daily AI limit reached.", ["limitSteps"] = "Step limit of the AI reached.", ["limitTokens"] = "Token limit reached.",
            ["limitTime"] = "Time limit reached.", ["loopDetected"] = "The AI repeated itself - handed over.", ["lowConfidence"] = "The AI was not sure enough.",
            ["invalidChoice"] = "The AI chose an invalid option.", ["provider.notConfigured"] = "No AI provider configured.", ["tool.unknown"] = "The AI requested an unknown tool.",
        });
        languages.Register("ai", "de", new Dictionary<string, string>
        {
            ["disabled"] = "Der KI-Agent ist ausgeschaltet.", ["policyOff"] = "Der Workflow erlaubt keine KI-Ausführung.", ["stepNotAllowed"] = "Die KI darf diesen Schritt nicht ausführen.",
            ["limitRunsPerDay"] = "Tageslimit der KI erreicht.", ["limitSteps"] = "Schrittlimit der KI erreicht.", ["limitTokens"] = "Token-Limit erreicht.",
            ["limitTime"] = "Zeitlimit erreicht.", ["loopDetected"] = "Die KI hat sich wiederholt - übergeben.", ["lowConfidence"] = "Die KI war sich nicht sicher genug.",
            ["invalidChoice"] = "Die KI hat eine ungültige Option gewählt.", ["provider.notConfigured"] = "Kein KI-Anbieter konfiguriert.", ["tool.unknown"] = "Die KI hat ein unbekanntes Werkzeug angefordert.",
        });
    }
}
