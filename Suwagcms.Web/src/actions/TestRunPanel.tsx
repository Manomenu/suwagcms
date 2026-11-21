import { Alert, ApiError, Badge, Button, ConfirmDialog, Field, Input, Paper } from "@maniumek/suwfrontkit";
import { mistakesOf, refusalOf } from "@/api/client";
import { useState } from "react";

import { implementationOf, runAction, type ActionTestAnswer, type ActionView } from "./api";

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
            {answer.note != null && <p className="font-medium">{answer.note}</p>}
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
                setRefusal(refusalOf(error));
            })
            .finally(() => {
                setRunning(false);
            });
    }

    const mistakes = mistakesOf(refusal);
    return (
        <Paper aria-label="Test run" role="region" className="flex flex-col gap-3">
            <h3 className="font-semibold">Test run</h3>
            {names.map((name) => (
                <Field
                    key={name}
                    prefix="test-run"
                    name={`values.${name}`}
                    label={name}
                    hint={action.parameters?.[name]?.description ?? "A value to try this run with."}
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
            <ConfirmDialog
                open={asking}
                onOpenChange={setAsking}
                title="Really call the service?"
                description={`This will really call ${call ?? ""}.`}
                confirmLabel="Call it"
                onConfirm={run}
            />
            {answer !== null && <Answer answer={answer} />}
        </Paper>
    );
}
