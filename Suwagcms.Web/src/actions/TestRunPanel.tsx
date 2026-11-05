import { useState } from "react";

import { ApiError } from "@/api/client";
import { Alert } from "@/design/alert";
import { Badge } from "@/design/badge";
import { Button } from "@/design/button";
import { Field } from "@/design/field";
import { Input } from "@/design/input";
import { Paper } from "@/design/page";

import { implementationOf, runAction, type ActionTestAnswer, type ActionView } from "./api";

/** What asking first says: the CMS cannot know what a code action does, so it asks as for any call that may do work; a GET runs at once. */
function realCall(action: ActionView): string | null {
    const implementation = implementationOf(action);
    if (implementation === "code") return `the bot's code for ${action.id}`;
    const http = implementation?.http;
    if (http == null) return null;
    return http.method === "GET" ? null : `${http.method} ${http.url}`;
}

function Answer({ answer }: { answer: ActionTestAnswer }) {
    const judged = answer.outcome === "success" || answer.outcome === "failure";
    return (
        <section aria-label="Test run result" className="flex flex-col gap-2 text-sm">
            <div className="flex flex-wrap items-center gap-2">
                <Badge variant={answer.outcome === "success" ? "success" : "danger"}>
                    {answer.outcome === "success" ? "success" : judged ? "failed" : "not judged"}
                </Badge>
                {answer.status !== null && <span className="text-muted-foreground">Answer {String(answer.status)}</span>}
                {judged && <span className="text-muted-foreground">{String(answer.elapsedMs)} ms</span>}
            </div>
            {answer.reason !== null && <p>{answer.reason}</p>}
            {Object.entries(answer.results).map(([name, value]) => (
                <p key={name}>
                    <span className="font-medium">{name}</span> = {value}
                </p>
            ))}
            {answer.answerStart !== null && answer.answerStart !== "" && (
                <>
                    <pre className="max-h-48 overflow-auto rounded border border-border p-2 font-mono text-xs whitespace-pre-wrap">
                        {answer.answerStart}
                    </pre>
                    {answer.answerCut && <p className="text-muted-foreground">Only the first 2000 characters of the answer are shown.</p>}
                </>
            )}
        </section>
    );
}

/** Tries a saved action through the bot. `unsaved` says the form holds changes, and a run always matches a stored action. */
export function TestRunPanel({ bot, action, unsaved }: { bot: string; action: ActionView; unsaved: boolean }) {
    const names = Object.keys(action.parameters ?? {});
    const [values, setValues] = useState<Record<string, string>>({});
    const [asking, setAsking] = useState(false);
    const [running, setRunning] = useState(false);
    const [answer, setAnswer] = useState<ActionTestAnswer | null>(null);
    const [refusal, setRefusal] = useState<ApiError | null>(null);
    const call = realCall(action);

    function run() {
        setAsking(false);
        setRunning(true);
        setAnswer(null);
        setRefusal(null);
        runAction(bot, action.id, Object.fromEntries(Object.entries(values).filter(([, value]) => value !== "")))
            .then(setAnswer)
            .catch((error: unknown) => {
                setRefusal(error instanceof ApiError ? error : new ApiError(String(error)));
            })
            .finally(() => {
                setRunning(false);
            });
    }

    const mistakes = refusal?.mistakes ?? [];
    return (
        <Paper aria-label="Test run" role="region" className="flex flex-col gap-3">
            <h3 className="font-semibold">Test run</h3>
            {names.map((name) => (
                <Field
                    key={name}
                    prefix="test-run"
                    name={`values.${name}`}
                    label={name}
                    errors={mistakes
                        .filter((mistake) => mistake.path === `values.${name}`)
                        .map((mistake) => `${mistake.message} ${mistake.fix}`)}
                >
                    {(props) => (
                        <Input
                            {...props}
                            value={values[name] ?? ""}
                            onChange={(event) => {
                                setValues((before) => ({ ...before, [name]: event.target.value }));
                            }}
                        />
                    )}
                </Field>
            ))}
            {refusal !== null && mistakes.length === 0 && <Alert variant="destructive">{refusal.message}</Alert>}
            <div className="flex items-center gap-3">
                <Button
                    type="button"
                    disabled={unsaved || running}
                    onClick={() => {
                        if (call === null) run();
                        else setAsking(true);
                    }}
                >
                    Run
                </Button>
                <span className="text-sm text-muted-foreground">
                    {unsaved ? "Save first: a run always tries the saved action." : `Runs on bot ${bot} and really calls the service.`}
                </span>
            </div>
            {asking && (
                <div
                    role="dialog"
                    aria-label="Really call the service"
                    className="flex flex-col gap-2 rounded border border-border p-3 text-sm"
                >
                    <p>This will really call {call}.</p>
                    <div className="flex gap-2">
                        <Button type="button" onClick={run}>
                            Call it
                        </Button>
                        <Button
                            type="button"
                            variant="outline"
                            onClick={() => {
                                setAsking(false);
                            }}
                        >
                            Cancel
                        </Button>
                    </div>
                </div>
            )}
            {answer !== null && <Answer answer={answer} />}
        </Paper>
    );
}
